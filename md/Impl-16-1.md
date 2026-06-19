# Impl 16-1 — Build order: compliance classifier, calendar feed, GET endpoint

**Executor:** GLM 5.2 (architect-implementer, per CLAUDE.md regenerated 2026-06-07).
**Acceptance contract:** `md/Tasks-16-1.md`. **Decision lock:** `md/decisions/0008-calendar-compliance.md` §1.
**Scope:** Backend only. No migration, no new package.

This is the step-by-step build order. Execute top-to-bottom; each step's verification is the gate to
the next. Commit once at the end with the message in `Tasks-16-1.md`.

## Step 0 — Pre-flight

- `git status` clean on `main`. `dotnet build api/Bryk.sln` green (baseline).
- Re-read `md/Tasks-16-1.md` + ADR-0008 §1. Open in editor:
  `api/Bryk.Application/Analytics/AnalyticsService.cs`, `AnalyticsController.cs`,
  `WeeklyLoadCalculator.cs`, `PeaksCalculator.cs`, `api/Bryk.Application/Training/ThisWeekService.cs`,
  `api/Bryk.Application/Training/Load/LoadCalculator.cs`,
  `api/Bryk.Domain/Interfaces/IWorkoutRepository.cs` / `IEventRepository.cs` /
  `ITrainingPlanRepository.cs`, `api/Bryk.Domain/Entities/{PlannedWorkout,Workout,Event}.cs`,
  `api/Bryk.API.Tests/Analytics/AnalyticsControllerTests.cs`.

## Step 1 — Domain/Interface: `IEventRepository.GetByAthleteInRangeAsync`

**File:** `api/Bryk.Domain/Interfaces/IEventRepository.cs` (add one method).

```csharp
/// <summary>
/// The athlete's <see cref="Event"/>s whose <see cref="Event.EventDate"/> is within [start, end]
/// inclusive, ordered by <see cref="Event.EventDate"/> then <see cref="Event.Priority"/>. No-tracking.
/// </summary>
Task<IReadOnlyList<Event>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default);
```

**File:** `api/Bryk.Infrastructure/Repositories/EventRepository.cs` (add the implementation).

Mirror the existing `GetByAthleteIdAsync` style: `AsNoTracking`, `.Where(e => e.AthleteId == athleteId && e.EventDate >= start && e.EventDate <= end)`,
`.OrderBy(e => e.EventDate).ThenBy(e => e.Priority).ToListAsync(ct)`.

**Verify:** `dotnet build api/Bryk.sln` green. (No tests yet — they come with the service.)

## Step 2 — DTOs in `Bryk.Application/Calendar/`

**New folder** `api/Bryk.Application/Calendar/`. Create the types verbatim from `Tasks-16-1.md`'s DTO
section: `ComplianceBucket` enum, `CalendarItemKind` enum, `CalendarItemDto`, `CalendarDayDto`,
`CalendarFeedResponse`, `CalendarFeedRequest`, `CalendarFeedRequestValidator`.

**Validator** (`CalendarFeedRequestValidator.cs`): mirror
`AnalyticsRangeRequestValidator`'s style. Rules:
- `From` required with a clear message.
- `To` required.
- `From <= To` (`Must((r, _) => r.From <= r.To)` with message "from must be on or before to").
- `(r.To.DayNumber - r.From.DayNumber) + 1 <= 62` with message "range must be 62 days or fewer".
- **No** `To <= today` rule — the calendar shows future planned workouts.

Register the validator in DI wherever `AnalyticsRangeRequestValidator` is registered (find it via grep
on `AddValidatorsFromAssembly` or the equivalent in `Program.cs` / the Application DI extension).

**Verify:** `dotnet build` green.

## Step 3 — Pure `ComplianceClassifier`

**New file** `api/Bryk.Application/Calendar/ComplianceClassifier.cs`.

