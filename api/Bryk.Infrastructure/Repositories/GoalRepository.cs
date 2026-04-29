using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using Bryk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bryk.Infrastructure.Repositories;

public class GoalRepository(ApplicationDbContext db) : IGoalRepository
{
    public async Task<Goal?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Goals
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async Task<IReadOnlyList<Goal>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default)
    {
        return await db.Goals
            .AsNoTracking()
            .Where(g => g.AthleteId == athleteId)
            .OrderBy(g => g.TargetDate == null ? 1 : 0)
            .ThenBy(g => g.TargetDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Goal>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Goals
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task AddAsync(Goal entity, CancellationToken ct = default)
    {
        await db.Goals.AddAsync(entity, ct);
    }

    public void Update(Goal entity)
    {
        db.Goals.Update(entity);
    }

    public void Delete(Goal entity)
    {
        db.Goals.Remove(entity);
    }
}
