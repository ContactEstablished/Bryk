using Bryk.Application.Common;
using Bryk.Application.Common.Validation;
using Bryk.Application.Training.Load;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentValidation;
using ValidationException = Bryk.Application.Exceptions.ValidationException;

namespace Bryk.Application.Training;

public class StructuredWorkoutService(
    ICurrentUserService currentUser,
    IValidator<WorkoutStructureRequest> validator,
    ITrainingPlanRepository planRepo,
    IUnitOfWork unitOfWork,
    ILoadService loadService) : IStructuredWorkoutService
{
    public async Task<PlannedWorkoutResponse> GetStructureAsync(Guid planId, Guid plannedWorkoutId, CancellationToken ct = default)
    {
        var workout = await LoadOwnedWorkoutAsync(planId, plannedWorkoutId, ct);
        return await MapWithLoadAsync(workout, ct);
    }

    public async Task<PlannedWorkoutResponse> SetStructureAsync(Guid planId, Guid plannedWorkoutId, WorkoutStructureRequest request, CancellationToken ct = default)
    {
        await validator.ValidateOrThrowAsync(request, ct);

        var workout = await LoadOwnedWorkoutAsync(planId, plannedWorkoutId, ct);
        ValidateForSport(workout.Sport, request);

        // Set/replace: stage-delete the existing blocks (steps cascade at the DB) and stage the new graph.
        foreach (var existing in workout.Blocks)
        {
            planRepo.RemoveWorkoutBlock(new WorkoutBlock { Id = existing.Id });
        }

        foreach (var blockDto in request.Blocks)
        {
            var block = new WorkoutBlock
            {
                Id = Guid.NewGuid(),
                AthleteId = workout.AthleteId,
                PlannedWorkoutId = workout.Id,
                OrderIndex = blockDto.OrderIndex,
                Repeats = blockDto.Repeats
            };

            var stepIndex = 0;
            foreach (var stepDto in blockDto.Steps)
            {
                block.Steps.Add(NewStep(workout.AthleteId, block.Id, stepIndex++, stepDto));
            }

            await planRepo.AddWorkoutBlockAsync(block, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);

        var refreshed = await LoadOwnedWorkoutAsync(planId, plannedWorkoutId, ct);
        return await MapWithLoadAsync(refreshed, ct);
    }

    // Loads the planned workout with its structured payload and asserts it belongs to the route's plan
    // and the current athlete (denormalized AthleteId, ADR-0003). KeyNotFoundException → 404.
    private async Task<PlannedWorkout> LoadOwnedWorkoutAsync(Guid planId, Guid plannedWorkoutId, CancellationToken ct)
    {
        var workout = await planRepo.GetPlannedWorkoutWithStructureAsync(plannedWorkoutId, ct);
        if (workout is null
            || workout.TrainingPlanId != planId
            || workout.AthleteId != currentUser.GetCurrentAthleteId())
        {
            throw new KeyNotFoundException();
        }

        return workout;
    }

    // Sport-discriminated rules (ADR-0004 §2/§3) the schema and the sport-agnostic validator can't express.
    private static void ValidateForSport(Sport sport, WorkoutStructureRequest request)
    {
        var errors = new List<string>();
        var isStrength = sport == Sport.Strength;
        var zoneCount = ZoneScheme.Count(sport);

        foreach (var step in request.Blocks.SelectMany(b => b.Steps))
        {
            if (isStrength)
            {
                if (step.Sets is null || step.Reps is null)
                {
                    errors.Add("A strength step requires both Sets and Reps.");
                }

                if (step.TargetZone is not null
                    || step.TargetPowerLow is not null || step.TargetPowerHigh is not null
                    || step.TargetPaceLow is not null || step.TargetPaceHigh is not null)
                {
                    errors.Add("A strength step cannot carry a power, pace, or zone target.");
                }
            }
            else
            {
                if (step.Sets is not null || step.Reps is not null || step.LoadKg is not null || step.Rpe is not null)
                {
                    errors.Add("A cardio step cannot carry strength fields (sets/reps/load/RPE).");
                }

                if ((step.DurationSeconds is null) == (step.DistanceMeters is null))
                {
                    errors.Add("A cardio step must set exactly one of duration or distance.");
                }

                if (step.TargetZone is { } zone && (zoneCount == 0 || zone > zoneCount))
                {
                    errors.Add(zoneCount == 0
                        ? $"{sport} has no training zones, so a step cannot reference a zone."
                        : $"TargetZone must be between 1 and {zoneCount} for {sport}.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors.Distinct());
        }
    }

    private static WorkoutStep NewStep(Guid athleteId, Guid blockId, int orderIndex, WorkoutStepDto dto) => new()
    {
        Id = Guid.NewGuid(),
        AthleteId = athleteId,
        WorkoutBlockId = blockId,
        OrderIndex = orderIndex,
        Intent = dto.Intent,
        Title = dto.Title,
        DurationSeconds = dto.DurationSeconds,
        DistanceMeters = dto.DistanceMeters,
        TargetZone = dto.TargetZone,
        TargetPowerLow = dto.TargetPowerLow,
        TargetPowerHigh = dto.TargetPowerHigh,
        TargetHrLow = dto.TargetHrLow,
        TargetHrHigh = dto.TargetHrHigh,
        TargetPaceLow = dto.TargetPaceLow,
        TargetPaceHigh = dto.TargetPaceHigh,
        Sets = dto.Sets,
        Reps = dto.Reps,
        LoadKg = dto.LoadKg,
        Rpe = dto.Rpe
    };

    // Wraps Map with the computed/effective load (ADR-0005 §3); the calculator needs the loaded Blocks.
    private async Task<PlannedWorkoutResponse> MapWithLoadAsync(PlannedWorkout workout, CancellationToken ct)
    {
        var response = Map(workout);
        var computed = await loadService.ComputePlannedLoadAsync(workout, ct);
        response.ComputedLoad = computed;
        response.IsLoadOverride = workout.PlannedLoad is not null;
        response.EffectiveLoad = workout.PlannedLoad ?? computed;
        return response;
    }

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
        Blocks = pw.Blocks
            .OrderBy(b => b.OrderIndex)
            .Select(b => new WorkoutBlockResponse
            {
                Id = b.Id,
                OrderIndex = b.OrderIndex,
                Repeats = b.Repeats,
                Steps = b.Steps
                    .OrderBy(s => s.OrderIndex)
                    .Select(MapStep)
                    .ToList()
            })
            .ToList()
    };

    private static WorkoutStepResponse MapStep(WorkoutStep s) => new()
    {
        Id = s.Id,
        OrderIndex = s.OrderIndex,
        Intent = s.Intent,
        Title = s.Title,
        DurationSeconds = s.DurationSeconds,
        DistanceMeters = s.DistanceMeters,
        TargetZone = s.TargetZone,
        TargetPowerLow = s.TargetPowerLow,
        TargetPowerHigh = s.TargetPowerHigh,
        TargetHrLow = s.TargetHrLow,
        TargetHrHigh = s.TargetHrHigh,
        TargetPaceLow = s.TargetPaceLow,
        TargetPaceHigh = s.TargetPaceHigh,
        Sets = s.Sets,
        Reps = s.Reps,
        LoadKg = s.LoadKg,
        Rpe = s.Rpe
    };
}
