# Task 8-1 — Profile read endpoints

## Goal
Expose three GET endpoints that return an athlete's saved onboarding data — the Required identity fields, the Recommended thresholds + HR fields, and the Goals (events + goals). Unblocks Task 8-2 (the Vue Profile surface) and Tasks 8-4 / 8-5 (dashboard card population).

Backend-only task. No DTO mutations, no migration, no UI work.

## Current code/status

- Phase 4 shipped POST endpoints for the three onboarding steps at `api/v1/onboarding/required`, `/recommended`, `/goals` plus a flags-only `GET /onboarding/status`. There are no GET endpoints for the saved data.
- `OnboardingStatusResponse` is intentionally flags-only ("No echoed data" per Phase 4 handoff). Don't extend it — add new endpoints instead.
- Existing repositories already expose the needed reads:
  - `IAthleteRepository.GetByIdAsync(athleteId, ct)` for Required fields.
  - `IAthleteRepository.GetWithSportProfilesAsync(athleteId, ct)` for Recommended (HR fields on Athlete + collection of `AthleteSportProfile`).
  - `IEventRepository.GetByAthleteIdAsync(athleteId, ct)` for events.
  - `IGoalRepository.GetByAthleteIdAsync(athleteId, ct)` for goals.
- Identity: `ICurrentUserService.GetCurrentAthleteId()`, same as `OnboardingService`.
- API versioning: all three endpoints land under `/api/v1/profile/...` with `[ApiVersion("1.0")]`, `[Route("api/v{version:apiVersion}/[controller]")]`.

## DTO shape decision required in this task

Two reasonable shapes for the response types:

- **(a)** Reuse `OnboardingRequiredRequest`, `OnboardingRecommendedRequest`, `OnboardingGoalsRequest` directly as response bodies. Same data; saves DTOs. Naming is mildly off (using `*Request` types for read responses).
- **(b)** Define dedicated `ProfileRequiredResponse`, `ProfileRecommendedResponse`, `ProfileGoalsResponse` DTOs that mirror the request shapes today and can drift later if read-only metadata (CreatedAt, etc.) is added.

**Recommendation: (b)** — naming honesty is worth a small amount of duplication. The DTOs are 8-line records; the duplication cost is minimal and the architectural clarity (Request types are write-side, Response types are read-side) compounds over the rest of v1.

DTOs go under `api/Bryk.Application/Profile/` (new folder, mirrors the Onboarding folder structure).

## Acceptance criteria

