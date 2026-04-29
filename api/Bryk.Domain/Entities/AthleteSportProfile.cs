using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

public class AthleteSportProfile : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public Sport Sport { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal? ThresholdValue { get; set; }
    public decimal? Lt1 { get; set; }
    public decimal? Lt2 { get; set; }
    public string? CustomZonesJson { get; set; }

    public Athlete Athlete { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
