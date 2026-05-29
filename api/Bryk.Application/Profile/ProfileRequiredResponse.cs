using Bryk.Domain.Entities;

namespace Bryk.Application.Profile;

// Read-side mirror of OnboardingRequiredRequest. HR fields are intentionally absent —
// they live on the Recommended response, mirroring the submission surface, not the
// Athlete entity's storage layout.
public class ProfileRequiredResponse
{
    public string Name { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public decimal HeightCm { get; set; }
    public decimal WeightKg { get; set; }
    public int YearsTraining { get; set; }
    public decimal TypicalWeeklyHours { get; set; }
    public MethodologyChoice Methodology { get; set; }
}
