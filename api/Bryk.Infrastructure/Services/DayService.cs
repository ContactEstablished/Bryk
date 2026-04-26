using Bryk.Application.DTOs.Day;
using Bryk.Application.Interfaces;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Bryk.Infrastructure.Services;

public class DayService(
    ApplicationDbContext context,
    IValidator<UpdateDayDto> updateDayValidator,
    IValidator<UpdateDayExerciseDto> updateDayExerciseValidator) : IDayService
{
    public async Task<DayDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Days
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"Day with ID {id} not found.");

        return ToDayDto(entity);
    }

    public async Task<DayWithExercisesDto> GetWithExercisesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Days
            .AsNoTracking()
            .Include(d => d.DayExercises)
                .ThenInclude(de => de.Exercise)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"Day with ID {id} not found.");

        var dto = new DayWithExercisesDto
        {
            Id = entity.Id,
            WeekId = entity.WeekId,
            Date = entity.Date,
            DayOfWeek = entity.DayOfWeek,
            TargetTss = entity.TargetTss,
            ActualTss = 0,
            IsRestDay = entity.IsRestDay,
            Notes = entity.Notes,
            Exercises = entity.DayExercises.Select(ToDayExerciseDto).ToList()
        };

        return dto;
    }

    public async Task<DayDto> UpdateAsync(Guid id, UpdateDayDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await updateDayValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
            throw new Bryk.Application.Exceptions.ValidationException(validationResult.Errors.Select(e => e.ErrorMessage));

        var entity = await context.Days
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"Day with ID {id} not found.");

        if (dto.TargetTss.HasValue) entity.TargetTss = dto.TargetTss.Value;
        if (dto.IsRestDay.HasValue) entity.IsRestDay = dto.IsRestDay.Value;
        if (dto.Notes is not null) entity.Notes = dto.Notes;

        await context.SaveChangesAsync(cancellationToken);

        return ToDayDto(entity);
    }

    public async Task<DayExerciseDto> AddExerciseAsync(Guid dayId, AddExerciseToDayDto dto, CancellationToken cancellationToken = default)
    {
        var dayExists = await context.Days.AnyAsync(d => d.Id == dayId, cancellationToken);
        if (!dayExists)
            throw new KeyNotFoundException($"Day with ID {dayId} not found.");

        var exerciseExists = await context.Exercises.AnyAsync(e => e.Id == dto.ExerciseId, cancellationToken);
        if (!exerciseExists)
            throw new KeyNotFoundException($"Exercise with ID {dto.ExerciseId} not found.");

        int orderIndex;
        if (dto.OrderIndex.HasValue)
        {
            orderIndex = dto.OrderIndex.Value;
        }
        else
        {
            var maxOrderIndex = await context.DayExercises
                .Where(de => de.DayId == dayId)
                .MaxAsync(de => (int?)de.OrderIndex, cancellationToken);
            orderIndex = (maxOrderIndex ?? -1) + 1;
        }

        var dayExercise = new DayExercise
        {
            Id = Guid.NewGuid(),
            DayId = dayId,
            ExerciseId = dto.ExerciseId,
            OrderIndex = orderIndex
        };

        context.DayExercises.Add(dayExercise);
        await context.SaveChangesAsync(cancellationToken);

        var loaded = await context.DayExercises
            .Include(de => de.Exercise)
            .FirstOrDefaultAsync(de => de.Id == dayExercise.Id, cancellationToken);

        return ToDayExerciseDto(loaded!);
    }

    public async Task<DayExerciseDto> UpdateExerciseAsync(Guid dayExerciseId, UpdateDayExerciseDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await updateDayExerciseValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
            throw new Bryk.Application.Exceptions.ValidationException(validationResult.Errors.Select(e => e.ErrorMessage));

        var entity = await context.DayExercises
            .Include(de => de.Exercise)
            .FirstOrDefaultAsync(de => de.Id == dayExerciseId, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"DayExercise with ID {dayExerciseId} not found.");

        if (dto.OrderIndex.HasValue) entity.OrderIndex = dto.OrderIndex.Value;
        if (dto.ActualTss.HasValue) entity.ActualTss = dto.ActualTss.Value;
        if (dto.CaloriesBurned.HasValue) entity.CaloriesBurned = dto.CaloriesBurned.Value;
        if (dto.AverageHeartRate.HasValue) entity.AverageHeartRate = dto.AverageHeartRate.Value;
        if (dto.MaxHeartRate.HasValue) entity.MaxHeartRate = dto.MaxHeartRate.Value;
        if (dto.WorkoutNotes is not null) entity.WorkoutNotes = dto.WorkoutNotes;

        if (dto.HrZoneDistribution is not null)
        {
            entity.Zone1Minutes = dto.HrZoneDistribution.Zone1Minutes;
            entity.Zone2Minutes = dto.HrZoneDistribution.Zone2Minutes;
            entity.Zone3Minutes = dto.HrZoneDistribution.Zone3Minutes;
            entity.Zone4Minutes = dto.HrZoneDistribution.Zone4Minutes;
            entity.Zone5Minutes = dto.HrZoneDistribution.Zone5Minutes;
        }

        if (dto.PaceMetrics is not null)
        {
            if (dto.PaceMetrics.AveragePace is not null) entity.AveragePace = dto.PaceMetrics.AveragePace;
            if (dto.PaceMetrics.MaxPace is not null) entity.MaxPace = dto.PaceMetrics.MaxPace;
            if (dto.PaceMetrics.AdditionalMetric is not null) entity.AdditionalPaceMetric = dto.PaceMetrics.AdditionalMetric;
        }

        if (dto.PerformanceComparison is not null)
        {
            if (dto.PerformanceComparison.PercentChange.HasValue) entity.PerformanceComparisonPercent = dto.PerformanceComparison.PercentChange.Value;
            if (dto.PerformanceComparison.ComparisonNotes is not null) entity.ComparisonNotes = dto.PerformanceComparison.ComparisonNotes;
        }

        if (dto.Weather is not null)
        {
            if (dto.Weather.TemperatureFahrenheit.HasValue) entity.TemperatureFahrenheit = dto.Weather.TemperatureFahrenheit.Value;
            if (dto.Weather.Condition is not null) entity.WeatherCondition = dto.Weather.Condition;
            if (dto.Weather.WindSpeed is not null) entity.WindSpeed = dto.Weather.WindSpeed;
            if (dto.Weather.HumidityPercent.HasValue) entity.HumidityPercent = dto.Weather.HumidityPercent.Value;
        }

        await context.SaveChangesAsync(cancellationToken);

        return ToDayExerciseDto(entity);
    }

    public async Task RemoveExerciseAsync(Guid dayExerciseId, CancellationToken cancellationToken = default)
    {
        var entity = await context.DayExercises
            .FirstOrDefaultAsync(de => de.Id == dayExerciseId, cancellationToken);

        if (entity is null)
            throw new KeyNotFoundException($"DayExercise with ID {dayExerciseId} not found.");

        context.DayExercises.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderExercisesAsync(Guid dayId, List<Guid> orderedExerciseIds, CancellationToken cancellationToken = default)
    {
        var exercises = await context.DayExercises
            .Where(de => de.DayId == dayId)
            .ToListAsync(cancellationToken);

        for (int i = 0; i < orderedExerciseIds.Count; i++)
        {
            var exercise = exercises.FirstOrDefault(de => de.Id == orderedExerciseIds[i]);
            if (exercise is not null)
            {
                exercise.OrderIndex = i;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static DayDto ToDayDto(Day entity)
    {
        return new DayDto
        {
            Id = entity.Id,
            WeekId = entity.WeekId,
            Date = entity.Date,
            DayOfWeek = entity.DayOfWeek,
            TargetTss = entity.TargetTss,
            ActualTss = 0,
            IsRestDay = entity.IsRestDay,
            Notes = entity.Notes
        };
    }

    private static DayExerciseDto ToDayExerciseDto(DayExercise entity)
    {
        var dto = new DayExerciseDto
        {
            Id = entity.Id,
            ExerciseId = entity.ExerciseId,
            OrderIndex = entity.OrderIndex,
            ExerciseName = entity.Exercise.Name,
            SportType = entity.Exercise.SportType.ToString(),
            PlannedTss = entity.Exercise.TssValue,
            DurationMinutes = entity.Exercise.DurationMinutes,
            IntensityZone = entity.Exercise.IntensityZone,
            Description = entity.Exercise.Description,
            ActualTss = entity.ActualTss
        };

        var hasMetrics = entity.ActualTss.HasValue
            || entity.CaloriesBurned.HasValue
            || entity.AverageHeartRate.HasValue
            || entity.MaxHeartRate.HasValue
            || entity.Zone1Minutes.HasValue
            || entity.Zone2Minutes.HasValue
            || entity.Zone3Minutes.HasValue
            || entity.Zone4Minutes.HasValue
            || entity.Zone5Minutes.HasValue
            || entity.AveragePace is not null
            || entity.MaxPace is not null
            || entity.AdditionalPaceMetric is not null
            || entity.PerformanceComparisonPercent.HasValue
            || entity.ComparisonNotes is not null
            || entity.TemperatureFahrenheit.HasValue
            || entity.WeatherCondition is not null
            || entity.WindSpeed is not null
            || entity.HumidityPercent.HasValue
            || entity.WorkoutNotes is not null
            || entity.CompletedAt.HasValue;

        if (hasMetrics)
        {
            dto.Metrics = new WorkoutMetricsDto
            {
                CaloriesBurned = entity.CaloriesBurned,
                AverageHeartRate = entity.AverageHeartRate,
                MaxHeartRate = entity.MaxHeartRate,
                WorkoutNotes = entity.WorkoutNotes,
                CompletedAt = entity.CompletedAt,
                HrZoneDistribution = HasAnyHrZone(entity)
                    ? new HrZoneDistributionDto
                    {
                        Zone1Minutes = entity.Zone1Minutes ?? 0,
                        Zone2Minutes = entity.Zone2Minutes ?? 0,
                        Zone3Minutes = entity.Zone3Minutes ?? 0,
                        Zone4Minutes = entity.Zone4Minutes ?? 0,
                        Zone5Minutes = entity.Zone5Minutes ?? 0
                    }
                    : null,
                PaceMetrics = HasAnyPaceMetric(entity)
                    ? new PaceMetricsDto
                    {
                        AveragePace = entity.AveragePace,
                        MaxPace = entity.MaxPace,
                        AdditionalMetric = entity.AdditionalPaceMetric
                    }
                    : null,
                PerformanceComparison = HasAnyPerformanceComparison(entity)
                    ? new PerformanceComparisonDto
                    {
                        PercentChange = entity.PerformanceComparisonPercent,
                        ComparisonNotes = entity.ComparisonNotes
                    }
                    : null,
                Weather = HasAnyWeather(entity)
                    ? new WeatherDto
                    {
                        TemperatureFahrenheit = entity.TemperatureFahrenheit,
                        Condition = entity.WeatherCondition,
                        WindSpeed = entity.WindSpeed,
                        HumidityPercent = entity.HumidityPercent
                    }
                    : null
            };
        }

        return dto;
    }

    private static bool HasAnyHrZone(DayExercise entity)
    {
        return entity.Zone1Minutes.HasValue
            || entity.Zone2Minutes.HasValue
            || entity.Zone3Minutes.HasValue
            || entity.Zone4Minutes.HasValue
            || entity.Zone5Minutes.HasValue;
    }

    private static bool HasAnyPaceMetric(DayExercise entity)
    {
        return entity.AveragePace is not null
            || entity.MaxPace is not null
            || entity.AdditionalPaceMetric is not null;
    }

    private static bool HasAnyPerformanceComparison(DayExercise entity)
    {
        return entity.PerformanceComparisonPercent.HasValue
            || entity.ComparisonNotes is not null;
    }

    private static bool HasAnyWeather(DayExercise entity)
    {
        return entity.TemperatureFahrenheit.HasValue
            || entity.WeatherCondition is not null
            || entity.WindSpeed is not null
            || entity.HumidityPercent.HasValue;
    }
}
