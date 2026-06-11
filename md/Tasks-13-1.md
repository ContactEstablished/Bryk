# Task 13-1 — Workout edit + delete (PUT / DELETE) with load recompute

## Surface
Backend only. Add `PUT /api/v1/workouts/{id}` (replace-style update) and
`DELETE /api/v1/workouts/{id}` (hard delete) to the existing `WorkoutsController`, plus the
`IWorkoutService.UpdateAsync/DeleteAsync` methods, an `UpdateWorkoutRequest` + validator, the
repository reads that back them, and one **additive** field on `WorkoutResponse`
(`TrainingPlanId`) so the Phase 13-4 detail view can reach the existing structure endpoint without
fattening the response with planned structure. No migration, no new entity, no UI.

## Why
Phase 11 shipped log + read for executed workouts but no edit/delete — history is append-only and
uncorrectable. Phase 14's PMC bakes on `EffectiveLoad`, so the write-gap must close (and load must
**recompute on every edit**) before analytics trust the series. Hard delete is the locked v1 default
(soft delete would be a migration → Sr. Dev approval; not in scope).

## Depends on
- **Task 11-3 / 11-4** — `Workout` + `WorkoutStepResult`, `IWorkoutService`/`WorkoutService`,
  `LogWorkoutRequest`(+validator), `WorkoutResponse`, `IWorkoutRepository`.
- **Task 11-1** — `ILoadService.ComputeActualLoadAsync`, reused verbatim on the edited actuals.
- **ADR-0005 §4–6** — execution shape, per-step actuals, actual-load reuse, `EffectiveLoad =
  LoadOverride ?? ComputedLoad`.

## Required reading
- `api/Bryk.Application/Training/Workouts/WorkoutService.cs` — the log/read aggregate to extend:
  `BuildStepResults`, `Map`/`MapResult`, ownership-via-`KeyNotFoundException`, single
  `SaveChangesAsync`.
- `api/Bryk.Application/Training/StructuredWorkoutService.cs` — **the replace reference**: stage-delete
  existing children, stage the new graph, single commit, re-read, map-with-load.
- `api/Bryk.Application/Training/Workouts/{LogWorkoutRequest,LogWorkoutRequestValidator,WorkoutResponse}.cs`
  — DTO + validator + response style to mirror.
- `api/Bryk.Infrastructure/Repositories/WorkoutRepository.cs` + `api/Bryk.Domain/Interfaces/IWorkoutRepository.cs`
  — note `GetByIdAsync` is `AsNoTracking`; update/delete need a **tracked** read.
- `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs` + `Fixtures/BrykWebApplicationFactory.cs`
  — integration-test conventions (InMemory provider, `TestAthleteId`).
- `api/Bryk.Application.Tests/Training/WorkoutServiceTests.cs` — unit-test stubs to extend.

## Decisions (locked here per the ROADMAP "confirm in task 1" note)
1. **Replace semantics + load.** `UpdateAsync` replaces the session actuals and the **whole**
   step-result list (positional, like the structure PUT). `Workout.ComputedLoad` is **recomputed
   from the edited actuals on every update** via `ILoadService.ComputeActualLoadAsync` and persisted.
2. **`LoadOverride` survives unless explicitly cleared.** `UpdateWorkoutRequest` carries
   `LoadOverride` (just like `LogWorkoutRequest`); the request value is written through verbatim.
   The 13-4 edit form pre-fills it from the current workout, so a normal round-trip preserves it;
   blanking the field sends `null` and clears the override. `EffectiveLoad = LoadOverride ??
   ComputedLoad` is still derived in the mapper — never stored.
3. **`WorkoutResponse` stays lean; add only `TrainingPlanId` (additive, nullable).** The client
   composes planned-vs-actual against the existing
   `GET /trainingplans/{planId}/plannedworkouts/{pwId}/structure` (13-4), which needs the plan id.
   Rather than embedding the planned structure in every workout read, expose the linked workout's
   plan id. Populate it **only on the single-workout `GetAsync` detail read** (resolved from the
   linked `PlannedWorkout`); leave it `null` on list reads so those stay single-table. Unlinked
   workouts (`PlannedWorkoutId == null`) keep it `null`.

