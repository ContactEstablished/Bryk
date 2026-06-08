using Bryk.Application.Zones;
using Bryk.Domain.Entities;

namespace Bryk.Application.Training.Load;

/// <summary>
/// Pure training-load (TSS) math from ADR-0005 §1–2 (ratified options a + c). No I/O — callers pass the
/// athlete's sport profile (thresholds) and that sport's effective zones (for the zone-only fallback),
/// so this is deterministic and unit-tested directly. Nullable inputs / missing thresholds degrade to 0.
/// </summary>
public static class LoadCalculator
{
    // Strength calibration (ADR-0005 §2 option c; tunable). Chosen so a representative session
    // (≈5 exercises × 3×10 @ 50 kg ≈ 7 500 kg tonnage) lands near ~52 TSS.
    private const decimal StrengthTonnageK = 0.007m;
    // Bodyweight fallback when LoadKg is absent: Σ(sets×reps×rpe) × k. ≈5×3×10×RPE7 ≈ 1 050 → ~52.
    private const decimal StrengthVolumeRpeK = 0.05m;
    // Open-ended top zone (null UpperBound): use LowerBound × this as the band-midpoint proxy.
    private const decimal OpenTopZoneMultiple = 1.1m;
    // Actual strength session load (ADR-0005 §6): session RPE × minutes (Foster sRPE), scaled.
    private const decimal StrengthSessionRpeK = 0.165m;

    /// <summary>
    /// Planned TSS for a workout whose <see cref="PlannedWorkout.Blocks"/> (with steps) are loaded.
    /// Returns null when there is no structure to compute from; 0 when nothing in the structure resolves.
    /// </summary>
    public static decimal? ComputePlannedLoad(
        PlannedWorkout workout,
        AthleteSportProfile? sportProfile,
        SportZonesResponse? sportZones)
    {
        if (workout.Blocks.Count == 0)
        {
            return null;
        }

        var isStrength = workout.Sport == Sport.Strength;
        decimal total = 0m;

        foreach (var block in workout.Blocks)
        {
            decimal blockLoad = 0m;
            foreach (var step in block.Steps)
            {
                blockLoad += isStrength
                    ? StrengthStepLoad(step)
                    : CardioStepTss(step, workout.Sport, sportProfile, sportZones);
            }

            total += Math.Max(block.Repeats, 1) * blockLoad;
        }

        return Math.Round(total, 2);
    }

    /// <summary>
    /// Actual load (TSS) for a completed <see cref="Workout"/> from captured actuals (ADR-0005 §6):
    /// cardio from each <see cref="WorkoutStepResult"/>'s avg power/pace/HR + duration (or the session as
    /// one effort when no per-step actuals exist); strength from session RPE × duration. Nullable
    /// inputs / missing thresholds degrade to 0.
    /// </summary>
    public static decimal? ComputeActualLoad(Workout workout, AthleteSportProfile? sportProfile)
    {
        if (workout.Sport == Sport.Strength)
        {
            if (workout.Rpe is { } rpe && rpe > 0m && workout.ActualDurationSeconds is { } sec && sec > 0)
            {
                return Math.Round(rpe * (sec / 60m) * StrengthSessionRpeK, 2);
            }

            return 0m;
        }

        if (workout.StepResults.Count > 0)
        {
            decimal total = 0m;
            foreach (var r in workout.StepResults)
            {
                total += ActualCardioTss(workout.Sport, sportProfile, r.AvgPower, r.AvgPace, r.AvgHr, r.ActualDurationSeconds, r.ActualDistanceMeters);
            }

            return Math.Round(total, 2);
        }

        // Session-level only: treat the whole session as one steady effort. Session-level power/pace
        // aren't captured (ADR-0005 §4), so this is HR-based when an Lt2 threshold is set, else 0.
        return Math.Round(
            ActualCardioTss(workout.Sport, sportProfile, null, null, workout.AvgHr, workout.ActualDurationSeconds, workout.ActualDistanceMeters), 2);
    }

    private static decimal ActualCardioTss(Sport sport, AthleteSportProfile? profile, int? avgPower, int? avgPace, int? avgHr, int? durationSeconds, int? distanceMeters)
    {
        var threshold = profile?.ThresholdValue;
        decimal? intensity = null;

        if (sport == Sport.Bike && threshold is { } ftp && ftp > 0m && avgPower is { } pw && pw > 0)
        {
            intensity = pw / ftp;
        }
        else if ((sport == Sport.Run || sport == Sport.Swim) && threshold is { } thr && thr > 0m && avgPace is { } pace && pace > 0)
        {
            intensity = thr / pace;
        }
        else if (avgHr is { } hr && hr > 0 && profile?.Lt2 is { } lthr && lthr > 0m)
        {
            intensity = hr / lthr;
        }

        if (intensity is not { } ifac || ifac <= 0m)
        {
            return 0m;
        }

        decimal? seconds = durationSeconds is { } d && d > 0 ? d : null;
        if (seconds is null && distanceMeters is { } dist && dist > 0 && avgPace is { } p && p > 0 && (sport == Sport.Run || sport == Sport.Swim))
        {
            seconds = sport == Sport.Run ? dist * (decimal)p / 1000m : dist * (decimal)p / 100m;
        }

        if (seconds is not { } sec || sec <= 0m)
        {
            return 0m;
        }

        return sec * ifac * ifac / 3600m * 100m;
    }

