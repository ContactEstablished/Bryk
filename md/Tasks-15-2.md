# Task 15-2 — Time-in-zone calculator + endpoint

## Surface
Backend only. A pure `TimeInZoneCalculator` in `Bryk.Application/Analytics/`; its DTOs; a third
`AnalyticsService` method (`GetTimeInZoneAsync`); one additive `AnalyticsController` action
(`time-in-zone`); unit + integration tests. **No migration, no new package.** Reuses the existing
`IValidator<AnalyticsRangeRequest>` for the range and the `GetPlannedWorkoutsByIdsWithStructureAsync`
read added in 15-1.

## Why
The Progress page's time-in-zone section (15-5). Per the ROADMAP math conventions it **stays coarse and
honestly "estimated"** until Phase 19 file import — derived from planned structure for linked workouts,
else session AvgHr, else unclassified. The per-method seconds must sum to the total.

## Depends on
- **ADR-0007** §4 (the 5-level intensity model, the 3 methods, %HRmax, the sums-to-total invariant).
- **ADR-0004** §1–3 (zone model, `WorkoutStep` targets) and **ADR-0006** §7 (range rules).
- **Task 15-1** — `GetPlannedWorkoutsByIdsWithStructureAsync` (added there); the extended
  `AnalyticsService` ctor deps (`ITrainingPlanRepository`, `IAthleteRepository`, `IZoneService`).

## Required reading
- `api/Bryk.Application/Training/Load/LoadCalculator.cs` — `ZoneBandMidpoint`, `TargetPower`/`TargetPace`,
  `Midpoint`, the open-top-zone handling; **the value↔zone band logic to invert** (value → zone number).
- `api/Bryk.Application/Zones/SportZonesResponse.cs` + `ZoneDto.cs` + `ZonesResponse.cs` — `Metric`
  (Power for bike, Pace for run/swim; **no HR bands**), `LowerBound`/`UpperBound` (null = open).
- `api/Bryk.Domain/Entities/WorkoutStep.cs`, `WorkoutBlock.cs`, `PlannedWorkout.cs`, `Workout.cs`,
  `Athlete.cs` (`MaxHr`) — step targets/duration, block `Repeats`, session `AvgHr`/`ActualDurationSeconds`,
  the linking `Workout.PlannedWorkoutId`.
- `api/Bryk.Application/Training/ThisWeekService.cs` — `GetWithSportProfilesAsync`/`GetZonesAsync` usage.
- `api/Bryk.Application.Tests/Analytics/` — the pure-calculator test style.

## Acceptance criteria

### DTOs (`Bryk.Application/Analytics/`)
- `ZoneTimeDto { int ZoneNumber; int Seconds; }` (zoneNumber 1..5).
- `ZoneTimeMethodBreakdownDto { int StructureSeconds; int SessionAvgSeconds; int UnclassifiedSeconds; }`.
- `TimeInZoneResponse { IReadOnlyList<ZoneTimeDto> Zones; ZoneTimeMethodBreakdownDto MethodBreakdown; int TotalSeconds; }`.

### `TimeInZoneCalculator` (pure, static — ADR-0007 §4)
- `Compute(IReadOnlyList<Workout> workouts, IReadOnlyDictionary<Guid, PlannedWorkout> structures, ZonesResponse zones, int? maxHr)` → `TimeInZoneResponse`.
- A **5-bucket** intensity histogram (`zoneNumber` 1..5) + the method breakdown. Per workout, in order:
  1. **structure** — `workout.PlannedWorkoutId` resolves in `structures` to a planned workout with ≥ 1
     step: for each block, for each step, `seconds = step.DurationSeconds ?? 0` (× `max(Repeats,1)`).
     Classify the step → zone:
     - `step.TargetZone` when set; else resolve the step's raw target against `zones` for the workout's
       sport (bike: `TargetPower` midpoint vs the Power bands; run/swim: `TargetPace` midpoint vs the Pace
       bands) to the band whose `[LowerBound, UpperBound)` contains it (`UpperBound` null = open top/bottom);
       else **unclassified**. Collapse the zone via `min(zone, 5)` (bike Z6/Z7 → 5).
     - Classified → add to `Zones[zone]` + `StructureSeconds`; unclassified (incl. HR-only or zero-duration
       steps) → `UnclassifiedSeconds`.
  2. **sessionAvg** — not linked-with-structure, and `workout.AvgHr` and `maxHr` both present: the whole
     `ActualDurationSeconds ?? 0` → `HrZone(AvgHr, maxHr)` bucket + `SessionAvgSeconds`. `%HRmax` bands
     (`AvgHr / maxHr`): `< 0.60`→1, `< 0.70`→2, `< 0.80`→3, `< 0.90`→4, `≥ 0.90`→5.
  3. **unclassified** — anything left with duration (no structure + no `AvgHr`/`maxHr`, incl. strength):
     `ActualDurationSeconds ?? 0` → `UnclassifiedSeconds`.
