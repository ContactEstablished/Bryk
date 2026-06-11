# HANDOFF — Phase 13 complete (Workout history & plan browser)

**Date:** 2026-06-11
**Phase:** 13 — Workout history & plan browser (✅ COMPLETE)
**Specs:** `md/Tasks-13-1.md` … `md/Tasks-13-5.md` (committed `5cee788`).
**Executed on the DevAuth stub** — Phase 12 (auth) is still deferred/approval-gated. All athlete
resolution flows through `ICurrentUserService`; no athlete id is ever read from a request body/query,
so the later auth swap doesn't touch this phase's code.

Phase 13 made logged training browsable and correctable end-to-end: the Workouts nav went live with a
filterable, paged history list; a workout detail page surfaces step-level planned-vs-actual (and the
long-captured `AvgPower`/`AvgPace`/`Notes`); full edit + hard-delete on workouts with load recompute;
and a plan browser that reopens the Phase-10 structure builder on existing planned workouts (closing
the carried Phase-10 gap).

## What shipped

| Task | Area | Scope | Commit |
|---|---|---|---|
| 13-1 | Backend | `PUT`/`DELETE /api/v1/workouts/{id}` — replace-style update (recompute `ComputedLoad`, `LoadOverride` passed through), hard delete (cascade, 204), both 404 on missing/foreign; additive `WorkoutResponse.TrainingPlanId` on the detail read | `f2fefc0` |
| 13-2 | Backend | `GET /api/v1/workouts` gains `from`/`to`/`sport`/`skip`/`take` (newest-first, capped) — non-breaking; records the pagination convention | `b3be03f` |
| 13-3 | Frontend | `WorkoutsView` at `/workouts` (Recent-Activity row style, sport + date-range filters, load-more); Workouts sidebar item + mobile tab flipped live | `fb08203` |
| 13-4 | Frontend | `WorkoutDetailView` at `/workouts/:id` — MetricTile strip, per-step planned-vs-actual (AvgPower/AvgPace/Notes), inline edit via `LogWorkoutForm` edit mode, delete-with-confirm | `0743c76` |
| 13-5 | Frontend | Plan browser — `PlansView` (`/plans`) + `PlanDetailView` (`/plans/:id`) reopening `WorkoutStructureBuilder`; Training nav repointed to the browser | `5171aa2` |

## Verification state

- **Backend:** `dotnet build` clean (only the known design-time `System.Security.Cryptography.Xml`
  advisory). `dotnet test api/Bryk.sln` green — **99 tests** (71 application + 28 integration; was 84).
- **Frontend:** `pnpm run build` (vue-tsc) green; `pnpm test` green — **87 tests / 31 files** (was 76).
  *Run `vitest run --no-file-parallelism` for a clean exit:* under parallelism a worker process
  intermittently exits with "Errors 1 error" while every test still passes (the transient crash noted
  in memory). No-parallelism confirms 31/31 files, 87/87 tests.
- **Manual smoke (dev API + seed):** edit/delete lifecycle (log → PUT → DELETE → 404) against real
  SQL Server; list filters (sport, inclusive date range, paging page1/page2, `take=500`→cap 9);
  detail view of a seeded linked Bike workout (planned-vs-actual with AvgPower, Notes); UI edit
  (duration 2400→1200 ⇒ load 48.5→24.25) and UI delete-with-confirm (row removed); plan browser list
  → detail → reopen builder (existing structure loaded); structure PUT→GET round-trip survives reload.

## Success criteria (ROADMAP Phase 13) — checked against the running app

- **Workouts nav live** — ✅ link renders without the "soon" badge; navigates to `/workouts`.
- **List filters + paginates** — ✅ sport + date-range filters and skip/take paging verified (API +
  Vitest). *Note:* the dev seed has **9** completed workouts, so the "Load more" button (shown only
  when a full 20-row page returns) doesn't surface in the UI; paging/cap correctness is covered by the
  API smoke and the store unit tests.
- **Detail shows planned-vs-actual incl. AvgPower/AvgPace/Notes** — ✅ (seeded Bike VO2 workout).
- **Editing duration/RPE changes ComputedLoad on save** — ✅ (48.5→24.25 on a duration halve).
- **Delete removes from list + dashboard feed** — ✅ (list row removed; `deleteWorkout` also refreshes
  the `recentWorkouts` slice the dashboard reads).
- **Plan browser reopens, edits, saves a structure that survives reload** — ✅ (reopen + existing-
  structure load verified in-browser; save→reload survival verified via the unchanged Phase-10
  structure PUT/GET on a throwaway planned workout).