## Acceptance criteria
- **DTO + validator** (`api/Bryk.Application/Training/Workouts/`):
  - `UpdateWorkoutRequest` with the same writable fields as `LogWorkoutRequest`
    (`Sport`, `CompletedDate`, `PlannedWorkoutId?`, session actuals, `LoadOverride?`, `Rpe?`,
    `Notes?`, `StepResults?` of `WorkoutStepResultDto`). Reuse `WorkoutStepResultDto`.
  - `UpdateWorkoutRequestValidator` mirrors `LogWorkoutRequestValidator` rule-for-rule (sport in
    enum; `CompletedDate` not future; `Notes` ≤ 2000; `Rpe` 0–10; `LoadOverride` ≥ 0; per-step
    actuals stay nullable). Use `ValidateOrThrowAsync`.
- **`WorkoutResponse`** gains `public Guid? TrainingPlanId { get; set; }` (additive). Mirror it on the
  TS type in 13-4 (not here).
- **Service** (`IWorkoutService` + `WorkoutService`):
  - `Task<WorkoutResponse> UpdateAsync(Guid id, UpdateWorkoutRequest request, CancellationToken ct)`
    — validate; load the **tracked** owned workout (404 `KeyNotFoundException` if missing or
    `AthleteId` mismatch); if `PlannedWorkoutId` is set, verify it belongs to the athlete (404 on
    foreign), optionally seeding step results from its planned steps when the request supplies none
    (reuse `BuildStepResults`); overwrite scalar fields; **replace** the step-result collection
    (clear tracked children → add the new rows, so EF orphan-deletes the old via the cascade
    relationship); recompute + persist `ComputedLoad`; single `SaveChangesAsync`; re-read and `Map`.
  - `Task DeleteAsync(Guid id, CancellationToken ct)` — load the tracked owned workout (404 if
    missing/foreign); `repo.Delete(workout)`; single `SaveChangesAsync`. `WorkoutStepResult`
    children cascade.
  - `GetAsync` now also resolves `TrainingPlanId` (decision 3) when `PlannedWorkoutId` is set.
- **Repository** (`IWorkoutRepository` + `WorkoutRepository`):
  - `Task<Workout?> GetByIdTrackedAsync(Guid id, CancellationToken ct)` — tracked, `.Include`
    `StepResults` (ordered), for update/delete. (Existing `Update`/`Delete` staging methods stay.)
- **Controller** (`WorkoutsController`):
  - `[HttpPut("{id:guid}")]` → `UpdateAsync`, returns `Ok(result)` (200).
  - `[HttpDelete("{id:guid}")]` → `DeleteAsync`, returns `NoContent()` (204).
  - XML `<summary>` on both; no try/catch (global middleware maps `KeyNotFoundException` → 404,
    `ValidationException` → 400).
- **Tests.**
  - *Application* (`WorkoutServiceTests`, extend stubs with `GetByIdTrackedAsync`): update recomputes
    `ComputedLoad` from the new actuals; update with `LoadOverride` set → `EffectiveLoad` = override &
    `IsLoadOverride` true; update with `LoadOverride` null → `EffectiveLoad` = recomputed; update of a
    foreign/missing workout → `KeyNotFoundException`, no save; delete stages a remove + one save;
    delete of foreign/missing → `KeyNotFoundException`, no save.
  - *Integration* (new `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs`): log → PUT changes a
    field and returns 200 with the new value; PUT to a random id → 404; DELETE returns 204 and a
    subsequent GET → 404; DELETE random id → 404. (InMemory cascade-deletes the loaded step results.)
- `dotnet build api/Bryk.sln` green; `dotnet test api/Bryk.sln` green (existing 84 + the new cases).

## What NOT to modify
- No migration, no entity/field changes (all columns exist). If you think you need one — **stop, ask.**
- Don't change the load formulas — reuse `ILoadService` as-is.
- Don't touch planned-workout / plan / structure endpoints or `LogAsync`/`GetRecentAsync` semantics
  (13-2 owns the list endpoint).
- Don't embed planned blocks/steps into `WorkoutResponse` — only the additive `TrainingPlanId`.
- No soft-delete, no UI.

## Suggested commit
```
feat: edit and delete executed workouts (PUT/DELETE + load recompute)

Replace-style PUT /workouts/{id} recomputes ComputedLoad from the edited
actuals (LoadOverride passed through, EffectiveLoad derived); hard DELETE
/workouts/{id} cascades step results and returns 204. Both 404 on
missing/foreign. WorkoutResponse gains an additive TrainingPlanId on the
detail read so the client can reach the existing structure endpoint.
```
