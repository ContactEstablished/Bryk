using Bryk.Application.Common;
using Bryk.Application.Exceptions;
using Bryk.Application.Onboarding;
using Bryk.Application.Onboarding.Validators;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Onboarding;

public class OnboardingServiceTests
{
    [Fact]
    public async Task SubmitRequiredAsync_WhenNameMissing_ThrowsValidationException()
    {
        var service = new OnboardingService(
            currentUser: new StubCurrentUserService(Guid.NewGuid()),
            requiredValidator: new OnboardingRequiredRequestValidator(),
            recommendedValidator: new OnboardingRecommendedRequestValidator(),
            goalsValidator: new OnboardingGoalsRequestValidator(),
            athleteRepo: null!,
            eventRepo: null!,
            goalRepo: null!,
            unitOfWork: null!);

        var request = new OnboardingRequiredRequest
        {
            Name = string.Empty,
            Gender = Gender.Male,
            DateOfBirth = new DateOnly(1990, 1, 1),
            HeightCm = 180m,
            WeightKg = 75m,
            YearsTraining = 5,
            TypicalWeeklyHours = 8m,
            Methodology = MethodologyChoice.Polarized
        };

        var act = () => service.SubmitRequiredAsync(request);

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().NotBeEmpty();
    }

    private sealed class StubCurrentUserService(Guid athleteId) : ICurrentUserService
    {
        public Guid GetCurrentAthleteId() => athleteId;
    }
}
