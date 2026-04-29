using Bryk.Domain.Entities;

namespace Bryk.Application.Onboarding;

public class SportThresholdsDto
{
    public Sport Sport { get; set; }
    public bool IsActive { get; set; }
    public decimal? ThresholdValue { get; set; }
    public decimal? Lt1 { get; set; }
    public decimal? Lt2 { get; set; }
    public string? CustomZonesJson { get; set; }
}
