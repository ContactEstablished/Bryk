# ADR-0004 — Structured-workout payload + training-zone model (Phase 10)

**Date:** 2026-06-06
**Status:** Proposed (awaiting Sr. Dev acceptance — this is the Phase-10 shape-pinning ADR)

## Context

[ADR-0003](0003-trainingplan-domain-shape.md) settled the Phase-9 `TrainingPlan` /
`PlannedWorkout` / `Workout` shapes and deliberately deferred two things to **Phase 10**:

1. The **structured-workout payload** — the ordered interval / strength-set detail that hangs off
   a `PlannedWorkout`. ADR-0003 §2 pinned the *mechanism* (an ordered child-row collection, one
   shared row shape, nullable columns discriminated by `Sport`, **not** JSON, **not** subtypes)
   and explicitly left the field list, the repeat structure, and the target representation open.
2. **Zones** (ADR-0003 §5) — the training-zone model the builder's intensity targets reference.

This ADR pins both so the Phase-10 implementation tasks (10-1 … 10-5) build from this document
alone, with zero remaining shape questions — the same role ADR-0003 played for Phase 9. It is a
follow-up to ADR-0003, not an edit of it.

The four keystone decisions below were taken by the Sr. Dev directly (2026-06-06):

- **Zones:** sport-tailored — 7-zone power (bike), 5-zone HR/pace (run & swim).
- **Intervals:** parent blocks owning ordered child steps, with repeat counts.
- **Step targets:** a zone reference **and** optional raw power/HR/pace ranges.
- **Scope:** the full zones feature (auto-calc **and** a config/override UI) ships in Phase 10.

### Conventions this ADR follows

Grounded in `Athlete.cs`, `AthleteSportProfile.cs`, `TrainingPlan.cs`, `PlannedWorkout.cs`:

- Every entity is `IAuditable` (`CreatedAt`/`UpdatedAt`, set by the interceptor — never manually),
  `Guid Id`, `Guid AthleteId`.
- `decimal` columns get `HasPrecision`; strings get `HasMaxLength`; enums are int-backed, 1-based.
- Denormalized `AthleteId` (indexed, no FK) where it avoids a SQL Server multiple-cascade-path
  diamond, exactly as `PlannedWorkout`/`Workout` already do (ADR-0003 *Relationships*).
- The `AthleteSportProfile` per-sport thresholds are the zone source: bike `ThresholdValue` = FTP
  (watts); run/swim `ThresholdValue` = threshold pace; `Lt1`/`Lt2` per sport; `MaxHr`/`RestingHr`
  live on `Athlete`.

## Decision

### 1. Training-zone model — sport-tailored, auto-computed, overridable

