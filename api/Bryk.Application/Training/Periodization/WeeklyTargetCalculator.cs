namespace Bryk.Application.Training.Periodization;

/// <summary>
/// Pure weekly-target ramp math (ADR-0009 §1–§3). Pure: no I/O, no <see cref="DateTime.UtcNow"/> — the
/// caller resolves <see cref="WeeklyTargetInput.Baseline"/> (ADR-0009 §1's fallback chain, a service
/// concern) and passes the plan window in. Two-pass: pass 1 walks the ramp taper-blind to establish each
/// week's build target (recovery weeks record the unchanged running value and do not advance the ramp);
/// pass 2 applies recovery-week scaling or taper scaling on top of it — never both on the same week.
/// </summary>
public static class WeeklyTargetCalculator
{
    private const decimal RampMultiplier = 1.07m;
    private const decimal TaperEventWeekMultiplier = 0.50m;
    private const decimal TaperPriorWeekMultiplier = 0.75m;
    private const decimal PercentDivisor = 100m;

    public static IReadOnlyList<WeeklyTargetDto> Compute(WeeklyTargetInput input)
    {
        if (input.Baseline is not { } baseline || baseline <= 0m) return [];
        if (input.EndDate < input.StartDate) return [];

        var firstWeekStart = WeekStart(input.StartDate);
        var weekCount = ((input.EndDate.DayNumber - firstWeekStart.DayNumber) / 7) + 1;

        var hasCadence = input.BuildWeeks is > 0 && input.RecoveryWeeks is > 0 && input.RecoveryWeekPercentage is not null;
        var cycle = hasCadence ? input.BuildWeeks!.Value + input.RecoveryWeeks!.Value : 0;

        var taperWeek = -1;
        if (input.EventDate is { } ev && ev >= input.StartDate && ev <= input.EndDate)
        {
            taperWeek = (WeekStart(ev).DayNumber - firstWeekStart.DayNumber) / 7;
        }

        // Pass 1 — ramp walk, taper-blind. Recovery weeks record the unchanged running build value; the
        // next build week ramps from the last build target, never from a recovery-scaled value.
        var isRecovery = new bool[weekCount];
        var ramp = new decimal[weekCount];
        var current = baseline;
        var seenFirstBuild = false;
        for (var i = 0; i < weekCount; i++)
        {
            isRecovery[i] = hasCadence && (i % cycle) >= input.BuildWeeks!.Value;
            if (!isRecovery[i])
            {
                if (seenFirstBuild)
                {
                    current = Math.Round(current * RampMultiplier, 2);
                }
                seenFirstBuild = true;
            }
            ramp[i] = current;
        }

        // Pass 2 — emit. Taper overrides recovery scaling on the same week; never both (ADR-0009 §3).
        var result = new List<WeeklyTargetDto>(weekCount);
        for (var i = 0; i < weekCount; i++)
        {
            var isTaper = taperWeek >= 0 && (i == taperWeek || i == taperWeek - 1);
            var target = isTaper
                ? Math.Round(ramp[i] * (i == taperWeek ? TaperEventWeekMultiplier : TaperPriorWeekMultiplier), 2)
                : isRecovery[i]
                    ? Math.Round(ramp[i] * (input.RecoveryWeekPercentage!.Value / PercentDivisor), 2)
                    : ramp[i];

            result.Add(new WeeklyTargetDto
            {
                WeekStart = firstWeekStart.AddDays(7 * i),
                TargetLoad = target,
                IsRecoveryWeek = isRecovery[i] && !isTaper,
                IsTaperWeek = isTaper
            });
        }

        return result;
    }

    // Monday-based ISO week start — same math as AnalyticsService.WeekStart / ThisWeekService.CurrentWeek.
    // Duplicated locally per Tasks-18-1 (do not refactor the existing two copies into a shared helper).
    private static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}

/// <summary>
/// Inputs to <see cref="WeeklyTargetCalculator.Compute"/> (ADR-0009). <see cref="Baseline"/> is resolved
/// by the caller via ADR-0009 §1's fallback chain (trailing 4-week mean actual load → plan's first-week
/// planned load → null); a null or non-positive baseline yields no targets, never a fabricated ramp.
/// <see cref="RecoveryWeekPercentage"/> is percent-scale (<c>60.0m</c> = 60 %, ADR-0009 §6).
/// <see cref="EventDate"/> is ignored unless it falls inside <c>[StartDate, EndDate]</c> inclusive.
/// </summary>
public sealed record WeeklyTargetInput(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal? Baseline,
    int? BuildWeeks,
    int? RecoveryWeeks,
    decimal? RecoveryWeekPercentage,
    DateOnly? EventDate);
