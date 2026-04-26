namespace Bryk.Domain.Entities;

public class Mesocycle
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public int NumberOfWeeks { get; set; }
    public int StartingWeeklyTss { get; set; }
    public decimal WeeklyIncreasePercentage { get; set; }
    public string BuildRecoveryRatio { get; set; } = "3:1"; // e.g., "3:1", "2:1"
    public int RecoveryWeekPercentage { get; set; } // % of peak week TSS
    public string WeeklyPatternType { get; set; } = "Polarized"; // Polarized, Pyramidal, Progressive, Custom
    
    // Navigation
    public ICollection<Week> Weeks { get; set; } = new List<Week>();
    
    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