```csharp
namespace Bryk.Application.Calendar;

/// <summary>
/// The 5-bucket compliance classifier (ADR-0008 §1). Pure: no I/O, no DateTime.UtcNow.
/// The service builds a ComplianceInput per planned workout and calls Classify.
/// </summary>
public static class ComplianceClassifier
{
    public static ComplianceBucket Classify(ComplianceInput input)
    {
        // Future → grey (today-no-completion is grey too: the day isn't over).
        if (input.ScheduledDate > input.Today) return ComplianceBucket.Grey;
        if (!input.HasCompletion && input.ScheduledDate >= input.Today) return ComplianceBucket.Grey;

        // Past + no completion → red (missed).
        if (!input.HasCompletion) return ComplianceBucket.Red;

        // Past/today with completion → ratio.
        var ratio = Ratio(input);
        return ratio switch
        {
            >= 0.8m and <= 1.2m => ComplianceBucket.Green,
            >= 0.5m => ComplianceBucket.Yellow,        // [0.5, 0.8) — the (1.2, ∞) case is also yellow
            _ => ComplianceBucket.Red,                 // < 0.5
        };
    }

    private static decimal Ratio(ComplianceInput input)
    {
        // Single null-load fallback chain (ADR-0008 §1).
        if (input.PlannedLoad is { } plannedLoad)
        {
            return plannedLoad == 0m ? 1.0m : (input.CompletedLoad ?? 0m) / plannedLoad;
        }
        if (input.PlannedDurationSeconds is { } plannedDur)
        {
            if (input.CompletedDurationSeconds is null) return 0.0m;
            return (decimal)input.CompletedDurationSeconds.Value / plannedDur;
        }
        return 1.0m;
    }
}

public sealed record ComplianceInput(
    DateOnly ScheduledDate,
    decimal? PlannedLoad,
    int? PlannedDurationSeconds,
    decimal? CompletedLoad,
    int? CompletedDurationSeconds,
    bool HasCompletion,
    DateOnly Today);
```

**Verify:** `dotnet build` green.

## Step 4 — Unit tests for `ComplianceClassifier`

**New file** `api/Bryk.Application.Tests/Calendar/ComplianceClassifierTests.cs`.

One named test per bullet in `Tasks-16-1.md`'s classifier section. Use xUnit + FluentAssertions
(match the `WeeklyLoadCalculatorTests` style). Pin a fixed `today = new DateOnly(2026, 6, 19)` (or
`DateOnly.FromDateTime(DateTime.UtcNow)` — but a fixed date makes the boundary tests deterministic;
prefer a fixed date and pass it in).

Every boundary: 0.8 → Green, 0.79 → Yellow, 1.2 → Green, 1.21 → Yellow, 0.5 → Yellow, 0.49 → Red.
The null-load branches: null `PlannedLoad` + `PlannedDurationSeconds` set + duration ratio 1.0 →
Green; null both + completion → Green; `PlannedLoad = 0` + completion → Green; null `PlannedLoad` +
`PlannedDurationSeconds` set + no completion past → Red.

**Verify:** `dotnet test api/Bryk.sln` green (the new tests pass; nothing else broke).

## Step 5 — `ICalendarService` + `CalendarService`

**New files** `api/Bryk.Application/Calendar/ICalendarService.cs`, `CalendarService.cs`.

`ICalendarService`:
```csharp
Task<CalendarFeedResponse> GetFeedAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);
```

`CalendarService` primary-ctor deps: `ICurrentUserService`, `ITrainingPlanRepository`,
`IWorkoutRepository`, `IEventRepository`, `IAthleteRepository`, `IZoneService`,
`IValidator<CalendarFeedRequest>`.

`GetFeedAsync` implementation per `Tasks-16-1.md`:
1. Resolve `athleteId = currentUser.GetCurrentAthleteId()`.
2. `today = DateOnly.FromDateTime(DateTime.UtcNow)`. Defaults: `to ?? today`,
   `from ?? today.AddDays(-41)`. Build `CalendarFeedRequest`, `await validator.ValidateOrThrowAsync(request, ct)`.
3. `planned = await planRepo.GetPlannedWorkoutsInRangeWithStructureAsync(athleteId, from, to, ct)` —
   structure needed for `ComputedLoad`. `athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct)`.
   `zones = await zoneService.GetZonesAsync(ct)`. Per planned: `computed = LoadCalculator.ComputePlannedLoad(pw, profile, sportZones)`;
   `effectiveLoad = pw.PlannedLoad ?? computed`; `plannedDur = pw.PlannedDurationMinutes * 60` when set.
4. `completed = await workoutRepo.GetByAthleteInRangeAsync(athleteId, from, to, ct)`. Build
   `Dictionary<Guid, Workout>` keyed by `PlannedWorkoutId` (skip nulls) for the match. Per completion:
   `effectiveLoad = LoadOverride ?? ComputedLoad ?? 0`.