Zones are **computed from each sport's thresholds** by a `ZoneService`, with per-athlete-per-sport
**overrides** persisted. **Strength has no zones** (it uses the step's sets/reps/load fields).

| Sport | Primary metric | Scheme | Basis |
|---|---|---|---|
| **Bike** | Power (watts) | **7-zone (Coggan)** | % of FTP (`ThresholdValue`) |
| **Run** | Pace (primary), HR (secondary) | **5-zone** | % of threshold pace (`ThresholdValue`); HR from LTHR/`MaxHr` |
| **Swim** | Pace | **5-zone** | % of threshold pace per 100 m (`ThresholdValue`) |
| **Strength** | — | none | — |

**Bike 7-zone (% FTP), Coggan boundaries** (the auto-calc default; exact edges finalized in 10-1):
Z1 ≤55, Z2 56–75, Z3 76–90, Z4 91–105, Z5 106–120, Z6 121–150, Z7 >150.

**Run/Swim 5-zone (% of threshold):** Z1 recovery, Z2 endurance, Z3 tempo, Z4 threshold, Z5 VO₂.
Pace math is **inverse** (a faster pace is a *lower* seconds-per-unit value) — the auto-calc encodes
this; it is the single most error-prone bit and gets a unit test in 10-1.

**Storage (persistence-boundary change — Sr. Dev approval required at 10-1):** a new
**`AthleteSportZone`** table. The `ZoneService` computes defaults from thresholds at read time; rows
in `AthleteSportZone` are **overrides only** (absence = use computed default), so the table stays
empty for athletes who never customize.

| `AthleteSportZone` | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `AthleteId` | `Guid` | **Denormalized**, indexed, no FK (avoids the cascade diamond; mirrors ADR-0003). |
| `Sport` | `Sport` | Bike/Run/Swim (not Strength). |
| `ZoneNumber` | `int` | 1–7 (bike) / 1–5 (run/swim). |
| `Metric` | `ZoneMetric` | Power / Hr / Pace (new enum). |
| `LowerBound` | `decimal` | `HasPrecision(7,2)` — watts / bpm / sec-per-unit. |
| `UpperBound` | `decimal?` | Null = open-ended top zone. `HasPrecision(7,2)`. |
| audit | | `CreatedAt`/`UpdatedAt`. |
| index | | unique `(AthleteId, Sport, ZoneNumber, Metric)`. |

This **supersedes** `AthleteSportProfile.CustomZonesJson` for zone overrides (that column was the
Phase-4 placeholder; ADR-0003 already flagged it as a small blob). 10-1 leaves the column in place
(no destructive migration) but stops writing it; a later cleanup task can drop it.

### 2. Structured-workout payload — repeatable blocks owning ordered steps

A `PlannedWorkout` (Phase 9) gains an ordered collection of **`WorkoutBlock`**s; each block owns an
ordered collection of **`WorkoutStep`**s and a repeat count. This is the ADR-0003 §2 child-row
mechanism, realized as **two levels** (workout → repeatable block → steps).

**Recursive block-in-block nesting is OUT of Phase-10 scope** — a block's `Repeats` *is* the nest
("4× [work / recovery]" = one block, `Repeats = 4`, two steps), which covers the overwhelming
majority of real sessions. A self-referential block tree is deferred unless a concrete need appears.
*(Flagged for confirmation — this is the one place the "true nesting" choice is bounded.)*

#### `WorkoutBlock` (an ordered, optionally-repeated group)

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `AthleteId` | `Guid` | Denormalized, indexed, no FK (ADR-0003 pattern). |
| `PlannedWorkoutId` | `Guid` | FK → `PlannedWorkout`, **Cascade**. |
| `OrderIndex` | `int` | Position within the workout. |
| `Repeats` | `int` | ≥1; 1 = run once. Expresses interval sets. |
| `Steps` | `ICollection<WorkoutStep>` | Owned children, `= new List<…>()`. |
| `PlannedWorkout` | `PlannedWorkout` | `= null!`. |
| audit | | `CreatedAt`/`UpdatedAt`. |
| index | | `(PlannedWorkoutId, OrderIndex)`; `AthleteId`. |

#### `WorkoutStep` (one prescribed effort — decision 3 for the target columns)

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK. |
| `AthleteId` | `Guid` | Denormalized, indexed, no FK. |
| `WorkoutBlockId` | `Guid` | FK → `WorkoutBlock`, **Cascade**. |
| `OrderIndex` | `int` | Position within the block. |
| `Intent` | `StepIntent` | Warmup / Work / Recovery / Cooldown / Rest (new enum). |
| `Title` | `string?` | `HasMaxLength(200)`, optional label. |
| **duration / distance** | | exactly one drives the step (validator-enforced) |
| `DurationSeconds` | `int?` | Time-based step. |
| `DistanceMeters` | `int?` | Distance-based step. |
| **targets (decision 3)** | | all nullable |
| `TargetZone` | `int?` | Zone number, interpreted against this workout's `Sport`. |
| `TargetPowerLow/High` | `int?` | Watts (bike). |
| `TargetHrLow/High` | `int?` | bpm. |
| `TargetPaceLow/High` | `int?` | Sec per unit (run = /km, swim = /100 m). |
| **strength** | | nullable; used when `Sport == Strength` |
| `Sets` | `int?` | |
| `Reps` | `int?` | |
| `LoadKg` | `decimal?` | `HasPrecision(6,2)` — prescribed load. |
| `Rpe` | `decimal?` | `HasPrecision(3,1)`, 0–10 optional. |
| audit | | `CreatedAt`/`UpdatedAt`. |
| index | | `(WorkoutBlockId, OrderIndex)`; `AthleteId`. |

**The step is the single shared shape (ADR-0003 §2, option a) — no subtypes.** Cardio steps use the
duration/distance + zone/power/HR/pace columns; strength steps use sets/reps/load/Rpe. Which columns
are meaningful is discriminated by the parent `PlannedWorkout.Sport`, enforced by the 10-2 validator,
not by the schema.

### 3. Step intensity — zone reference *and* optional raw ranges

Each `WorkoutStep` may carry a `TargetZone` (int, resolved against the athlete's zones for that
sport) **and/or** explicit raw target ranges (`TargetPowerLow/High`, `TargetHrLow/High`,
`TargetPaceLow/High`). A single value sets `Low` only (or `Low == High`); a range sets both. This
matches how coaches prescribe — "Z3" or "250–270 W" or both. The builder UI offers a zone picker
that *pre-fills* the raw range from the athlete's computed zones, which the user can then fine-tune.

### 4. Phase-10 scope boundary

**Phase 10 builds:** the `AthleteSportZone` model + `ZoneService` auto-calc + zones config/override
UI (10-1, 10-2-zones-UI); the `WorkoutBlock`/`WorkoutStep` entities + additive migration (10-3); the
structured-workout CRUD service/DTOs/validators/controller editing blocks+steps **through the
`PlannedWorkout` aggregate** (10-4); the structured-workout builder UI — interval grid for cardio,
sets/reps table for strength, zone picker, repeat blocks (10-5).

**Phase 10 does NOT build:**

- **Load / TSS math** — the computed training-load number for a structured session (and the
  strength-load formula). Confirmed **Phase 11** (ADR-0001 §2, ADR-0003 §2). Phase 10 stores
  *prescribed* targets only; it computes nothing.
- **Executed-`Workout` step capture** — actual vs. planned per step. **Phase 11**; `Workout` stays
  dormant. The block/step graph hangs off `PlannedWorkout` only.
- **Device export** (Garmin/Wahoo `.fit`/structured-workout push) — `candidate`, post-v1.
- **Recursive block nesting** — deferred (decision 2).

#### Relationships & delete behavior

| Relationship | FK | Delete | Inverse nav added? |
|---|---|---|---|
| `PlannedWorkout` → `WorkoutBlock` | `WorkoutBlock.PlannedWorkoutId` | **Cascade** | Yes — `PlannedWorkout.Blocks`. |
| `WorkoutBlock` → `WorkoutStep` | `WorkoutStep.WorkoutBlockId` | **Cascade** | Yes — `WorkoutBlock.Steps`. |
| `Athlete` → `AthleteSportZone` | denormalized `AthleteId`, **no FK** | — | No. |

`Athlete` deletion reaches blocks/steps only via `Athlete → TrainingPlan → PlannedWorkout → Block →
Step` (single cascade chain — SQL Server allows it). `AthleteId` on block/step/zone is the
denormalized indexed convenience column (ADR-0003), kept in sync by the service.

## Consequences

**Closed by this decision:** ADR-0003 §2's deferred field list, repeat structure, and target
representation; ADR-0003 §5's "Zones → Phase 10" with a concrete sport-tailored model.

**Created by this decision:**

- A new `Sport.Strength`-aware `ZoneService` and an `AthleteSportZone` table (10-1 migration —
  **additive**; Sr. Dev approval before apply, per CLAUDE.md).
- `WorkoutBlock` / `WorkoutStep` tables (10-3 migration — **additive**, FK to the existing
  `PlannedWorkouts`; does not alter Phase-9 base columns, exactly as ADR-0003 §2 promised).
- Two new enums: `ZoneMetric` (Power/Hr/Pace), `StepIntent` (Warmup/Work/Recovery/Cooldown/Rest).
- `CustomZonesJson` becomes vestigial (superseded by `AthleteSportZone`); dropped by a later cleanup.

**Sub-decisions flagged for review (architect calls, confirm or override at ADR acceptance):**

1. **Two-level nesting** (block→step, no recursive blocks). — decision 2.
2. **Run/swim zones pace-primary, HR secondary.** — decision 1.
3. **`AthleteSportZone` override table** rather than reusing `CustomZonesJson`. — decision 1
   (persistence-boundary; Sr. Dev approval gate at 10-1).
4. **`StepIntent` enum** included for builder UX (small, additive).

### For Tasks 10-1 … 10-5

| Task | Surface | Depends on | Pins from this ADR |
|---|---|---|---|
| **10-1** Zones: `ZoneService` auto-calc + `AthleteSportZone` + migration + read API | Backend | ADR-0004 | Decision 1 (schemes, basis, override table, inverse pace math). **Additive migration → approval.** |
| **10-2** Zones config UI (view computed zones, edit/reset overrides) | Frontend | 10-1 | Decision 1 + 4 (full config UI). |
| **10-3** `WorkoutBlock`/`WorkoutStep` entities + repo + DbContext + additive migration | Backend | ADR-0004 | Decision 2 (field lists, relationships, indexes). **Additive migration → approval.** |
| **10-4** Structured-workout CRUD (blocks+steps through the `PlannedWorkout` aggregate) | Backend | 10-3, 10-1 | Decisions 2 + 3 (DTOs carry blocks/steps + targets; sport-discriminated validation; zone refs validated against 10-1's zones). |
| **10-5** Structured-workout builder UI (interval grid / sets table / zone picker / repeats) | Frontend | 10-4, 10-2 | Decisions 2 + 3 (two-level blocks, repeat counts, zone-prefilled raw ranges). |

## Alternatives considered

- **Unified 5-zone (all sports), or 3-zone polarized.** Rejected in favour of sport-tailored
  (decision 1): a single scheme can't express Coggan power zones for the bike while staying sane for
  run/swim pace. Cost is a per-sport auto-calc; benefit is TrainingPeaks-grade fidelity.
- **Flat steps with a repeat count (no blocks).** Rejected (decision 2) — a flat list can't cleanly
  express "4× [3min/2min]" without a grouping concept; blocks are that concept. Recursive nesting was
  also rejected as over-build for v1.
- **Zone-only or raw-only targets.** Rejected (decision 3) — zone-only can't prescribe an exact
  wattage; raw-only loses the "ride Z2" shorthand. Storing both, with the zone pre-filling the range,
  gets both.
- **Reuse `AthleteSportProfile.CustomZonesJson` for overrides.** Rejected — zones are now edited and
  read per-zone by the builder; a structured, queryable table beats parsing a blob (the same
  reasoning ADR-0003 §2 used to choose child rows over JSON for workout steps).
- **JSON payload / EF subtypes for steps.** Rejected — directly contradicts ADR-0003 §2; the shared
  child-row shape stands.
