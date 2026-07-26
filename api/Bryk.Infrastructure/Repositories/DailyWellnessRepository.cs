using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using Bryk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bryk.Infrastructure.Repositories;

public class DailyWellnessRepository(ApplicationDbContext db) : IDailyWellnessRepository
{
    public async Task<DailyWellness?> GetByAthleteAndDateTrackedAsync(Guid athleteId, DateOnly date, CancellationToken ct = default)
    {
        // No AsNoTracking() on purpose: the per-day upsert (Task 20-2) mutates this instance in place and
        // commits once through IUnitOfWork. Adding AsNoTracking() here silently breaks that write path.
        return await db.DailyWellness
            .FirstOrDefaultAsync(w => w.AthleteId == athleteId && w.Date == date, ct);
    }

    public async Task<IReadOnlyList<DailyWellness>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        return await db.DailyWellness
            .AsNoTracking()
            .Where(w => w.AthleteId == athleteId && w.Date >= from && w.Date <= to)
            .OrderBy(w => w.Date)
            .ToListAsync(ct);
    }

    public async Task AddAsync(DailyWellness entity, CancellationToken ct = default)
    {
        await db.DailyWellness.AddAsync(entity, ct);
    }

    public void Update(DailyWellness entity)
    {
        db.DailyWellness.Update(entity);
    }
}
