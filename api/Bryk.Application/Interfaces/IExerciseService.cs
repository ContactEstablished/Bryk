using Bryk.Application.DTOs.Exercise;

namespace Bryk.Application.Interfaces;

public interface IExerciseService
{
    Task<ExerciseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ExerciseListDto> GetAllAsync(string? sportType = null, string? searchTerm = null, 
        string? sortBy = null, CancellationToken cancellationToken = default);
    Task<ExerciseDto> CreateAsync(CreateExerciseDto dto, CancellationToken cancellationToken = default);
    Task<ExerciseDto> UpdateAsync(Guid id, UpdateExerciseDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ExerciseDto> DuplicateAsync(Guid id, CancellationToken cancellationToken = default);
}
