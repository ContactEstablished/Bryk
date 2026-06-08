# ADR-0005 — Training-load engine + executed-workout capture (Phase 11)

**Date:** 2026-06-08
**Status:** Accepted (2026-06-08) — HR §1 = option (a); strength §2 = option (c)

## Context

[ADR-0004](0004-structured-workout-and-zones.md) settled the Phase-10 structured-workout payload
(`WorkoutBlock` / `WorkoutStep`) and the training-zone model, and §4 deliberately deferred two
things to **Phase 11**:

1. **Load / TSS math** — the computed training-load number for a structured session, plus a
   **strength-load formula**. (Also deferred by [ADR-0001](0001-mesocycle-vs-trainingplan.md) §2 and
   [ADR-0003](0003-trainingplan-domain-shape.md) §2.) Phase 10 stores *prescribed* targets only and
   computes nothing.
2. **Executed-`Workout` step capture** — actual vs. planned, per step. ADR-0004 §4 kept `Workout`
   dormant; the block/step graph hangs off `PlannedWorkout` only.

This ADR pins both so the Phase-11 implementation tasks (11-1 … 11-5) build from this document alone
— the same role ADR-0004 played for Phase 10. It is a follow-up to ADR-0004, not an edit of it.

The `Workout` entity (`api/Bryk.Domain/Entities/Workout.cs`) has waited for this with six columns —
`Id`, `AthleteId`, `PlannedWorkoutId?`, `Sport`, `CompletedDate`, plus `IAuditable` audit fields.
`PlannedWorkout.PlannedLoad` (`decimal?`) already exists as the manual-override slot for planned load.

The scope keystones below are taken as locked for this ADR (Sr. Dev, 2026-06-08):

- **Scope:** load engine **and** executed-`Workout` capture. The **PMC / CTL-ATL-TSB** ("Form (TSB)")
  chart is **out** of Phase 11 — it needs a rolling daily-load history and lands in a later phase.
  Phase 11 wires the **"Weekly Load"** dashboard card only.
- **Execution depth:** capture **session-level and per-step actuals**, with per-step actual fields
  **nullable** so manual entry stays light; a post-v1 device importer fills them later.
- **Compute model:** **compute-on-read + manual override**, mirroring the Phase-10 zones pattern.

### Conventions this ADR follows

Grounded in `Workout.cs`, `PlannedWorkout.cs`, `WorkoutBlock.cs`, `WorkoutStep.cs`,
`AthleteSportProfile.cs`, `ZoneService.cs`:

- Every entity is `IAuditable` (`CreatedAt`/`UpdatedAt`, set by the interceptor — never manually),
  `Guid Id`, `Guid AthleteId`.
- `decimal` columns get `HasPrecision`; strings get `HasMaxLength`; enums are int-backed, 1-based.
- Denormalized `AthleteId` (indexed, no FK) where it avoids a SQL Server multiple-cascade-path
  diamond, exactly as `PlannedWorkout` / `Workout` / `WorkoutBlock` / `WorkoutStep` already do
  (ADR-0003 *Relationships*, ADR-0004 §4).
- The `AthleteSportProfile` per-sport thresholds are the load basis: bike `ThresholdValue` = FTP
  (watts); run/swim `ThresholdValue` = threshold pace (sec per unit); `Lt1`/`Lt2` per sport;
  `MaxHr`/`RestingHr` live on `Athlete`. **There is no LTHR (threshold-HR) field** — see §1.
- Compute-on-read with a persisted override, surfaced with an `IsOverride`-style flag, is the
  established pattern (`ZoneService` + `ZoneDto.IsOverride`); load reuses it.

## Decision

### 1. Cardio planned-load (TSS) formula — IF² × duration, summed over the structure

Load is **computed at read time** from a `PlannedWorkout`'s blocks/steps by a `LoadCalculator`
(Task 11-1), not persisted. For each cardio `WorkoutStep`:

1. Resolve an **Intensity Factor (IF)** from the step's target relative to the sport threshold
   (`AthleteSportProfile.ThresholdValue` for the workout's `Sport`):

   | Sport metric | IF | Notes |
   |---|---|---|
   | Power (bike) | `targetW / FTP` | Use the midpoint of `TargetPowerLow/High` when a range. |
   | Pace (run/swim) | `thresholdPace / targetPace` | **Inverse** — a faster pace is a *smaller* sec-per-unit value, so the threshold is the numerator. Midpoint of `TargetPaceLow/High` when a range. |

2. Resolve **duration in seconds**: `DurationSeconds` directly, or for a distance-only step,
   `DistanceMeters / targetSpeed` derived from the target pace.
3. The step contributes `sec × IF² / 3600 × 100` TSS. **Recommended** whole-workout formula:

   ```
   TSS = Σ_blocks ( Repeats × Σ_steps ( sec × IF² / 3600 × 100 ) )
   ```

   This is the standard normalized TSS at the per-step granularity the structure already gives us
   (no separate Normalized-Power estimate is needed — each step is treated as steady).

**Zone-only fallback.** When a step carries a `TargetZone` but no raw range, resolve the zone's
**effective band** for the workout's sport via `IZoneService.GetZonesAsync()` (override-aware, current
athlete — the same source the builder uses) and take the **band midpoint** as the target value, then
apply the IF rule above. The top open-ended zone (null `UpperBound`) uses a bounded multiple of its
`LowerBound` (finalized in 11-1; recommend `LowerBound × 1.1`).

