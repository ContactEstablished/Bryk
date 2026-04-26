using Bryk.Application.DTOs.Mesocycle;

namespace Bryk.Application.Interfaces;

public interface IMesocycleService
{
    Task<MesocycleDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MesocycleWithWeeksDto> GetWithWeeksAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<MesocycleDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<MesocycleDto> CreateAsync(CreateMesocycleDto dto, CancellationToken cancellationToken = default);
    Task<MesocycleDto> UpdateAsync(Guid id, UpdateMesocycleDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task RegenerateWeeksAsync(Guid id, CancellationToken cancellationToken = default);
}
