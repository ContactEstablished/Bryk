using Bryk.Application.Common;
using Bryk.Application.Events;
using Bryk.Application.Goals;
using Bryk.Application.Onboarding;
using Bryk.Domain.Interfaces;

namespace Bryk.Application.Profile;

public class ProfileService(
    ICurrentUserService currentUser,
    IAthleteRepository athleteRepo,
    IEventRepository eventRepo,
    IGoalRepository goalRepo) : IProfileService
{
    public async Task<ProfileRequiredResponse?> GetRequiredAsync(CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();
        var athlete = await athleteRepo.GetByIdAsync(athleteId, ct);

        if (athlete is null)
        {
            return null;
        }

        return new ProfileRequiredResponse
        {
            Name = athlete.Name,
            Gender = athlete.Gender,
            DateOfBirth = athlete.DateOfBirth,
            HeightCm = athlete.HeightCm,
            WeightKg = athlete.WeightKg,
            YearsTraining = athlete.YearsTraining,
            TypicalWeeklyHours = athlete.TypicalWeeklyHours,
            Methodology = athlete.Methodology
        };
    }

    public async Task<ProfileRecommendedResponse?> GetRecommendedAsync(CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();
        var athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct);

        if (athlete is null)
        {
            return null;
        }

        return new ProfileRecommendedResponse
        {
            RestingHr = athlete.RestingHr,
            MaxHr = athlete.MaxHr,
            SportThresholds = athlete.SportProfiles
                .Select(p => new SportThresholdsDto
                {
                    Sport = p.Sport,
                    IsActive = p.IsActive,
                    ThresholdValue = p.ThresholdValue,
                    Lt1 = p.Lt1,
                    Lt2 = p.Lt2,
                    CustomZonesJson = p.CustomZonesJson
                })
                .ToList()
        };
    }

    public async Task<ProfileGoalsResponse?> GetGoalsAsync(CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();

        // Existence check so a missing athlete yields 404, consistent with the other two
        // endpoints. Empty Events/Goals for an existing athlete is a valid 200.
        var athlete = await athleteRepo.GetByIdAsync(athleteId, ct);
        if (athlete is null)
        {
            return null;
        }

        var events = await eventRepo.GetByAthleteIdAsync(athleteId, ct);
        var goals = await goalRepo.GetByAthleteIdAsync(athleteId, ct);

        return new ProfileGoalsResponse
        {
            Events = events
                .Select(e => new EventResponse
                {
                    Id = e.Id,
                    Name = e.Name,
                    EventDate = e.EventDate,
                    Sport = e.Sport,
                    TriathlonDistance = e.TriathlonDistance,
                    CustomDistanceName = e.CustomDistanceName,
                    Priority = e.Priority,
                    Notes = e.Notes
                })
                .ToList(),
            Goals = goals
                .Select(g => new GoalResponse
                {
                    Id = g.Id,
                    Type = g.Type,
                    Description = g.Description,
                    TargetDate = g.TargetDate
                })
                .ToList()
        };
    }
}
