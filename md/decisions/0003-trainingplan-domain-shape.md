# ADR-0003 — TrainingPlan / PlannedWorkout / Workout domain shape

**Date:** 2026-06-03
**Status:** Accepted

## Context

[ADR-0001](0001-mesocycle-vs-trainingplan.md) superseded the Mesocycle surface with a
`TrainingPlan` / `PlannedWorkout` / `Workout` framework, but deliberately left three shape
questions open "to Phase 9 design" (its *Open follow-ups deliberately deferred* section):

1. The entity shape for strength workouts within `PlannedWorkout` / `Workout` — a single
   shared entity with a discipline-specific payload, or sport-typed subtypes.
2. The strength load metric (a TSS-equivalent for resistance work) — ADR-0001 puts this in
   **Phase 11**, not Phase 9.
3. Where `Methodology` lives — on `Athlete` (a default), on `TrainingPlan` (per-plan), or both.

Every remaining Phase 9 task (9-2 … 9-6) builds against these shapes. Settling them inside a
code PR would force the decisions under implementation pressure and scatter the rationale
across commits. This ADR pins the three entity field lists and resolves the open questions so
Task 9-2 can implement the entities, the `Sport` enum change, and the migration from this
document alone — with zero remaining shape questions.

This is a follow-up to ADR-0001, not an edit of it. ADR-0001 is left untouched; this ADR
cross-references it.

### Entity conventions this ADR follows

Grounded in `Athlete.cs`, `Event.cs`, `Goal.cs`, `AthleteSportProfile.cs`:

- Every entity is `IAuditable` (`CreatedAt` / `UpdatedAt`, set globally by
  `AuditableEntityInterceptor` — never manually) with a `Guid Id` and a `Guid AthleteId`.
- Foreign keys are `Guid`; required navs are `= null!`, collections are `= new List<…>()`.
- Enums live in `Bryk.Domain.Entities` (file under `Entities/Enums/`), 1-based explicit values.
- `decimal` columns get `HasPrecision`; strings get `HasMaxLength`.

## Decision

### 1. Entity field lists

Three entities. `TrainingPlan` is the aggregate root; `PlannedWorkout` is owned by it;
`Workout` is a separate, minimal, **dormant-until-Phase-11** record of executed reality.

Types below are a binding spec, not C# — Task 9-2 writes the C#.

#### `TrainingPlan` (aggregate root — intent container)

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `AthleteId` | `Guid` | Owner. FK → `Athlete`, **Cascade**. |
| `Name` | `string` | Required, `HasMaxLength(200)`. |
| `EventId` | `Guid?` | **Optional** target A-race. FK → `Event`, **SetNull**. Null = standalone plan. |
| `StartDate` | `DateOnly` | Plan window start. |
| `EndDate` | `DateOnly` | Plan window end (validator: `EndDate >= StartDate`). |
| `Methodology` | `MethodologyChoice` | **Per-plan** (see decision 3). Seeded from `Athlete.Methodology`, overridable. |
| `BuildWeeks` | `int?` | Build:recovery ratio, numerator (e.g. `3`). Forward-looking (Phase 13 ATP); not surfaced in Phase 9 UI. |
| `RecoveryWeeks` | `int?` | Build:recovery ratio, denominator (e.g. `1`). Together with `BuildWeeks` replaces Mesocycle's single `BuildRecoveryRatio` field with an unambiguous pair. |
| `RecoveryWeekPercentage` | `decimal?` | Recovery-week volume as % of a build week (e.g. `60.0`). `HasPrecision(5,2)`. Forward-looking. |
| `PlannedWorkouts` | `ICollection<PlannedWorkout>` | Owned children. `= new List<…>()`. |
| `Event` | `Event?` | Nav for the optional target event. |
| `Athlete` | `Athlete` | `= null!`. |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Audit. |

Mesocycle's `WeeklyPatternType` (Polarized / Pyramidal / Periodization / Custom) is **not**
carried as a separate field — it is the same intensity-distribution axis as `MethodologyChoice`
and is subsumed by `Methodology` (see decision 3). The three numeric periodization fields
(`BuildWeeks`, `RecoveryWeeks`, `RecoveryWeekPercentage`) are the genuinely distinct survivors;
all are nullable and forward-looking, so Phase 9's authoring flow need not populate them.

