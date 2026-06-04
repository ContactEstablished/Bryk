# Task 9-3 — TrainingPlan CRUD: service, validators, DTOs, controller

## Goal
Build the application + API surface for authoring training plans: create a plan, read the athlete's plans, and add / edit / remove planned workouts within a plan. Reuses the entities + repository from Task 9-2.

Backend-only (Application + API). No migration, no UI, no new entities.

## Depends on
- **Task 9-2** — `TrainingPlan` / `PlannedWorkout` entities, `ITrainingPlanRepository`, DbContext config, and the applied migration must exist.
- **Task 9-1 (ADR-0003)** — aggregate boundary (is `PlannedWorkout` edited through the `TrainingPlan` aggregate, or directly?) and the payload decision drive the DTO shapes.

## Required reading
- `md/decisions/0003-trainingplan-domain-shape.md` — aggregate boundary + payload shape.
- `api/Bryk.Application/Events/EventService.cs` — **the reference implementation.** Primary-constructor service consuming `ICurrentUserService` + `IValidator<T>` + repository + `IUnitOfWork`; `await validator.ValidateOrThrowAsync(request, ct)`; ownership check (`entity.AthleteId != currentUser.GetCurrentAthleteId()` → `throw new KeyNotFoundException()`); stage via repo then `await unitOfWork.SaveChangesAsync(ct)`; private static `Map(entity)` → response DTO.
- `api/Bryk.Application/Events/EventResponse.cs`, `Onboarding/EventDto.cs` — read-side (`*Response`, Id-bearing) vs write-side (`*Dto`/`*Request`, Id-less) DTO split.
- `api/Bryk.Application/Onboarding/Validators/EventDtoValidator.cs` — `AbstractValidator<T>` style (`NotEmpty`, `MaximumLength`, `Must(...).WithMessage`, `IsInEnum().When(...)`).
- `api/Bryk.Application/Common/Validation/ValidationExtensions.cs` — use `ValidateOrThrowAsync`, NOT `ValidateAndThrowAsync` (middleware won't handle FluentValidation's exception type).
- `api/Bryk.API/Controllers/EventsController.cs` — thin controller: `[ApiController]` + `[ApiVersion("1.0")]` + `[Route("api/v{version:apiVersion}/[controller]")]`, `IActionResult` returns (`201`/`Ok`/`NoContent`), XML `<summary>` per endpoint, no try/catch.
- `api/Bryk.API/Program.cs` — scoped service registration block.
- `api/Bryk.Application.Tests/Events/EventServiceTests.cs` and `api/Bryk.API.Tests/Events/EventsControllerTests.cs` — test conventions; `api/Bryk.API.Tests/Fixtures/BrykWebApplicationFactory.cs` for integration setup.

## Acceptance criteria

**Application — DTOs (`api/Bryk.Application/Training/`** — new folder):**
- `TrainingPlanRequest.cs` — write-side, Id-less: name, methodology (`MethodologyChoice` per ADR-0003 per-plan decision), date range, optional `EventId`, periodization fields per ADR-0003, and (per the aggregate decision) optionally a list of `PlannedWorkoutDto` for create.
- `PlannedWorkoutDto.cs` — write-side: sport, scheduled date, title/description, planned duration/load, + payload per ADR-0003.
- `TrainingPlanResponse.cs` — read-side, Id-bearing, with `PlannedWorkoutResponse` children (Id-bearing) so the client can target per-item edit/delete.
- `PlannedWorkoutResponse.cs` — read-side, Id-bearing.

