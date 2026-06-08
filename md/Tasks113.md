# Task 11-3 — `Workout` execution fields + `WorkoutStepResult` entity + additive migration

## Goal
Bring the dormant `Workout` entity to life for executed-workout capture (ADR-0005 §4–5): add the
session-level execution fields, add the `WorkoutStepResult` child entity (per-step actuals, all
nullable), their repository contract, DbContext config, and the additive EF migration. Backend only
(Domain + Infrastructure). **No service, no controller** (Task 11-4).

**Generates a migration → Sr. Dev approval before apply. Additive only — must not alter any Phase-9/10
column** (`Workout`'s existing six columns, `PlannedWorkout`/`WorkoutBlock`/`WorkoutStep`).

## Depends on
- **ADR-0005 §4** — the `Workout` execution field list.
- **ADR-0005 §5** — the `WorkoutStepResult` field list, FK/delete behavior, indexes.
- **ADR-0005 "Relationships & delete behavior"** — `Workout → WorkoutStepResult` Cascade;
  `WorkoutStep → WorkoutStepResult` **NoAction**.

## Required reading
- `md/decisions/0005-training-load-and-execution.md` §4, §5, relationships table.
- `api/Bryk.Domain/Entities/Workout.cs` — the dormant entity to extend (keep its six columns intact).
- `api/Bryk.Domain/Entities/{WorkoutBlock,WorkoutStep}.cs` — the sibling child-entity + denormalized
  `AthleteId` + nav conventions to mirror.
- `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` — `OnModelCreating` (keys, precision, FK
  `OnDelete`, indexes); how `WorkoutBlock`/`WorkoutStep` are configured.
- `api/Bryk.Infrastructure/Migrations/20260608011822_AddStructuredWorkoutPayload.cs` — the
  additive-migration format + the run-from-Infrastructure (`IDesignTimeDbContextFactory`) flow.
- `api/Bryk.Domain/Interfaces/ITrainingPlanRepository.cs` — repo-contract style ("stages, does NOT
  call SaveChanges").

## Acceptance criteria
- **`Workout` (added, ADR-0005 §4)**: `ActualDurationSeconds`, `ActualDistanceMeters`, `AvgHr`, `MaxHr`
  (`int?`); `ComputedLoad`, `LoadOverride` (`decimal?`, `HasPrecision(7,2)`); `Rpe` (`decimal?`,
  `HasPrecision(3,1)`); `Notes` (`string?`, `HasMaxLength(2000)`); `StepResults`
  (`ICollection<WorkoutStepResult>`, init to `new List<…>()`). Existing columns unchanged.
- **`WorkoutStepResult` (new, ADR-0005 §5)**: `Id`, denormalized indexed `AthleteId` (no FK),
  `WorkoutId` (FK → `Workout`, **Cascade**), `WorkoutStepId` (`Guid?`, FK → `WorkoutStep`,
  **NoAction/Restrict**), `OrderIndex`, nullable `ActualDurationSeconds`/`ActualDistanceMeters`/
  `AvgPower`/`AvgHr`/`AvgPace` (`int?`), `Rpe` (`decimal?`, `HasPrecision(3,1)`); `IAuditable`.
- **DbContext**: `DbSet<WorkoutStepResult>` (and ensure `DbSet<Workout>` is mapped); config the new
  columns/precision, FK cascade from `Workout`, **NoAction** on `WorkoutStepId`, indexes
  `(WorkoutId, OrderIndex)` and `AthleteId`.
- **Repo**: an `IWorkoutRepository` contract (new) — stage add/update/remove a `Workout` with its
  `StepResults`; load a `Workout` with `StepResults` (and the linked `PlannedWorkout`/steps as needed
  for 11-4); list by athlete within a date range. Document "stages, does NOT call SaveChanges." Register
  in DI. (Reads/writes the aggregate; mirrors `ITrainingPlanRepository`'s staging style.)
- **Migration**: `dotnet ef migrations add AddWorkoutExecution` — review Up/Down (new columns on
  `Workouts`, one new `WorkoutStepResults` table, FK cascade + NoAction; **no alter of**
  `PlannedWorkouts`/`WorkoutBlocks`/`WorkoutSteps` base columns); do not apply without approval.
- **Build green; existing tests green** (entity scaffolding exercised by 11-4 tests).

## What NOT to modify
- Do not add a service/controller/DTOs — Task 11-4.
- Do not add load-formula code — Task 11-1 owns the calculator; 11-4 reuses it.
- Do not alter `Workout`'s existing six columns or any Phase-9/10 table/column.
- Do not add a session-level `AvgPower`/`AvgPace` — those live on `WorkoutStepResult` (ADR-0005 §4).

## Suggested commit
```
feat: add Workout execution fields and WorkoutStepResult entity

Bring the dormant Workout to life for executed-workout capture: session
actuals (duration/distance/HR/RPE/notes, computed + override load) and a
WorkoutStepResult child with nullable per-step actuals optionally linked
to the planned step (no-action so plan edits don't touch history).
Additive AddWorkoutExecution migration; reviewed and applied with Sr. Dev
approval. No service or load math (Tasks 11-4 / 11-1).
```
