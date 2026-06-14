using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using Bryk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bryk.Infrastructure.Repositories;

public class WorkoutRepository(ApplicationDbContext db) : IWorkoutRepository
{
    public async Task<Workout?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Workouts
            .AsNoTracking()
            .Include(w => w.StepResults.OrderBy(r => r.OrderIndex))
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<Workout?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Workouts
            .Include(w => w.StepResults)
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<IReadOnlyList<Workout>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        return await db.Workouts
            .AsNoTracking()
            .Where(w => w.AthleteId == athleteId && w.CompletedDate >= start && w.CompletedDate <= end)
            .OrderByDescending(w => w.CompletedDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Workout>> GetByAthleteFilteredAsync(Guid athleteId, DateOnly? from, DateOnly? to, Sport? sport, int skip, int take, CancellationToken ct = default)
    {
        var query = db.Workouts.AsNoTracking().Where(w => w.AthleteId == athleteId);

        if (from is { } f) query = query.Where(w => w.CompletedDate >= f);
        if (to is { } t) query = query.Where(w => w.CompletedDate <= t);
        if (sport is { } s) query = query.Where(w => w.Sport == s);

        return await query
            .OrderByDescending(w => w.CompletedDate)
            .ThenByDescending(w => w.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<DateOnly?> GetFirstWorkoutDateAsync(Guid athleteId, CancellationToken ct = default)
    {
        // Project to a nullable so an athlete with no workouts yields null instead of throwing on Min.
        return await db.Workouts
            .Where(w => w.AthleteId == athleteId)
            .Select(w => (DateOnly?)w.CompletedDate)
            .MinAsync(ct);
    }

    public async Task<IReadOnlyList<Workout>> GetByAthleteWithStepResultsAsync(Guid athleteId, Sport? sport, CancellationToken ct = default)
    {
        var query = db.Workouts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(w => w.StepResults)
            .Where(w => w.AthleteId == athleteId);

        if (sport is { } s) query = query.Where(w => w.Sport == s);

        return await query
            .OrderByDescending(w => w.CompletedDate)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Workout workout, CancellationToken ct = default) => await db.Workouts.AddAsync(workout, ct);

    public void Update(Workout workout) => db.Workouts.Update(workout);

    public void Delete(Workout workout) => db.Workouts.Remove(workout);
}
