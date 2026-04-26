using Bryk.Application.DTOs.Week;

namespace Bryk.Application.Interfaces;

public interface IWeekService
{
    Task<WeekDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WeekWithDaysDto> GetWithDaysAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WeekDto> UpdateAsync(Guid id, UpdateWeekDto dto, CancellationToken cancellationToken = default);
    Task<WeekDto> CopyWeekAsync(Guid sourceWeekId, Guid targetWeekId, CancellationToken cancellationToken = default);
}
