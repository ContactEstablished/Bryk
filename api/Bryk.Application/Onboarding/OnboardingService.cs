using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using Bryk.Application.Common;
using FluentValidation;
using Bryk.Application.Exceptions;

namespace Bryk.Application.Onboarding;

public class OnboardingService(
    ICurrentUserService currentUser,
    IValidator<OnboardingRequiredRequest> requiredValidator,
    IValidator<OnboardingRecommendedRequest> recommendedValidator,
    IValidator<OnboardingGoalsRequest> goalsValidator,
    IAthleteRepository athleteRepo,
    IEventRepository eventRepo,
    IGoalRepository goalRepo,
    IUnitOfWork unitOfWork) : IOnboardingService
{
    public async Task SubmitRequiredAsync(OnboardingRequiredRequest request, CancellationToken ct = default)
    {
        var validationResult = await requiredValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            throw new Bryk.Application.Exceptions.ValidationException(
                validationResult.Errors.Select(e => e.ErrorMessage));

        var athleteId = currentUser.GetCurrentAthleteId();
        var existing = await athleteRepo.GetByIdAsync(athleteId, ct);

        if (existing is null)
        {
            var athlete = new Athlete
            {
                Id = athleteId,
                Name = request.Name,
                Gender = request.Gender,
                DateOfBirth = request.DateOfBirth,
                HeightCm = request.HeightCm,
                WeightKg = request.WeightKg,
                YearsTraining = request.YearsTraining,
                TypicalWeeklyHours = request.TypicalWeeklyHours,
                Methodology = request.Methodology
            };
            await athleteRepo.AddAsync(athlete, ct);
        }
        else
        {
            existing.Name = request.Name;
            existing.Gender = request.Gender;
            existing.DateOfBirth = request.DateOfBirth;
            existing.HeightCm = request.HeightCm;
            existing.WeightKg = request.WeightKg;
            existing.YearsTraining = request.YearsTraining;
            existing.TypicalWeeklyHours = request.TypicalWeeklyHours;
            existing.Methodology = request.Methodology;
            athleteRepo.Update(existing);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SubmitRecommendedAsync(OnboardingRecommendedRequest request, CancellationToken ct = default)
    {
        var validationResult = await recommendedValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            throw new Bryk.Application.Exceptions.ValidationException(
                validationResult.Errors.Select(e => e.ErrorMessage));

        var athleteId = currentUser.GetCurrentAthleteId();
        var athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct);

        if (athlete is null)
        {
            throw new InvalidOperationException(
                "Cannot submit recommended onboarding step before required step is complete.");
        }

        athlete.RestingHr = request.RestingHr;
        athlete.MaxHr = request.MaxHr;

        foreach (var dto in request.SportThresholds)
        {
            var existing = await athleteRepo.GetSportProfileAsync(athleteId, dto.Sport, ct);
            if (existing is null)
            {
                athleteRepo.AddSportProfile(new AthleteSportProfile
                {
                    Id = Guid.NewGuid(),
                    AthleteId = athleteId,
                    Sport = dto.Sport,
                    IsActive = dto.IsActive,
                    ThresholdValue = dto.ThresholdValue,
                    Lt1 = dto.Lt1,
                    Lt2 = dto.Lt2,
                    CustomZonesJson = dto.CustomZonesJson
                });
            }
            else
            {
                existing.IsActive = dto.IsActive;
                existing.ThresholdValue = dto.ThresholdValue;
                existing.Lt1 = dto.Lt1;
                existing.Lt2 = dto.Lt2;
                existing.CustomZonesJson = dto.CustomZonesJson;
                athleteRepo.UpdateSportProfile(existing);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SubmitGoalsAsync(OnboardingGoalsRequest request, CancellationToken ct = default)
    {
        var validationResult = await goalsValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            throw new Bryk.Application.Exceptions.ValidationException(
                validationResult.Errors.Select(e => e.ErrorMessage));

        var athleteId = currentUser.GetCurrentAthleteId();

        foreach (var dto in request.Events)
        {
            await eventRepo.AddAsync(new Event
            {
                Id = Guid.NewGuid(),
                AthleteId = athleteId,
                Name = dto.Name,
                EventDate = dto.EventDate,
                Sport = dto.Sport,
                TriathlonDistance = dto.TriathlonDistance,
                Priority = dto.Priority,
                Notes = dto.Notes
            }, ct);
        }

        foreach (var dto in request.Goals)
        {
            await goalRepo.AddAsync(new Goal
            {
                Id = Guid.NewGuid(),
                AthleteId = athleteId,
                Type = dto.Type,
                Description = dto.Description,
                TargetDate = dto.TargetDate
            }, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<OnboardingStatusResponse> GetStatusAsync(CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();
        var athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct);

        if (athlete is null)
        {
            return new OnboardingStatusResponse
            {
                RequiredComplete = false,
                RecommendedComplete = false,
                GoalsComplete = false
            };
        }

        var events = await eventRepo.GetByAthleteIdAsync(athleteId, ct);
        var goals = await goalRepo.GetByAthleteIdAsync(athleteId, ct);

        return new OnboardingStatusResponse
        {
            RequiredComplete = true,
            RecommendedComplete = athlete.SportProfiles.Count > 0,
            GoalsComplete = events.Count > 0 || goals.Count > 0
        };
    }
}
