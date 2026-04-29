using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using Bryk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bryk.Infrastructure.Repositories;

public class AthleteRepository(ApplicationDbContext db) : IAthleteRepository
{
    public async Task<Athlete?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Athletes
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<Athlete?> GetWithSportProfilesAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Athletes
            .Include(a => a.SportProfiles)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<Athlete?> GetFullProfileAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Athletes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(a => a.SportProfiles)
            .Include(a => a.Events)
            .Include(a => a.Goals)
            .Include(a => a.Equipment)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<IReadOnlyList<Athlete>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Athletes
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task AddAsync(Athlete athlete, CancellationToken ct = default)
    {
        await db.Athletes.AddAsync(athlete, ct);
    }

    public void Update(Athlete athlete)
    {
        db.Athletes.Update(athlete);
    }

    public void Delete(Athlete athlete)
    {
        db.Athletes.Remove(athlete);
    }
}