- `TotalSeconds = StructureSeconds + SessionAvgSeconds + UnclassifiedSeconds`. `Zones` contains buckets
  1..5 (include zero-second buckets so the UI has a fixed axis, or omit empties — **include 1..5**).
- **Invariant (pinned):** the three method seconds sum to `TotalSeconds`, and `Σ Zones[z].Seconds =
  TotalSeconds − UnclassifiedSeconds`.

### `AnalyticsService.GetTimeInZoneAsync(DateOnly? from, DateOnly? to, Sport? sport, CancellationToken ct)`
1. Build `AnalyticsRangeRequest { From = from, To = to }`; `ValidateOrThrowAsync` with the **existing**
   `IValidator<AnalyticsRangeRequest>` (both required, `from ≤ to`, ≤ 400 days, no future `to`). Resolve
   `athleteId`.
2. `workouts = GetByAthleteInRangeAsync(athleteId, from, to)` (filter to `sport` in-memory when given, or
   add the filter to the read — keep it simple: filter in the service).
3. `linkedIds = workouts.Where(w => w.PlannedWorkoutId is not null).Select(...).Distinct()`;
   `structures = GetPlannedWorkoutsByIdsWithStructureAsync(linkedIds)` → `ToDictionary(p => p.Id)`.
4. `zones = GetZonesAsync(ct)`; `maxHr = (GetWithSportProfilesAsync(athleteId)).MaxHr`.
5. `TimeInZoneCalculator.Compute(workouts, structures, zones, maxHr)`.

Add `GetTimeInZoneAsync` to `IAnalyticsService`.

### Controller (additive action on `AnalyticsController`)
- `GET time-in-zone` → `[HttpGet("time-in-zone")]`, `[FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
  [FromQuery] Sport? sport`, `Ok(TimeInZoneResponse)`. XML `<summary>` noting it is an **estimate** (coarse,
  structure/AvgHr/unclassified) until file import. No try/catch.

### Tests
- **Unit** (`Bryk.Application.Tests/Analytics/TimeInZoneCalculatorTests`):
  - A linked workout whose planned steps carry `TargetZone` → those seconds land in the right buckets under
    `StructureSeconds`; bike Z6/Z7 collapse to bucket 5.
  - An unlinked workout with `AvgHr`+`maxHr` → whole duration in the right `%HRmax` bucket under
    `SessionAvgSeconds`; the band boundaries (e.g. exactly 0.80) pin to the documented zone.
  - A workout with no structure and no `AvgHr` (and a strength workout) → all duration `UnclassifiedSeconds`.
  - **The invariant**: for a mixed set, `Structure+SessionAvg+Unclassified == Total` and
    `Σ Zones == Total − Unclassified`.
- **Integration** (`AnalyticsControllerTests`, extend): missing/future/over-400-day range → 400; a seeded
  linked workout yields non-zero structure seconds in the histogram; `sport` filter restricts the set.
- `dotnet build api/Bryk.sln` + `dotnet test api/Bryk.sln` green.

## What NOT to modify
- No migration, no new package, no HR-zone persistence — the `%HRmax` scheme is computed inline.
- Don't fabricate sample-derived zone time — coarse + "estimated" only (Phase 19 supplies real samples).
- Don't break 15-1's calculators/endpoints or the 14-2 surface; only *add*.
- Don't scale a linked workout's planned durations to its actual duration — planned durations as-is (the
  honest estimate); keep the sums-to-total invariant trivial.
- Always `ICurrentUserService` for the athlete.

## Suggested commit
```
feat: time-in-zone analytics (coarse, estimated) + endpoint

Pure TimeInZoneCalculator: a 5-level intensity histogram in seconds with a
structure/sessionAvg/unclassified method breakdown (ADR-0007 §4) — planned
structure (TargetZone, bike Z6/Z7→5) for linked workouts, %HRmax session
AvgHr classification otherwise, unclassified for the rest. Per-method
seconds sum to the total (pinned). GET /analytics/time-in-zone reusing the
analytics range validator; no migration. Stays coarse until Phase 19.
```
