namespace Bryk.Application.DTOs.Exercise;

public class ExerciseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SportType { get; set; } = string.Empty;
    public int TssValue { get; set; }
    public int? DurationMinutes { get; set; }
    public string? IntensityZone { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateExerciseDto
{
    public string Name { get; set; } = string.Empty;
    public string SportType { get; set; } = string.Empty;
    public int TssValue { get; set; }
    public int? DurationMinutes { get; set; }
    public string? IntensityZone { get; set; }
    public string? Description { get; set; }
}

public class UpdateExerciseDto
{
    public string? Name { get; set; }
    public string? SportType { get; set; }
    public int? TssValue { get; set; }
    public int? DurationMinutes { get; set; }
    public string? IntensityZone { get; set; }
    public string? Description { get; set; }
}

public class ExerciseListDto
{
    public List<ExerciseDto> Exercises { get; set; } = new();
    public int TotalCount { get; set; }
    public SportCountsDto SportCounts { get; set; } = new();
}

public class SportCountsDto
{
    public int BikeCount { get; set; }
    public int RunCount { get; set; }
    public int SwimCount { get; set; }
    public int StrengthCount { get; set; }
    public int OtherCount { get; set; }
}
