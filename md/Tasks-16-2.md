# Task 16-2 — Schedule PATCH (reschedule within the plan window)

## Surface
Backend only. A `ScheduleRequest` DTO + validator; `TrainingPlanService.RescheduleAsync` +
`ITrainingPlanService.RescheduleAsync`; one additive `PATCH` action on `TrainingPlansController`;
integration + unit tests. **No migration, no new package, no new repo method** (uses the existing
`ITrainingPlanRepository.GetByIdAsync` + `UpdatePlannedWorkout`).

## Why
The calendar's drag (16-4) and tap-to-move (mobile) need a lightweight, dedicated reschedule endpoint.
A full-DTO PUT would force the client to round-trip the whole `PlannedWorkoutDto` and risk overwriting
fields the user didn't touch; a single-field PATCH is surgical. The plan-window-is-authoritative
contract (ADR-0008 §2) keeps Phase 18's ramp targets meaningful.

## Depends on
- **ADR-0008** §2 (reject out-of-window reschedule with 400; 404 on missing/foreign; 204 on success;
  stage a fresh nav-free `PlannedWorkout` entity).
- **Task 9-3** — `TrainingPlanService.UpdatePlannedWorkoutAsync` is the staging discipline template
  (the comment about no-tracking `Include` and re-attaching the aggregate graph).

## Required reading
- `api/Bryk.Application/Training/TrainingPlanService.cs` — **the staging pattern to mirror verbatim**:
  `LoadOwnedPlanAsync` (404 on missing/foreign), build a fresh `PlannedWorkout` with carried-over
  `Id`/`AthleteId`/`TrainingPlanId`/`CreatedAt`, call `planRepo.UpdatePlannedWorkout`, `SaveChangesAsync`.
- `api/Bryk.Application/Training/ITrainingPlanService.cs` — extend with `RescheduleAsync`.
- `api/Bryk.API/Controllers/TrainingPlansController.cs` — the controller to add the PATCH action to.
- `api/Bryk.Application/Training/Validators/PlannedWorkoutDtoValidator.cs` — the validator style.
- `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs` — the integration harness to extend.

## Acceptance criteria

### DTO (`Bryk.Application/Calendar/`)
- `ScheduleRequest { DateOnly ScheduledDate; }` — single field, no other props (surgical PATCH).

### Validator
- `ScheduleRequestValidator`: `ScheduledDate` required (DateOnly is a non-nullable struct, so this is
  the default; no explicit `NotNull` rule needed, but add a clear message if you want). **No range
  rule here** — the window check needs the plan's `StartDate`/`EndDate`, which the validator can't
  see. The window check lives in the service (throws `InvalidOperationException` → 409 via the
  existing middleware? **No** — per ADR-0008 §2 the out-of-window case is a 400 validation error. Use
  the existing `Bryk.Application.Exceptions.ValidationException` with a field error on `ScheduledDate`,
  thrown from the service after loading the plan. This keeps the 400 semantics and the field-named
  error message consistent with the rest of the API).
- Validate via `ValidateOrThrowAsync` (catches struct-level issues; the window check is a separate
  throw — see service).

### `ITrainingPlanService` / `TrainingPlanService` (extend; do not break 9-3)
Add:
- `Task RescheduleAsync(Guid planId, Guid plannedWorkoutId, ScheduleRequest request, CancellationToken ct = default)`:
  1. `await scheduleValidator.ValidateOrThrowAsync(request, ct)` (basic non-null check).
  2. `var plan = await LoadOwnedPlanAsync(planId, ct)` — 404 on missing/foreign (existing helper).
  3. `var existing = plan.PlannedWorkouts.FirstOrDefault(pw => pw.Id == plannedWorkoutId) ?? throw new KeyNotFoundException()`.
  4. **Window check:** if `request.ScheduledDate < plan.StartDate || request.ScheduledDate > plan.EndDate`,
     throw `new Bryk.Application.Exceptions.ValidationException(new[] {
       $"ScheduledDate: Scheduled date must be within the plan window ({plan.StartDate:yyyy-MM-dd} to {plan.EndDate:yyyy-MM-dd})."
     })` — this maps to 400 via the global middleware, which serializes `errors` as a string array.
     **Note the shape:** `Bryk.Application.Exceptions.ValidationException` takes `IEnumerable<string>`
     (verified in `api/Bryk.Application/Exceptions/ValidationException.cs`), **not** FluentValidation's
     `ValidationFailure` objects. The middleware (`ExceptionHandlingMiddleware`) serializes the
     `errors` array verbatim — so the frontend receives `errors: ["ScheduledDate: ..."]`. Prefix the
     message with the field name (`"ScheduledDate: "`) so the frontend's `apiErrors` parser can map it
     back to a field; check `ui/src/services/apiErrors.ts` for the established convention and match it.
  5. Stage a fresh nav-free `PlannedWorkout` exactly as `UpdatePlannedWorkoutAsync` does — carry over
     `Id`, `AthleteId`, `TrainingPlanId`, `Sport`, `Title`, `Description`, `PlannedDurationMinutes`,
     `PlannedLoad`, `CreatedAt`; set `ScheduledDate = request.ScheduledDate`. Call
     `planRepo.UpdatePlannedWorkout(updated)`.
  6. `await unitOfWork.SaveChangesAsync(ct)`.
  7. Return `Task` (no body — controller returns 204).

