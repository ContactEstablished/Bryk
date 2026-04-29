using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

public class DayExercise : IAuditable
{
    public Guid Id { get; set; }
    public Guid DayId { get; set; }
    public Guid ExerciseId { get; set; }
    public int OrderIndex { get; set; } // For ordering exercises within a day
    
    // Actual performance metrics (null if not yet completed)
    public int? ActualTss { get; set; }
    public int? CaloriesBurned { get; set; }
    public int? AverageHeartRate { get; set; }
    public int? MaxHeartRate { get; set; }
    
    // HR Zone distribution (minutes in each zone)
    public int? Zone1Minutes { get; set; }
    public int? Zone2Minutes { get; set; }
    public int? Zone3Minutes { get; set; }
    public int? Zone4Minutes { get; set; }
    public int? Zone5Minutes { get; set; }
    
    // Pace metrics (stored as strings to handle different formats: mph, min/mile, min/100m, watts)
    public string? AveragePace { get; set; }
    public string? MaxPace { get; set; }
    public string? AdditionalPaceMetric { get; set; } // e.g., "Avg Power: 245W" or "Tempo Pace: 7:15"
    
    // Performance comparison
    public decimal? PerformanceComparisonPercent { get; set; } // +5.0 = 5% faster, -3.0 = 3% slower
    public string? ComparisonNotes { get; set; }
    
    // Weather
    public int? TemperatureFahrenheit { get; set; }
    public string? WeatherCondition { get; set; } // "Sunny", "Cloudy", "Rainy", etc.
    public string? WindSpeed { get; set; } // e.g., "5 mph W"
    public int? HumidityPercent { get; set; }
    
    public string? WorkoutNotes { get; set; }
    
    // Navigation
    public Day Day { get; set; } = null!;
    public Exercise Exercise { get; set; } = null!;
    
    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
