using Bryk.Application.DTOs.Day;

namespace Bryk.Application.Interfaces;

public interface IDayService
{
    Task<DayDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DayWithExercisesDto> GetWithExercisesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DayDto> UpdateAsync(Guid id, UpdateDayDto dto, CancellationToken cancellationToken = default);
    
    // Exercise management
    Task<DayExerciseDto> AddExerciseAsync(Guid dayId, AddExerciseToDayDto dto, CancellationToken cancellationToken = default);
    Task<DayExerciseDto> UpdateExerciseAsync(Guid dayExerciseId, UpdateDayExerciseDto dto, CancellationToken cancellationToken = default);
    Task RemoveExerciseAsync(Guid dayExerciseId, CancellationToken cancellationToken = default);
    Task ReorderExercisesAsync(Guid dayId, List<Guid> orderedExerciseIds, CancellationToken cancellationToken = default);
}
