using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using Bryk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bryk.Infrastructure.Repositories;

public class ActivityFileRepository(ApplicationDbContext db) : IActivityFileRepository
{
    public async Task AddAsync(ActivityFile file, CancellationToken ct = default) => await db.ActivityFiles.AddAsync(file, ct);

    public async Task<ActivityFile?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default)
    {
        return await db.ActivityFiles.FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<IReadOnlyList<ActivityFile>> GetByParsedWorkoutIdsAsync(Guid athleteId, IEnumerable<Guid> workoutIds, CancellationToken ct = default)
    {
        var ids = workoutIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return Array.Empty<ActivityFile>();
        }

        // Project scalar columns only — never Content. 19-6 calls this once per workout in a 90-day
        // analytics range; loading varbinary(max) for every matched row would be tens of megabytes per
        // request. Do NOT replace this with a plain entity query "for readability".
        var rows = await db.ActivityFiles
            .AsNoTracking()
            .Where(f => f.AthleteId == athleteId && f.ParsedWorkoutId != null && ids.Contains(f.ParsedWorkoutId.Value))
            .Select(f => new { f.Id, f.AthleteId, f.FileName, f.Format, f.ByteSize, f.UploadedAt, f.ParsedWorkoutId, f.ZoneHistogramJson })
            .ToListAsync(ct);

        return rows.Select(r => new ActivityFile
        {
            Id = r.Id,
            AthleteId = r.AthleteId,
            FileName = r.FileName,
            Format = r.Format,
            ByteSize = r.ByteSize,
            UploadedAt = r.UploadedAt,
            ParsedWorkoutId = r.ParsedWorkoutId,
            ZoneHistogramJson = r.ZoneHistogramJson
        }).ToList();
    }

    public void Delete(ActivityFile file) => db.ActivityFiles.Remove(file);
}
