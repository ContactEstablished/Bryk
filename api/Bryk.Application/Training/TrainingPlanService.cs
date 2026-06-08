using Bryk.Application.Common;
using Bryk.Application.Common.Validation;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentValidation;

namespace Bryk.Application.Training;

public class TrainingPlanService(
    ICurrentUserService currentUser,
    IValidator<TrainingPlanRequest> planValidator,
    IValidator<PlannedWorkoutDto> plannedWorkoutValidator,
    ITrainingPlanRepository planRepo,
    IUnitOfWork unitOfWork) : ITrainingPlanService
{
    public async Task<TrainingPlanResponse> CreateAsync(TrainingPlanRequest request, CancellationToken ct = default)
    {
        await planValidator.ValidateOrThrowAsync(request, ct);

        var athleteId = currentUser.GetCurrentAthleteId();
        var plan = new TrainingPlan
        {
            Id = Guid.NewGuid(),
            AthleteId = athleteId,
            Name = request.Name,
            Methodology = request.Methodology,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            EventId = request.EventId,
            BuildWeeks = request.BuildWeeks,
            RecoveryWeeks = request.RecoveryWeeks,
            RecoveryWeekPercentage = request.RecoveryWeekPercentage
        };

        if (request.PlannedWorkouts is not null)
        {
            foreach (var dto in request.PlannedWorkouts)
            {
                plan.PlannedWorkouts.Add(NewPlannedWorkout(plan.Id, athleteId, dto));
            }
        }

        await planRepo.AddAsync(plan, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Map(plan);
    }

    public async Task<IReadOnlyList<TrainingPlanResponse>> GetByAthleteAsync(CancellationToken ct = default)
    {
        var plans = await planRepo.GetByAthleteIdAsync(currentUser.GetCurrentAthleteId(), ct);
        return plans.Select(Map).ToList();
    }

    public async Task<TrainingPlanResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await LoadOwnedPlanAsync(id, ct);
        return Map(plan);
    }

    public async Task<PlannedWorkoutResponse> AddPlannedWorkoutAsync(Guid planId, PlannedWorkoutDto request, CancellationToken ct = default)
    {
        await plannedWorkoutValidator.ValidateOrThrowAsync(request, ct);

        var plan = await LoadOwnedPlanAsync(planId, ct);
        var plannedWorkout = NewPlannedWorkout(plan.Id, plan.AthleteId, request);

        await planRepo.AddPlannedWorkoutAsync(plannedWorkout, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Map(plannedWorkout);
    }

    public async Task<PlannedWorkoutResponse> UpdatePlannedWorkoutAsync(Guid planId, Guid plannedWorkoutId, PlannedWorkoutDto request, CancellationToken ct = default)
    {
        await plannedWorkoutValidator.ValidateOrThrowAsync(request, ct);

        var plan = await LoadOwnedPlanAsync(planId, ct);
        var existing = plan.PlannedWorkouts.FirstOrDefault(pw => pw.Id == plannedWorkoutId)
            ?? throw new KeyNotFoundException();

        // Stage a fresh, nav-free entity: the loaded `existing` came from a no-tracking Include,
        // so its TrainingPlan nav points back at `plan` — re-attaching it would drag the whole
        // aggregate into the change tracker. CreatedAt is carried over; the interceptor sets UpdatedAt.
        var updated = new PlannedWorkout
        {
            Id = existing.Id,
            AthleteId = existing.AthleteId,
            TrainingPlanId = existing.TrainingPlanId,
            Sport = request.Sport,
            ScheduledDate = request.ScheduledDate,
            Title = request.Title,
            Description = request.Description,
            PlannedDurationMinutes = request.PlannedDurationMinutes,
            PlannedLoad = request.PlannedLoad,
            CreatedAt = existing.CreatedAt
        };

        planRepo.UpdatePlannedWorkout(updated);
        await unitOfWork.SaveChangesAsync(ct);

        return Map(updated);
    }

    public async Task RemovePlannedWorkoutAsync(Guid planId, Guid plannedWorkoutId, CancellationToken ct = default)
    {
        var plan = await LoadOwnedPlanAsync(planId, ct);
        var existing = plan.PlannedWorkouts.FirstOrDefault(pw => pw.Id == plannedWorkoutId)
            ?? throw new KeyNotFoundException();

        // Remove by a key-only stub to avoid attaching the loaded no-tracking aggregate graph.
        planRepo.RemovePlannedWorkout(new PlannedWorkout { Id = existing.Id });
        await unitOfWork.SaveChangesAsync(ct);
    }

    // Loads a plan with its planned workouts and asserts current-athlete ownership.
    // Throws KeyNotFoundException (→ 404) when missing or owned by another athlete.
    private async Task<TrainingPlan> LoadOwnedPlanAsync(Guid planId, CancellationToken ct)
    {
        var plan = await planRepo.GetByIdAsync(planId, ct);
        if (plan is null || plan.AthleteId != currentUser.GetCurrentAthleteId())
        {
            throw new KeyNotFoundException();
        }

        return plan;
    }

    private static PlannedWorkout NewPlannedWorkout(Guid planId, Guid athleteId, PlannedWorkoutDto dto) => new()
    {
        Id = Guid.NewGuid(),
        AthleteId = athleteId,
        TrainingPlanId = planId,
        Sport = dto.Sport,
        ScheduledDate = dto.ScheduledDate,
        Title = dto.Title,
        Description = dto.Description,
        PlannedDurationMinutes = dto.PlannedDurationMinutes,
        PlannedLoad = dto.PlannedLoad
    };

    private static TrainingPlanResponse Map(TrainingPlan p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Methodology = p.Methodology,
        StartDate = p.StartDate,
        EndDate = p.EndDate,
        EventId = p.EventId,
        BuildWeeks = p.BuildWeeks,
        RecoveryWeeks = p.RecoveryWeeks,
        RecoveryWeekPercentage = p.RecoveryWeekPercentage,
        PlannedWorkouts = p.PlannedWorkouts
            .OrderBy(pw => pw.ScheduledDate)
            .Select(Map)
            .ToList()
    };

    private static PlannedWorkoutResponse Map(PlannedWorkout pw) => new()
    {
        Id = pw.Id,
        TrainingPlanId = pw.TrainingPlanId,
        Sport = pw.Sport,
        ScheduledDate = pw.ScheduledDate,
        Title = pw.Title,
        Description = pw.Description,
        PlannedDurationMinutes = pw.PlannedDurationMinutes,
        PlannedLoad = pw.PlannedLoad,
        // Plan-level read (no Blocks) — computed stays null; effective falls back to the manual override.
        EffectiveLoad = pw.PlannedLoad,
        IsLoadOverride = pw.PlannedLoad is not null
    };
}
