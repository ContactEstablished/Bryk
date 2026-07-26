using Bryk.Application.Calendar;
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
    IValidator<ScheduleRequest> scheduleValidator,
    IValidator<TrainingPlanUpdateRequest> updateValidator,
    ITrainingPlanRepository planRepo,
    IEventRepository eventRepo,
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

    public async Task<TrainingPlanResponse> UpdateAsync(Guid id, TrainingPlanUpdateRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateOrThrowAsync(request, ct);

        var plan = await LoadOwnedPlanAsync(id, ct);

        // Orphan guard (ADR-0009 §5): a window that would leave existing planned workouts stranded is
        // rejected — the client reschedules or removes them first. Window containment is inclusive on
        // both ends (a workout scheduled exactly on StartDate or EndDate is NOT stranded).
        var stranded = plan.PlannedWorkouts
            .Where(pw => pw.ScheduledDate < request.StartDate || pw.ScheduledDate > request.EndDate)
            .ToList();
        if (stranded.Count > 0)
        {
            throw new Exceptions.ValidationException(new[]
            {
                $"PlanWindow: {stranded.Count} planned workout(s) fall outside the requested window " +
                $"({request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}); reschedule or remove them first " +
                $"(earliest {stranded.Min(pw => pw.ScheduledDate):yyyy-MM-dd}, latest {stranded.Max(pw => pw.ScheduledDate):yyyy-MM-dd})."
            });
        }

        // Event-ownership guard. A null EventId clears the link with no read.
        if (request.EventId is { } eventId)
        {
            var ev = await eventRepo.GetByIdAsync(eventId, ct);
            if (ev is null || ev.AthleteId != plan.AthleteId)
            {
                throw new Exceptions.ValidationException(new[]
                {
                    "EventId: The selected event does not exist or belongs to another athlete."
                });
            }
        }

        // Stage a fresh, nav-free entity: the loaded `plan` came from a no-tracking Include, so
        // re-attaching it would drag PlannedWorkouts into the change tracker. CreatedAt is carried
        // over; the interceptor sets UpdatedAt. Never set UpdatedAt here.
        var updated = new TrainingPlan
        {
            Id = plan.Id,
            AthleteId = plan.AthleteId,
            Name = request.Name,
            Methodology = request.Methodology,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            EventId = request.EventId,
            BuildWeeks = request.BuildWeeks,
            RecoveryWeeks = request.RecoveryWeeks,
            RecoveryWeekPercentage = request.RecoveryWeekPercentage,
            CreatedAt = plan.CreatedAt
        };

        planRepo.Update(updated);
        await unitOfWork.SaveChangesAsync(ct);

        // TRAP: Map(updated) alone returns an EMPTY PlannedWorkouts — `updated` is nav-free by design
        // (see the staging comment above). Re-attach the untouched children from the originally loaded
        // `plan` for the projection only; do not mutate `updated.PlannedWorkouts` after SaveChangesAsync.
        var response = Map(updated);
        response.PlannedWorkouts = plan.PlannedWorkouts.OrderBy(pw => pw.ScheduledDate).Select(Map).ToList();
        return response;
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

    public async Task RescheduleAsync(Guid planId, Guid plannedWorkoutId, ScheduleRequest request, CancellationToken ct = default)
    {
        await scheduleValidator.ValidateOrThrowAsync(request, ct);

        var plan = await LoadOwnedPlanAsync(planId, ct);
        var existing = plan.PlannedWorkouts.FirstOrDefault(pw => pw.Id == plannedWorkoutId)
            ?? throw new KeyNotFoundException();

        if (request.ScheduledDate < plan.StartDate || request.ScheduledDate > plan.EndDate)
        {
            throw new Exceptions.ValidationException(new[]
            {
                $"ScheduledDate: Scheduled date must be within the plan window ({plan.StartDate:yyyy-MM-dd} to {plan.EndDate:yyyy-MM-dd})."
            });
        }

        // Stage a fresh nav-free entity (mirror UpdatePlannedWorkoutAsync's discipline).
        var updated = new PlannedWorkout
        {
            Id = existing.Id,
            AthleteId = existing.AthleteId,
            TrainingPlanId = existing.TrainingPlanId,
            Sport = existing.Sport,
            ScheduledDate = request.ScheduledDate,
            Title = existing.Title,
            Description = existing.Description,
            PlannedDurationMinutes = existing.PlannedDurationMinutes,
            PlannedLoad = existing.PlannedLoad,
            CreatedAt = existing.CreatedAt
        };

        planRepo.UpdatePlannedWorkout(updated);
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
