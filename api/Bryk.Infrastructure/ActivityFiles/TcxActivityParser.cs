using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Bryk.Application.ActivityFiles;
using Bryk.Application.Exceptions;
using Bryk.Domain.Entities;

namespace Bryk.Infrastructure.ActivityFiles;

/// <summary>
/// Parses a Garmin Training Center XML (.tcx) activity file into a <see cref="ParsedActivity"/> using
/// <see cref="System.Xml.Linq"/> — no package (ADR-0010 §1). Implements the cross-format resolution rules
/// documented on <see cref="ParsedActivity"/> (sport, session averages, duration/distance, pace).
/// </summary>
public class TcxActivityParser : IActivityFileParser
{
    private static readonly XNamespace Tcx = "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2";
    private static readonly XNamespace Tpx = "http://www.garmin.com/xmlschemas/ActivityExtension/v2";

    public ActivityFileFormat Format => ActivityFileFormat.Tcx;

    public async Task<ParsedActivity> ParseAsync(Stream content, CancellationToken ct = default)
    {
        XDocument doc;
        try
        {
            doc = await XDocument.LoadAsync(content, LoadOptions.None, ct);
        }
        catch (XmlException)
        {
            throw new ValidationException(new[] { "File: The .tcx file could not be parsed." });
        }

        if (doc.Root is not { } root || root.Name != Tcx + "TrainingCenterDatabase")
        {
            throw new ValidationException(new[] { "File: The file is not a valid .tcx activity." });
        }

        try
        {
            var activityElement = root.Descendants(Tcx + "Activity").FirstOrDefault();
            var laps = activityElement?.Elements(Tcx + "Lap").ToList() ?? new List<XElement>();
            var trackpoints = laps.SelectMany(lap => lap.Descendants(Tcx + "Trackpoint")).ToList();

            // Pass 1 — raw per-trackpoint values. A trackpoint with no <Time> is skipped entirely.
            var raw = new List<(DateTime Time, int? Hr, int? Power, int? CumulativeDistance)>();
            foreach (var tp in trackpoints)
            {
                var timeText = tp.Element(Tcx + "Time")?.Value;
                if (string.IsNullOrWhiteSpace(timeText))
                {
                    continue;
                }

                var time = DateTime.Parse(timeText, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

                var hrText = tp.Element(Tcx + "HeartRateBpm")?.Element(Tcx + "Value")?.Value;
                var hr = ActivitySampleBounds.Hr(ParseIntOrNull(hrText));

                var wattsText = tp.Element(Tcx + "Extensions")?.Element(Tpx + "TPX")?.Element(Tpx + "Watts")?.Value;
                var power = ActivitySampleBounds.Power(ParseIntOrNull(wattsText));

                var distance = ParseIntOrNull(tp.Element(Tcx + "DistanceMeters")?.Value);

                raw.Add((time, hr, power, distance));
            }

            if (raw.Count == 0)
            {
                throw new ValidationException(new[] { "File: The file contains no track data." });
            }

            var startTimeUtc = raw[0].Time;
            var sport = ResolveSport(activityElement?.Attribute("Sport")?.Value, raw.Select(r => r.Power));

            // Pass 2 — elapsed seconds + per-sample pace (Run/Swim only), from the cumulative-distance
            // delta between consecutive trackpoints.
            var samples = new List<ActivitySample>(raw.Count);
            for (var i = 0; i < raw.Count; i++)
            {
                var elapsedSeconds = (int)Math.Round((raw[i].Time - startTimeUtc).TotalSeconds);
                samples.Add(new ActivitySample(elapsedSeconds, raw[i].Hr, raw[i].Power, SamplePace(sport, raw, i)));
            }

            var lapDurations = laps.Select(l => ParseDoubleOrNull(l.Element(Tcx + "TotalTimeSeconds")?.Value))
                .Where(v => v is not null).Select(v => v!.Value).ToList();
            var durationSeconds = lapDurations.Count > 0 ? (int)Math.Round(lapDurations.Sum()) : samples[^1].ElapsedSeconds;

            var lapDistances = laps.Select(l => ParseDoubleOrNull(l.Element(Tcx + "DistanceMeters")?.Value))
                .Where(v => v is not null).Select(v => v!.Value).ToList();
            var distanceMeters = lapDistances.Count > 0 ? (int)Math.Round(lapDistances.Sum()) : raw[^1].CumulativeDistance;

            var avgHr = Average(samples.Select(s => s.Hr));
            var maxHr = Max(samples.Select(s => s.Hr));
            var avgPower = Average(samples.Select(s => s.Power));
            var avgPace = ResolvePace(sport, durationSeconds, distanceMeters);

            return new ParsedActivity(sport, startTimeUtc, durationSeconds, distanceMeters, avgHr, maxHr, avgPower, avgPace, samples);
        }
        catch (FormatException)
        {
            throw new ValidationException(new[] { "File: The .tcx file could not be parsed." });
        }
    }

    // §Sport fallback chain (ParsedActivity.cs rule 1): recognised file metadata → Bike if any sample
    // carries power → Run. "Other"/absent both fall through to the same default arm.
    private static Sport ResolveSport(string? sportAttribute, IEnumerable<int?> powers) => sportAttribute switch
    {
        "Running" => Sport.Run,
        "Biking" => Sport.Bike,
        "Swimming" => Sport.Swim,
        _ => powers.Any(p => p is not null) ? Sport.Bike : Sport.Run
    };

    private static int? SamplePace(Sport sport, List<(DateTime Time, int? Hr, int? Power, int? CumulativeDistance)> raw, int i)
    {
        if (i == 0 || (sport != Sport.Run && sport != Sport.Swim))
        {
            return null;
        }

        if (raw[i].CumulativeDistance is not { } distance || raw[i - 1].CumulativeDistance is not { } previous || distance <= previous)
        {
            return null;
        }

        var deltaSeconds = (raw[i].Time - raw[i - 1].Time).TotalSeconds;
        var unit = sport == Sport.Run ? 1000m : 100m;
        return (int)Math.Round((decimal)deltaSeconds / ((distance - previous) / unit));
    }

    // Rule 4: DurationSeconds / (DistanceMeters / unit), Run/Swim only, both > 0.
    private static int? ResolvePace(Sport sport, int? durationSeconds, int? distanceMeters)
    {
        if ((sport != Sport.Run && sport != Sport.Swim) || durationSeconds is not { } dur || dur <= 0
            || distanceMeters is not { } dist || dist <= 0)
        {
            return null;
        }

        var unit = sport == Sport.Run ? 1000m : 100m;
        return (int)Math.Round((decimal)dur / (dist / unit));
    }

    private static int? Average(IEnumerable<int?> values)
    {
        var present = values.Where(v => v is not null).Select(v => v!.Value).ToList();
        return present.Count == 0 ? null : (int)Math.Round(present.Average());
    }

    private static int? Max(IEnumerable<int?> values)
    {
        var present = values.Where(v => v is not null).Select(v => v!.Value).ToList();
        return present.Count == 0 ? null : present.Max();
    }

    private static int? ParseIntOrNull(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : (int)Math.Round(double.Parse(text, CultureInfo.InvariantCulture));

    private static double? ParseDoubleOrNull(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : double.Parse(text, CultureInfo.InvariantCulture);
}
