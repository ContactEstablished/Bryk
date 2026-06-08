# Task 11-4 — Executed-workout CRUD + actual-load computation

## Goal
The Application + API surface to **log a completed workout** and read it back (ADR-0005 §4–6): an
`IWorkoutService` aggregate that captures session-level + per-step actuals (optionally seeded from a
`PlannedWorkout`'s planned steps), computes the **actual load** by reusing Task 11-1's calculator, and
reads a workout / lists by athlete-week. Backend only. No migration, no UI, no new entities.

## Depends on
- **Task 11-3** — `Workout` execution fields + `WorkoutStepResult` + `IWorkoutRepository` + applied migration.
- **Task 11-1** — `ILoadService` / `LoadCalculator`, reused on captured actuals.
- **ADR-0005 §4, §5, §6** — execution shape, per-step actuals, actual-load reuse.

## Required reading
- `md/decisions/0005-training-load-and-execution.md` §4, §5, §6.
- `api/Bryk.Application/Training/StructuredWorkoutService.cs` — **the reference** for aggregate
  capture: primary-constructor service, `ValidateOrThrowAsync`, denormalized-`AthleteId` ownership →
  `KeyNotFoundException`, stage-through-aggregate then single `SaveChangesAsync`, private `Map`.
- `api/Bryk.Application/Training/TrainingPlanService.cs` — the validate→stage→single-commit flow.
- `api/Bryk.Application/Training/{WorkoutStepDto,WorkoutStepResponse}.cs` — DTO style to parallel for
  step-result shapes.
- `api/Bryk.API/Controllers/TrainingPlansController.cs` — thin controller / `[ApiVersion]` / route style.
- `api/Bryk.Application.Tests/Training/` + `api/Bryk.API.Tests/Training/` — test conventions +
  `BrykWebApplicationFactory`.

## Acceptance criteria
- **DTOs** (`api/Bryk.Application/Training/` or a `…/Workouts/` folder): `LogWorkoutRequest`
  (sport, completedDate, optional `plannedWorkoutId`, session actuals, `stepResults[]`),
  `WorkoutStepResultDto` (write: optional `workoutStepId`, orderIndex, nullable actuals, rpe),
  `WorkoutResponse` + `WorkoutStepResultResponse` (Id-bearing, with `ComputedLoad`/`EffectiveLoad`/
  `IsLoadOverride` per ADR-0005 §4 — `EffectiveLoad = LoadOverride ?? ComputedLoad`).
- **Service** `IWorkoutService`: `LogAsync(request)` — if `plannedWorkoutId` is supplied, verify it
  belongs to the current athlete (`KeyNotFoundException` → 404) and optionally seed step results from
  its planned steps; capture session + per-step actuals; **compute actual load** via `ILoadService`
  and persist to `Workout.ComputedLoad`; denormalize `AthleteId` onto the workout + step results;
  validate → stage → single `SaveChangesAsync`. Reads: `GetAsync(id)` (owned, else 404) and
  `GetByWeekAsync(start, end)` / list-by-athlete for Recent Activity + the dashboard.
- **Validators**: nullable per-step actuals are allowed (partial entry OK, ADR-0005 §5); `completedDate`
  not in the future; if `plannedWorkoutId` set it must be a Guid; sport required. Use
  `ValidateOrThrowAsync`.
- **API**: a new `WorkoutsController` — `POST /workouts` (log), `GET /workouts/{id}`, `GET /workouts`
  (by week / recent). `[ApiController]` + `[ApiVersion("1.0")]` + `[Route("api/v{version:apiVersion}/[controller]")]`;
  XML summaries; no try/catch.
- **Tests** (≥4): log an owned workout (with `plannedWorkoutId`) returns it with computed actual load;
  foreign `plannedWorkoutId` → 404; **partial** per-step actuals (some fields null) log successfully;
  actual load is computed from captured actuals (assert a known value).

## What NOT to modify
- Do not add migrations/entities — Task 11-3.
- Do not change the load formulas — Task 11-1 owns them; reuse `ILoadService`.
- Do not build the log-workout UI — Task 11-5.
- Do not touch the planned-workout / plan endpoints.

## Suggested commit
```
feat: add executed-workout capture (log + actual load)

Log a completed workout through an aggregate: session and nullable
per-step actuals, optionally seeded from a planned workout, ownership-
checked, committed once via IUnitOfWork. Actual load reuses the 11-1
calculator on captured actuals (LoadOverride as the manual override).
```
