using Bryk.Application.Common;
using Bryk.Application.Common.Validation;
using Bryk.Application.Onboarding;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentValidation;

namespace Bryk.Application.Goals;

public class GoalService(
    ICurrentUserService currentUser,
    IValidator<GoalDto> validator,
    IGoalRepository goalRepo,
    IUnitOfWork unitOfWork) : IGoalService
{
    public async Task<GoalResponse> CreateAsync(GoalDto request, CancellationToken ct = default)
    {
        await validator.ValidateOrThrowAsync(request, ct);

        var entity = new Goal
        {
            Id = Guid.NewGuid(),
            AthleteId = currentUser.GetCurrentAthleteId(),
            Type = request.Type,
            Description = request.Description,
            TargetDate = request.TargetDate
        };

        await goalRepo.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task<GoalResponse> UpdateAsync(Guid id, GoalDto request, CancellationToken ct = default)
    {
        await validator.ValidateOrThrowAsync(request, ct);

        var entity = await goalRepo.GetByIdAsync(id, ct);
        if (entity is null || entity.AthleteId != currentUser.GetCurrentAthleteId())
        {
            throw new KeyNotFoundException();
        }

        entity.Type = request.Type;
        entity.Description = request.Description;
        entity.TargetDate = request.TargetDate;

        goalRepo.Update(entity);
        await unitOfWork.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await goalRepo.GetByIdAsync(id, ct);
        if (entity is null || entity.AthleteId != currentUser.GetCurrentAthleteId())
        {
            throw new KeyNotFoundException();
        }

        goalRepo.Delete(entity);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static GoalResponse Map(Goal g) => new()
    {
        Id = g.Id,
        Type = g.Type,
        Description = g.Description,
        TargetDate = g.TargetDate
    };
}
