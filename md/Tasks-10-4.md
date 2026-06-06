# Task 10-4 — Structured-workout CRUD (blocks + steps through the aggregate)

## Goal
The Application + API surface to read and edit a `PlannedWorkout`'s structured payload — its blocks
and steps — through the `PlannedWorkout` aggregate (ADR-0004 §4). Reuses Task 10-3's entities/repo
and Task 10-1's zones for target validation. Backend only. No migration, no UI, no new entities.

## Depends on
- **Task 10-3** — `WorkoutBlock`/`WorkoutStep` entities + repository + applied migration.
- **Task 10-1** — zones, so a step's `TargetZone` can be validated against the athlete's sport zones.
- **ADR-0004 §2, §3** — field lists, the zone+raw-range target shape, sport-discriminated rules.

## Required reading
- `md/decisions/0004-structured-workout-and-zones.md` §2, §3.
- `api/Bryk.Application/Training/TrainingPlanService.cs` — **the reference**: primary-constructor service, `ValidateOrThrowAsync`, ownership check → `KeyNotFoundException`, stage-through-aggregate then single `SaveChangesAsync`, private `Map`.
- `api/Bryk.Application/Training/Validators/PlannedWorkoutDtoValidator.cs` — validator style.
- `api/Bryk.API/Controllers/TrainingPlansController.cs` — thin controller, nested-route style (`…/{id}/plannedworkouts/{pwId}`).
- `api/Bryk.Application.Tests/Training/` + `api/Bryk.API.Tests/Training/` — test conventions + `BrykWebApplicationFactory`.

## Acceptance criteria
- **DTOs** (`api/Bryk.Application/Training/`): `WorkoutBlockDto` (write: orderIndex, repeats, steps[]), `WorkoutStepDto` (write: intent, duration/distance, zone + power/HR/pace ranges, sets/reps/load/rpe), and Id-bearing `*Response` shapes. Reuse the existing `PlannedWorkoutResponse` (extend it with `blocks`).
- **Service** (extend `ITrainingPlanService` or a focused `IStructuredWorkoutService` — pick one, note why): set/replace a planned workout's blocks+steps (`SetStructureAsync(planId, plannedWorkoutId, blocks)`), and a read returning the workout with its blocks/steps ordered. Ownership checked on the parent plan; `KeyNotFoundException` → 404. Validate → stage → single `SaveChangesAsync`; `AthleteId` denormalized onto blocks/steps from the plan.
- **Validators**: per ADR-0004 §2/§3 — exactly one of duration/distance set; `Repeats ≥ 1`; ranges `High ≥ Low`; **sport-discriminated** (strength step requires sets/reps and forbids pace/power zone; cardio step forbids sets/reps); `TargetZone` within the sport's zone count (consult Task 10-1).
- **API**: endpoints under the existing `TrainingPlansController` (e.g. `PUT …/{id}/plannedworkouts/{pwId}/structure`, `GET …/structure`). XML summaries; no try/catch.
- **Tests** (≥4): set structure on an owned workout returns it with ordered blocks/steps; foreign plan → 404; invalid (both duration+distance, or strength step with a power zone) → 400; zone out of range → 400.

## What NOT to modify
- Do not compute load/TSS — Phase 11.
- Do not add migrations/entities — Task 10-3.
- Do not build the builder UI — Task 10-5.
- Do not change Phase-9 plan/planned-workout base endpoints beyond adding the structure sub-resource.

## Suggested commit
```
feat: add structured-workout CRUD (blocks + steps)

Edit a planned workout's blocks and steps through the TrainingPlan
aggregate: validated (sport-discriminated, zone refs checked against the
athlete's zones, exactly one of duration/distance), ownership-checked,
committed once via IUnitOfWork. Targets carry zone + optional raw ranges.
```