## Decisions made

- **Hard delete** is the v1 default (soft delete would be a migration → approval; explicitly out).
- **Pagination convention (recorded in `Tasks-13-2.md`):** `skip` (offset, default 0, clamped ≥ 0) +
  `take` (page size, default 20, clamped 1..100); out-of-range clamps, never rejects; newest-first
  (`CompletedDate` desc, `CreatedAt` desc tiebreak); bare array response (no envelope/total-count).
  **Every later list endpoint (14–18) follows this.**
- **`WorkoutResponse.TrainingPlanId` (additive, nullable)** — populated only on the single-workout
  detail `GetAsync` (resolved from the linked planned workout); `null` on list reads (keeps them
  single-table) and unlinked workouts. Lets the detail view reach `GET .../structure` for
  planned-vs-actual **without** fattening `WorkoutResponse` with planned blocks.
- **Replace-style PUT semantics:** the whole step-result list is replaced; `ComputedLoad` is
  **recomputed from the edited actuals on every update** via the unchanged 11-1 `LoadCalculator`;
  `LoadOverride` is written through verbatim — the edit form pre-fills it so a normal round-trip
  preserves it, and blanking the field clears it. `EffectiveLoad = LoadOverride ?? ComputedLoad` is
  always derived, never stored.
- **IA:** the **Training** sidebar item now points at the plan **browser** (`/plans`); the create-plan
  form stays at `/training` and is reached via "New plan" on the browser (nav-item count unchanged).

## New backend surface (no migration, no new packages)

- `IWorkoutService.UpdateAsync`/`DeleteAsync`/`GetWorkoutsAsync` (replaced `GetRecentAsync`);
  `IWorkoutRepository.GetByIdTrackedAsync` + `GetByAthleteFilteredAsync` (replaced
  `GetRecentByAthleteAsync`). `UpdateWorkoutRequest` + `UpdateWorkoutRequestValidator` mirror the log
  request. `WorkoutsController` gains `PUT`/`DELETE` and a filtered `GET`.

## Known gaps / carry-forward

- **Seed has 9 completed workouts, not 20+** — enough to exercise filters but not the "Load more"
  button. If a later phase wants to demo paging in-UI, bump the seed past 20 rows.
- **Preview/transition note:** the headless preview tab freezes `requestAnimationFrame`, so the
  `<Transition mode="out-in">` route animation stalls there; driving views in-preview needs a
  `requestAnimationFrame = setTimeout` shim. **Not a product bug** — real browsers paint normally.
- **Structure re-save vs executed history:** `SetStructureAsync` stage-deletes `WorkoutStep`s; a
  `WorkoutStepResult.WorkoutStepId` FK is `NoAction`/Restrict (ADR-0005), so re-saving the structure of
  a planned workout whose steps are already referenced by a *completed* workout's step results could
  hit a FK restriction. Not triggered by Phase 13 (browse + structure-edit only), but worth hardening
  when structure editing meets executed history (Phase 18/19).
- **CLAUDE.md tech-debt list** (DbUpdateException→409, NotImplemented→501, ProblemDetails,
  per-version SwaggerDoc) untouched by Phase 13.

## Next — Phase 14 (Daily-load history & PMC engine)

Compute-on-read CTL/ATL/TSB/ACWR over Phase 11's `EffectiveLoad`, reusing the 13-2 date-range list
surface; lights up the dashboard "Form (TSB)" tile. Needs the **PMC computation-strategy ADR** before
code (see ROADMAP Phase 14 *Decisions needed*). Authentication (Phase 12) remains the declared
approval-gated phase and is still deferred.

## Session-start checklist

1. Read this handoff + `md/Tasks-13-*.md` and the ROADMAP Phase 14 entry.
2. `git status` clean; `git log --oneline -12`.
3. Backend: `dotnet test api/Bryk.sln` (expect 99). Frontend (from `ui/`): `pnpm run build` +
   `pnpm exec vitest run --no-file-parallelism` (expect 87; re-run plain `pnpm test` once if the
   transient worker crash appears with all tests passing).
4. `dotnet user-secrets list` (from `api/Bryk.API/`) shows `ConnectionStrings:DefaultConnection` +
   `DevAuth:CurrentAthleteId`. Seed: paste the athlete id into `db/dev-seed.sql` and run it.
5. Dev stack: start the API (`dotnet run` from `api/Bryk.API`, listens on `https://localhost:60129` /
   `http://localhost:60130`); `pnpm dev` from `ui/` (vite proxies `/api` → 60129).
