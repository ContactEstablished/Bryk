# Impl 16-2 — Build order: schedule PATCH (reschedule within the plan window)

**Executor:** GLM 5.2. **Acceptance contract:** `md/Tasks-16-2.md`. **Decision lock:** ADR-0008 §2.
**Scope:** Backend only. No migration, no new package, no new repo method.

## Step 0 — Pre-flight

- `git status` clean (16-1 committed). `dotnet build api/Bryk.sln` green.
- Re-read `md/Tasks-16-2.md` + ADR-0008 §2. Open: `api/Bryk.Application/Training/TrainingPlanService.cs`
  (the staging-discipline template — study `UpdatePlannedWorkoutAsync` and its comment about the
  no-tracking `Include` + nav-free staging), `api/Bryk.Application/Training/ITrainingPlanService.cs`,
  `api/Bryk.API/Controllers/TrainingPlansController.cs`,
  `api/Bryk.Application/Exceptions/ValidationException.cs` (**confirmed shape: takes `IEnumerable<string>`**),
  `api/Bryk.API/Middleware/ExceptionHandlingMiddleware.cs` (**confirmed: serializes `errors` array**),
  `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs`.

## Step 1 — `ScheduleRequest` DTO + validator

**New file** `api/Bryk.Application/Calendar/ScheduleRequest.cs`:
```csharp
namespace Bryk.Application.Calendar;

public sealed class ScheduleRequest
{
    public DateOnly ScheduledDate { get; init; }
}
```

**New file** `api/Bryk.Application/Calendar/Validators/ScheduleRequestValidator.cs`:
```csharp
using Bryk.Application.Common.Validation;
using FluentValidation;

namespace Bryk.Application.Calendar.Validators;

public sealed class ScheduleRequestValidator : AbstractValidator<ScheduleRequest>
{
    public ScheduleRequestValidator()
    {
        // DateOnly is a non-nullable struct, so NotEmpty/NotNull are no-ops; the meaningful
        // rule (plan-window) can't run here — the validator can't see the plan. The window check
        // lives in the service, which throws ValidationException after loading the plan.
        // This validator exists for shape consistency; add a message if you want.
    }
}
```

Register in DI (same place as the other validators — `AddValidatorsFromAssembly` should pick it up
automatically if the Application assembly marker is configured; verify by grepping
`AddValidatorsFromAssemblyContaining` or `ApplicationAssemblyMarker`).

**Verify:** `dotnet build` green.

## Step 2 — Extend `ITrainingPlanService` + `TrainingPlanService`

**Edit** `ITrainingPlanService.cs` — add:
```csharp
/// <summary>
/// Moves a planned workout to a new scheduled date within the owning plan's window
/// [StartDate, EndDate]. 400 (validation) if the date is outside the window; 404 if the plan or
/// planned workout is missing or foreign (ADR-0008 §2). Returns Task (204 NoContent).
/// </summary>
Task RescheduleAsync(Guid planId, Guid plannedWorkoutId, ScheduleRequest request, CancellationToken ct = default);
```

(Add `using Bryk.Application.Calendar;` to the interface file for the `ScheduleRequest` reference.)

**Edit** `TrainingPlanService.cs`:
- Add `IValidator<ScheduleRequest> scheduleValidator` to the primary-ctor params.
- Add `using Bryk.Application.Calendar;`, `using Bryk.Application.Exceptions;`.
- Implement:
```csharp
public async Task RescheduleAsync(Guid planId, Guid plannedWorkoutId, ScheduleRequest request, CancellationToken ct = default)
{
    await scheduleValidator.ValidateOrThrowAsync(request, ct);

    var plan = await LoadOwnedPlanAsync(planId, ct);
    var existing = plan.PlannedWorkouts.FirstOrDefault(pw => pw.Id == plannedWorkoutId)
        ?? throw new KeyNotFoundException();

    if (request.ScheduledDate < plan.StartDate || request.ScheduledDate > plan.EndDate)
    {
        throw new ValidationException(new[]
        {
            $"ScheduledDate: Scheduled date must be within the plan window ({plan.StartDate:yyyy-MM-dd} to {plan.EndDate:yyyy-MM-dd})."
        });
    }

    // Stage a fresh nav-free entity (mirror UpdatePlannedWorkoutAsync's discipline).
    var updated = new PlannedWorkout
    {
        Id = existing.Id,
        AthleteId = existing.AthleteId,
        TrainingPlanId = existing.TrainingPlanId,
        Sport = existing.Sport,
        ScheduledDate = request.ScheduledDate,
        Title = existing.Title,
        Description = existing.Description,
        PlannedDurationMinutes = existing.PlannedDurationMinutes,
        PlannedLoad = existing.PlannedLoad,
        CreatedAt = existing.CreatedAt
    };

    planRepo.UpdatePlannedWorkout(updated);
    await unitOfWork.SaveChangesAsync(ct);
}
```

