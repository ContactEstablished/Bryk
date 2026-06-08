using FluentValidation;

namespace Bryk.Application.Training.Validators;

// Sport-AGNOSTIC structural rules only. Sport-discriminated rules (exactly one of duration/distance
// for cardio, strength requires sets/reps and forbids cardio targets, TargetZone within the sport's
// zone count) need the parent workout's Sport and live in StructuredWorkoutService.
public class WorkoutStructureRequestValidator : AbstractValidator<WorkoutStructureRequest>
{
    public WorkoutStructureRequestValidator()
    {
        RuleForEach(x => x.Blocks).SetValidator(new WorkoutBlockDtoValidator());
    }
}

public class WorkoutBlockDtoValidator : AbstractValidator<WorkoutBlockDto>
{
    public WorkoutBlockDtoValidator()
    {
        RuleFor(x => x.Repeats).GreaterThanOrEqualTo(1);
        RuleForEach(x => x.Steps).SetValidator(new WorkoutStepDtoValidator());
    }
}

public class WorkoutStepDtoValidator : AbstractValidator<WorkoutStepDto>
{
    public WorkoutStepDtoValidator()
    {
        RuleFor(x => x.Intent).IsInEnum();
        RuleFor(x => x.Title).MaximumLength(200).When(x => x.Title != null);

        RuleFor(x => x.DurationSeconds).GreaterThan(0).When(x => x.DurationSeconds.HasValue);
        RuleFor(x => x.DistanceMeters).GreaterThan(0).When(x => x.DistanceMeters.HasValue);
        RuleFor(x => x.TargetZone).GreaterThanOrEqualTo(1).When(x => x.TargetZone.HasValue);

        // Non-negative raw bounds, and High >= Low when a full range is supplied.
        RuleFor(x => x.TargetPowerLow).GreaterThanOrEqualTo(0).When(x => x.TargetPowerLow.HasValue);
        RuleFor(x => x.TargetHrLow).GreaterThanOrEqualTo(0).When(x => x.TargetHrLow.HasValue);
        RuleFor(x => x.TargetPaceLow).GreaterThanOrEqualTo(0).When(x => x.TargetPaceLow.HasValue);
        RuleFor(x => x.TargetPowerHigh).GreaterThanOrEqualTo(x => x.TargetPowerLow!.Value)
            .When(x => x.TargetPowerLow.HasValue && x.TargetPowerHigh.HasValue);
        RuleFor(x => x.TargetHrHigh).GreaterThanOrEqualTo(x => x.TargetHrLow!.Value)
            .When(x => x.TargetHrLow.HasValue && x.TargetHrHigh.HasValue);
        RuleFor(x => x.TargetPaceHigh).GreaterThanOrEqualTo(x => x.TargetPaceLow!.Value)
            .When(x => x.TargetPaceLow.HasValue && x.TargetPaceHigh.HasValue);

        RuleFor(x => x.Sets).GreaterThan(0).When(x => x.Sets.HasValue);
        RuleFor(x => x.Reps).GreaterThan(0).When(x => x.Reps.HasValue);
        RuleFor(x => x.LoadKg).GreaterThanOrEqualTo(0m).When(x => x.LoadKg.HasValue);
        RuleFor(x => x.Rpe).InclusiveBetween(0m, 10m).When(x => x.Rpe.HasValue);
    }
}
