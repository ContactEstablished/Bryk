namespace Bryk.Domain.Entities;

public class Day
{
    public Guid Id { get; set; }
    public Guid WeekId { get; set; }
    public DateTime Date { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public int TargetTss { get; set; }
    public bool IsRestDay { get; set; }
    public string? Notes { get; set; }
    
    // Navigation
    public Week Week { get; set; } = null!;
    public ICollection<DayExercise> DayExercises { get; set; } = new List<DayExercise>();
    
    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
