using Bryk.Application.Common;
using Bryk.Application.Common.Validation;
using Bryk.Application.Training.Load;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentValidation;

namespace Bryk.Application.Training.Workouts;

public class WorkoutService(
    ICurrentUserService currentUser,
    IValidator<LogWorkoutRequest> validator,
    IWorkoutRepository workoutRepo,
    ITrainingPlanRepository planRepo,
    ILoadService loadService,
    IUnitOfWork unitOfWork) : IWorkoutService
{
    public async Task<WorkoutResponse> LogAsync(LogWorkoutRequest request, CancellationToken ct = default)
    {
        await validator.ValidateOrThrowAsync(request, ct);
        var athleteId = currentUser.GetCurrentAthleteId();

        // If linked to a plan, verify ownership (and seed step results from its planned steps if none given).
        PlannedWorkout? planned = null;
        if (request.PlannedWorkoutId is { } pwId)
        {
            planned = await planRepo.GetPlannedWorkoutWithStructureAsync(pwId, ct);
            if (planned is null || planned.AthleteId != athleteId)
            {
                throw new KeyNotFoundException();
            }
        }

        var workout = new Workout
        {
            Id = Guid.NewGuid(),
            AthleteId = athleteId,
            PlannedWorkoutId = request.PlannedWorkoutId,
            Sport = request.Sport,
            CompletedDate = request.CompletedDate,
            ActualDurationSeconds = request.ActualDurationSeconds,
            ActualDistanceMeters = request.ActualDistanceMeters,
            AvgHr = request.AvgHr,
            MaxHr = request.MaxHr,
            LoadOverride = request.LoadOverride,
            Rpe = request.Rpe,
            Notes = request.Notes
        };

        foreach (var result in BuildStepResults(request, planned, athleteId, workout.Id))
        {
            workout.StepResults.Add(result);
        }

        // Actual load from captured actuals (ADR-0005 §6), persisted so historical reads stay single-table.
        workout.ComputedLoad = await loadService.ComputeActualLoadAsync(workout, ct);

        await workoutRepo.AddAsync(workout, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var saved = await workoutRepo.GetByIdAsync(workout.Id, ct);
        return Map(saved ?? workout);
    }

    public async Task<WorkoutResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var workout = await workoutRepo.GetByIdAsync(id, ct);
        if (workout is null || workout.AthleteId != currentUser.GetCurrentAthleteId())
        {
            throw new KeyNotFoundException();
        }

        return Map(workout);
    }

    public async Task<IReadOnlyList<WorkoutResponse>> GetRecentAsync(int take, CancellationToken ct = default)
    {
        var capped = take is > 0 and <= 100 ? take : 10;
        var workouts = await workoutRepo.GetRecentByAthleteAsync(currentUser.GetCurrentAthleteId(), capped, ct);
        return workouts.Select(Map).ToList();
    }

    // Per-step actuals: from the request, else seeded (empty, linked) from the planned steps for comparison.
    private static List<WorkoutStepResult> BuildStepResults(LogWorkoutRequest request, PlannedWorkout? planned, Guid athleteId, Guid workoutId)
    {
        if (request.StepResults is { Count: > 0 })
        {
            return request.StepResults.Select((dto, i) => new WorkoutStepResult
            {
                Id = Guid.NewGuid(),
                AthleteId = athleteId,
                WorkoutId = workoutId,
                WorkoutStepId = dto.WorkoutStepId,
                OrderIndex = i,
                ActualDurationSeconds = dto.ActualDurationSeconds,
                ActualDistanceMeters = dto.ActualDistanceMeters,
                AvgPower = dto.AvgPower,
                AvgHr = dto.AvgHr,
                AvgPace = dto.AvgPace,
                Rpe = dto.Rpe
            }).ToList();
        }

        if (planned is not null)
        {
            var plannedSteps = planned.Blocks
                .OrderBy(b => b.OrderIndex)
                .SelectMany(b => b.Steps.OrderBy(s => s.OrderIndex))
                .ToList();
            return plannedSteps.Select((s, i) => new WorkoutStepResult
            {
                Id = Guid.NewGuid(),
                AthleteId = athleteId,
                WorkoutId = workoutId,
                WorkoutStepId = s.Id,
                OrderIndex = i
            }).ToList();
        }

        return new List<WorkoutStepResult>();
    }

    private static WorkoutResponse Map(Workout w) => new()
    {
        Id = w.Id,
        PlannedWorkoutId = w.PlannedWorkoutId,
        Sport = w.Sport,
        CompletedDate = w.CompletedDate,
        ActualDurationSeconds = w.ActualDurationSeconds,
        ActualDistanceMeters = w.ActualDistanceMeters,
        AvgHr = w.AvgHr,
        MaxHr = w.MaxHr,
        ComputedLoad = w.ComputedLoad,
        LoadOverride = w.LoadOverride,
        EffectiveLoad = w.LoadOverride ?? w.ComputedLoad,
        IsLoadOverride = w.LoadOverride is not null,
        Rpe = w.Rpe,
        Notes = w.Notes,
        StepResults = w.StepResults
            .OrderBy(r => r.OrderIndex)
            .Select(MapResult)
            .ToList()
    };

    private static WorkoutStepResultResponse MapResult(WorkoutStepResult r) => new()
    {
        Id = r.Id,
        WorkoutStepId = r.WorkoutStepId,
        OrderIndex = r.OrderIndex,
        ActualDurationSeconds = r.ActualDurationSeconds,
        ActualDistanceMeters = r.ActualDistanceMeters,
        AvgPower = r.AvgPower,
        AvgHr = r.AvgHr,
        AvgPace = r.AvgPace,
        Rpe = r.Rpe
    };
}
