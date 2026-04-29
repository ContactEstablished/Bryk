using Bryk.Domain.Entities;

namespace Bryk.Application.Onboarding;

public class OnboardingRequiredRequest
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
