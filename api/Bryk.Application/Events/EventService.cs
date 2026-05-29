using Bryk.Application.Common;
using Bryk.Application.Common.Validation;
using Bryk.Application.Onboarding;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentValidation;

namespace Bryk.Application.Events;

public class EventService(
    ICurrentUserService currentUser,
    IValidator<EventDto> validator,
    IEventRepository eventRepo,
    IUnitOfWork unitOfWork) : IEventService
{
    public async Task<EventResponse> CreateAsync(EventDto request, CancellationToken ct = default)
    {
        await validator.ValidateOrThrowAsync(request, ct);

        var entity = new Event
        {
            Id = Guid.NewGuid(),
            AthleteId = currentUser.GetCurrentAthleteId(),
            Name = request.Name,
            EventDate = request.EventDate,
            Sport = request.Sport,
            TriathlonDistance = request.TriathlonDistance,
            CustomDistanceName = request.CustomDistanceName,
            Priority = request.Priority,
            Notes = request.Notes
        };

        await eventRepo.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task<EventResponse> UpdateAsync(Guid id, EventDto request, CancellationToken ct = default)
    {
        await validator.ValidateOrThrowAsync(request, ct);

        var entity = await eventRepo.GetByIdAsync(id, ct);
        if (entity is null || entity.AthleteId != currentUser.GetCurrentAthleteId())
        {
            throw new KeyNotFoundException();
        }

        entity.Name = request.Name;
        entity.EventDate = request.EventDate;
        entity.Sport = request.Sport;
        entity.TriathlonDistance = request.TriathlonDistance;
        entity.CustomDistanceName = request.CustomDistanceName;
        entity.Priority = request.Priority;
        entity.Notes = request.Notes;

        eventRepo.Update(entity);
        await unitOfWork.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await eventRepo.GetByIdAsync(id, ct);
        if (entity is null || entity.AthleteId != currentUser.GetCurrentAthleteId())
        {
            throw new KeyNotFoundException();
        }

        eventRepo.Delete(entity);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static EventResponse Map(Event e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        EventDate = e.EventDate,
        Sport = e.Sport,
        TriathlonDistance = e.TriathlonDistance,
        CustomDistanceName = e.CustomDistanceName,
        Priority = e.Priority,
        Notes = e.Notes
    };
}