### Controller (additive action on `TrainingPlansController`)
- `PATCH {planId:guid}/plannedworkouts/{plannedWorkoutId:guid}/schedule` →
  `[HttpPatch("{planId:guid}/plannedworkouts/{plannedWorkoutId:guid}/schedule")]`,
  `[FromBody] ScheduleRequest request`, returns `NoContent()` (204). XML `<summary>` noting the
  plan-window constraint and 404 on missing/foreign. No try/catch.

### Tests
- **Unit** (`Bryk.Application.Tests/Training/TrainingPlanServiceTests.cs`, extend, or a new
  `RescheduleTests.cs` if the existing file is crowded — match house style):
  - `RescheduleAsync` moves `ScheduledDate` to the new value; survives reload (mock repo returns the
    staged entity with the new date).
  - Out-of-window date (below `StartDate` or above `EndDate`) throws `ValidationException` with a
    `ScheduledDate` field error.
  - Missing plan → `KeyNotFoundException`. Foreign plan (owned by another athlete) → `KeyNotFoundException`.
  - Missing planned workout (plan exists, pw id wrong) → `KeyNotFoundException`.
  - On-window boundary (`ScheduledDate == plan.StartDate` or `== plan.EndDate`) succeeds (inclusive).
- **Integration** (`Bryk.API.Tests/Training/TrainingPlansControllerTests.cs`, extend):
  - `PATCH .../schedule` with a valid in-window date → 204; a follow-up `GET /trainingplans/{id}`
    shows the planned workout on the new date.
  - Out-of-window date → 400 with a `ScheduledDate` field error in the response body.
  - Missing plan id → 404. Foreign plan (seeded under a second athlete) → 404.
  - Missing planned workout id → 404.
- `dotnet build api/Bryk.sln` + `dotnet test api/Bryk.sln` green.

## What NOT to modify
- No migration, no new package, no new repo method (reuse `GetByIdAsync` + `UpdatePlannedWorkout`).
- Don't add a full-DTO PUT path — the PATCH is single-field by design.
- Don't change `UpdatePlannedWorkoutAsync` or its validation — the PATCH is a separate method.
- Don't accept `AthleteId` from the body — always `ICurrentUserService`.
- Don't return a body on success — 204 NoContent (the calendar feed re-fetches).
- Don't put the window check in the validator — it can't see the plan; the service throws the
  `ValidationException` after loading the plan.
- Don't use a different exception type for the out-of-window case — `Bryk.Application.Exceptions.ValidationException`
  (which takes `IEnumerable<string>`, **not** FluentValidation `ValidationFailure` — verified in
  `api/Bryk.Application/Exceptions/ValidationException.cs`) → 400, consistent with every other
  validation error in the API. Prefix the message with the field name so the frontend's
  `apiErrors` parser can map it.

## Suggested commit
```
feat: planned-workout reschedule PATCH (calendar scheduling)

PATCH /api/v1/trainingplans/{id}/plannedworkouts/{pwId}/schedule takes
{scheduledDate} only and rejects (400) dates outside the plan window
[StartDate, EndDate] inclusive (ADR-0008 §2). 404 on missing/foreign
plan or planned workout; 204 on success. Stages a fresh nav-free
PlannedWorkout mirroring UpdatePlannedWorkoutAsync's discipline. No
migration, no new repo method. xUnit pins the window boundaries.
```
