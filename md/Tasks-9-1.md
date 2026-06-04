# Task 9-1 — ADR-0003: TrainingPlan domain shape + resolve the three deferred decisions

## Goal
Produce a design ADR that pins down the `TrainingPlan` / `PlannedWorkout` / `Workout` entity shapes **before any code is written**, and resolves the three open follow-ups that ADR-0001 explicitly deferred "to Phase 9 design." Every remaining Phase 9 task (9-2 … 9-6) builds against this document.

Docs-only task. No production code, no entities, no migration, no UI. Output is one new markdown file under `md/decisions/`.

## Why this is its own task
ADR-0001 (`md/decisions/0001-mesocycle-vs-trainingplan.md`) superseded the Mesocycle surface with TrainingPlan but deliberately left three shape decisions open (its "Open follow-ups deliberately deferred" section):

1. **Strength workout entity shape** within `PlannedWorkout` / `Workout` — single entity with a discipline-specific payload field vs sport-typed subtypes.
2. **Strength load metric** — TSS-equivalent formula for resistance work. (ADR-0001 defers this to **Phase 11**, not Phase 9 — note it as out of scope here and confirm the deferral; do not solve it.)
3. **Where `Methodology` lives** — on `Athlete` (a default), on `TrainingPlan` (per-plan), or both.

Settling these inside a code PR would force shape decisions under implementation pressure and scatter the rationale across commits. One decision doc first.

## Required reading before writing
- `md/decisions/0001-mesocycle-vs-trainingplan.md` — the parent decision; this ADR is its follow-up. Mirror its section structure (Context / Decision / Consequences / Alternatives considered).
- `md/decisions/0002-coaches-as-first-class.md` — for ADR format/tone consistency.
- `api/Bryk.Domain/Entities/Athlete.cs` — note `Methodology` (`MethodologyChoice`) already lives here, set during onboarding.
- `api/Bryk.Domain/Entities/Event.cs` and `Enums/Sport.cs` — the current `Sport` enum is `Swim=1, Bike=2, Run=3, Triathlon=4`; ADR-0001 says it gains `Strength`.
- `api/Bryk.Domain/Entities/Goal.cs` — for the `IAuditable` + `Guid Id` + `AthleteId` entity convention every Bryk entity follows.

## Decisions this ADR must record

**1. Entity field lists.** Concrete property lists for all three entities, each following Bryk conventions (`Guid Id`, `Guid AthleteId`, `IAuditable` → `CreatedAt`/`UpdatedAt`, `Guid` foreign keys, nav properties). At minimum:
   - **`TrainingPlan`** — owns: name, the periodization/methodology fields carried forward from Mesocycle per ADR-0001 (`WeeklyPatternType` / build-recovery ratio / recovery-week percentage — name them concretely), a date range (start/end), and a collection of `PlannedWorkout`. Decide whether `TrainingPlan` links to an `Event` (the A-race it targets) — recommend **optional `Guid? EventId`** so a plan can target a race or stand alone.
   - **`PlannedWorkout`** — owns: `Guid TrainingPlanId`, `Sport`, a scheduled `DateOnly`, a title/description, planned duration and/or planned load, and the discipline-specific payload (see decision 2). This is *intent*.
   - **`Workout`** — *executed reality.* Per ADR-0001 it may exist without a `PlannedWorkout` (unplanned executions are first-class) → **nullable `Guid? PlannedWorkoutId`**. ADR-0001 also says the rich actual-metrics fields (HR-zone minutes, avg/max HR, pace, weather, calories, performance comparison) land here **in Phase 11**, not now. Decide explicitly: define `Workout`'s *shape* now (so the schema 9-2 generates is stable), but mark the execution-capture fields as Phase-11 additions. Recommend defining a **minimal `Workout`** in Phase 9 (Id, AthleteId, optional PlannedWorkoutId, Sport, a completed-date, audit) and noting the Phase-11 metric columns as a planned follow-up migration.