**Steps with no resolvable target** (no range, no zone, no threshold set) contribute **0** and the
workout's `computedLoad` degrades gracefully rather than throwing.

#### Sub-decision (open) — heart-rate-target handling

`WorkoutStep` has `TargetHrLow/High`, but the model has **no LTHR / threshold-HR field**: `Athlete`
exposes only `MaxHr`/`RestingHr`, and `AthleteSportProfile` carries `Lt1`/`Lt2` (lactate thresholds),
not an explicit threshold-HR. Two drafted options:

- **(a) — recommended** Treat `AthleteSportProfile.Lt2` as the threshold-HR and compute an
  hrTSS-style `IF = targetHr / Lt2` when `Lt2` is set; if `Lt2` is null, fall back to the zone /
  power / pace target on the step, then to 0. Reuses an existing field, no schema change.
- **(b)** Ignore HR for load entirely in Phase 11 — an HR-only step degrades to its `TargetZone`
  (zone fallback) or 0. Simplest; defers a real hrTSS to a later phase. Listed as an Alternative.

This is the **one genuinely-open sub-decision** in this ADR; the rest is settled pending acceptance.

### 2. Strength planned-load formula

Strength has no zones (ADR-0004 §1); load comes from the step's `Sets`/`Reps`/`LoadKg`/`Rpe`
(all nullable). Drafted options for the `LoadCalculator`'s strength path:

- **(a) Scaled tonnage** — `Σ_steps (Sets × Reps × LoadKg) × k`, with a calibration constant `k`
  chosen so a typical session lands in a TSS-comparable range. Needs `LoadKg`; null → contributes 0.
- **(b) Session-RPE** — `(overall RPE) × (duration in minutes)` (Foster sRPE). Uses the planned
  duration + step `Rpe`; robust when `LoadKg` is absent (bodyweight work).
- **(c) — recommended** Blended: use scaled tonnage when `LoadKg` is present, else fall back to
  per-step `Rpe`-weighted duration, so a session always yields a number whatever fields the athlete
  filled. Calibration constant(s) finalized in 11-1 and unit-tested.

Whichever is ratified, **nullable inputs degrade gracefully** to 0 (never throw).

### 3. Compute-on-read + manual override

`PlannedWorkout.PlannedLoad` (already on the entity) is the **manual override**. The read surface
exposes three fields on `PlannedWorkoutResponse`:

| Field | Type | Meaning |
|---|---|---|
| `ComputedLoad` | `decimal?` | The `LoadCalculator` result from the structure. **Null** on reads that don't load `Blocks` (see below). |
| `EffectiveLoad` | `decimal?` | `PlannedLoad ?? ComputedLoad` — what the UI displays. |
| `IsLoadOverride` | `bool` | `true` when `PlannedLoad` is set (mirrors `ZoneDto.IsOverride`). |

**Read cost / where it's paid.** The calculator needs `Blocks.Steps` in memory. Only the
structure-detail read (`StructuredWorkoutService.GetStructureAsync`, which loads the full graph) and
the weekly aggregation (Task 11-2, via a new structure-including repo read) populate `ComputedLoad`.
The plan-level and bare This-Week mappers that intentionally **don't** load blocks leave
`ComputedLoad = null`, so `EffectiveLoad` falls back to the manual `PlannedLoad` there. This keeps the
existing single-table reads cheap and confines the `.Include(Blocks.Steps)` cost to the surfaces that
display a computed number.

### 4. `Workout` execution shape

The dormant `Workout` gains session-level execution fields. `CompletedDate` and the nullable
`PlannedWorkoutId` already exist — **unplanned workouts are first-class** (logging a session that was
never on the plan is valid; `PlannedWorkoutId` stays null).

