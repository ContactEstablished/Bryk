using Bryk.Application.ActivityFiles;
using Bryk.Application.Exceptions;
using Bryk.Domain.Entities;
using Dynastream.Fit;
using DomainSport = Bryk.Domain.Entities.Sport; // Dynastream.Fit also declares a `Sport` enum — alias to
                                                // avoid ambiguity between the two types of the same name.

namespace Bryk.Infrastructure.ActivityFiles;

/// <summary>
/// FIT parser behind <see cref="IActivityFileParser"/> (ADR-0010 §1, Task 19-2's contract). The only
/// place <c>Dynastream.Fit</c> (Garmin.FIT.Sdk 21.205.0) is referenced in the solution.
/// LIMITATION (documented, not implemented): a multisport/triathlon FIT file decodes to whichever single
/// <see cref="SessionMesg"/> the device wrote (or none at all) — this parser does not split a multisport
/// file into per-leg sessions. That is a future item.
/// </summary>
public class FitActivityParser : IActivityFileParser
{
    public ActivityFileFormat Format => ActivityFileFormat.Fit;

    /// <summary>
    /// See <see cref="IActivityFileParser.ParseAsync"/>. Pure function of <paramref name="content"/>: no
    /// file I/O, no clock read, no configuration. <c>Decode.Read</c> is synchronous — there is no true
    /// async work here, so the result is wrapped in <see cref="Task.FromResult{TResult}"/>.
    /// </summary>
    public Task<ParsedActivity> ParseAsync(Stream content, CancellationToken ct = default)
    {
        var records = new List<RecordMesg>();
        SessionMesg? session = null;

        var decode = new Decode();
        var broadcaster = new MesgBroadcaster();

        decode.MesgEvent += broadcaster.OnMesg;
        decode.MesgDefinitionEvent += broadcaster.OnMesgDefinition;
        broadcaster.RecordMesgEvent += (_, e) => records.Add(new RecordMesg(e.mesg));
        broadcaster.SessionMesgEvent += (_, e) => session = new SessionMesg(e.mesg);

        try
        {
            // Read the stream once via the broadcaster's subscriptions above — no second buffer.
            if (!decode.Read(content))
            {
                throw DecodeFailure();
            }
        }
        catch (FitException)
        {
            throw DecodeFailure();
        }
        catch (EndOfStreamException)
        {
            throw DecodeFailure();
        }

        var withTimestamp = records.Where(r => r.GetTimestamp() is not null).ToList();
        if (withTimestamp.Count == 0)
        {
            throw new ValidationException(new[] { "File: The file contains no track data." });
        }

        var startTimeUtc = ToUtc(withTimestamp[0].GetTimestamp());

        // Pass 1 — elapsed seconds, sanity-bounded Hr/Power, cumulative distance. No pace yet: pace needs
        // the sport, and the sport's power-fallback (below) needs to see every sample's Power first.
        var elapsed = new int[withTimestamp.Count];
        var hr = new int?[withTimestamp.Count];
        var power = new int?[withTimestamp.Count];
        var distance = new float?[withTimestamp.Count]; // cumulative metres

        for (var i = 0; i < withTimestamp.Count; i++)
        {
            var record = withTimestamp[i];
            elapsed[i] = (int)(ToUtc(record.GetTimestamp()) - startTimeUtc).TotalSeconds;
            hr[i] = ActivitySampleBounds.Hr(record.GetHeartRate());
            power[i] = ActivitySampleBounds.Power(record.GetPower());
            distance[i] = record.GetDistance();
        }

        var sport = ResolveSport(session, power);

        // Pass 2 — per-sample pace (Run/Swim only), from the cumulative-distance delta to the previous
        // sample, exactly as the TCX parser derives it (seconds per km run, per 100 m swim).
        var paceUnit = sport switch { DomainSport.Run => 1000d, DomainSport.Swim => 100d, _ => 0d };
        var samples = new List<ActivitySample>(withTimestamp.Count);

        for (var i = 0; i < withTimestamp.Count; i++)
        {
            int? pace = null;
            if (paceUnit > 0 && i > 0 && distance[i] is { } d && distance[i - 1] is { } prev
                && d > prev && elapsed[i] > elapsed[i - 1])
            {
                var deltaMeters = d - prev;
                var deltaSeconds = elapsed[i] - elapsed[i - 1];
                pace = (int)Math.Round(deltaSeconds / (deltaMeters / paceUnit));
            }

            samples.Add(new ActivitySample(elapsed[i], hr[i], power[i], pace));
        }

        // Session aggregates come from the retained SAMPLES, never from SessionMesg's own
        // GetAvgHeartRate/GetMaxHeartRate/GetAvgPower — the one rule Task 19-2 fixed across all three
        // formats. Those SessionMesg accessors exist but are deliberately unused here.
        var avgHr = Average(samples.Select(s => s.Hr));
        var avgPower = Average(samples.Select(s => s.Power));
        var hrValues = samples.Where(s => s.Hr is not null).Select(s => s.Hr!.Value).ToList();
        var maxHr = hrValues.Count > 0 ? hrValues.Max() : (int?)null;

        var durationSeconds = session?.GetTotalTimerTime() is { } timer
            ? (int)Math.Round(timer)
            : elapsed[^1];
        var distanceMeters = session?.GetTotalDistance() is { } dist
            ? (int)Math.Round(dist)
            : (distance[^1] is { } lastDistance ? (int)Math.Round(lastDistance) : (int?)null);

        int? avgPace = null;
        if (paceUnit > 0 && durationSeconds is > 0 && distanceMeters is > 0)
        {
            avgPace = (int)Math.Round(durationSeconds / (distanceMeters.Value / paceUnit));
        }

        return Task.FromResult(new ParsedActivity(
            sport, startTimeUtc, durationSeconds, distanceMeters, avgHr, maxHr, avgPower, avgPace, samples));
    }

    // Session message's sport maps Cycling/Running/Swimming to Bike/Run/Swim; anything else, or no
    // session message at all, falls through to Task 19-2's shared chain: power present -> Bike, else
    // Run. Deliberately no case for a multisport/triathlon file - see the class-level LIMITATION comment.
    private static DomainSport ResolveSport(SessionMesg? session, int?[] power)
    {
        if (session?.GetSport() is { } fitSport)
        {
            var mapped = fitSport switch
            {
                Dynastream.Fit.Sport.Cycling => (DomainSport?)DomainSport.Bike,
                Dynastream.Fit.Sport.Running => (DomainSport?)DomainSport.Run,
                Dynastream.Fit.Sport.Swimming => (DomainSport?)DomainSport.Swim,
                _ => null
            };
            if (mapped is { } m)
            {
                return m;
            }
        }

        return power.Any(p => p is not null) ? DomainSport.Bike : DomainSport.Run;
    }

    // FIT timestamps are seconds since the FIT epoch (1989-12-31T00:00:00Z); the SDK's
    // Dynastream.Fit.DateTime wrapper exposes the converted value via GetDateTime(). That wrapper is a
    // reference type, so an absent timestamp is a null instance rather than a null-valued struct.
    private static System.DateTime ToUtc(Dynastream.Fit.DateTime fitTimestamp) =>
        System.DateTime.SpecifyKind(fitTimestamp.GetDateTime(), DateTimeKind.Utc);

    private static int? Average(IEnumerable<int?> values)
    {
        var present = values.Where(v => v is not null).Select(v => v!.Value).ToList();
        return present.Count > 0 ? (int)Math.Round(present.Average()) : null;
    }

    private static ValidationException DecodeFailure() =>
        new(new[] { "File: The .fit file could not be decoded." });
}
