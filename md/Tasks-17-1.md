# Task 17-1 — Event & Goal GET endpoints + linked-plan reverse lookup

## Surface
Backend only. Read-side additions to the existing `EventService`/`GoalService` (both currently
write-only) + their controllers: `GET /api/v1/events` (ordered, `upcoming` filter, linked-plan ids +
`Notes`), `GET /api/v1/events/{id}`, `GET /api/v1/goals` (computed days-remaining + status). One new
additive read on `ITrainingPlanRepository` for the reverse `TrainingPlan.EventId` lookup; two new
response DTOs; integration + unit tests. **No migration, no new package.**

## Why
Closes a verified API gap: events and goals have **no GET endpoints at all** today — the dashboard and
Profile compose them from `GET /profile/goals` (`ProfileService`). Phase 17's `/goals` page (17-3) needs
first-class list reads with the derived fields the design surface shows: the linked-plan chip (reverse
`EventId` lookup, read-only per the ROADMAP's "display-only in Phase 17" decision) and the goal's
date-based progress/status. Computing days-remaining + status **server-side** keeps the client a dumb
renderer and makes the status thresholds testable in isolation, mirroring how `AnalyticsService`
computes PMC summaries rather than shipping raw rows.

## Depends on
- **Phase 8** — `EventService`/`GoalService`/`EventResponse`/`GoalResponse`, `ProfileService.GetGoalsAsync`
  (the composition this task promotes to first-class endpoints), `EventDto`/`GoalDto`, the validators.
- **Phase 9 / ADR-0003** — `TrainingPlan.EventId` (the dormant link this surfaces read-only) and
  `ITrainingPlanRepository` ownership + `AsNoTracking` read patterns.
- **ROADMAP Phase 17 "Decisions needed"** — quantitative goal progress is **deferred** (no
  `TargetValue/Unit/CurrentValue`; date-based only); plan↔event write surface waits for Phase 18's plan
  PUT — this task is **read-only** on the link.

## Required reading
- `api/Bryk.Application/Events/EventService.cs` + `IEventService.cs` + `EventResponse.cs` — the
  primary-ctor service, `ICurrentUserService` athlete resolution, `KeyNotFoundException` → 404 pattern,
  and the `Map(Event)` shape to extend with a linked-plan field.
- `api/Bryk.Application/Goals/GoalService.cs` + `IGoalService.cs` + `GoalResponse.cs` — same, for goals;
  the `Map(Goal)` shape to wrap with a new response carrying days-remaining + status.
- `api/Bryk.Application/Profile/ProfileService.cs` — `GetGoalsAsync`: the existing
  `GetByAthleteIdAsync` reads and the `EventResponse`/`GoalResponse` projections to reuse. **Do not
  change this method** — the new endpoints sit alongside; the dashboard/Profile keep using it.
- `api/Bryk.Domain/Interfaces/IEventRepository.cs`, `IGoalRepository.cs` — existing
  `GetByAthleteIdAsync` ordering contracts (events by `EventDate` asc; goals by `TargetDate` asc nulls
  last) — reuse verbatim, no new repo reads on these two.
- `api/Bryk.Domain/Interfaces/ITrainingPlanRepository.cs` — add the reverse-lookup read here, mirroring
  the `AsNoTracking` / single-table style of `GetPlannedWorkoutsInRangeAsync`.
- `api/Bryk.Domain/Entities/TrainingPlan.cs` (`EventId`, `Name`, `Id`), `Event.cs`, `Goal.cs` — field
  shapes.
- `api/Bryk.API/Controllers/EventsController.cs`, `GoalsController.cs` — thin controllers to extend with
  `[HttpGet]` actions; mirror the `AnalyticsController` `[FromQuery]` + `Ok(...)` / `NotFound()` style.
- `api/Bryk.API.Tests/` — the `Mvc.Testing` integration harness (e.g. `Analytics/AnalyticsControllerTests.cs`
  or the profile tests) to mirror for the new endpoint tests; `Bryk.Application.Tests/` for the pure
  status/days-remaining unit tests.

## Acceptance criteria

### New DTOs (`Bryk.Application/`)
- `Bryk.Application/Events/EventListItemResponse.cs` — extends the existing `EventResponse` shape with the
  linked plan(s):
  - all existing `EventResponse` fields (`Id`, `Name`, `EventDate`, `Sport`, `TriathlonDistance`,
    `CustomDistanceName`, `Priority`, `Notes`), **plus**
  - `IReadOnlyList<LinkedPlanDto> LinkedPlans` — the plans whose `EventId == this.Id` (usually 0 or 1;
    a list because the schema doesn't enforce 1:1). `LinkedPlanDto { Guid Id; string Name; }` — id +
    name only (the chip navigates to `/plans/{id}`; no plan body needed).
  - **Do not** add a new `GET /events/{id}` shape that diverges — `GET /events/{id}` returns the same
    `EventListItemResponse` (single).
- `Bryk.Application/Goals/GoalListItemResponse.cs`:
  - all existing `GoalResponse` fields (`Id`, `Type`, `Description`, `TargetDate`), **plus**
  - `int? DaysRemaining` — whole days from **today (UTC)** to `TargetDate` inclusive of today (`0` on the
    target date, negative if past); `null` when `TargetDate` is null.
  - `GoalStatus Status` — a new enum `GoalStatus { NoDate = 0, Upcoming = 1, DueSoon = 2, Overdue = 3 }`
    (in `Bryk.Application/Goals/`). Computed purely from `DaysRemaining`:
    - `TargetDate is null` → `NoDate`.
    - `DaysRemaining < 0` → `Overdue`.
    - `DaysRemaining <= 14` → `DueSoon`.
    - else → `Upcoming`.
  - **No** `TargetValue`/`Unit`/`CurrentValue`/percent-complete field — quantitative progress is
    deferred (ROADMAP decision). Date-based only. If a reviewer asks for a completion percentage, it is
    out of scope for v1.

### Pure status helper (unit-testable, no `DateTime.UtcNow` inside)
- A static `GoalProgress.Compute(DateOnly? targetDate, DateOnly today) → (int? DaysRemaining, GoalStatus Status)`
  in `Bryk.Application/Goals/GoalProgress.cs`. `today` is passed in (calculators-take-`today` convention
  from `WeeklyLoadCalculator`/`PmcCalculator`) so it is deterministic under test. The service computes
  `today = DateOnly.FromDateTime(DateTime.UtcNow)` and passes it in.
- Unit-tested (`Bryk.Application.Tests/Goals/GoalProgressTests.cs`), exact values pinned:
  - `targetDate = null` → `(null, NoDate)`.
  - `targetDate = today` → `(0, DueSoon)`.
  - `targetDate = today + 14` → `(14, DueSoon)` (boundary inclusive).
  - `targetDate = today + 15` → `(15, Upcoming)`.
  - `targetDate = today - 1` → `(-1, Overdue)`.

### `IEventService` / `EventService` additions
- Add to `IEventService`:
  - `Task<IReadOnlyList<EventListItemResponse>> GetAllAsync(bool upcomingOnly, CancellationToken ct = default)`.
  - `Task<EventListItemResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)` — returns `null`
    when the event doesn't exist or belongs to another athlete (controller maps null → 404, matching the
    ProfileController null→NotFound style; do **not** throw `KeyNotFoundException` from a GET).
- `EventService.GetAllAsync`:
  1. `athleteId = currentUser.GetCurrentAthleteId()`.
  2. `events = eventRepo.GetByAthleteIdAsync(athleteId, ct)` (already ordered by `EventDate` asc).
  3. When `upcomingOnly`: filter `e.EventDate >= DateOnly.FromDateTime(DateTime.UtcNow)` (today
     inclusive — a race today is still "upcoming"). Preserve the repo's date-asc order.
  4. Load linked plans once: `planRepo.GetByEventIdsAsync(events.Select(e => e.Id), ct)` (new read
     below), group by `EventId`, project each into `LinkedPlanDto`. Map each event → `EventListItemResponse`
     with its group (empty list when none).
- `EventService.GetByIdAsync`: load via `eventRepo.GetByIdAsync(id, ct)`; if null or
  `AthleteId != currentUser.GetCurrentAthleteId()` → return `null`. Else load its linked plans via the
  same new read (single-element id set) and map.
- `EventService` gains `ITrainingPlanRepository planRepo` as a **new ctor dependency** (already
  registered in `Program.cs`).

### `IGoalService` / `GoalService` additions
- Add to `IGoalService`:
  - `Task<IReadOnlyList<GoalListItemResponse>> GetAllAsync(CancellationToken ct = default)`.
- `GoalService.GetAllAsync`:
  1. `athleteId = currentUser.GetCurrentAthleteId()`; `today = DateOnly.FromDateTime(DateTime.UtcNow)`.
  2. `goals = goalRepo.GetByAthleteIdAsync(athleteId, ct)` (already ordered by `TargetDate` asc nulls
     last).
  3. Map each → `GoalListItemResponse`, computing `(DaysRemaining, Status)` via `GoalProgress.Compute`.

### New repository read (`ITrainingPlanRepository` — additive, no migration)
- `Task<IReadOnlyList<TrainingPlan>> GetByEventIdsAsync(IEnumerable<Guid> eventIds, CancellationToken ct = default)`
  → plans whose `EventId` is in `eventIds` (and non-null), **entity only** (no `PlannedWorkouts`
  include — the chip needs `Id`+`Name` only), `AsNoTracking`. An empty `eventIds` returns an empty list
  with no query (mirror `GetPlannedWorkoutsByIdsWithStructureAsync`'s empty-guard). This is a read across
  the athlete's plans keyed by `EventId`; scoping is by `EventId` (the events are already athlete-scoped
  when we resolve them), so **no** cross-athlete leak — but the calling service only ever passes ids of
  events it already loaded for the current athlete.

### Controllers (additive `[HttpGet]` actions)
- `EventsController`:
  - `[HttpGet]` → `GetAllAsync([FromQuery] bool upcoming, CancellationToken ct)` → `Ok(IReadOnlyList<EventListItemResponse>)`.
    XML `<summary>` noting date-asc order, that `upcoming=true` filters to `EventDate >= today`, and that
    items carry `Notes` + linked-plan ids. `upcoming` defaults to `false` (all events).
  - `[HttpGet("{id:guid}")]` → `GetByIdAsync(Guid id, CancellationToken ct)` → `result is null ? NotFound() : Ok(result)`.
- `GoalsController`:
  - `[HttpGet]` → `GetAllAsync(CancellationToken ct)` → `Ok(IReadOnlyList<GoalListItemResponse>)`. XML
    `<summary>` noting date-asc order and computed `daysRemaining` + `status`.
- Thin controllers, no try/catch, athlete always via `ICurrentUserService` (never from query/route).
  `EventsController`/`GoalsController` ctors are unchanged (they already inject the service).

### Tests
- **Unit** (`Bryk.Application.Tests/Goals/GoalProgressTests.cs`): every `GoalProgress.Compute` bullet
  above, exact `(DaysRemaining, Status)` assertions.
- **Integration** (`Bryk.API.Tests/Events/EventsControllerGetTests.cs`, `Goals/GoalsControllerGetTests.cs`,
  new): seed via the existing `POST /events`, `POST /goals`, `POST /trainingplans` (set the plan's
  `EventId` to the seeded event).
  - `GET /events` returns seeded events ordered by `EventDate` ascending; `Notes` echoed.
  - `GET /events?upcoming=true` excludes a seeded past event, includes a today-dated and future event.
  - A plan linked to an event → that event's `LinkedPlans` has one entry with the plan's `Id` + `Name`;
    an unlinked event → empty `LinkedPlans`.
  - `GET /events/{id}` returns the single event with `LinkedPlans`; unknown id → 404; another athlete's
    event id (swap `DevAuth:CurrentAthleteId` in the test host, or seed a second athlete) → 404.
  - `GET /goals` returns seeded goals; a goal with `TargetDate = today + 3` → `daysRemaining = 3`,
    `status = "DueSoon"`; a null-target goal → `daysRemaining = null`, `status = "NoDate"`; a past-target
    goal → `status = "Overdue"`.
  - Fresh athlete (no events/goals) → 200 with empty arrays (not 404 — these are collection reads, unlike
    `/profile/goals` which 404s on a missing athlete).
- `dotnet build api/Bryk.sln` + `dotnet test api/Bryk.sln` green.

## What NOT to modify
- **No migration, no new package.** No `TargetValue`/`Unit`/`CurrentValue` columns — if a task appears to
  need quantitative goal progress, **STOP and flag it as a blocker** (it's a deferred product decision,
  ROADMAP Phase 17). This task must add zero EF model changes.
- **Do not** add a write path for the plan↔event link — it is display-only in Phase 17 (the write surface
  waits for Phase 18's plan PUT). `GetByEventIdsAsync` is a read only.
- **Do not** change `ProfileService.GetGoalsAsync` or the existing `EventResponse`/`GoalResponse` shapes —
  the dashboard/Profile still consume them. The new list responses are additive DTOs.
- **Do not** change the existing `POST`/`PUT`/`DELETE` actions or the `EventDto`/`GoalDto` validators.
- **Do not** accept an athlete id from query/route/body — always `ICurrentUserService`.
- **Do not** include `PlannedWorkouts` in `GetByEventIdsAsync` — id + name only.
- **Do not** throw `KeyNotFoundException` from the GET-by-id path — return `null` and let the controller
  map to `NotFound()`.

## Suggested commit
```
feat: events & goals GET endpoints with linked-plan lookup

Promote events/goals to first-class read endpoints (the dashboard
composed them from /profile/goals before): GET /api/v1/events (date-asc,
upcoming=true filter, Notes + linked-plan ids), GET /api/v1/events/{id},
GET /api/v1/goals (computed days-remaining + status). Linked plans come
from a new additive read-only ITrainingPlanRepository.GetByEventIdsAsync
reverse EventId lookup (display-only; the write path waits for Phase 18).
Goal status/days-remaining computed by a pure GoalProgress helper (unit
tests pin the DueSoon/Upcoming/Overdue boundaries). No migration; goal
target-value tracking stays deferred per the Phase 17 decision. xUnit
covers ordering, the upcoming filter, linked/unlinked plans, and 404s.
```
