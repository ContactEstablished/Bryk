namespace Bryk.Application.DTOs.Week;

public class WeekDto
{
    public Guid Id { get; set; }
    public Guid MesocycleId { get; set; }
    public int WeekNumber { get; set; }
    public int TargetTss { get; set; }
    public int ActualTss { get; set; }
    public bool IsRecoveryWeek { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Notes { get; set; }
    public int WorkoutsPlanned { get; set; }
    public int DaysActive { get; set; }
}

public class WeekWithDaysDto : WeekDto
{
    public List<DaySummaryDto> Days { get; set; } = new();
    public SportBreakdownDto SportBreakdown { get; set; } = new();
    public IntensityBreakdownDto IntensityBreakdown { get; set; } = new();
}

public class DaySummaryDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public int TargetTss { get; set; }
    public int ActualTss { get; set; }
    public bool IsRestDay { get; set; }
    public int ExerciseCount { get; set; }
}

public class SportBreakdownDto
{
    public int BikeWorkouts { get; set; }
    public int BikeTss { get; set; }
    public int RunWorkouts { get; set; }
    public int RunTss { get; set; }
    public int SwimWorkouts { get; set; }
    public int SwimTss { get; set; }
    public int StrengthWorkouts { get; set; }
    public int StrengthTss { get; set; }
}

public class IntensityBreakdownDto
{
    public int Zone1Tss { get; set; }
    public int Zone2Tss { get; set; }
    public int Zone3Tss { get; set; }
    public int Zone4Tss { get; set; }
    public int Zone5Tss { get; set; }
    public int RecoveryTss { get; set; }
}

public class UpdateWeekDto
{
    public int? TargetTss { get; set; }
    public string? Notes { get; set; }
}
