using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using Bryk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bryk.Infrastructure.Repositories;

public class EventRepository(ApplicationDbContext db) : IEventRepository
{
    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<IReadOnlyList<Event>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default)
    {
        return await db.Events
            .AsNoTracking()
            .Where(e => e.AthleteId == athleteId)
            .OrderBy(e => e.EventDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Event>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Events
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task AddAsync(Event entity, CancellationToken ct = default)
    {
        await db.Events.AddAsync(entity, ct);
    }

    public void Update(Event entity)
    {
        db.Events.Update(entity);
    }

    public void Delete(Event entity)
    {
        db.Events.Remove(entity);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await db.SaveChangesAsync(ct);
    }
}
