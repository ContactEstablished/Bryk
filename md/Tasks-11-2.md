# Task 11-2 — Weekly-load total + "Weekly Load" dashboard card

## Goal
Sum the current week's effective training load and surface it on the dashboard: extend
`ThisWeekResponse` with a weekly effective-load total (and per-workout effective load), compute it in
`ThisWeekService` using Task 11-1's calculator, and replace the "Weekly Load" placeholder card in the
Vue dashboard with a wired card. Backend (This-Week read) + frontend. No migration, no entity change.

## Depends on
- **Task 11-1** — `ILoadService` / `LoadCalculator` and the `ComputedLoad`/`EffectiveLoad`/
  `IsLoadOverride` fields on `PlannedWorkoutResponse`.
- **ADR-0005 §3** — effective-load definition and the read-cost note.

## Required reading
- `md/decisions/0005-training-load-and-execution.md` §3.
- `api/Bryk.Application/Training/ThisWeekService.cs` + `ThisWeekResponse.cs` — the Monday-week range
  logic and the `Map` that currently leaves load fields bare.
- `api/Bryk.Domain/Interfaces/ITrainingPlanRepository.cs` — note `GetPlannedWorkoutsInRangeAsync` is a
  **single-table, no-include** query; you'll add a structure-including sibling read.
- `api/Bryk.Infrastructure/Repositories/TrainingPlanRepository.cs` — the no-tracking / split-query
  include style (see `GetPlannedWorkoutWithStructureAsync`).
- `ui/src/components/dashboard/{RestingHrCard,ThisWeekCard}.vue` — the store→service card pattern;
  `ThisWeekCard` already renders `plannedLoad` as "TSS".
- `ui/src/views/HomeView.vue` — the "Weekly Load" `PlaceholderCard` to replace.
- `ui/src/stores/training.ts`, `services/training.ts`, `types/training.ts` — extend, don't fork.
- `ui/src/components/dashboard/__tests__/` — Vue test conventions.

## Acceptance criteria
- **Repo read**: add `GetPlannedWorkoutsInRangeWithStructureAsync(athleteId, start, end, ct)` to
  `ITrainingPlanRepository` + `TrainingPlanRepository` — same filter as the existing in-range read but
  `.Include(b => b.Blocks).ThenInclude(s => s.Steps)`, `.AsSplitQuery()`, no-tracking. (Leave the
  existing structure-free read in place for callers that don't need load.)
- **Service**: `ThisWeekService` uses the new read + `ILoadService` to populate each
  `PlannedWorkoutResponse`'s `ComputedLoad`/`EffectiveLoad`/`IsLoadOverride`, and adds a
  `WeeklyLoad` total on `ThisWeekResponse` = `Σ EffectiveLoad` over the week's workouts (null effective
  loads count as 0). XML summary updated.
- **Frontend**: `WeeklyLoadCard.vue` (mirror `RestingHrCard`) reads `stores/training` (reuse
  `loadThisWeek` / `thisWeek`), shows the weekly total as a TSS number with loading / empty states;
  replace the `PlaceholderCard title="Weekly Load"` in `HomeView.vue` with it. `ThisWeekCard` shows
  each session's effective load (point the existing "TSS" span at `effectiveLoad`). Extend
  `types/training.ts` with the new fields.
- **Tests**: backend — weekly total sums effective load across the week; a workout with a `PlannedLoad`
  override contributes the override. Vue (≥2) — card renders the total; empty/loading state renders.
- `dotnet test` + `pnpm run build` + `pnpm test` green; Vue test count up by ≥2.

## What NOT to modify
- Do not wire the "Form (TSB)" card — PMC is a later phase (ADR-0005 §7); leave that placeholder.
- Do not change the load formulas — Task 11-1 owns them.
- Do not add execution capture — Tasks 11-3/11-4/11-5.
- Do not remove the existing structure-free `GetPlannedWorkoutsInRangeAsync` (other callers use it).
- Do not call `fetch`/`axios` directly from the component.

## Suggested commit
```
feat: add weekly training-load total and Weekly Load card

This Week now loads the week's structured workouts (split-query include)
and sums their effective load via the 11-1 calculator, surfaced as a
weekly TSS total. Wire the WeeklyLoadCard on the dashboard and show
per-session effective load in the This Week card.
```
