namespace Bryk.Application.Wellness;

public class WellnessEntryResponse
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal? SleepHours { get; set; }
    public int? SleepQuality { get; set; }
    public int? RestingHr { get; set; }
    public decimal? WeightKg { get; set; }
    public int? Soreness { get; set; }
    public int? HrvMs { get; set; }
    public string? Notes { get; set; }
}

// One metric's 7-day picture. Average is over the days that CARRY a value — a missing day is missing,
// not a zero. Delta is Average - PriorAverage, null unless both windows have data.
public class WellnessMetricSummaryDto
{
    public decimal? Average { get; set; }
    public decimal? PriorAverage { get; set; }
    public decimal? Delta { get; set; }
    public int DaysWithData { get; set; }
}

// One entered day, metrics only (no id, no notes) — the sparkline series.
public class WellnessDailyPointDto
{
    public DateOnly Date { get; set; }
    public decimal? SleepHours { get; set; }
    public int? SleepQuality { get; set; }
    public int? RestingHr { get; set; }
    public decimal? WeightKg { get; set; }
    public int? Soreness { get; set; }
    public int? HrvMs { get; set; }
}

public class WellnessSummaryResponse
{
    public DateOnly To { get; set; }        // today (UTC)
    public DateOnly From { get; set; }      // To.AddDays(-6)  — the current 7-day window
    public DateOnly PriorFrom { get; set; } // To.AddDays(-13) — start of the prior window and of Days
    public WellnessMetricSummaryDto SleepHours { get; set; } = new();
    public WellnessMetricSummaryDto SleepQuality { get; set; } = new();
    public WellnessMetricSummaryDto RestingHr { get; set; } = new();
    public WellnessMetricSummaryDto WeightKg { get; set; } = new();
    public WellnessMetricSummaryDto Soreness { get; set; } = new();
    public WellnessMetricSummaryDto HrvMs { get; set; } = new();
    public IReadOnlyList<WellnessDailyPointDto> Days { get; set; } = [];
    public bool HasAnyEntries { get; set; }
}
