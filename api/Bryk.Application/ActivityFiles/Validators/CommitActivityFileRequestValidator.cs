using FluentValidation;

namespace Bryk.Application.ActivityFiles.Validators;

public class CommitActivityFileRequestValidator : AbstractValidator<CommitActivityFileRequest>
{
    public CommitActivityFileRequestValidator()
    {
        RuleFor(x => x.PlannedWorkoutId)
            .NotEqual(Guid.Empty)
            .When(x => x.PlannedWorkoutId.HasValue);
    }
}
