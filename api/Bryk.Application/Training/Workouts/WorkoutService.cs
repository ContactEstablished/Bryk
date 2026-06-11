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
    IValidator<UpdateWorkoutRequest> updateValidator,
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

        foreach (var result in BuildStepResults(request.StepResults, planned, athleteId, workout.Id))
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

    public async Task<WorkoutResponse> UpdateAsync(Guid id, UpdateWorkoutRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateOrThrowAsync(request, ct);
        var athleteId = currentUser.GetCurrentAthleteId();

        var workout = await workoutRepo.GetByIdTrackedAsync(id, ct);
        if (workout is null || workout.AthleteId != athleteId)
        {
            throw new KeyNotFoundException();
        }

        // If (re)linked to a plan, verify ownership (and seed step results from its planned steps if none given).
        PlannedWorkout? planned = null;
        if (request.PlannedWorkoutId is { } pwId)
        {
            planned = await planRepo.GetPlannedWorkoutWithStructureAsync(pwId, ct);
            if (planned is null || planned.AthleteId != athleteId)
            {
                throw new KeyNotFoundException();
            }
        }

        workout.PlannedWorkoutId = request.PlannedWorkoutId;
        workout.Sport = request.Sport;
        workout.CompletedDate = request.CompletedDate;
        workout.ActualDurationSeconds = request.ActualDurationSeconds;
        workout.ActualDistanceMeters = request.ActualDistanceMeters;
        workout.AvgHr = request.AvgHr;
        workout.MaxHr = request.MaxHr;
        workout.LoadOverride = request.LoadOverride;
        workout.Rpe = request.Rpe;
        workout.Notes = request.Notes;

        // Replace the step-result list: clearing the tracked collection orphan-deletes the old rows
        // (required FK → cascade), then stage the new ones.
        workout.StepResults.Clear();
        foreach (var result in BuildStepResults(request.StepResults, planned, athleteId, workout.Id))
        {
            workout.StepResults.Add(result);
        }

        // Recompute actual load from the edited actuals (LoadOverride is passed through above).
        workout.ComputedLoad = await loadService.ComputeActualLoadAsync(workout, ct);

        await unitOfWork.SaveChangesAsync(ct);

        var saved = await workoutRepo.GetByIdAsync(workout.Id, ct);
        return await MapWithPlanAsync(saved ?? workout, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var workout = await workoutRepo.GetByIdTrackedAsync(id, ct);
        if (workout is null || workout.AthleteId != currentUser.GetCurrentAthleteId())
        {
            throw new KeyNotFoundException();
        }

        workoutRepo.Delete(workout);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<WorkoutResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        var workout = await workoutRepo.GetByIdAsync(id, ct);
        if (workout is null || workout.AthleteId != currentUser.GetCurrentAthleteId())
        {
            throw new KeyNotFoundException();
        }

        return await MapWithPlanAsync(workout, ct);
    }

    public async Task<IReadOnlyList<WorkoutResponse>> GetWorkoutsAsync(DateOnly? from, DateOnly? to, Sport? sport, int? skip, int? take, CancellationToken ct = default)
    {
        // Pagination convention: take clamped 1..100 (default 20); skip >= 0 (default 0). Out-of-range clamps, never rejects.
        var pageSize = take switch { null or <= 0 => 20, > 100 => 100, _ => take.Value };
        var offset = skip is > 0 ? skip.Value : 0;

        var workouts = await workoutRepo.GetByAthleteFilteredAsync(
            currentUser.GetCurrentAthleteId(), from, to, sport, offset, pageSize, ct);
        return workouts.Select(Map).ToList();
    }

    // Per-step actuals: from the request, else seeded (empty, linked) from the planned steps for comparison.
    private static List<WorkoutStepResult> BuildStepResults(List<WorkoutStepResultDto>? requestStepResults, PlannedWorkout? planned, Guid athleteId, Guid workoutId)
    {
        if (requestStepResults is { Count: > 0 })
        {
            return requestStepResults.Select((dto, i) => new WorkoutStepResult
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

    // Detail/edit reads also expose the linked plan id (so the client can reach GET .../structure for
    // planned-vs-actual). Resolving it needs one extra lookup, so list reads stay on the plain Map.
    private async Task<WorkoutResponse> MapWithPlanAsync(Workout w, CancellationToken ct)
    {
        var response = Map(w);
        if (w.PlannedWorkoutId is { } pwId)
        {
            var planned = await planRepo.GetPlannedWorkoutWithStructureAsync(pwId, ct);
            response.TrainingPlanId = planned?.TrainingPlanId;
        }

        return response;
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