| `Workout` (added) | Type | Notes |
|---|---|---|
| `ActualDurationSeconds` | `int?` | Session duration actually performed. |
| `ActualDistanceMeters` | `int?` | Distance actually covered. |
| `AvgHr` | `int?` | Average HR, bpm. |
| `MaxHr` | `int?` | Peak HR, bpm. |
| `ComputedLoad` | `decimal?` | `HasPrecision(7,2)`. Actual load from the calculator on captured actuals (decision 6). Persisted so historical reads are cheap. |
| `LoadOverride` | `decimal?` | `HasPrecision(7,2)`. Manual actual-load override (parallels `PlannedWorkout.PlannedLoad`). `EffectiveLoad = LoadOverride ?? ComputedLoad`. |
| `Rpe` | `decimal?` | `HasPrecision(3,1)`, 0–10. Overall session RPE. |
| `Notes` | `string?` | `HasMaxLength(2000)`. |
| `StepResults` | `ICollection<WorkoutStepResult>` | Owned children, `= new List<…>()`. |

Existing columns (`Id`, `AthleteId`, `PlannedWorkoutId?`, `Sport`, `CompletedDate`, audit) are
unchanged. `AvgPower`/`AvgPace` live at the **step** level (§5), not the session level, since they're
only meaningful per effort; the session keeps HR (which is meaningful as a whole-session average).

### 5. `WorkoutStepResult` child entity — per-step actuals, all nullable

Each `Workout` owns an ordered collection of `WorkoutStepResult` rows capturing what was actually
done per effort. Every actual field is **nullable** so manual entry stays light; a post-v1 device
importer fills them. A row optionally references the planned `WorkoutStep` it realizes for
planned-vs-actual comparison.

| `WorkoutStepResult` | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `AthleteId` | `Guid` | **Denormalized**, indexed, no FK (cascade-diamond avoidance; ADR-0003/0004). |
| `WorkoutId` | `Guid` | FK → `Workout`, **Cascade**. |
| `WorkoutStepId` | `Guid?` | Optional FK → the planned `WorkoutStep` this realizes. **No cascade** (`OnDelete(NoAction/Restrict)`) — deleting a planned step must not delete execution history; nullable so unplanned/ad-hoc rows are allowed. |
| `OrderIndex` | `int` | Position within the workout. |
| `ActualDurationSeconds` | `int?` | |
| `ActualDistanceMeters` | `int?` | |
| `AvgPower` | `int?` | Watts. |
| `AvgHr` | `int?` | bpm. |
| `AvgPace` | `int?` | Sec per unit (run = /km, swim = /100 m). |
| `Rpe` | `decimal?` | `HasPrecision(3,1)`, optional per-step RPE. |
| audit | | `CreatedAt`/`UpdatedAt`. |
| index | | `(WorkoutId, OrderIndex)`; `AthleteId`. |

No new enum is needed — `Sport` lives on the parent `Workout`, and a step result doesn't carry an
`Intent` (it mirrors a planned step, which already has one).

### 6. Actual-load computation

Actual load reuses the **same `LoadCalculator`** (decisions 1–2) applied to captured actuals rather
than prescribed targets: cardio uses `AvgPower` / `AvgPace` / `AvgHr` + `ActualDurationSeconds`
(or `ActualDistanceMeters`) per `WorkoutStepResult`; strength uses the actuals' sets/reps/load/RPE
where captured, else the session `Rpe` × `ActualDurationSeconds`. The result is persisted to
`Workout.ComputedLoad` at log time (so the dashboard's Recent Activity read stays a single-table
query); `Workout.LoadOverride` is the manual escape hatch.

### 7. Phase-11 scope boundary

**Phase 11 builds:** the `LoadCalculator` + planned `EffectiveLoad` on reads (11-1); the weekly-load
total + "Weekly Load" card (11-2); `Workout` execution fields + `WorkoutStepResult` + additive
migration (11-3); executed-workout CRUD + actual-load (11-4); the log-workout UI + planned-vs-actual
+ Recent Activity (11-5).

**Phase 11 does NOT build:**

- **PMC / CTL-ATL-TSB** ("Form (TSB)") — needs a rolling daily-load history; a later phase. The
  "Form (TSB)" dashboard card stays a placeholder.
- **Device / `.fit` import** (Garmin/Wahoo) — post-v1; until then per-step actuals are entered
  manually and stay nullable.
- **Recursive block nesting** — still deferred (ADR-0004 §2).

### Relationships & delete behavior

| Relationship | FK | Delete | Inverse nav added? |
|---|---|---|---|
| `Workout` → `WorkoutStepResult` | `WorkoutStepResult.WorkoutId` | **Cascade** | Yes — `Workout.StepResults`. |
| `WorkoutStep` → `WorkoutStepResult` | `WorkoutStepResult.WorkoutStepId` (nullable) | **NoAction / Restrict** | No — one-directional reference for comparison only. |
| `PlannedWorkout` → `Workout` | `Workout.PlannedWorkoutId` (nullable, pre-existing) | unchanged | No (not added in Phase 11). |
| `Athlete` → `WorkoutStepResult` | denormalized `AthleteId`, **no FK** | — | No. |

