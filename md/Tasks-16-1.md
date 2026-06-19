# Task 16-1 — Compliance classifier, calendar feed, GET endpoint

## Surface
Backend only. A pure `ComplianceClassifier` in `Bryk.Application/Calendar/` mirroring
`PmcCalculator`/`WeeklyLoadCalculator`; the `CalendarService` (merged day-keyed feed of planned +
completed + events); `CalendarFeedRequest` + validator; DTOs; one additive `CalendarController`
action (`GET /calendar`); two additive repo reads; integration + unit tests. **No migration, no new
package.**

## Why
The calendar grid (16-3) renders from this feed, and the compliance coloring is the cross-phase
contract Phase 18's `ThisWeekCard` reuses. Computing it server-side (one home for the rule) keeps the
frontend a dumb renderer and makes the bands testable in isolation.

## Depends on
- **ADR-0008** §1 (5 buckets + single null-load fallback rule), §3 (sidebar IA — not used here, but
  the feed shape must support the chip rendering 16-3 needs).
- **ADR-0005** (`EffectiveLoad = LoadOverride ?? ComputedLoad`; `LoadCalculator.ComputePlannedLoad`).
- **Task 9-3** — `TrainingPlanService`/`ITrainingPlanRepository` ownership + no-tracking `Include`
  pattern; `ThisWeekService` for the Monday-week + planned-load computation template.

## Required reading
- `api/Bryk.Application/Analytics/AnalyticsService.cs` + `AnalyticsController.cs` — the
  primary-ctor DI / `ValidateOrThrowAsync` / thin-controller pattern to mirror.
- `api/Bryk.Application/Training/ThisWeekService.cs` — **the planned-load + Monday-week template**:
  `GetPlannedWorkoutsInRangeWithStructureAsync` + `GetWithSportProfilesAsync` + `GetZonesAsync` +
  `LoadCalculator.ComputePlannedLoad`; `EffectiveLoad = PlannedLoad ?? computed`.
- `api/Bryk.Application/Analytics/WeeklyLoadCalculator.cs` / `PeaksCalculator.cs` — the pure-calculator
  style (static, `today` passed in, no `DateTime.UtcNow`).
- `api/Bryk.Application/Training/Load/LoadCalculator.cs` — `ComputePlannedLoad(pw, profile, sportZones)`.
- `api/Bryk.Domain/Interfaces/IWorkoutRepository.cs`, `ITrainingPlanRepository.cs`,
  `IEventRepository.cs` — the existing reads; add the two new ones alongside, mirror `AsNoTracking`/
  `AsSplitQuery`/`Include` style.
- `api/Bryk.Domain/Entities/PlannedWorkout.cs`, `Workout.cs`, `Event.cs` — the field shapes.
- `api/Bryk.Application/Analytics/Validators/AnalyticsRangeRequestValidator.cs` — the validator style
  (the calendar range rules are identical: both bounds required, `from ≤ to`, ≤ 62 days, no future `to`).
- `api/Bryk.API.Tests/Analytics/AnalyticsControllerTests.cs` — the integration harness to mirror.

## Acceptance criteria

### DTOs (`Bryk.Application/Calendar/`)
- `ComplianceBucket` enum: `Grey=0, Green=1, Yellow=2, Red=3` (4 buckets; `unplanned` is a flag, not a bucket).
- `CalendarItemKind` enum: `Planned=1, Completed=2, Event=3`.
- `CalendarItemDto`:
  - `Guid Id` (the underlying entity id — `PlannedWorkout.Id`, `Workout.Id`, or `Event.Id`).
  - `CalendarItemKind Kind`.
  - `Sport? Sport` (events may carry a sport; planned/completed always do).
  - `string Title` (planned/completed: `PlannedWorkout.Title` / derived from linked planned or
    `Workout.Sport` fallback; event: `Event.Name`).
  - `decimal? Load` (planned: `EffectiveLoad`; completed: `EffectiveLoad`; event: null).
  - `decimal? PlannedLoad` (planned: its `EffectiveLoad`; completed: the linked planned's
    `EffectiveLoad` when linked, else null; event: null) — drives the chip's planned-vs-actual.
  - `ComplianceBucket? Compliance` (planned: the classified bucket; completed: `Green` if
    `unplanned`, else the linked planned's bucket; event: null — events aren't graded).
  - `bool IsUnplanned` (true only for a completed `Workout` with null `PlannedWorkoutId`).
  - `Guid? PlannedWorkoutId` (completed linked to planned; else null).
  - `Guid? WorkoutId` (planned matched to a completion; else null — inverse link for the popover).
  - `EventPriority? Priority` (events only; null otherwise) — A/B/C styling.
  - `string? Notes` (event: `Event.Notes`; else null) — Phase 17 will render event notes; surface now.
