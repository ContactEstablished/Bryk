using Bryk.Application.DTOs.Week;
using Bryk.Application.Interfaces;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Bryk.Infrastructure.Services;

public class WeekService(
    ApplicationDbContext context,
    IValidator<UpdateWeekDto> updateValidator) : IWeekService
{
    // GET: return a single WeekDto, throw KeyNotFoundException if not found
    public async Task<WeekDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Weeks
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"Week with ID {id} not found.");

        return ToDto(entity);
    }

    // GET: return WeekWithDaysDto with days included, throw KeyNotFoundException if not found
    public async Task<WeekWithDaysDto> GetWithDaysAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Weeks
            .AsNoTracking()
            .Include(w => w.Days)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"Week with ID {id} not found.");

        var dto = new WeekWithDaysDto
        {
            Id = entity.Id,
            MesocycleId = entity.MesocycleId,
            WeekNumber = entity.WeekNumber,
            TargetTss = entity.TargetTss,
            ActualTss = 0,
            IsRecoveryWeek = entity.IsRecoveryWeek,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Notes = entity.Notes,
            WorkoutsPlanned = 0,
            DaysActive = 0,
            Days = entity.Days.Select(ToDaySummaryDto).ToList(),
            SportBreakdown = new SportBreakdownDto(),
            IntensityBreakdown = new IntensityBreakdownDto()
        };

        return dto;
    }

    // PUT: validate, load entity (tracking), apply only non-null fields, save, return WeekDto
    public async Task<WeekDto> UpdateAsync(Guid id, UpdateWeekDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
            throw new Bryk.Application.Exceptions.ValidationException(validationResult.Errors.Select(e => e.ErrorMessage));

        var entity = await context.Weeks
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"Week with ID {id} not found.");

        if (dto.TargetTss.HasValue) entity.TargetTss = dto.TargetTss.Value;
        if (dto.Notes is not null) entity.Notes = dto.Notes;

        await context.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    // TODO: implement week copy logic
    public Task<WeekDto> CopyWeekAsync(Guid sourceWeekId, Guid targetWeekId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    // Private static mapping methods

    private static WeekDto ToDto(Week entity)
    {
        return new WeekDto
        {
            Id = entity.Id,
            MesocycleId = entity.MesocycleId,
            WeekNumber = entity.WeekNumber,
            TargetTss = entity.TargetTss,
            ActualTss = 0,
            IsRecoveryWeek = entity.IsRecoveryWeek,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Notes = entity.Notes,
            WorkoutsPlanned = 0,
            DaysActive = 0
        };
    }

    private static DaySummaryDto ToDaySummaryDto(Day entity)
    {
        return new DaySummaryDto
        {
            Id = entity.Id,
            Date = entity.Date,
            DayOfWeek = entity.DayOfWeek,
            TargetTss = entity.TargetTss,
            ActualTss = 0,
            IsRestDay = entity.IsRestDay,
            ExerciseCount = 0
        };
    }
}
