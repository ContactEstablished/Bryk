namespace Bryk.Application.Training.Periodization;

public class WeeklyTargetsResponse
{
    public Guid PlanId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal? Baseline { get; set; }
    public TargetBaselineSource BaselineSource { get; set; }
    public IReadOnlyList<WeeklyTargetWeekDto> Weeks { get; set; } = new List<WeeklyTargetWeekDto>();
}
