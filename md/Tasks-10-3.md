# Task 10-3 — `WorkoutBlock` / `WorkoutStep` entities + additive migration

## Goal
Add the structured-workout payload from ADR-0004 §2: `WorkoutBlock` (repeatable, ordered, owned by
`PlannedWorkout`) and `WorkoutStep` (the shared cardio+strength row), their repository contract,
DbContext config, and the additive EF migration. Backend only (Domain + Infrastructure). No service,
no controller (Task 10-4).

**Generates a migration → Sr. Dev approval before apply. Additive only — must not alter Phase-9
`PlannedWorkout` columns (ADR-0003 §2 / ADR-0004 §4).**

## Depends on
- **ADR-0004 §2** — the `WorkoutBlock` / `WorkoutStep` field lists, relationships, delete behavior, indexes.

## Required reading
- `md/decisions/0004-structured-workout-and-zones.md` §2, §4 (relationships table).
- `api/Bryk.Domain/Entities/PlannedWorkout.cs`, `TrainingPlan.cs` — entity + nav conventions; the denormalized-`AthleteId` pattern.
- `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` — `OnModelCreating` config (keys, precision, FK `OnDelete`, indexes).
- `api/Bryk.Infrastructure/Migrations/<latest>_AddTrainingPlanDomain.cs` — generated-migration format + the `IDesignTimeDbContextFactory` run-from-Infrastructure flow.
- `api/Bryk.Domain/Interfaces/ITrainingPlanRepository.cs` — repo-contract style ("stages, does NOT call SaveChanges").

## Acceptance criteria
- **Enum:** `StepIntent { Warmup = 1, Work = 2, Recovery = 3, Cooldown = 4, Rest = 5 }`.
- **Entities** (ADR-0004 §2 tables): `WorkoutBlock` (`PlannedWorkoutId` FK Cascade, `OrderIndex`, `Repeats`, denormalized `AthleteId`, `Steps` collection); `WorkoutStep` (`WorkoutBlockId` FK Cascade, `OrderIndex`, `Intent`, nullable duration/distance, nullable zone + power/HR/pace low-high, nullable strength sets/reps/`LoadKg`/`Rpe`). `IAuditable`; precision `LoadKg (6,2)`, `Rpe (3,1)`.
- **Inverse nav:** add `PlannedWorkout.Blocks` (`ICollection<WorkoutBlock>`); add `WorkoutBlock.Steps`. Match existing init style.
- **Repo:** extend `ITrainingPlanRepository` (or a new contract) with block/step staging used by 10-4 — load a `PlannedWorkout` with `Blocks.Steps` included; stage add/update/remove of blocks & steps. Decide via ADR-0004's aggregate boundary (edited through `PlannedWorkout`); document "stages, does NOT call SaveChanges."
- **DbContext:** `DbSet<WorkoutBlock>`, `DbSet<WorkoutStep>`; config blocks (keys, FK Cascade, indexes `(PlannedWorkoutId, OrderIndex)`, `(WorkoutBlockId, OrderIndex)`, `AthleteId`).
- **Migration:** `dotnet ef migrations add AddStructuredWorkoutPayload` — review Up/Down (two new tables only, FK cascade, no alter of `PlannedWorkouts`); do not apply without approval.
- **DI:** register any new repository.
- **Build green; existing tests green** (entity scaffolding exercised by 10-4 tests).

## What NOT to modify
- Do not add the load/TSS columns or any execution-capture fields — Phase 11.
- Do not build recursive block-in-block nesting — two-level only (ADR-0004 §2).
- Do not alter `PlannedWorkout`/`TrainingPlan`/`Workout` columns or the Phase-9 migrations.
- Do not add DTOs/services/controllers — Task 10-4.

## Suggested commit
```
feat: add WorkoutBlock / WorkoutStep structured-workout payload

Repeatable blocks owning ordered steps hang off PlannedWorkout (ADR-0004);
shared cardio+strength step row with nullable zone/power/HR/pace ranges and
sets/reps/load. Additive AddStructuredWorkoutPayload migration; reviewed and
applied with Sr. Dev approval. No load math or execution capture (Phase 11).
```
