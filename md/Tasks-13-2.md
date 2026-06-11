# Task 13-2 — Workout list filters + pagination (`from`/`to`/`sport`/`skip`/`take`)

## Surface
Backend only. Extend the existing `GET /api/v1/workouts` so it accepts optional `from`, `to`,
`sport`, `skip`, and `take` query parameters, newest-first, capped. All parameters optional and
**non-breaking** — `GET /workouts` and `GET /workouts?take=10` keep behaving exactly as today
(the dashboard's Recent Activity read must not change). This is the date-range/paged workhorse
Phase 14 reuses for the daily-load series.

## Why
13-3's history view needs server-side filtering (sport + date range) and "load more" paging; doing
it client-side won't scale past the seed set. Establishing the **pagination convention now** (recorded
below) means every later list endpoint (14 daily-load, 15 peaks, 16 calendar feed, 17 events/goals)
follows one shape instead of each inventing its own.

## Depends on
- **Task 11-4** — `GET /workouts` (`GetRecentAsync`/`GetRecentByAthleteAsync`), `WorkoutResponse`.
- **Task 13-1** is independent of this task (can land in either order); they touch the same
  controller/service/repo, so rebase carefully.

## Required reading
- `api/Bryk.API/Controllers/WorkoutsController.cs` — the `[HttpGet]` `GetRecentAsync([FromQuery] int take)`
  action to generalize.
- `api/Bryk.Application/Training/Workouts/WorkoutService.cs` — `GetRecentAsync` (the `take is > 0 and
  <= 100 ? take : 10` cap) → becomes the filtered/paged method.
- `api/Bryk.Infrastructure/Repositories/WorkoutRepository.cs` — `GetRecentByAthleteAsync`
  (`OrderByDescending(CompletedDate).ThenByDescending(CreatedAt)`) + `GetByAthleteInRangeAsync` as the
  building blocks; confirm whether either becomes orphaned (remove only if truly unused — grep callers).
- `api/Bryk.Domain/Entities/Sport.cs` — int-backed enum bound from the query string.

## Pagination convention (record verbatim — later list endpoints follow it)
> **Bryk list pagination.** List endpoints page with `skip` (offset, default `0`, clamped to `≥ 0`)
> and `take` (page size, default `20`, clamped to `1..100`). Out-of-range values are clamped, never
> rejected. Results are returned newest-first (`CompletedDate` desc, then `CreatedAt` desc as a stable
> tiebreak). Optional filters narrow before paging. The response is the bare array for v1 (no
> envelope/total-count); "load more" advances `skip` by the page size until a short page returns.

## Acceptance criteria
- **Controller** — `[HttpGet]` action takes
  `[FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Sport? sport, [FromQuery] int? skip,
  [FromQuery] int? take` and delegates to the service. Updated XML `<summary>` documenting the filters
  + the cap/default. Returns `Ok(IReadOnlyList<WorkoutResponse>)`.
- **Service** — replace `GetRecentAsync(int take)` with
  `Task<IReadOnlyList<WorkoutResponse>> GetWorkoutsAsync(DateOnly? from, DateOnly? to, Sport? sport,
  int? skip, int? take, CancellationToken ct)`:
  - `take` clamped to `1..100`, default `20`; `skip` clamped to `≥ 0`, default `0`.
  - Resolves the current athlete via `ICurrentUserService` (never from the request).
  - Delegates to the repo, maps via the existing `Map` (no step results on the list — entity only, as
    today). `TrainingPlanId` stays `null` on list reads (13-1 decision 3).
- **Repository** — add
  `Task<IReadOnlyList<Workout>> GetByAthleteFilteredAsync(Guid athleteId, DateOnly? from, DateOnly? to,
  Sport? sport, int skip, int take, CancellationToken ct)`: `AsNoTracking`, apply
  `AthleteId` + optional `CompletedDate >= from` / `<= to` / `Sport == sport`, order newest-first
  (`CompletedDate` desc, `CreatedAt` desc), `.Skip(skip).Take(take)`. Remove `GetRecentByAthleteAsync`
  **only if** no other caller remains after the swap (grep first; `GetByAthleteInRangeAsync` is used by
  the weekly aggregation — leave it).
- **Non-breaking proof in tests.** `GET /workouts` (no params) and `GET /workouts?take=10` return the
  same newest-first shape as before.
- **Tests** (extend `WorkoutServiceTests` unit + `WorkoutsControllerTests` integration from 13-1, or a
  sibling file):
  - `from`/`to` filter to the in-range workouts (boundary-inclusive).
  - `sport` filter returns only that sport.
  - `skip`/`take` page correctly and the cap holds (`take=500` → ≤ 100; `take=0`/absent → 20).
  - newest-first ordering preserved.
- `dotnet build api/Bryk.sln` + `dotnet test api/Bryk.sln` green.

## What NOT to modify
- Don't change `WorkoutResponse`, `LogAsync`, `GetAsync`, or the PUT/DELETE from 13-1.
- Don't add a response envelope or total-count (bare array for v1 — the convention says so).
- Don't filter on anything but `from`/`to`/`sport` (no text search, no plan filter — later phases).
- Don't accept an athlete id from the query (always `ICurrentUserService`).
- Don't touch the dashboard or any UI (13-3 consumes this).

## Suggested commit
```
feat: filter and paginate the workouts list endpoint

GET /workouts gains optional from/to/sport/skip/take (newest-first,
take clamped 1..100 default 20, skip >= 0). Backwards compatible with the
bare and ?take= reads the dashboard uses. Records the skip/take pagination
convention every later list endpoint follows.
```
