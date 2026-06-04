# Task 9-4 — "This Week" read endpoint

## Goal
Add a focused read endpoint that returns the current week's planned workouts for the athlete, shaped for the dashboard's "This Week" card. Server computes the Mon–Sun window; the client just renders.

Backend-only (Application + API). Thin — reuses Task 9-2's repository / Task 9-3's `Training` folder. No new entity, no migration, no UI.

## Depends on
- **Task 9-2** — `PlannedWorkout` entity + `ITrainingPlanRepository`.
- **Task 9-3** — the `Bryk.Application/Training/` folder, the service pattern, and the `PlannedWorkoutResponse` shape (reuse it; don't redefine).

## Why a separate endpoint (not part of 9-3)
The dashboard card needs a purpose-built read model — "this athlete's planned workouts whose scheduled date falls in the current week, flattened across all their plans, ordered by day." That's a different query and a different response shape from "give me plan X with its planned workouts." Keeping it in its own task keeps each PR single-purpose and the read model honest.

## Required reading
- `md/decisions/0003-trainingplan-domain-shape.md` — `PlannedWorkout` carries a scheduled `DateOnly` and a `Sport`; confirm the field names.
- `api/Bryk.Application/Profile/ProfileService.cs` + `IProfileService.cs` — **the reference for a read-only service** (no `IUnitOfWork`, identity from `ICurrentUserService`, returns response DTOs).
- `api/Bryk.API/Controllers/ProfileController.cs` — read-only controller returning 200 + body.
- `api/Bryk.Infrastructure/Repositories/EventRepository.cs` `GetByAthleteIdAsync` — the `AsNoTracking().Where(...).OrderBy(...)` date-query style to mirror for the week-range query.
- `api/Bryk.Application/Onboarding/Validators/EventDtoValidator.cs` line 14 — the canonical "today in UTC" expression: `DateOnly.FromDateTime(DateTime.UtcNow)`. Use the same basis for week-boundary math.

## Week computation (specify and document)
- "This week" = Monday 00:00 through Sunday, in **UTC**, consistent with how the rest of the domain treats `DateOnly` (server compares against `DateOnly.FromDateTime(DateTime.UtcNow)`).
- Compute `startOfWeek` = today minus `((int)today.DayOfWeek + 6) % 7` days (Monday-based); `endOfWeek` = `startOfWeek.AddDays(6)`. Put this in the service (or a small private helper), documented with a comment — do not scatter the math across layers.
- The endpoint returns planned workouts with `scheduledDate` in `[startOfWeek, endOfWeek]`, across **all** the athlete's plans, ordered by `scheduledDate` then sport.

## Acceptance criteria

**Repository (extend `ITrainingPlanRepository` / `TrainingPlanRepository`):**
- Add `GetPlannedWorkoutsInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct)` → `IReadOnlyList<PlannedWorkout>`, `AsNoTracking()`, filtered by athlete (join through plan) and `scheduledDate` range, ordered by date. XML doc in the existing style. (If ADR-0003 made `PlannedWorkout` reachable only via the plan aggregate, query through `TrainingPlans.SelectMany(p => p.PlannedWorkouts)` with the athlete filter — keep it one round-trip.)

**Application (`api/Bryk.Application/Training/`):**
- `IThisWeekService.cs` + `ThisWeekService.cs` **or** add a `GetThisWeekAsync()` method to the existing `ITrainingPlanService` — **pick one and note why.** Recommendation: a small dedicated `IThisWeekService` (read-only, no `IUnitOfWork`), mirroring `ProfileService`'s read-only shape, to keep the authoring service focused. Returns a `ThisWeekResponse`.
- `ThisWeekResponse.cs` — the card's read model: the week range (`weekStart`, `weekEnd`) + an ordered list of `PlannedWorkoutResponse` (reuse 9-3's type; add a `trainingPlanId`/plan-name reference field if the card needs to show which plan a session belongs to — keep minimal).

**API (`api/Bryk.API/Controllers/`):**
- A `GET` endpoint returning `ThisWeekResponse` with 200. Route: `GET /api/v1/training/this-week` (add a `TrainingController` if you went with `IThisWeekService`, or a `[HttpGet("this-week")]` action on `TrainingPlansController` — match whatever service decision you made; recommendation is a dedicated read-only `TrainingController`). Always 200 (empty list when no plans / nothing this week — NOT 404). XML `<summary>`.

**DI:**
- `api/Bryk.API/Program.cs` — register the new read service (scoped) if you added one.

**Tests:**
- `api/Bryk.Application.Tests/Training/ThisWeekServiceTests.cs` — at least: a planned workout dated inside the current week is returned; one dated last week and one dated next week are excluded; empty result when the athlete has no plans. Use a fixed/injected clock or seed dates relative to `DateOnly.FromDateTime(DateTime.UtcNow)` so the test is not flaky across week boundaries.
- `api/Bryk.API.Tests/Training/` — one integration test hitting the endpoint for the happy path (200 + the in-week session).
- Net new tests ≥ 3.

## Files likely to change/add
- `api/Bryk.Domain/Interfaces/ITrainingPlanRepository.cs` — add the range query
- `api/Bryk.Infrastructure/Repositories/TrainingPlanRepository.cs` — implement it
- `api/Bryk.Application/Training/{IThisWeekService,ThisWeekService,ThisWeekResponse}.cs` (new)
- `api/Bryk.API/Controllers/TrainingController.cs` (new) — or an action on TrainingPlansController
- `api/Bryk.API/Program.cs` — one DI line (if a new service)
- `api/Bryk.Application.Tests/Training/ThisWeekServiceTests.cs` (new)
- `api/Bryk.API.Tests/Training/ThisWeekControllerTests.cs` (new)

## What NOT to modify
- Do not change the authoring endpoints, DTOs, or validators from Task 9-3 (you may REUSE `PlannedWorkoutResponse`).
- Do not add write/execution behavior — read-only endpoint.
- Do not add a migration or touch entities — the query uses existing shapes.
- Do not build UI — Task 9-5.
- Do not return 404 for an empty week — empty list + 200 is correct.
- Do not duplicate the week math in the controller — it lives in the service.

## Test plan
1. `dotnet build api/Bryk.sln` green.
2. `dotnet test api/Bryk.sln` green; count up by ≥3; week-boundary tests are deterministic (relative dates, not hardcoded).
3. Manual smoke: with a plan that has sessions this week + sessions in adjacent weeks, `GET /api/v1/training/this-week` returns only the in-week sessions, ordered by date, with the week range; an athlete with no plans gets `200` + empty list.
4. `git diff --stat` — only the repository extension, the new read service/controller, one Program.cs line, and the two test files.

## Suggested commit
```
feat: add This Week planned-workout read endpoint

GET /api/v1/training/this-week returns the current Mon–Sun (UTC) window
of the athlete's planned workouts, flattened across plans and ordered by
date, for the dashboard This Week card. Read-only service mirroring
ProfileService; empty weeks return 200 + empty list. Week math lives in
the service, computed against DateOnly.FromDateTime(DateTime.UtcNow).
```
