# ADR-0001 — Supersede Mesocycle with TrainingPlan as the training framework

**Date:** 2026-05-26
**Status:** Accepted

## Context

The Bryk codebase ships with two overlapping attempts at modeling endurance training plans:

1. **Mesocycle.** Entities (`Mesocycle`, `Week`, `Day`, `DayExercise`, `Exercise`), service (`MesocycleService` in `Bryk.Infrastructure/Services/`), four controllers (`Mesocycle`, `Week`, `Day`, `Exercise`), validators (`MesocycleValidators` in `Bryk.Application/Validators/`). Predates the Cursor + Claude Code workflow and the Clean Architecture conventions locked in Phase 3. Pre-launch — no production data.
2. **TrainingPlan / PlannedWorkout / Workout.** Proposed in ROADMAP Phase 7 but not yet built.

Both target the same problem space: endurance training plans with periodization, planned workouts, and executed workouts. The differences:

- **Hierarchy.** Mesocycle uses a rigid 4-level structure (Mesocycle → Week → Day → DayExercise → Exercise). The proposed TrainingPlan model flattens this — `PlannedWorkout` carries its own date, weeks become a view rather than a structural entity.
- **Plan vs execution.** Mesocycle conflates them via nullable "actual" fields on `DayExercise`. TrainingPlan separates `PlannedWorkout` (intent) from `Workout` (executed reality), and allows `Workout` to exist without a `PlannedWorkout` reference — making unplanned executions first-class.
- **Architecture compliance.** `MesocycleService` lives in `Bryk.Infrastructure/Services/` (layer violation tracked as tech debt), accesses `ApplicationDbContext` directly (no repository), and `Exercise` declares a `SportType` enum (Bike/Run/Swim/Strength/Other) that conflicts with the `Sport` enum (Swim/Bike/Run/Triathlon) used by the onboarding domain.
- **API consumers.** Mesocycle controllers exist but are not called by any UI surface — the onboarding wizard, dashboard shell, and forthcoming profile editor do not reference them.

Phase 9 (the renumbered original Phase 7) cannot start cleanly without resolving which model is the source of truth.

## Decision

**Supersede Mesocycle.** Phase 9 builds `TrainingPlan` / `PlannedWorkout` / `Workout` as the unified training framework. The five Mesocycle entities, the service, the four controllers, and the validators are retired.

**Methodology and periodization fields carry forward as attributes on `TrainingPlan`.** The concepts represented on `Mesocycle` (`BuildRecoveryRatio`, `RecoveryWeekPercentage`, `WeeklyPatternType` — Polarized / Pyramidal / Periodization / Custom) land on the new `TrainingPlan` entity. They are forward-looking and inform Phase 13 ATP design. Methodologies remain first-class: Pyramidal, Periodization, Polarized, Norwegian (and any future additions) are configurable per plan rather than rebuilt as separate entity hierarchies.

**Executed-workout capture field design carries forward to `Workout`.** The rich actual-metrics fields on `DayExercise` (HR zone minutes, average/max HR, pace, weather, performance comparison, calories) are well-shaped for the executed `Workout` entity. They land in Phase 11 when execution capture is built, not in Phase 9.

**Strength training is a first-class v1 discipline.** Bryk offers strength training the same depth of support, advice, and tracking as cardio disciplines. Implications:

- The `Sport` enum gains `Strength`. Current values become `Swim`, `Bike`, `Run`, `Triathlon`, `Strength`.
- `PlannedWorkout` / `Workout` accommodate strength sessions in the same shape as cardio sessions. Sport-specific differences (e.g., sets/reps/load for strength, interval steps with target zones for cardio) live as discipline-specific payload on a shared base, not as separate entity hierarchies.
- Strength sessions carry a load metric comparable to TSS so they contribute to weekly load, fitness/fatigue calculations, and the Performance Management Chart on equal footing with cardio. Exact formula deferred to Phase 11.

## Consequences

**Closed by this decision:**

- ROADMAP Phase 6 Task 6-6 (Mesocycle vs TrainingPlan decision) — resolved.
- `CLAUDE.md` pending decision "Mesocycle vs new TrainingPlan model" — closed.
- Phase 6 Task 6-4 tech-debt sweep — the `MesocycleService` move to `Bryk.Application/Services/` no longer applies; the service is deleted instead. The CS8604 nullability warning in `MesocycleValidators.cs` is moot — the file is deleted. Task 7-4 (the renumbered tech-debt sweep) will be revised to reflect this.

**Created by this decision:**

- A retirement migration is required to drop the five Mesocycle tables. Scheduled as part of Phase 9 work but may land earlier as a Phase 7 cleanup task if convenient. Pre-launch, so no data-migration path needed.
- Files to delete: `api/Bryk.Domain/Entities/{Mesocycle,Week,Day,DayExercise,Exercise}.cs`; `api/Bryk.Infrastructure/Services/MesocycleService.cs`; `api/Bryk.API/Controllers/{Mesocycle,Week,Day,Exercise}Controller.cs`; `api/Bryk.Application/Validators/MesocycleValidators.cs`; any DI registrations referencing those types. Sr. Dev approval applies to the migration apply per CLAUDE.md.
- The `SportType` enum (Bike/Run/Swim/Strength/Other) defined in `Exercise.cs` is removed; `Sport` (Swim/Bike/Run/Triathlon/Strength) becomes the canonical sport enum. This is an API breaking change in principle, but no current API consumer relies on `SportType`.
- The `Methodology` enum on `Athlete` (currently Pyramidal/Periodization/Polarized/Norwegian, set during onboarding) and the periodization fields on `TrainingPlan` need reconciliation when Phase 9 lands. Resolve as part of Phase 9 design — likely make `TrainingPlan` carry its own methodology field independent of the athlete-default, since methodology can vary plan-by-plan.

**Open follow-ups deliberately deferred:**

- Exact entity shape for strength workouts within `PlannedWorkout` / `Workout` — single entity with discipline-specific payload field vs sport-typed subtypes. Decide during Phase 9 design.
- Strength load metric — TSS-equivalent formula for resistance work. Decide during Phase 11 design when the metrics engine is built.
- Whether the `Methodology` field belongs on `Athlete` (a default), on `TrainingPlan` (per-plan), or both. Decide during Phase 9 design.

## Alternatives considered

**Coexist.** Keep Mesocycle and build TrainingPlan alongside, each for a separate domain. Rejected: both models target the same problem, so every new piece of work would require deciding which to use. Doubles the bug surface, requires keeping the layer violation in `MesocycleService` (per Task 6-4), and locks in the rigid Mesocycle hierarchy permanently. The only saved cost is one drop migration; the ongoing tax is larger.

**Integrate.** Wrap Mesocycle in a TrainingPlan-shaped facade. Rejected as the worst of both worlds — preserves the rigid hierarchy and architectural violations while adding an adapter layer that adds zero capability.