    // sec × IF² / 3600 × 100, the standard normalized TSS at the per-step (steady) granularity.
    private static decimal CardioStepTss(WorkoutStep step, Sport sport, AthleteSportProfile? profile, SportZonesResponse? zones)
    {
        if (IntensityFactor(step, sport, profile, zones) is not { } ifac || ifac <= 0m)
        {
            return 0m;
        }

        if (DurationSeconds(step, sport, zones) is not { } sec || sec <= 0m)
        {
            return 0m;
        }

        return sec * ifac * ifac / 3600m * 100m;
    }

    private static decimal? IntensityFactor(WorkoutStep step, Sport sport, AthleteSportProfile? profile, SportZonesResponse? zones)
    {
        var threshold = profile?.ThresholdValue;

        if (sport == Sport.Bike && threshold is { } ftp && ftp > 0m && TargetPower(step, zones) is { } watts && watts > 0m)
        {
            return watts / ftp;
        }

        if ((sport == Sport.Run || sport == Sport.Swim) && threshold is { } thresholdPace && thresholdPace > 0m
            && TargetPace(step, zones) is { } pace && pace > 0m)
        {
            return thresholdPace / pace; // inverse: a faster pace is a smaller sec-per-unit value
        }

        // HR fallback (ADR-0005 §1 option a): IF = targetHr / Lt2 (Lt2 as threshold-HR).
        if (Midpoint(step.TargetHrLow, step.TargetHrHigh) is { } hr && hr > 0m
            && profile?.Lt2 is { } lthr && lthr > 0m)
        {
            return hr / lthr;
        }

        return null;
    }

    private static decimal? DurationSeconds(WorkoutStep step, Sport sport, SportZonesResponse? zones)
    {
        if (step.DurationSeconds is { } d && d > 0)
        {
            return d;
        }

        // Distance-only step: convert via target pace (run = sec/km, swim = sec/100 m).
        if (step.DistanceMeters is { } dist && dist > 0 && (sport == Sport.Run || sport == Sport.Swim)
            && TargetPace(step, zones) is { } pace && pace > 0m)
        {
            return sport == Sport.Run ? dist * pace / 1000m : dist * pace / 100m;
        }

        return null;
    }

    private static decimal? TargetPower(WorkoutStep step, SportZonesResponse? zones)
        => Midpoint(step.TargetPowerLow, step.TargetPowerHigh) is { } pw && pw > 0m
            ? pw
            : ZoneBandMidpoint(step.TargetZone, zones);

    private static decimal? TargetPace(WorkoutStep step, SportZonesResponse? zones)
        => Midpoint(step.TargetPaceLow, step.TargetPaceHigh) is { } pace && pace > 0m
            ? pace
            : ZoneBandMidpoint(step.TargetZone, zones);

    // The zone's effective band (override-aware) is already in absolute units (watts / sec-per-unit).
    private static decimal? ZoneBandMidpoint(int? zoneNumber, SportZonesResponse? zones)
    {
        if (zoneNumber is not { } zn || zones is null)
        {
            return null;
        }

        var zone = zones.Zones.FirstOrDefault(z => z.ZoneNumber == zn);
        if (zone is null)
        {
            return null;
        }

        return zone.UpperBound is { } upper
            ? (zone.LowerBound + upper) / 2m
            : zone.LowerBound * OpenTopZoneMultiple;
    }

    // ADR-0005 §2 option c: scaled tonnage when LoadKg present, else sets×reps×rpe volume proxy.
    private static decimal StrengthStepLoad(WorkoutStep step)
    {
        if (step.Sets is not { } sets || sets <= 0 || step.Reps is not { } reps || reps <= 0)
        {
            return 0m;
        }

        if (step.LoadKg is { } load && load > 0m)
        {
            return sets * reps * load * StrengthTonnageK;
        }

        if (step.Rpe is { } rpe && rpe > 0m)
        {
            return sets * reps * rpe * StrengthVolumeRpeK;
        }

        return 0m;
    }

    private static decimal? Midpoint(int? low, int? high)
    {
        if (low is { } l && high is { } h)
        {
            return (l + h) / 2m;
        }

        if (low is { } lo)
        {
            return lo;
        }

        if (high is { } hi)
        {
            return hi;
        }

        return null;
    }
}
