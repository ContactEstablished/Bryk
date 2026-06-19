namespace Bryk.Application.Calendar;

/// <summary>
/// The 5-bucket compliance classifier (ADR-0008 §1). Pure: no I/O, no <see cref="DateTime.UtcNow"/>.
/// The service builds a <see cref="ComplianceInput"/> per planned workout and calls <see cref="Classify"/>.
/// </summary>
public static class ComplianceClassifier
{
    private const decimal GreenLower = 0.8m;
    private const decimal GreenUpper = 1.2m;
    private const decimal YellowLower = 0.5m;

    public static ComplianceBucket Classify(ComplianceInput input)
    {
        // Future → grey. Today-no-completion is also grey: the day isn't over (guard before the ratio branch).
        if (input.ScheduledDate > input.Today) return ComplianceBucket.Grey;
        if (!input.HasCompletion && input.ScheduledDate >= input.Today) return ComplianceBucket.Grey;

        // Past + no completion → red (missed).
        if (!input.HasCompletion) return ComplianceBucket.Red;

        // Past/today with completion → ratio.
        var ratio = Ratio(input);
        return ratio switch
        {
            >= GreenLower and <= GreenUpper => ComplianceBucket.Green,    // [0.8, 1.2]
            >= YellowLower => ComplianceBucket.Yellow,                     // [0.5, 0.8) — (1.2, ∞) lands here too
            _ => ComplianceBucket.Red                                      // [0, 0.5)
        };
    }

    // Single null-load fallback chain (ADR-0008 §1): planned-load ratio → planned-duration ratio → 1.0.
    private static decimal Ratio(ComplianceInput input)
    {
        if (input.PlannedLoad is { } plannedLoad)
        {
            // Planned 0 + completed anything → green (degenerate, don't div-by-zero).
            return plannedLoad == 0m ? 1.0m : (input.CompletedLoad ?? 0m) / plannedLoad;
        }

        if (input.PlannedDurationSeconds is { } plannedDur)
        {
            // No actual duration → ratio 0 (red); else actual / planned.
            if (input.CompletedDurationSeconds is null) return 0.0m;
            return (decimal)input.CompletedDurationSeconds.Value / plannedDur;
        }

        // Neither planned load nor duration → completion is green.
        return 1.0m;
    }
}

/// <summary>
/// Inputs to <see cref="ComplianceClassifier.Classify"/> for a single planned workout. The service
/// resolves <see cref="PlannedLoad"/> (<c>PlannedLoad ?? ComputedLoad</c>), <see cref="PlannedDurationSeconds"/>
/// (<c>PlannedDurationMinutes × 60</c>), and the matched completion's load/duration (if any).
/// </summary>
public sealed record ComplianceInput(
    DateOnly ScheduledDate,
    decimal? PlannedLoad,
    int? PlannedDurationSeconds,
    decimal? CompletedLoad,
    int? CompletedDurationSeconds,
    bool HasCompletion,
    DateOnly Today);
