namespace Bryk.Application.Wellness;

public class WellnessEntryRequest
{
    // Populated by the service from the {date} route segment before validation — the route always
    // wins over anything a client puts in the body. Present on the DTO so one validator can carry both
    // the date rules and the metric rules (see WellnessEntryRequestValidator).
    public DateOnly Date { get; set; }

    public decimal? SleepHours { get; set; }
    public int? SleepQuality { get; set; }
    public int? RestingHr { get; set; }
    public decimal? WeightKg { get; set; }
    public int? Soreness { get; set; }
    public int? HrvMs { get; set; }
    public string? Notes { get; set; }
}
