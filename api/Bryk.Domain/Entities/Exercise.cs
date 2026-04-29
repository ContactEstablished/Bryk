using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

public class Exercise : IAuditable
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SportType SportType { get; set; }
    public int TssValue { get; set; }
    public int? DurationMinutes { get; set; }
    public string? IntensityZone { get; set; } // Zone1, Zone2, Zone3, Zone4, Zone5, Recovery
    public string? Description { get; set; }
    
    // Navigation
    public ICollection<DayExercise> DayExercises { get; set; } = new List<DayExercise>();
    
    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum SportType
{
    Bike = 1,
    Run = 2,
    Swim = 3,
    Strength = 4,
    Other = 5
}
