namespace Bryk.Application.DTOs.Mesocycle;

public class MesocycleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public int NumberOfWeeks { get; set; }
    public int StartingWeeklyTss { get; set; }
    public decimal WeeklyIncreasePercentage { get; set; }
    public string BuildRecoveryRatio { get; set; } = string.Empty;
    public int RecoveryWeekPercentage { get; set; }
    public string WeeklyPatternType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Calculated fields
    public int TotalTss { get; set; }
    public int AverageWeeklyTss { get; set; }
    public int PeakWeekTss { get; set; }
    public int PeakWeekNumber { get; set; }
    public int RecoveryWeekCount { get; set; }
}

public class CreateMesocycleDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public int NumberOfWeeks { get; set; }
    public int StartingWeeklyTss { get; set; }
    public decimal WeeklyIncreasePercentage { get; set; }
    public string BuildRecoveryRatio { get; set; } = "3:1";
    public int RecoveryWeekPercentage { get; set; } = 50;
    public string WeeklyPatternType { get; set; } = "Polarized";
}

public class UpdateMesocycleDto
{
    public string? Name { get; set; }
    public DateTime? StartDate { get; set; }
    public int? NumberOfWeeks { get; set; }
    public int? StartingWeeklyTss { get; set; }
    public decimal? WeeklyIncreasePercentage { get; set; }
    public string? BuildRecoveryRatio { get; set; }
    public int? RecoveryWeekPercentage { get; set; }
    public string? WeeklyPatternType { get; set; }
}

public class MesocycleWithWeeksDto : MesocycleDto
{
    public List<WeekSummaryDto> Weeks { get; set; } = new();
}

public class WeekSummaryDto
{
    public Guid Id { get; set; }
    public int WeekNumber { get; set; }
    public int TargetTss { get; set; }
    public int ActualTss { get; set; }
    public bool IsRecoveryWeek { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