- `CalendarDayDto`:
  - `DateOnly Date`.
  - `IReadOnlyList<CalendarItemDto> Items` (ordered: events first by priority, then planned by title,
    then unplanned completions by title; matched planned+completed pairs are NOT merged — they render
    as two chips with the inverse-link ids so the popover can show planned-vs-actual).
- `CalendarFeedResponse`:
  - `DateOnly RangeStart`, `DateOnly RangeEnd` (echoed).
  - `IReadOnlyList<CalendarDayDto> Days` (one entry per day in `[from, to]` inclusive, even empty days —
    the grid needs every cell; empty day → empty `Items`).
- `CalendarFeedRequest { DateOnly From; DateOnly To; }` (in `Bryk.Application/Calendar/`).
- `CalendarFeedRequestValidator`: `From` required; `To` required; `From <= To`; `(To - From).Days + 1 <= 62`
  (clear message: "range must be 62 days or fewer"); `To <= today` is **NOT** required — the calendar
  shows future planned workouts. Validate via `ValidateOrThrowAsync` (→ 400).

### `ComplianceClassifier` (pure, static — ADR-0008 §1)
- Input: a record `ComplianceInput { DateOnly ScheduledDate; decimal? PlannedLoad; int? PlannedDurationSeconds; decimal? CompletedLoad; int? CompletedDurationSeconds; bool HasCompletion; DateOnly Today; }`.
- Output: `ComplianceBucket`.
- Logic (verbatim from ADR-0008 §1):
  - `ScheduledDate > Today` → `Grey`.
  - `ScheduledDate < Today && !HasCompletion` → `Red`.
  - Otherwise (past/today with completion, or today without completion-but-classify-anyway when
    `HasCompletion`): compute `ratio` per the null-load fallback chain:
    - `PlannedLoad is not null`: `EffectiveLoad(planned) == 0 ? 1.0 : CompletedLoad / PlannedLoad`.
    - else `PlannedDurationSeconds is not null`: `!HasCompletion || CompletedDurationSeconds is null ? 0.0 : CompletedDurationSeconds / PlannedDurationSeconds`.
    - else: `1.0`.
  - `ratio ∈ [0.8, 1.2]` → `Green`; `ratio ∈ [0.5, 0.8) ∪ (1.2, ∞)` → `Yellow`; `ratio < 0.5` → `Red`.
  - Edge: today + no completion → `Grey` (the day isn't over — guard this **before** the ratio branch;
    i.e. `!HasCompletion && ScheduledDate >= Today` → `Grey`).
