# Task 11-1 — Load engine: planned-load calculator + effective load on reads

## Goal
The training-load compute engine from ADR-0005 §1–3: a `LoadCalculator` / `ILoadService` that computes
a `PlannedWorkout`'s TSS from its blocks/steps + the athlete's sport thresholds + zone bands, and the
`ComputedLoad` / `EffectiveLoad` / `IsLoadOverride` read fields on `PlannedWorkoutResponse`. Backend
only (Application + API mapping). **No persistence change, no migration, no entity change.**

## Depends on
- **ADR-0005 §1** — cardio TSS formula (IF² × duration, inverse pace, zone-only fallback, the HR
  sub-decision once ratified).
- **ADR-0005 §2** — strength-load formula (once ratified).
- **ADR-0005 §3** — compute-on-read + `PlannedLoad` override; where `ComputedLoad` is populated.

## Required reading
- `md/decisions/0005-training-load-and-execution.md` §1, §2, §3.
- `api/Bryk.Application/Zones/ZoneService.cs` — **the reference** for compute-on-read: `ComputeBands`,
  the inverse-pace band math, `PrimaryMetric`, reuse of `ICurrentUserService` + `AthleteSportProfile`
  thresholds. The load engine resolves zone-fallback bands via `IZoneService.GetZonesAsync()`.
- `api/Bryk.Application/Zones/ZoneDto.cs` — `IsOverride` semantics to mirror with `IsLoadOverride`.
- `api/Bryk.Application/Training/StructuredWorkoutService.cs` — the structure read + `Map` where the
  new fields get populated (this read loads `Blocks.Steps`); the ownership pattern.
- `api/Bryk.Domain/Entities/{PlannedWorkout,WorkoutBlock,WorkoutStep,AthleteSportProfile}.cs` — the
  inputs (step targets, `Repeats`, `ThresholdValue`/`Lt1`/`Lt2`); `Athlete.{MaxHr,RestingHr}`.
- `api/Bryk.Application.Tests/Zones/ZoneServiceTests.cs` — the unit-test conventions to mirror.

## Acceptance criteria
- **`ILoadService` + `LoadCalculator`** (`api/Bryk.Application/Training/` or a new `…/Load/` folder):
  a pure compute service, primary-constructor style, that given a `PlannedWorkout` with `Blocks.Steps`
  loaded + the athlete's sport profile + effective zones returns a `decimal?` computed TSS.
  - Cardio per ADR-0005 §1: `IF = targetW/FTP` (power) or `thresholdPace/targetPace` (pace, **inverse**);
    `TSS = Σ_blocks Repeats × Σ_steps (sec × IF² / 3600 × 100)`; distance-only steps convert via target
    pace; **zone-only fallback** resolves the band midpoint via `IZoneService.GetZonesAsync()`; the
    open-ended top zone uses the ratified bounded multiple.
  - Strength per ADR-0005 §2 (ratified option). Nullable inputs / missing thresholds degrade to **0**,
    never throw.
- **Read fields**: add `ComputedLoad` (`decimal?`), `EffectiveLoad` (`decimal?`), `IsLoadOverride`
  (`bool`) to `PlannedWorkoutResponse`. Populate them in `StructuredWorkoutService.Map` (where `Blocks`
  are loaded): `ComputedLoad` = calculator result, `IsLoadOverride` = `PlannedLoad is not null`,
  `EffectiveLoad = PlannedLoad ?? ComputedLoad`. The mappers that don't load `Blocks`
  (`TrainingPlanService`, `ThisWeekService`) leave `ComputedLoad` null and set `EffectiveLoad =
  PlannedLoad` — **do not** make them load the structure here (11-2 owns the weekly read).
- **DI**: register `ILoadService` → `LoadCalculator`.
- **Unit tests** (heavy — the math is the crux; mirror `ZoneServiceTests`): power IF/TSS on a known
  step; **inverse-pace** IF/TSS; distance→time conversion; multi-step block × `Repeats`; **zone-only
  fallback** midpoint; missing threshold / no target → 0; override beats computed in `EffectiveLoad`;
  strength formula on a known set.
- **Build green; existing tests green.**

## What NOT to modify
- Do not add or alter any entity, DbContext config, or migration — Phase 11-3 owns persistence.
- Do not load `Blocks` in the plan-level or This-Week reads — that's 11-2's structure-including repo read.
- Do not add a method to `IZoneService` — reuse `GetZonesAsync()` for fallback bands.
- Do not build the dashboard card or weekly total — Task 11-2.
- Do not touch executed-`Workout` capture — Tasks 11-3/11-4.

## Suggested commit
```
feat: add training-load calculator and effective load on planned reads

Compute a planned workout's TSS from its blocks/steps, sport thresholds,
and effective zones (IF² × duration; inverse pace; zone-midpoint fallback;
strength formula per ADR-0005). Surface computedLoad / effectiveLoad /
isLoadOverride on the structure read, with PlannedLoad as the manual
override. Compute-on-read, no persistence change.
```