5. `events = await eventRepo.GetByAthleteInRangeAsync(athleteId, from, to, ct)`.
6. For each planned, build `ComplianceInput` (matched completion from the dict, else `HasCompletion=false`),
   call `ComplianceClassifier.Classify`. Build `CalendarItemDto` with `Kind=Planned`, the planned's
   fields, `Compliance` set, `WorkoutId` = the matched completion's id (or null), `PlannedWorkoutId` null.
7. For each completed: if `PlannedWorkoutId` is null → `Kind=Completed`, `IsUnplanned=true`,
   `Compliance=Green`, `PlannedWorkoutId` null. If linked → `Kind=Completed`, `IsUnplanned=false`,
   `Compliance` = the linked planned's bucket (reclassify or reuse — reuse the planned's bucket to
   avoid double work; the planned's `WorkoutId` and the completed's `PlannedWorkoutId` are the inverse
   link), `PlannedWorkoutId` set, `PlannedLoad` = the linked planned's effective load.
8. For each event: `Kind=Event`, `Compliance=null`, `Priority` set, `Notes` set, `Load`/`PlannedLoad`
   null, `PlannedWorkoutId`/`WorkoutId` null.
9. Build `CalendarDayDto` for every date in `[from, to]` inclusive (`Enumerable.Range(0, days+1)`
   projecting `from.AddDays(i)`). Per day, gather items, order: events first (by `Priority`), then
   planned (by `Title`), then unplanned completions (by `Title`). Assemble `CalendarFeedResponse`
   with `RangeStart`/`RangeEnd` echoed.

**Verify:** `dotnet build` green.

## Step 6 — `CalendarController`

**New file** `api/Bryk.API/Controllers/CalendarController.cs`.

Mirror `AnalyticsController` exactly: `[ApiController]`, `[ApiVersion("1.0")]`,
`[Route("api/v{version:apiVersion}/[controller]")]`, primary-ctor `CalendarController(ICalendarService calendarService)`.

One action:
```csharp
[HttpGet]
public async Task<IActionResult> GetFeedAsync(
    [FromQuery] DateOnly? from,
    [FromQuery] DateOnly? to,
    CancellationToken cancellationToken)
{
    CalendarFeedResponse result = await calendarService.GetFeedAsync(from, to, cancellationToken);
    return Ok(result);
}
```

XML `<summary>` noting: returns the merged planned + completed + events feed for the current athlete
over `[from, to]` (defaults: today-41 to today); range ≤ 62 days, `from <= to`; empty days included;
compliance per ADR-0008 §1.

**Verify:** `dotnet build` green. No DI registration needed if `Program.cs` auto-registers
`ICalendarService` → `CalendarService` via the Application assembly scan (grep for how
`IAnalyticsService` is registered and mirror it; if there's a manual `services.AddScoped<…>()` list,
add the pair there).

## Step 7 — Integration tests

**New file** `api/Bryk.API.Tests/Calendar/CalendarControllerTests.cs`.

Mirror `AnalyticsControllerTests`'s harness: `WebApplicationFactory<Program>`, in-memory DbContext,
seed via the API endpoints (`POST /trainingplans`, `POST /workouts`, `POST /events` via onboarding or
the events endpoint — find the events POST route; if there's no events POST outside onboarding, seed
the `Event` row directly in the in-memory context via a helper).

Tests per `Tasks-16-1.md`'s integration bullets:
- `from > to` → 400. Range > 62 days → 400.
- Defaults return a 42-day window ending today (assert `RangeEnd == today`, `Days.Count == 42`).
- Past planned, no completion → `Red`, `WorkoutId` null.
- Linked completion, ratio 1.0 → planned `Green`, completed `Green`, inverse-link ids set.
- Unplanned completion → `IsUnplanned=true`, `Green`, `PlannedWorkoutId` null.
- Event on a seeded day → `Kind=Event`, `Priority` set, `Notes` echoed.
- Empty days appear for every date in range (pick a date with no items, assert `Items` empty).
- Fresh athlete → 200, full day range, every `Days[i].Items` empty.

**Verify:** `dotnet test api/Bryk.sln` green. Note the total test count for the handoff.

## Step 8 — Final verification + commit

- `dotnet build api/Bryk.sln` — 0 errors (warnings unchanged from baseline).
- `dotnet test api/Bryk.sln` — all green.
- `git diff --stat` — confirm only the expected files changed/added (the new `Calendar/` folder, the
  `IEventRepository` + `EventRepository` additions, the new test files, no stray changes).
- Commit with the message in `Tasks-16-1.md`.