The `WorkoutStepId` `NoAction` choice is deliberate: a `Workout` and its `WorkoutStepResult`s are
the historical record; editing or deleting a *plan* later must never cascade into completed history.
`Athlete` deletion reaches step results via `Athlete → … → Workout → WorkoutStepResult` (single
cascade chain — SQL Server allows it); the denormalized `AthleteId` is the indexed convenience column.

## Consequences

**Closed by this decision:** ADR-0004 §4's deferred load/TSS math (cardio + strength formulas, the
compute/override surface) and executed-`Workout` step capture; ADR-0001 §2 / ADR-0003 §2's
"strength-load formula → later."

**Created by this decision:**

- A `LoadCalculator` / `ILoadService` (11-1) — pure compute, no persistence; depends on
  `AthleteSportProfile` thresholds and `IZoneService.GetZonesAsync()` (reused, not extended).
- Three read fields on `PlannedWorkoutResponse` (`ComputedLoad`, `EffectiveLoad`, `IsLoadOverride`).
- A weekly-load total on `ThisWeekResponse` + a structure-including repo read for the week (11-2).
- Execution columns on `Workouts` + a new `WorkoutStepResult` table — one **additive** migration
  (`AddWorkoutExecution`, 11-3; Sr. Dev approval before apply, per CLAUDE.md). Does not alter any
  Phase-9/10 column.
- `IWorkoutService` + DTOs/validators + `WorkoutsController` (11-4).
- **No new enum** — `Sport` and `StepIntent` are reused.

**Ratified on acceptance (2026-06-08):** §1 HR-target = **option (a)** — `IF = targetHr / Lt2`
(`AthleteSportProfile.Lt2` as threshold-HR), degrading to the step's zone/power/pace target then 0
when `Lt2` is null; §2 strength = **option (c)** — scaled tonnage `Σ(Sets×Reps×LoadKg)×k` when `LoadKg`
is present, else per-step `Rpe`-weighted duration. The calibration constant `k` is set and unit-tested
in Task 11-1 and documented as tunable. Nullable inputs / missing thresholds always degrade to 0.

### For Tasks 11-1 … 11-5

| Task | Surface | Depends on | Pins from this ADR |
|---|---|---|---|
| **11-1** `LoadCalculator`/`ILoadService` + `ComputedLoad`/`EffectiveLoad`/`IsLoadOverride` on reads | Backend | ADR-0005 | Decisions 1–3 (formulas, zone fallback, override model). No persistence change. |
| **11-2** Weekly-load total + "Weekly Load" card | Backend + Frontend | 11-1 | Decision 3 (effective load) + the structure-including weekly read. |
| **11-3** `Workout` exec fields + `WorkoutStepResult` + repo + migration | Backend | ADR-0005 | Decisions 4–5 (field lists, relationships, delete). **Additive migration → approval.** |
| **11-4** Executed-workout CRUD + actual-load | Backend | 11-3, 11-1 | Decisions 4–6 (aggregate capture, reuse calculator on actuals). |
| **11-5** Log-workout UI + planned-vs-actual + Recent Activity | Frontend | 11-4 | Decisions 4–6 (session + per-step actuals, comparison view). |

## Alternatives considered

- **Persist planned load instead of computing on read.** Rejected — it would drift the moment a step
  is edited, exactly the staleness the Phase-10 zones design avoided by computing from thresholds.
  Computing on read with `PlannedLoad` as an override keeps one source of truth and a manual escape
  hatch, mirroring `ZoneService`.
- **A single `Activity`/`Workout` table with planned + actual columns interleaved.** Rejected — keeps
  the clean `PlannedWorkout` (prescription) vs. `Workout` (execution) split ADR-0003 established;
  `WorkoutStepResult` referencing `WorkoutStep` gives planned-vs-actual without merging the tables.
- **HR-only TSS via a new LTHR field on `Athlete`.** Deferred — adding a threshold-HR column is a
  data-model change for a secondary metric; §1 reuses `Lt2` (option a) or degrades (option b) without
  a migration. A dedicated LTHR can land with the device-import phase if needed.
- **Cascade delete on `WorkoutStepResult.WorkoutStepId`.** Rejected — would let a plan edit destroy
  completed-workout history; `NoAction` keeps execution records immutable against plan changes.
- **Recompute actual load on every read instead of persisting it.** Rejected for actuals (unlike
  planned) — completed workouts are immutable history and feed the (future) PMC; persisting
  `Workout.ComputedLoad` keeps the Recent-Activity and future daily-load reads single-table.
