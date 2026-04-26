using Bryk.Application.DTOs.Mesocycle;
using Bryk.Application.Interfaces;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bryk.Infrastructure.Services;

public class MesocycleService(ApplicationDbContext context) : IMesocycleService
{
    // GET: return all mesocycles as List<MesocycleDto>
    public async Task<List<MesocycleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await context.Mesocycles
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.Select(ToDto).ToList();
    }

    // GET: return a single MesocycleDto, throw KeyNotFoundException if not found
    public async Task<MesocycleDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Mesocycles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"Mesocycle with ID {id} not found.");

        return ToDto(entity);
    }

    // GET: return MesocycleWithWeeksDto with weeks included, throw KeyNotFoundException if not found
    public async Task<MesocycleWithWeeksDto> GetWithWeeksAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Mesocycles
            .AsNoTracking()
            .Include(m => m.Weeks)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"Mesocycle with ID {id} not found.");

        var dto = new MesocycleWithWeeksDto
        {
            Id = entity.Id,
            Name = entity.Name,
            StartDate = entity.StartDate,
            NumberOfWeeks = entity.NumberOfWeeks,
            StartingWeeklyTss = entity.StartingWeeklyTss,
            WeeklyIncreasePercentage = entity.WeeklyIncreasePercentage,
            BuildRecoveryRatio = entity.BuildRecoveryRatio,
            RecoveryWeekPercentage = entity.RecoveryWeekPercentage,
            WeeklyPatternType = entity.WeeklyPatternType,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Weeks = entity.Weeks.Select(ToWeekSummaryDto).ToList()
        };

        return dto;
    }

    // POST: create entity from CreateMesocycleDto, save, return MesocycleDto
    public async Task<MesocycleDto> CreateAsync(CreateMesocycleDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new Mesocycle
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            StartDate = dto.StartDate,
            NumberOfWeeks = dto.NumberOfWeeks,
            StartingWeeklyTss = dto.StartingWeeklyTss,
            WeeklyIncreasePercentage = dto.WeeklyIncreasePercentage,
            BuildRecoveryRatio = dto.BuildRecoveryRatio,
            RecoveryWeekPercentage = dto.RecoveryWeekPercentage,
            WeeklyPatternType = dto.WeeklyPatternType,
        };

        context.Mesocycles.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    // PUT: load entity, apply only non-null fields from UpdateMesocycleDto, save, return MesocycleDto
    public async Task<MesocycleDto> UpdateAsync(Guid id, UpdateMesocycleDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await context.Mesocycles
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"Mesocycle with ID {id} not found.");

        if (dto.Name is not null) entity.Name = dto.Name;
        if (dto.StartDate.HasValue) entity.StartDate = dto.StartDate.Value;
        if (dto.NumberOfWeeks.HasValue) entity.NumberOfWeeks = dto.NumberOfWeeks.Value;
        if (dto.StartingWeeklyTss.HasValue) entity.StartingWeeklyTss = dto.StartingWeeklyTss.Value;
        if (dto.WeeklyIncreasePercentage.HasValue) entity.WeeklyIncreasePercentage = dto.WeeklyIncreasePercentage.Value;
        if (dto.BuildRecoveryRatio is not null) entity.BuildRecoveryRatio = dto.BuildRecoveryRatio;
        if (dto.RecoveryWeekPercentage.HasValue) entity.RecoveryWeekPercentage = dto.RecoveryWeekPercentage.Value;
        if (dto.WeeklyPatternType is not null) entity.WeeklyPatternType = dto.WeeklyPatternType;

        await context.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    // DELETE: load entity, delete, save, throw KeyNotFoundException if not found
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Mesocycles
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"Mesocycle with ID {id} not found.");

        context.Mesocycles.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    // TODO: implement week generation logic
    public Task RegenerateWeeksAsync(Guid id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    // Private static mapping methods

    private static MesocycleDto ToDto(Mesocycle entity)
    {
        return new MesocycleDto
        {
            Id = entity.Id,
            Name = entity.Name,
            StartDate = entity.StartDate,
            NumberOfWeeks = entity.NumberOfWeeks,
            StartingWeeklyTss = entity.StartingWeeklyTss,
            WeeklyIncreasePercentage = entity.WeeklyIncreasePercentage,
            BuildRecoveryRatio = entity.BuildRecoveryRatio,
            RecoveryWeekPercentage = entity.RecoveryWeekPercentage,
            WeeklyPatternType = entity.WeeklyPatternType,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            // Computed fields set to 0 — requires weeks to be loaded; calculated in follow-up
            TotalTss = 0,
            AverageWeeklyTss = 0,
            PeakWeekTss = 0,
            PeakWeekNumber = 0,
            RecoveryWeekCount = 0,
        };
    }

    private static WeekSummaryDto ToWeekSummaryDto(Week entity)
    {
        return new WeekSummaryDto
        {
            Id = entity.Id,
            WeekNumber = entity.WeekNumber,
            TargetTss = entity.TargetTss,
            ActualTss = 0, // Requires day-level data; calculated in follow-up
            IsRecoveryWeek = entity.IsRecoveryWeek,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
        };
    }
}