**2. Strength-vs-cardio payload (ADR-0001 §53).** Choose one and justify:
   - **(a) Single entity + discipline-specific payload** — one `PlannedWorkout`/`Workout` shape with a nullable structured payload (e.g. a JSON/owned-type column carrying cardio interval steps OR strength sets/reps/load, discriminated by `Sport`). Fewer tables, matches ADR-0001's "shared base, not separate entity hierarchies" language.
   - **(b) Sport-typed subtypes** — EF TPH/TPT inheritance with `CardioPlannedWorkout` / `StrengthPlannedWorkout` subclasses.
   - **Recommendation: (a)**, consistent with ADR-0001's stated intent ("discipline-specific payload on a shared base, not as separate entity hierarchies"). If (a), specify the payload representation (owned type vs JSON column vs separate child rows) — recommend a **structured child collection for steps/sets** kept simple for Phase 9, or an explicit "payload is a Phase-10 builder concern; Phase 9 stores title + sport + duration/load only" carve-out. **Pick and document which.**

**3. Where `Methodology` lives (ADR-0001 §55).** Recommend **per-plan**: `TrainingPlan` carries its own methodology field (reuse the existing `MethodologyChoice` enum) independent of the `Athlete.Methodology` default, since methodology can vary plan-by-plan. The athlete-level field stays as the onboarding default / new-plan seed. Document the reconciliation.

**4. `Sport` enum gains `Strength`.** Confirm `Strength = 5` is added in 9-2. Note the consequence: the onboarding `Sport` select and existing `SportThresholdsDto` flows are NOT required to expose Strength in Phase 9 (cardio thresholds only) — flag it as a known gap, not a Phase 9 deliverable.

**5. Scope boundary.** Record explicitly what Phase 9 does NOT build: executed-`Workout` capture endpoints (Phase 11), TSS/load math and the strength load formula (Phase 11), the structured-workout builder UI (Phase 10), zones (Phase 10). Phase 9 = author a plan + schedule planned workouts + surface this week's planned workouts.

## Acceptance criteria
- New file `md/decisions/0003-trainingplan-domain-shape.md`, `Status: Accepted`, dated, following the ADR-0001/0002 section structure.
- All five decisions above are recorded with a concrete recommendation and rationale, not left open.
- The entity field lists are concrete enough that 9-2 can be implemented without further design discussion.
- A short "Consequences → for Tasks 9-2…9-6" subsection mapping each downstream task to the decisions it depends on.
- `md/decisions/0001-mesocycle-vs-trainingplan.md` is **not modified** (this is a follow-up ADR, not an edit). Optionally add a one-line "superseded-follow-up: ADR-0003" pointer **only if** you do it as a single surgical line — otherwise leave 0001 untouched and cross-reference from 0003.

## What NOT to do
- Do not write any C# — no entities, no enum edits, no migration. That is Task 9-2.
- Do not solve the strength load-metric formula — ADR-0001 defers it to Phase 11. Note the deferral and move on.
- Do not design the workout-builder UI or zones — Phase 10.
- Do not modify ADR-0001 or ADR-0002 beyond (optionally) a single cross-reference line in 0001.
- Do not add the migration plan for Phase-11 `Workout` metric columns beyond a one-line "planned follow-up" note.

## Test plan
Docs-only — no build/test. Verification is a read-through:
1. A developer picking up 9-2 can implement all three entities + the enum change from this doc alone, with zero open shape questions.
2. Each of decisions 2, 3, 4 has a single chosen answer (not "either could work").
3. The scope boundary makes clear what is Phase 9 vs Phase 10 vs Phase 11.

## Suggested commit
```
docs: ADR-0003 — TrainingPlan domain shape; resolve Phase 9 deferred decisions

Pins the TrainingPlan / PlannedWorkout / Workout entity field lists and
resolves the three follow-ups ADR-0001 deferred to Phase 9 design:
strength uses a shared-base discipline payload (not subtypes), Methodology
lives per-plan on TrainingPlan, and Sport gains Strength. Executed-Workout
metric capture and the strength load formula stay deferred to Phase 11.

Design-only; Tasks 9-2…9-6 build against this ADR.
```
