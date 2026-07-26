namespace Bryk.Application.Training.Periodization;

public class WeeklyTargetDto
{
    public DateOnly WeekStart { get; set; }
    public decimal TargetLoad { get; set; }
    public bool IsRecoveryWeek { get; set; }
    public bool IsTaperWeek { get; set; }
}