#### `PlannedWorkout` (intent — a scheduled session)

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `AthleteId` | `Guid` | **Denormalized**, indexed (see *Relationships* below). Service sets `= TrainingPlan.AthleteId`. |
| `TrainingPlanId` | `Guid` | FK → `TrainingPlan`, **Cascade**. |
| `Sport` | `Sport` | Includes `Strength` (decision 4). |
| `ScheduledDate` | `DateOnly` | The day the session is planned for. Drives the "This Week" query (9-4). |
| `Title` | `string` | Required, `HasMaxLength(200)`. |
| `Description` | `string?` | `HasMaxLength(2000)`. |
| `PlannedDurationMinutes` | `int?` | Planned duration, whole minutes. |
| `PlannedLoad` | `decimal?` | Planned training-load target (TSS-equivalent), `HasPrecision(6,2)`. A manually entered/plan-set number — **not** the computed strength load formula (Phase 11, decision 2). |
| `TrainingPlan` | `TrainingPlan` | `= null!`. |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Audit. |

**No discipline payload field in Phase 9** (interval steps / strength sets) — see decision 2.

#### `Workout` (executed reality — minimal, dormant until Phase 11)

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `AthleteId` | `Guid` | **Denormalized**, indexed (see *Relationships*). |
| `PlannedWorkoutId` | `Guid?` | **Nullable** — unplanned executions are first-class (ADR-0001 §16). FK → `PlannedWorkout`, **SetNull**. |
| `Sport` | `Sport` | The executed discipline. |
| `CompletedDate` | `DateOnly` | When it was performed. |
| `PlannedWorkout` | `PlannedWorkout?` | Nullable nav. |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Audit. |

The rich execution-capture metrics ADR-0001 §28 earmarks for `Workout` (HR-zone minutes,
avg/max HR, pace, distance, weather, calories, performance-vs-plan, actual load) are **Phase 11**.
They land via a single additive follow-up migration that touches only the `Workout` table.
Defining `Workout`'s shape now keeps the Phase-9 schema stable; Phase 11 adds columns, not a new
relationship graph.

#### Relationships & delete behavior

| Relationship | FK | Delete behavior | Inverse nav added in Phase 9? |
|---|---|---|---|
| `Athlete` → `TrainingPlan` | `TrainingPlan.AthleteId` | **Cascade** | Yes — `Athlete.TrainingPlans`. |
| `TrainingPlan` → `PlannedWorkout` | `PlannedWorkout.TrainingPlanId` | **Cascade** | Yes — `TrainingPlan.PlannedWorkouts`. |
| `Event` → `TrainingPlan` | `TrainingPlan.EventId` (nullable) | **SetNull** | No (not needed in Phase 9). |
| `PlannedWorkout` → `Workout` | `Workout.PlannedWorkoutId` (nullable) | **SetNull** | No (Workout dormant). |

**`AthleteId` on `PlannedWorkout` and `Workout` is a denormalized, indexed column with no FK
relationship to `Athlete`.** This is deliberate:

- It keeps each table to a **single delete-action path**, sidestepping SQL Server's
  "multiple cascade paths" error. `PlannedWorkout` is reached for deletion only via
  `Athlete → TrainingPlan → PlannedWorkout` (cascade); `Workout` is touched only by the
  SetNull on its own nullable FK. Adding a second constrained `Athlete →` path to either table
  would create a diamond SQL Server rejects.
- Authoritative integrity still holds: a `PlannedWorkout` belongs to a `TrainingPlan` via the
  `TrainingPlanId` cascade FK; `AthleteId` is a query/ownership convenience the service keeps in
  sync (`PlannedWorkout.AthleteId == TrainingPlan.AthleteId`, set on staging).
- It gives 9-4's "This Week" query a single-table, indexed filter
  (`WHERE AthleteId = @id AND ScheduledDate BETWEEN @start AND @end`) with no join.

**Indexes:** `TrainingPlan(AthleteId)`, `TrainingPlan(EventId)`; `PlannedWorkout(AthleteId, ScheduledDate)`
(composite, for the week query) and the auto-indexed `PlannedWorkout(TrainingPlanId)`;
`Workout(AthleteId)` and `Workout(PlannedWorkoutId)`.

**Athlete nav collections (Task 9-2):** add **`ICollection<TrainingPlan> TrainingPlans`** only.
Do **not** add `Athlete.Workouts` — `Workout` is dormant in Phase 9 and nothing enumerates it;
the inverse collection is a no-migration code addition when Phase 11 builds execution capture.

### 2. Strength vs. cardio payload — shared base, payload deferred to Phase 10