- Unit-tested (pin exact buckets):
  - future planned → `Grey`.
  - today planned, no completion → `Grey`.
  - today planned, completion ratio 1.0 → `Green`.
  - past planned, no completion → `Red`.
  - past planned, ratio 0.8 → `Green` (boundary inclusive).
  - past planned, ratio 0.79 → `Yellow`.
  - past planned, ratio 1.21 → `Yellow`.
  - past planned, ratio 0.49 → `Red`.
  - null `PlannedLoad`, `PlannedDurationSeconds` set, completion with duration ratio 1.0 → `Green`.
  - null `PlannedLoad`, null `PlannedDurationSeconds`, completion exists → `Green` (the tail branch).
  - `PlannedLoad = 0`, completion exists → `Green` (don't div-by-zero).
  - null `PlannedLoad`, `PlannedDurationSeconds` set, no completion, past → `Red` (the `0.0` ratio path).

### Repository reads (additive — no migration)
- `IWorkoutRepository.GetByAthleteInRangeWithPlannedAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct)`
  → the athlete's completed workouts in `[start, end]` inclusive, **entity only** (no step results —
  the feed doesn't need them), `AsNoTracking`, ordered by `CompletedDate` then `CreatedAt` desc. The
  `PlannedWorkoutId` column is already on `Workout`; this read just makes the range filter explicit
  (the existing `GetByAthleteInRangeAsync` already does this — **prefer reusing it** and only add a new
  method if the signature genuinely diverges; if reused, no new repo method is needed and this bullet
  is satisfied by "use `GetByAthleteInRangeAsync`").
- `IEventRepository.GetByAthleteInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct)`
  → the athlete's events whose `EventDate` ∈ `[start, end]`, ordered by `EventDate` then `Priority`,
  `AsNoTracking`. **Genuinely new** — `IEventRepository` has no range read today (only
  `GetByAthleteIdAsync`, which is unbounded). Add this one.

### `CalendarService` (new; primary-ctor DI)
Ctor deps: `ICurrentUserService`, `ITrainingPlanRepository`, `IWorkoutRepository`,
`IEventRepository`, `IAthleteRepository`, `IZoneService`, `IValidator<CalendarFeedRequest>` (all
already registered). Extends a new `ICalendarService` interface:
- `Task<CalendarFeedResponse> GetFeedAsync(DateOnly? from, DateOnly? to, CancellationToken ct)`:
  1. Resolve `athleteId`. Build `CalendarFeedRequest { From = from ?? <default>, To = to ?? <default> }`.
     Defaults when absent: `To = today`, `From = today - 41 days` (a 42-day window — one month of
     history + the current week + 5 days forward; matches the ≤62-day cap). `ValidateOrThrowAsync`.
  2. `planned = GetPlannedWorkoutsInRangeWithStructureAsync(athleteId, From, To)` (structure needed for
     `ComputedLoad`); `athlete = GetWithSportProfilesAsync`; `zones = GetZonesAsync`. Per planned
     workout: `EffectiveLoad = PlannedLoad ?? LoadCalculator.ComputePlannedLoad(pw, profile, sportZones)`.
     `PlannedDurationSeconds = PlannedDurationMinutes * 60` when set.
  3. `completed = GetByAthleteInRangeAsync(athleteId, From, To)`. Index by `PlannedWorkoutId` for the
     match: a planned workout matches at most one completion (the linked `Workout`). Per completion:
     `EffectiveLoad = LoadOverride ?? ComputedLoad ?? 0`. `IsUnplanned = PlannedWorkoutId is null`.
  4. `events = GetByAthleteInRangeAsync(athleteId, From, To)`.
  5. For each planned workout, classify via `ComplianceClassifier` (build the `ComplianceInput` from
     the planned + its matched completion + `today`). For each completed-without-planned, `Compliance =
     Green`, `IsUnplanned = true`. Events have null `Compliance`.
  6. Build `CalendarDayDto` for every day in `[From, To]` (even empty), assemble items per the ordering
     rule in the DTO spec, return `CalendarFeedResponse`.

### Controller (new `CalendarController`)
- `[ApiController]`, `[ApiVersion("1.0")]`, `[Route("api/v{version:apiVersion}/[controller]")]`.
- `GET` → `[HttpGet]`, `[FromQuery] DateOnly? from`, `[FromQuery] DateOnly? to`,
  `Ok(CalendarFeedResponse)`. XML `<summary>` noting the ≤62-day cap, defaults, and that the feed
  includes empty days. No try/catch. Athlete always via `ICurrentUserService` (never from query).

### Tests
- **Unit** (`Bryk.Application.Tests/Calendar/ComplianceClassifierTests.cs`): every bullet in the
  classifier section above as a named test with exact `ComplianceBucket` assertions.
- **Integration** (`Bryk.API.Tests/Calendar/CalendarControllerTests.cs`, new): seed via
  `POST /trainingplans` + `POST /workouts` + `POST /events` (or the onboarding events endpoint).
  - `from > to` → 400; range > 62 days → 400.
  - Defaults (no params) return a 42-day window ending today.
  - A seeded planned workout (no completion) on a past day → `Red` with no `WorkoutId`.
  - A seeded completed workout linked to a planned, ratio 1.0 → planned `Green`, completed `Green`,
    inverse-link ids set on both.
  - An unplanned completion → `IsUnplanned = true`, `Green`, `PlannedWorkoutId` null.
  - An event on a seeded day → `CalendarItemKind.Event`, `Priority` set, `Notes` echoed.
  - Empty days (no items) appear in `Days` for every date in the range.
  - Fresh athlete (no data) → 200, full day range, every `Days[i].Items` empty.
- `dotnet build api/Bryk.sln` + `dotnet test api/Bryk.sln` green.

## What NOT to modify
- No migration, no new package, no snapshot table. If a read seems too slow — **STOP and ask**.
- Don't change existing repo methods or the 14/15 analytics behaviour (only *add* the event range read;
  reuse `GetByAthleteInRangeAsync` for workouts).
- Don't accept an athlete id from query/body — always `ICurrentUserService`.
- Don't put the bucket/ratio math in the service — it lives in `ComplianceClassifier`.
- Don't merge matched planned+completed into one chip — the grid renders two chips with inverse-link ids.
- Don't classify events — they aren't graded (`Compliance = null`).
- Don't add `Program.cs` DI changes beyond what the new service/controller ctor deps already satisfy
  (all registered types are already wired).

## Suggested commit
```
feat: calendar feed + compliance classifier (endpoint, tests)

Pure ComplianceClassifier (5 buckets: grey/green/yellow/red + unplanned
flag, ADR-0008 §1) with a single null-load fallback rule (planned-load
ratio → planned-duration ratio → completion=green). CalendarService
merges planned + completed + events into a day-keyed feed over a bounded
range; additive GET /api/v1/calendar. One additive repo read
(IEventRepository.GetByAthleteInRangeAsync); no migration. xUnit pins
every bucket boundary and the null-load fallback branches.
```