**Verify:** `dotnet build` green.

## Step 3 — `PATCH` action on `TrainingPlansController`

**Edit** `api/Bryk.API/Controllers/TrainingPlansController.cs` — add (between the existing PUT and
DELETE planned-workout actions, or after the structure PUT — match house ordering):

```csharp
/// <summary>
/// Moves a planned workout to a new scheduled date within the owning plan's window
/// [StartDate, EndDate]. 400 if the date is outside the window; 404 if the plan or planned workout
/// is missing or foreign. Returns 204 NoContent (ADR-0008 §2).
/// </summary>
[HttpPatch("{id:guid}/plannedworkouts/{plannedWorkoutId:guid}/schedule")]
public async Task<IActionResult> RescheduleAsync(Guid id, Guid plannedWorkoutId, [FromBody] ScheduleRequest request, CancellationToken cancellationToken)
{
    await trainingPlanService.RescheduleAsync(id, plannedWorkoutId, request, cancellationToken);
    return NoContent();
}
```

Add `using Bryk.Application.Calendar;` to the controller file.

**Verify:** `dotnet build` green.

## Step 4 — Unit tests for `RescheduleAsync`

**New file** `api/Bryk.Application.Tests/Training/RescheduleTests.cs` (or extend
`TrainingPlanServiceTests.cs` — match house style; a new file is cleaner since the existing file may
be large).

Mirror the existing `TrainingPlanServiceTests` mock setup (in-memory `ITrainingPlanRepository`,
`IUnitOfWork`, `ICurrentUserService`, `IValidator<TrainingPlanRequest>`, `IValidator<PlannedWorkoutDto>`,
+ the new `IValidator<ScheduleRequest>`).

Tests:
- `RescheduleAsync_OnWindow_UpdatesScheduledDate` — plan window `[2026-06-01, 2026-06-30]`, request
  `2026-06-15`, assert the staged `PlannedWorkout.ScheduledDate == 2026-06-15` and `UpdatePlannedWorkout`
  was called with that date; `SaveChangesAsync` called once.
- `RescheduleAsync_AtWindowBoundary_Succeeds` — `request.ScheduledDate == plan.StartDate` and a second
  case `== plan.EndDate` both succeed (inclusive).
- `RescheduleAsync_BelowWindow_ThrowsValidationException` — `request.ScheduledDate == plan.StartDate - 1 day`,
  assert `ValidationException` thrown, `Errors` contains a string starting `"ScheduledDate:"`.
- `RescheduleAsync_AboveWindow_ThrowsValidationException` — `request.ScheduledDate == plan.EndDate + 1 day`,
  same assertion.
- `RescheduleAsync_MissingPlan_ThrowsKeyNotFound` — repo returns null → `KeyNotFoundException`.
- `RescheduleAsync_ForeignPlan_ThrowsKeyNotFound` — repo returns a plan with a different `AthleteId`
  than `currentUser` → `KeyNotFoundException`.
- `RescheduleAsync_MissingPlannedWorkout_ThrowsKeyNotFound` — plan exists, pw id not in
  `plan.PlannedWorkouts` → `KeyNotFoundException`.

**Verify:** `dotnet test api/Bryk.sln` green.

## Step 5 — Integration tests

**Edit** `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs` (extend).

Mirror the existing harness: seed a plan via `POST /trainingplans` with a planned workout, then:

- `PATCH .../schedule` with a valid in-window date → 204. Follow-up `GET /trainingplans/{id}` shows the
  planned workout's `ScheduledDate` moved.
- Out-of-window (below `StartDate` or above `EndDate`) → 400, response body has `errors: ["ScheduledDate: ..."]`.
- Missing plan id (random guid) → 404.
- Foreign plan (seed a second athlete's plan via a separate factory/`ICurrentUserService` override —
  match how the existing tests fake the current athlete) → 404.
- Missing planned workout id (valid plan, random pw guid) → 404.

**Verify:** `dotnet test api/Bryk.sln` green. Record the total count.

## Step 6 — Final verification + commit

- `dotnet build` — 0 errors.
- `dotnet test` — all green.
- `git diff --stat` — only the expected files: `ScheduleRequest.cs`, `ScheduleRequestValidator.cs`,
  `ITrainingPlanService.cs`, `TrainingPlanService.cs`, `TrainingPlansController.cs`, the new test
  file(s). No changes to `UpdatePlannedWorkoutAsync`, no migration, no repo changes.
- Commit with the message in `Tasks-16-2.md`.
