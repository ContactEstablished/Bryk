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

    public async Task<IReadOnlyList<Workout>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        return await db.Workouts
            .AsNoTracking()
            .Where(w => w.AthleteId == athleteId && w.CompletedDate >= start && w.CompletedDate <= end)
            .OrderByDescending(w => w.CompletedDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Workout>> GetRecentByAthleteAsync(Guid athleteId, int take, CancellationToken ct = default)
    {
        return await db.Workouts
            .AsNoTracking()
            .Where(w => w.AthleteId == athleteId)
            .OrderByDescending(w => w.CompletedDate)
            .ThenByDescending(w => w.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Workout workout, CancellationToken ct = default) => await db.Workouts.AddAsync(workout, ct);

    public void Update(Workout workout) => db.Workouts.Update(workout);

    public void Delete(Workout workout) => db.Workouts.Remove(workout);
}