**Application layer:**
- New folder `api/Bryk.Application/Profile/` with:
  - `IProfileService.cs` — three methods (`GetRequiredAsync`, `GetRecommendedAsync`, `GetGoalsAsync`) all taking only `CancellationToken` (athlete identity from `ICurrentUserService`, never from caller).
  - `ProfileService.cs` — primary-constructor service consuming `ICurrentUserService`, `IAthleteRepository`, `IEventRepository`, `IGoalRepository`. No `IUnitOfWork` needed (read-only). No `DbContext` access.
  - `ProfileRequiredResponse.cs` — mirrors `OnboardingRequiredRequest` shape: `Name`, `Gender`, `DateOfBirth`, `HeightCm`, `WeightKg`, `YearsTraining`, `TypicalWeeklyHours`, `Methodology`, plus `RestingHr` and `MaxHr` (currently part of the Athlete entity but submitted via Recommended).

    Decide explicitly: do HR fields go on the Required response (because they live on Athlete) or the Recommended response (because they're submitted there)? Recommendation: **Recommended** — the response shape mirrors the *submission* surface for parity, not the entity storage layout. Document in the file.
  - `ProfileRecommendedResponse.cs` — mirrors `OnboardingRecommendedRequest`: `RestingHr`, `MaxHr`, `SportThresholds` (list of `SportThresholdsDto` — reuse the existing one).
  - `ProfileGoalsResponse.cs` — mirrors `OnboardingGoalsRequest`: `Events` (list of `EventDto`), `Goals` (list of `GoalDto`). Reuse existing `EventDto` and `GoalDto`.

**API layer:**
- New `api/Bryk.API/Controllers/ProfileController.cs`:
  - `[ApiController]`, `[ApiVersion("1.0")]`, `[Route("api/v{version:apiVersion}/[controller]")]`.
  - Three endpoints: `GET /required` → `ProfileRequiredResponse`, `GET /recommended` → `ProfileRecommendedResponse`, `GET /goals` → `ProfileGoalsResponse`. All return 200 with the body, or **404** if no Athlete row exists for the current user (i.e., they haven't completed onboarding's Required step yet).
  - XML `<summary>` doc on each endpoint per convention.
  - Thin controller — service does the work; no try/catch.

**DI:**
- `api/Bryk.API/Program.cs` — register `IProfileService` → `ProfileService` (scoped).

**Tests:**
- `api/Bryk.Application.Tests/Profile/ProfileServiceTests.cs` — at least:
  - Returns saved Required data after submission.
  - Returns 404-equivalent (whatever the service signals — null return vs exception, decide explicitly) when athlete row is missing.
- `api/Bryk.API.Tests/Profile/ProfileControllerTests.cs` — at least one integration test per endpoint exercising the happy path against `WebApplicationFactory<Program>`.

**Build / test:**
- `dotnet build api/Bryk.sln` green.
- `dotnet test api/Bryk.sln` green — test count grows by ≥4 tests.

## Files likely to change/add

- `api/Bryk.Application/Profile/IProfileService.cs` (new)
- `api/Bryk.Application/Profile/ProfileService.cs` (new)
- `api/Bryk.Application/Profile/ProfileRequiredResponse.cs` (new)
- `api/Bryk.Application/Profile/ProfileRecommendedResponse.cs` (new)
- `api/Bryk.Application/Profile/ProfileGoalsResponse.cs` (new)
- `api/Bryk.API/Controllers/ProfileController.cs` (new)
- `api/Bryk.API/Program.cs` — one DI line added.
- `api/Bryk.Application.Tests/Profile/ProfileServiceTests.cs` (new)
- `api/Bryk.API.Tests/Profile/ProfileControllerTests.cs` (new)

## What NOT to modify

- Do not change anything in `Bryk.Application/Onboarding/` — Phase 4 surface is locked.
- Do not extend `OnboardingStatusResponse` — it stays flags-only.
- Do not introduce new repositories — existing ones cover all needed reads.
- Do not introduce write endpoints under `/profile/*` — writes still flow through onboarding POSTs (upsert / append). Phase 8 deliberately uses the existing write surface to avoid a parallel API.
- Do not touch any UI files — Task 8-2 wires the surface.
- Do not introduce validators for the responses (responses don't need validation).
- Do not add `Strength` to the `Sport` enum — that's Phase 9.

## Test plan

1. `dotnet build api/Bryk.sln` green.
2. `dotnet test api/Bryk.sln` green; new tests visible in the count.
3. Manual smoke against running API:
   - Fresh `DevAuth:CurrentAthleteId` GUID → `GET /api/v1/profile/required` returns 404 (no athlete yet).
   - Complete onboarding Required step → `GET /api/v1/profile/required` returns 200 with the submitted values.
   - Complete Recommended → `GET /api/v1/profile/recommended` returns 200 with HR + sport thresholds.
   - Complete Goals → `GET /api/v1/profile/goals` returns 200 with events + goals.
4. `git diff --stat` — no Onboarding files modified, no UI files touched.

## Suggested commit

Single commit:

```
feat: add profile read endpoints (Required / Recommended / Goals)

Three GET endpoints under /api/v1/profile/ return saved onboarding
data for the current athlete. Backend-only — unblocks Task 8-2's
Vue Profile surface and Tasks 8-4 / 8-5 dashboard card population.

Dedicated Response DTOs under Bryk.Application/Profile/ rather than
reusing the onboarding Request types. Reads via existing repositories;
no DbContext access, no UnitOfWork needed.
```
