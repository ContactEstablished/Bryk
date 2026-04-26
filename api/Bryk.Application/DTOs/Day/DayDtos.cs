namespace Bryk.Application.DTOs.Day;

public class DayDto
{
    public Guid Id { get; set; }
    public Guid WeekId { get; set; }
    public DateTime Date { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public int TargetTss { get; set; }
    public int ActualTss { get; set; }
    public bool IsRestDay { get; set; }
    public string? Notes { get; set; }
}

public class DayWithExercisesDto : DayDto
{
    public List<DayExerciseDto> Exercises { get; set; } = new();
}

public class DayExerciseDto
{
    public Guid Id { get; set; }
    public Guid ExerciseId { get; set; }
    public int OrderIndex { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public string SportType { get; set; } = string.Empty;
    public int PlannedTss { get; set; }
    public int? DurationMinutes { get; set; }
    public string? IntensityZone { get; set; }
    public string? Description { get; set; }
    
    // Actual metrics
    public int? ActualTss { get; set; }
    public WorkoutMetricsDto? Metrics { get; set; }
}

public class WorkoutMetricsDto
{
    public int? CaloriesBurned { get; set; }
    public int? AverageHeartRate { get; set; }
    public int? MaxHeartRate { get; set; }
    
    public HrZoneDistributionDto? HrZoneDistribution { get; set; }
    public PaceMetricsDto? PaceMetrics { get; set; }
    public PerformanceComparisonDto? PerformanceComparison { get; set; }
    public WeatherDto? Weather { get; set; }
    
    public string? WorkoutNotes { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class HrZoneDistributionDto
{
    public int Zone1Minutes { get; set; }
    public int Zone2Minutes { get; set; }
    public int Zone3Minutes { get; set; }
    public int Zone4Minutes { get; set; }
    public int Zone5Minutes { get; set; }
}

public class PaceMetricsDto
{
    public string? AveragePace { get; set; }
    public string? MaxPace { get; set; }
    public string? AdditionalMetric { get; set; }
}

public class PerformanceComparisonDto
{
    public decimal? PercentChange { get; set; }
    public string? ComparisonNotes { get; set; }
}

public class WeatherDto
{
    public int? TemperatureFahrenheit { get; set; }
    public string? Condition { get; set; }
    public string? WindSpeed { get; set; }
    public int? HumidityPercent { get; set; }
}

public class AddExerciseToDayDto
{
    public Guid ExerciseId { get; set; }
    public int? OrderIndex { get; set; }
}

public class UpdateDayExerciseDto
{
    public int? OrderIndex { get; set; }
    public int? ActualTss { get; set; }
    public int? CaloriesBurned { get; set; }
    public int? AverageHeartRate { get; set; }
    public int? MaxHeartRate { get; set; }
    public HrZoneDistributionDto? HrZoneDistribution { get; set; }
    public PaceMetricsDto? PaceMetrics { get; set; }
    public PerformanceComparisonDto? PerformanceComparison { get; set; }
    public WeatherDto? Weather { get; set; }
    public string? WorkoutNotes { get; set; }
}

public class UpdateDayDto
{
    public int? TargetTss { get; set; }
    public bool? IsRestDay { get; set; }
    public string? Notes { get; set; }
}
