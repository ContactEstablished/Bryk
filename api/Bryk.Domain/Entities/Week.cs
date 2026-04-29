using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

public class Week : IAuditable
{
    public Guid Id { get; set; }
    public Guid MesocycleId { get; set; }
    public int WeekNumber { get; set; } // 1-based week number within mesocycle
    public int TargetTss { get; set; }
    public bool IsRecoveryWeek { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Notes { get; set; }
    
    // Navigation
    public Mesocycle Mesocycle { get; set; } = null!;
    public ICollection<Day> Days { get; set; } = new List<Day>();
    
    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
