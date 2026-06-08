using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using Bryk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bryk.Infrastructure.Repositories;

public class TrainingPlanRepository(ApplicationDbContext db) : ITrainingPlanRepository
{
    public async Task<TrainingPlan?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.TrainingPlans
            .AsNoTracking()
            .Include(p => p.PlannedWorkouts)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<TrainingPlan>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default)
    {
        return await db.TrainingPlans
            .AsNoTracking()
            .Where(p => p.AthleteId == athleteId)
            .OrderBy(p => p.StartDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        return await db.PlannedWorkouts
            .AsNoTracking()
            .Where(pw => pw.AthleteId == athleteId && pw.ScheduledDate >= start && pw.ScheduledDate <= end)
            .OrderBy(pw => pw.ScheduledDate)
            .ThenBy(pw => pw.Sport)
            .ToListAsync(ct);
    }

    public async Task AddAsync(TrainingPlan entity, CancellationToken ct = default)
    {
        await db.TrainingPlans.AddAsync(entity, ct);
    }

    public void Update(TrainingPlan entity)
    {
        db.TrainingPlans.Update(entity);
    }

    public void Delete(TrainingPlan entity)
    {
        db.TrainingPlans.Remove(entity);
    }

    public async Task AddPlannedWorkoutAsync(PlannedWorkout plannedWorkout, CancellationToken ct = default)
    {
        await db.PlannedWorkouts.AddAsync(plannedWorkout, ct);
    }

    public void UpdatePlannedWorkout(PlannedWorkout plannedWorkout)
    {
        db.PlannedWorkouts.Update(plannedWorkout);
    }

    public void RemovePlannedWorkout(PlannedWorkout plannedWorkout)
    {
        db.PlannedWorkouts.Remove(plannedWorkout);
    }

    public async Task<PlannedWorkout?> GetPlannedWorkoutWithStructureAsync(Guid plannedWorkoutId, CancellationToken ct = default)
    {
        return await db.PlannedWorkouts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(pw => pw.Blocks.OrderBy(b => b.OrderIndex))
                .ThenInclude(b => b.Steps.OrderBy(s => s.OrderIndex))
            .FirstOrDefaultAsync(pw => pw.Id == plannedWorkoutId, ct);
    }

    public async Task AddWorkoutBlockAsync(WorkoutBlock block, CancellationToken ct = default)
    {
        await db.WorkoutBlocks.AddAsync(block, ct);
    }

    public void UpdateWorkoutBlock(WorkoutBlock block)
    {
        db.WorkoutBlocks.Update(block);
    }

    public void RemoveWorkoutBlock(WorkoutBlock block)
    {
        db.WorkoutBlocks.Remove(block);
    }

    public async Task AddWorkoutStepAsync(WorkoutStep step, CancellationToken ct = default)
    {
        await db.WorkoutSteps.AddAsync(step, ct);
    }

    public void UpdateWorkoutStep(WorkoutStep step)
    {
        db.WorkoutSteps.Update(step);
    }

    public void RemoveWorkoutStep(WorkoutStep step)
    {
        db.WorkoutSteps.Remove(step);
    }
}
