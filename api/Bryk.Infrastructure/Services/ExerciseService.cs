using Bryk.Application.DTOs.Exercise;
using Bryk.Application.Interfaces;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Bryk.Infrastructure.Services;

public class ExerciseService(
    ApplicationDbContext context,
    IValidator<CreateExerciseDto> createValidator,
    IValidator<UpdateExerciseDto> updateValidator) : IExerciseService
{
    public async Task<ExerciseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Exercises
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"Exercise with ID {id} not found.");

        return ToDto(entity);
    }

    public async Task<ExerciseListDto> GetAllAsync(
        string? sportType = null,
        string? searchTerm = null,
        string? sortBy = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Exercise> query = context.Exercises.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(sportType))
        {
            var parsedSportType = Enum.Parse<SportType>(sportType, ignoreCase: true);
            query = query.Where(e => e.SportType == parsedSportType);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(e => EF.Functions.Like(e.Name, $"%{searchTerm}%"));
        }

        query = sortBy?.ToLowerInvariant() switch
        {
            "name" => query.OrderBy(e => e.Name),
            "tss" => query.OrderByDescending(e => e.TssValue),
            "duration" => query.OrderByDescending(e => e.DurationMinutes),
            _ => query.OrderByDescending(e => e.CreatedAt)
        };

        var exercises = await query.ToListAsync(cancellationToken);

        var sportCounts = new SportCountsDto
        {
            BikeCount = exercises.Count(e => e.SportType == SportType.Bike),
            RunCount = exercises.Count(e => e.SportType == SportType.Run),
            SwimCount = exercises.Count(e => e.SportType == SportType.Swim),
            StrengthCount = exercises.Count(e => e.SportType == SportType.Strength),
            OtherCount = exercises.Count(e => e.SportType == SportType.Other)
        };

        return new ExerciseListDto
        {
            Exercises = exercises.Select(ToDto).ToList(),
            TotalCount = exercises.Count,
            SportCounts = sportCounts
        };
    }

    public async Task<ExerciseDto> CreateAsync(CreateExerciseDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await createValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
            throw new Bryk.Application.Exceptions.ValidationException(validationResult.Errors.Select(e => e.ErrorMessage));

        var entity = new Exercise
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            SportType = Enum.Parse<SportType>(dto.SportType, ignoreCase: true),
            TssValue = dto.TssValue,
            DurationMinutes = dto.DurationMinutes,
            IntensityZone = dto.IntensityZone,
            Description = dto.Description
        };

        context.Exercises.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<ExerciseDto> UpdateAsync(Guid id, UpdateExerciseDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
            throw new Bryk.Application.Exceptions.ValidationException(validationResult.Errors.Select(e => e.ErrorMessage));

        var entity = await context.Exercises
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"Exercise with ID {id} not found.");

        if (dto.Name is not null) entity.Name = dto.Name;
        if (dto.SportType is not null) entity.SportType = Enum.Parse<SportType>(dto.SportType, ignoreCase: true);
        if (dto.TssValue.HasValue) entity.TssValue = dto.TssValue.Value;
        if (dto.DurationMinutes.HasValue) entity.DurationMinutes = dto.DurationMinutes.Value;
        if (dto.IntensityZone is not null) entity.IntensityZone = dto.IntensityZone;
        if (dto.Description is not null) entity.Description = dto.Description;

        await context.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Exercises
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"Exercise with ID {id} not found.");

        var isReferenced = await context.DayExercises
            .AnyAsync(de => de.ExerciseId == id, cancellationToken);

        if (isReferenced)
            throw new InvalidOperationException("Exercise cannot be deleted because it is assigned to one or more days.");

        context.Exercises.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExerciseDto> DuplicateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Exercises
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"Exercise with ID {id} not found.");

        var duplicate = new Exercise
        {
            Id = Guid.NewGuid(),
            Name = entity.Name + " (Copy)",
            SportType = entity.SportType,
            TssValue = entity.TssValue,
            DurationMinutes = entity.DurationMinutes,
            IntensityZone = entity.IntensityZone,
            Description = entity.Description
        };

        context.Exercises.Add(duplicate);
        await context.SaveChangesAsync(cancellationToken);

        return ToDto(duplicate);
    }

    private static ExerciseDto ToDto(Exercise entity)
    {
        return new ExerciseDto
        {
            Id = entity.Id,
            Name = entity.Name,
            SportType = entity.SportType.ToString(),
            TssValue = entity.TssValue,
            DurationMinutes = entity.DurationMinutes,
            IntensityZone = entity.IntensityZone,
            Description = entity.Description,
            CreatedAt = entity.CreatedAt
        };
    }
}
