using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using Bryk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bryk.Infrastructure.Repositories;

public class EquipmentRepository(ApplicationDbContext db) : IEquipmentRepository
{
    public async Task<Equipment?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Equipment
            .AsNoTracking()
            .FirstOrDefaultAsync(eq => eq.Id == id, ct);
    }

    public async Task<IReadOnlyList<Equipment>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default)
    {
        return await db.Equipment
            .AsNoTracking()
            .Where(eq => eq.AthleteId == athleteId)
            .OrderBy(eq => eq.Type)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Equipment>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Equipment
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task AddAsync(Equipment entity, CancellationToken ct = default)
    {
        await db.Equipment.AddAsync(entity, ct);
    }

    public void Update(Equipment entity)
    {
        db.Equipment.Update(entity);
    }

    public void Delete(Equipment entity)
    {
        db.Equipment.Remove(entity);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await db.SaveChangesAsync(ct);
    }
}