**Chosen: a single shared `PlannedWorkout` / `Workout` shape (option a), with the
discipline-specific payload deferred to Phase 10.** This matches ADR-0001 §33 exactly:
"Sport-specific differences … live as discipline-specific payload on a shared base, **not** as
separate entity hierarchies." Sport-typed EF subtypes (option b) are rejected — they contradict
ADR-0001 and impose an inheritance hierarchy on two disciplines that share almost all fields.

**Phase 9 stores the shared base only** — `Sport`, `ScheduledDate`, `Title`, `Description`,
`PlannedDurationMinutes`, `PlannedLoad`. There is **no payload column, owned type, or child
table in Phase 9**, because nothing in Phase 9 reads or writes one: Task 9-3's DTOs carry only
the base fields, and Task 9-6 explicitly forbids the interval/sets editor ("Phase 10 scope
creep"). Building a payload store now would be speculative scaffolding (CLAUDE.md §2/§3).

**Forward mechanism (pinned, built in Phase 10):** when the structured-workout builder lands,
the payload attaches as an **ordered child-row collection** — a `PlannedWorkoutStep`-style
entity, one shared row shape carrying cardio-interval fields (target zone, target power/HR/pace,
duration/distance) and strength-set fields (sets, reps, load) as nullable columns discriminated
by the parent's `Sport`. **Not** a JSON blob, **not** subtypes.

Rationale for child rows over a JSON-string column (despite the `AthleteSportProfile.CustomZonesJson`
precedent): workout steps drive the Phase-11 load math and must be queryable and aggregable in
SQL; `CustomZonesJson` is a small, never-aggregated athlete config blob — a different use case.
Critically, this is **additive**: the Phase-10 child table is a new table with an FK to
`PlannedWorkout`; it does **not** alter the Phase-9 base columns. Deferring it costs Task 9-2
nothing and keeps the Phase-9 schema forward-stable.

**Out of scope here (confirming ADR-0001's deferral):** the strength **load metric** — a
TSS-equivalent formula for resistance work — is **Phase 11**, decided when the metrics engine is
built. This ADR does not solve it. (`PlannedLoad` above is a stored target number, not a formula.)

### 3. `Methodology` lives per-plan, with the athlete field as the default

**Chosen: per-plan, both fields retained and independent.**

- `TrainingPlan.Methodology` (reusing the existing `MethodologyChoice` enum) is authoritative
  for that plan. Methodology can legitimately vary plan-to-plan (a base block vs. a peak block).
- `Athlete.Methodology` stays exactly as it is — the onboarding-set **default** that seeds a new
  plan's methodology selection. After seeding, the two are independent; editing one does not
  touch the other.
- No new enum is introduced. As noted in decision 1, Mesocycle's `WeeklyPatternType` is the same
  axis and is **subsumed** by `Methodology` — we do not carry a duplicate. (The lone
  `WeeklyPatternType` value without a `MethodologyChoice` equivalent, *Custom*, is dropped for
  now; it can be added to `MethodologyChoice` later if a real need appears.)

Reconciliation (resolving ADR-0001 §49): Task 9-3's create flow seeds
`TrainingPlanRequest.Methodology` from the current athlete's `Methodology`; Task 9-6's create form
pre-selects that default in the methodology `<select>` while allowing override.

### 4. `Sport` gains `Strength`

Confirmed. `Sport` becomes `Swim=1, Bike=2, Run=3, Triathlon=4, Strength=5`. The value is added
in **Task 9-2** (do not renumber existing values; the enum is int-backed so the addition does
not alter existing rows). Strength is a first-class v1 discipline (ADR-0001 §30).

**Known gap, not a Phase 9 deliverable:** the onboarding `Sport` select and the existing
`SportThresholdsDto` flow are **not** required to expose `Strength` in Phase 9 (cardio thresholds
only). Surfacing strength in onboarding/threshold config is deferred; flagged here so it is a
conscious gap, not an oversight.

### 5. Phase 9 / 10 / 11 scope boundary

**Phase 9 builds:** the three entities + `Sport.Strength` + migration (9-2); TrainingPlan CRUD —
author a plan, list/read the athlete's plans, add/edit/remove planned workouts (9-3); the
"This Week" planned-workout read endpoint (9-4); the This Week dashboard card (9-5); a minimal
plan-authoring UI (9-6). In one line: **author a plan, schedule planned workouts, and surface
this week's planned workouts.**

**Phase 9 does NOT build:**

- **Executed-`Workout` capture** — write endpoints, the execution-capture metric columns, and an
  `IWorkoutRepository`. `Workout` is a dormant, minimal entity in Phase 9. → **Phase 11.**
- **TSS / load math and the strength load formula.** → **Phase 11.**
- **The structured-workout builder** — interval steps with target zones, strength sets/reps/load
  UI, and the payload child table from decision 2. → **Phase 10.**
- **Zones.** → **Phase 10.**

## Consequences

**Closed by this decision:** the three follow-ups ADR-0001 deferred "to Phase 9 design" (its
items 1 and 3) are resolved; ADR-0001's item 2 (strength load metric) is reconfirmed as Phase 11.
ADR-0001's §49 `Methodology` reconciliation is settled (decision 3).

**Created by this decision:**

- Task 9-2 generates the `AddTrainingPlanDomain` migration (Sr. Dev approval required before
  apply, per CLAUDE.md).
- A purely additive Phase-10 migration will introduce the `PlannedWorkoutStep`-style payload
  child table (decision 2). *Planned follow-up note only — not designed here.*
- A purely additive Phase-11 migration will add the execution-capture metric columns to
  `Workout` (decision 1). *Planned follow-up note only — not designed here.*

**ADR-0001 is not modified.** Per Task 9-1's acceptance criteria, this is a follow-up ADR; the
cross-reference lives here, in 0003.

### For Tasks 9-2 … 9-6

| Task | Depends on (from this ADR) |
|---|---|
| **9-2** Entities + `Sport.Strength` + migration | The three field lists (decision 1) **verbatim**; the relationships/delete-behavior/index/precision table; `Sport.Strength=5` (decision 4); **no** payload child entity (decision 2 carve-out); add `Athlete.TrainingPlans` only, **not** `Athlete.Workouts`; add `ITrainingPlanRepository`, **not** `IWorkoutRepository` (Workout dormant). |
| **9-3** TrainingPlan CRUD | Aggregate boundary — `PlannedWorkout` is edited **through** the `TrainingPlan` aggregate (`AddPlannedWorkout` / `UpdatePlannedWorkout` / `RemovePlannedWorkout` keyed by `planId`); ownership checked on the plan. DTO fields = the base field lists (no payload). `TrainingPlanRequest` carries `Methodology` seeded from `Athlete.Methodology` (decision 3); `EndDate >= StartDate` validation. |
| **9-4** "This Week" read endpoint | `PlannedWorkout.ScheduledDate` + `Sport` field names (decision 1); the denormalized `PlannedWorkout.AthleteId` + the `(AthleteId, ScheduledDate)` index enable a single-table, single-round-trip range query. |
| **9-5** This Week card (Vue) | `PlannedWorkoutResponse` fields to render — `scheduledDate`, `sport`, `title`, optional `plannedDurationMinutes` / `plannedLoad`; `Sport` union includes `Strength`. |
| **9-6** Plan-authoring UI (Vue) | Form captures the base fields only — plan name, `Methodology` (select defaulting to the athlete's), date range, optional target event; planned workouts with sport / scheduled date / title / duration / load. **No** interval or sets/reps builder (decision 2 → Phase 10). |

## Alternatives considered

**Strength as sport-typed subtypes (EF TPH/TPT).** Rejected — directly contradicts ADR-0001 §33
and imposes an inheritance hierarchy on two disciplines that share nearly every field. The shared
base accommodates both with one schema.

**Build the payload store (child table or JSON column) in Phase 9.** Rejected — no Phase 9 task
reads or writes a payload (9-3 DTOs and 9-6 UI are base-only; 9-6 forbids the builder). It would
be speculative scaffolding (CLAUDE.md §2/§3). The mechanism is pinned for forward stability; the
table is built in Phase 10 as an additive migration that leaves the base tables untouched.

**A JSON-string payload column** (mirroring `AthleteSportProfile.CustomZonesJson`). Rejected as
the Phase-10 mechanism — workout steps drive Phase-11 load aggregation and need to be queryable
in SQL; an opaque blob would force deserialize-to-compute. `CustomZonesJson` is a small,
never-aggregated config blob — not a comparable case.

**`Methodology` on `Athlete` only.** Rejected — methodology varies plan-to-plan (base vs. peak
block); a single athlete-level value can't express that. Per-plan with the athlete field as the
seed default keeps both the flexibility and the convenient onboarding default.

**Constrained `Athlete → PlannedWorkout` / `Athlete → Workout` FK relationships.** Rejected for
Phase 9 — they create multiple cascade/set-null paths from `Athlete` that SQL Server rejects.
`AthleteId` is kept as a denormalized indexed column; the cascade authority flows through
`TrainingPlan`. Phase 11 can revisit `Workout`'s athlete relationship when execution capture and
an `IWorkoutRepository` are built.
