using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Bryk.Application.ActivityFiles;
using Bryk.Application.Exceptions;
using Bryk.Domain.Entities;

namespace Bryk.Infrastructure.ActivityFiles;

/// <summary>
/// Parses a GPX 1.1 (.gpx) track into a <see cref="ParsedActivity"/> using <see cref="System.Xml.Linq"/>
/// — no package (ADR-0010 §1). GPX 1.1 carries no power extension: <see cref="ParsedActivity.AvgPower"/>
/// is always null here and no sample carries a power value — do not chase vendor power extensions in v1.
/// Implements the cross-format resolution rules documented on <see cref="ParsedActivity"/>.
/// </summary>
public class GpxActivityParser : IActivityFileParser
{
    private const double EarthRadiusMeters = 6371000d;

    private static readonly XNamespace Gpx = "http://www.topografix.com/GPX/1/1";
    private static readonly XNamespace Tpx1 = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";

    public ActivityFileFormat Format => ActivityFileFormat.Gpx;

    public async Task<ParsedActivity> ParseAsync(Stream content, CancellationToken ct = default)
    {
        XDocument doc;
        try
        {
            doc = await XDocument.LoadAsync(content, LoadOptions.None, ct);
        }
        catch (XmlException)
        {
            throw new ValidationException(new[] { "File: The .gpx file could not be parsed." });
        }

        if (doc.Root is not { } root || root.Name != Gpx + "gpx")
        {
            throw new ValidationException(new[] { "File: The file is not a valid .gpx activity." });
        }

        try
        {
            var track = root.Descendants(Gpx + "trk").FirstOrDefault();
            var trackType = track?.Element(Gpx + "type")?.Value;
            var trackpoints = track?.Descendants(Gpx + "trkpt").ToList() ?? new List<XElement>();

            // Pass 1 — raw per-point values. A point with no <time> is skipped entirely.
            var raw = new List<(DateTime Time, double Lat, double Lon, int? Hr)>();
            foreach (var pt in trackpoints)
            {
                var timeText = pt.Element(Gpx + "time")?.Value;
                if (string.IsNullOrWhiteSpace(timeText))
                {
                    continue;
                }

                var time = DateTime.Parse(timeText, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

                var lat = double.Parse(pt.Attribute("lat")!.Value, CultureInfo.InvariantCulture);
                var lon = double.Parse(pt.Attribute("lon")!.Value, CultureInfo.InvariantCulture);

                var hrText = pt.Element(Gpx + "extensions")?.Element(Tpx1 + "TrackPointExtension")?.Element(Tpx1 + "hr")?.Value;
                var hr = ActivitySampleBounds.Hr(string.IsNullOrWhiteSpace(hrText)
                    ? null
                    : (int)Math.Round(double.Parse(hrText, CultureInfo.InvariantCulture)));

                raw.Add((time, lat, lon, hr));
            }

            if (raw.Count == 0)
            {
                throw new ValidationException(new[] { "File: The file contains no track data." });
            }

            var startTimeUtc = raw[0].Time;
            var sport = ResolveSport(trackType);

            // Pass 2 — elapsed seconds + running haversine distance + per-sample pace (Run/Swim only).
            var samples = new List<ActivitySample>(raw.Count);
            var totalDistanceMeters = 0d;
            for (var i = 0; i < raw.Count; i++)
            {
                var elapsedSeconds = (int)Math.Round((raw[i].Time - startTimeUtc).TotalSeconds);
                int? pace = null;

                if (i > 0)
                {
                    var segmentMeters = Haversine(raw[i - 1].Lat, raw[i - 1].Lon, raw[i].Lat, raw[i].Lon);
                    totalDistanceMeters += segmentMeters;

                    if ((sport == Sport.Run || sport == Sport.Swim) && segmentMeters > 0)
                    {
                        var deltaSeconds = (raw[i].Time - raw[i - 1].Time).TotalSeconds;
                        var unit = sport == Sport.Run ? 1000d : 100d;
                        pace = (int)Math.Round(deltaSeconds / (segmentMeters / unit));
                    }
                }

                samples.Add(new ActivitySample(elapsedSeconds, raw[i].Hr, null, pace));
            }

            var durationSeconds = samples[^1].ElapsedSeconds;
            var distanceMeters = (int)Math.Round(totalDistanceMeters); // rounded once at the end, not per segment
            var avgHr = Average(samples.Select(s => s.Hr));
            var maxHr = Max(samples.Select(s => s.Hr));
            var avgPace = ResolvePace(sport, durationSeconds, distanceMeters);

            return new ParsedActivity(sport, startTimeUtc, durationSeconds, distanceMeters, avgHr, maxHr, null, avgPace, samples);
        }
        catch (FormatException)
        {
            throw new ValidationException(new[] { "File: The .gpx file could not be parsed." });
        }
    }

    // §Sport fallback chain: case-insensitive Contains on <type>. GPX 1.1 never carries a power sample
    // (no vendor power extension chased in v1), so rule 1b ("Bike if any sample carries power") can never
    // fire here — the fallback is always Run, made explicit rather than looping over an always-empty
    // power check.
    private static Sport ResolveSport(string? trackType)
    {
        if (!string.IsNullOrWhiteSpace(trackType))
        {
            if (trackType.Contains("run", StringComparison.OrdinalIgnoreCase)) return Sport.Run;
            if (trackType.Contains("bik", StringComparison.OrdinalIgnoreCase)
                || trackType.Contains("cycl", StringComparison.OrdinalIgnoreCase)
                || trackType.Contains("ride", StringComparison.OrdinalIgnoreCase)) return Sport.Bike;
            if (trackType.Contains("swim", StringComparison.OrdinalIgnoreCase)) return Sport.Swim;
        }

        return Sport.Run;
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;

    private static int? ResolvePace(Sport sport, int? durationSeconds, int? distanceMeters)
    {
        if ((sport != Sport.Run && sport != Sport.Swim) || durationSeconds is not { } dur || dur <= 0
            || distanceMeters is not { } dist || dist <= 0)
        {
            return null;
        }

        var unit = sport == Sport.Run ? 1000d : 100d;
        return (int)Math.Round(dur / (dist / unit));
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
}
