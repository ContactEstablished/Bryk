using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using Bryk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bryk.Infrastructure.Repositories;

public class AthleteSportZoneRepository(ApplicationDbContext db) : IAthleteSportZoneRepository
{
    public async Task<IReadOnlyList<AthleteSportZone>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default)
    {
        return await db.AthleteSportZones
            .AsNoTracking()
            .Where(z => z.AthleteId == athleteId)
            .ToListAsync(ct);
    }

    public async Task AddAsync(AthleteSportZone entity, CancellationToken ct = default)
    {
        await db.AthleteSportZones.AddAsync(entity, ct);
    }

    public void Remove(AthleteSportZone entity)
    {
        db.AthleteSportZones.Remove(entity);
    }
}