**Application — service (`api/Bryk.Application/Training/`):**
- `ITrainingPlanService.cs` + `TrainingPlanService.cs` — primary-constructor, consuming `ICurrentUserService`, the validators, `ITrainingPlanRepository`, `IUnitOfWork`. Methods (final set follows ADR-0003's aggregate boundary; this is the recommended set):
  - `CreateAsync(TrainingPlanRequest) → TrainingPlanResponse`
  - `GetByAthleteAsync() → IReadOnlyList<TrainingPlanResponse>` (current athlete from `ICurrentUserService`, never from caller)
  - `GetByIdAsync(Guid) → TrainingPlanResponse` (ownership-checked; `KeyNotFoundException` if missing/foreign)
  - `AddPlannedWorkoutAsync(Guid planId, PlannedWorkoutDto) → PlannedWorkoutResponse`
  - `UpdatePlannedWorkoutAsync(Guid planId, Guid plannedWorkoutId, PlannedWorkoutDto) → PlannedWorkoutResponse`
  - `RemovePlannedWorkoutAsync(Guid planId, Guid plannedWorkoutId)`
  - Every mutator: validate → ownership check → stage → single `SaveChangesAsync`. Audit fields never set manually.

**Application — validators (`api/Bryk.Application/Training/Validators/`):**
- `TrainingPlanRequestValidator.cs` — name `NotEmpty`/`MaximumLength`; end date ≥ start date; methodology `IsInEnum`; etc.
- `PlannedWorkoutDtoValidator.cs` — title `NotEmpty`/`MaximumLength`; sport `IsInEnum`; scheduled date present; duration/load non-negative; payload rules per ADR-0003.
- Validators are auto-discovered by `AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>()` — no manual registration needed.

**API — controller (`api/Bryk.API/Controllers/TrainingPlansController.cs`):**
- `[ApiController]` + `[ApiVersion("1.0")]` + `[Route("api/v{version:apiVersion}/[controller]")]` → routes under `/api/v1/trainingplans`.
- Endpoints: `POST /` (201 + body), `GET /` (athlete's plans), `GET /{id:guid}` (one plan, 404 if missing/foreign), `POST /{id:guid}/plannedworkouts` (201), `PUT /{id:guid}/plannedworkouts/{plannedWorkoutId:guid}` (Ok), `DELETE /{id:guid}/plannedworkouts/{plannedWorkoutId:guid}` (NoContent).
- XML `<summary>` per endpoint; thin; no try/catch (global middleware handles `KeyNotFoundException` → 404 and `ValidationException` → 400, as it does for Events/Goals).

**DI:**
- `api/Bryk.API/Program.cs` — register `ITrainingPlanService → TrainingPlanService` (scoped).

**Tests:**
- `api/Bryk.Application.Tests/Training/TrainingPlanServiceTests.cs` — at least: create plan returns it; add planned workout to owned plan; update/remove on a foreign plan throws `KeyNotFoundException`; invalid request (end < start, or empty title) throws `ValidationException`.
- `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs` — at least one integration test for the create→read happy path and one 404 path against `BrykWebApplicationFactory`.
- Net new tests ≥ 4.

## Files likely to change/add
- `api/Bryk.Application/Training/{ITrainingPlanService,TrainingPlanService,TrainingPlanRequest,PlannedWorkoutDto,TrainingPlanResponse,PlannedWorkoutResponse}.cs` (new)
- `api/Bryk.Application/Training/Validators/{TrainingPlanRequestValidator,PlannedWorkoutDtoValidator}.cs` (new)
- `api/Bryk.API/Controllers/TrainingPlansController.cs` (new)
- `api/Bryk.API/Program.cs` — one service DI line
- `api/Bryk.Application.Tests/Training/TrainingPlanServiceTests.cs` (new)
- `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs` (new)

## What NOT to modify
- Do not add or alter entities, the `Sport` enum, the DbContext, or any migration — that was Task 9-2.
- Do not build the "this week" read endpoint — that's Task 9-4 (deliberately a separate, purpose-built read model).
- Do not build any UI — Tasks 9-5 / 9-6.
- Do not add executed-`Workout` write endpoints — Phase 11.
- Do not touch the Onboarding / Profile / Events / Goals surfaces.
- Do not switch any query to Dapper — EF Core per CLAUDE.md.

## Test plan
1. `dotnet build api/Bryk.sln` green.
2. `dotnet test api/Bryk.sln` green; count up by ≥4.
3. Manual smoke (API running, `DevAuth:CurrentAthleteId` set): `POST /api/v1/trainingplans` create a plan → 201; `POST .../{id}/plannedworkouts` add two sessions → 201 each; `GET /api/v1/trainingplans/{id}` → plan with both planned workouts; `PUT`/`DELETE` a planned workout → Ok/NoContent; create with end < start → 400; GET a random GUID → 404.
4. `git diff --stat` — only Application + API + tests + one Program.cs line.

## Suggested commit
```
feat: add TrainingPlan CRUD (service, validators, DTOs, controller)

Authoring surface for training plans under /api/v1/trainingplans: create
a plan, list/read the athlete's plans, and add/edit/remove planned
workouts. Service lives in Bryk.Application/Training (correct layer —
not the old Mesocycle violation), validates via ValidateOrThrowAsync,
and commits once through IUnitOfWork. Ownership-checked; foreign/missing
resources 404 via the global handler.
```
