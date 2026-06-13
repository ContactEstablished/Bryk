# HANDOFF — Phase 14 complete (Daily-load history & PMC engine)

**Date:** 2026-06-12
**Phase:** 14 — Daily-load history & PMC engine (CTL / ATL / TSB / ACWR) (✅ COMPLETE)
**Decision:** `md/decisions/0006-pmc-computation.md` (Accepted 2026-06-12).
**Specs:** `md/Tasks-14-1.md` … `md/Tasks-14-4.md` (committed `b7215c8`).
**Executed on the DevAuth stub** — Phase 12 (auth) is still deferred/approval-gated. All athlete
resolution flows through `ICurrentUserService`; no athlete id is read from a request/query, so the
later auth swap doesn't touch this phase's code.

Phase 14 turns Phase 11's per-workout `EffectiveLoad` into the Performance Management Chart —
deterministic, compute-on-read CTL/ATL/TSB and the ACWR injury-risk ratio — and lights up the dashboard
"Form (TSB)" placeholder plus a Weekly Load ACWR chip. **No migration, no new packages, no snapshot
table** (compute-on-read per ADR-0006).

## What shipped

| Task | Area | Scope | Commit |
|---|---|---|---|
| ADR-0006 | Docs | Compute-on-read (no snapshot); 180-day seeded lookback; `current` = range last day, null for a fresh athlete; TSB bands > +10 / ±10 / < −10 | `d2dcc04` |
| 14-1 | Backend | Pure `PmcCalculator` (42-day CTL / 7-day ATL EWMA, yesterday-offset TSB) + `AcwrCalculator` (7:28, null < 28 days) in `Bryk.Application/Analytics/` + xUnit | `acf3131` |
| 14-2 | Backend | `IAnalyticsService`/`AnalyticsService` (group-by-date, sum `EffectiveLoad`, zero-fill, delegate) + `AnalyticsController` (`GET /analytics/daily-load`, `/pmc`) + range validator + additive `GetFirstWorkoutDateAsync` + integration tests | `2fd0e2e` |
| 14-3 | Frontend | Live Form (TSB) tile (signed TSB, DeltaChip vs 7d, Fresh/Neutral/Fatigued) + Weekly Load ACWR chip; `services/analytics.ts` + Pinia slice + `types/analytics.ts`; additive `MetricTile` `signed` prop; Vitest | `068ac0a` |
| 14-4 | Both | End-to-end verification pass against seed + fresh athlete (no polish needed → no code commit) | — |

## Verification state

- **Backend:** `dotnet build` clean (only the known design-time `System.Security.Cryptography.Xml`
  advisory). `dotnet test api/Bryk.sln` green — **119 tests** (81 application + 38 integration; was 99).
- **Frontend:** `pnpm run build` (vue-tsc) green; `pnpm test` green — **97 tests / 33 files** (was 87/31).
  *Run `vitest run --no-file-parallelism` for a clean exit* (the transient worker crash noted in memory).
- **Live end-to-end (dev API on SQL Server `IRONMAN` + `db/dev-seed.sql`, athlete `…112`, today 2026-06-12):**
  - **pmc sanity:** `GET /analytics/pmc?from=2026-03-14&to=2026-06-12` → 91-day contiguous series,
    `current.date = 2026-06-12`. EWMA hand-verified: TSB(06-12) −18.15 = CTL(06-11) 12.21 − ATL(06-11)
    30.36 (yesterday-offset exact); CTL(06-12) 11.92 = 12.21 − 12.21/42; ATL(06-12) 26.02 = 30.36 − 30.36/7.
  - **daily-load zero-fill:** 06-07/06-08 (no workouts) report load 0 between 06-06 (118.4) and 06-09
    (36.8) — contiguous, no skipped dates.
  - **TSB moves on log/delete (Phase 13 endpoints):** logging a 120-TSS Bike dated **06-11** dropped
    today's TSB −18.15 → −32.44 (CTL→14.71, ATL→40.71); delete restored exactly to −18.15. Confirms
    compute-on-read reads live `EffectiveLoad`. **Note (by design):** a workout dated **today** does
    *not* change *today's* TSB — TSB is yesterday's CTL−ATL by convention; it changes today's CTL/ATL,
    the daily-load series, and tomorrow's TSB.
  - **ACWR numeric path:** a back-dated workout (05-10, 60 TSS → 33 days of history) made
    `current.acwr` None → **1.24**; deleted to restore the seed.
  - **ACWR "—" path:** with the stock seed (first workout 05-25, ~18 days back) `current.acwr` is null.
  - **Dashboard render (vite dev + preview, real data):** Form (TSB) tile shows **−18 · +5.2 · Fatigued**
    (matches `current.tsb` −18.15; delta = −18.15 − (−23.39 on 06-05) = +5.2 ✓); Weekly Load shows
    **381 TSS · ACWR —**. Zero console warnings/errors.
  - **Fresh athlete:** covered by integration test `Pmc_FreshAthlete_CurrentNull_AndZeroSeries`
    (`current == null`, all-zero series); the Form tile renders "—" + "Log a workout to see your form"
    and the ACWR chip renders "—" (FormCard/WeeklyLoadCard Vitest specs). Live DevAuth is config-bound to
    one athlete, so the empty path is proven by tests rather than a live athlete swap.
  - **Seed left intact** — all verification workouts were deleted; the DB is back to its 9-workout seed.

## Success criteria (ROADMAP Phase 14) — checked

- **PMC endpoint matches hand-verifiable EWMA examples** — ✅ (xUnit constant-100→CTL≈94.45/120d, plus
  the live hand-check above).
- **Form tile shows a real TSB that changes after logging/deleting a workout and matches `current`** — ✅
  (−18 on the tile = `current.tsb`; moved on a recent-past log, restored on delete).
- **ACWR renders "—" under 28 days of history** — ✅ (stock seed); numeric path also verified via back-date.

## Decisions made (ADR-0006)

- **Compute-on-read, no `DailyLoadSnapshot` table.** Inputs already live in `Workouts`; a snapshot needs
  invalidation on every Phase-13 write for no v1 benefit. A snapshot table is a future approval-gated
  migration; hitting a perf wall mid-phase is a STOP-and-ask (not triggered).
- **Seeding window:** `computeFrom = max(min(firstWorkout, from), from − 180)`, EWMA seeded 0, sliced to
  `[from, to]`. 180 days ≫ the 42-day CTL constant (≈1.4% residual).
- **`current` = the range's last day** (= today for the dashboard's `to = today` call), **null for an
  athlete with no workouts** so the tile is honest ("—", not a 0/0/0 that reads as real form).
- **TSB derived from displayed (rounded) yesterday CTL/ATL** so the form a user reads reconciles with the
  prior day's shown numbers (EWMA recurrence still carries unrounded values — no drift).
- **TSB bands:** > +10 Fresh / −10…+10 Neutral / < −10 Fatigued.
- **Pagination/range convention reused** from 13-2 (date-range read); analytics range rules: both bounds
  required, `from ≤ to`, ≤ 400 days, no future `to`.

## New surface (no migration, no new packages)

- `Bryk.Application/Analytics/`: `PmcCalculator`, `AcwrCalculator`, `DailyLoadDto`, `PmcPointDto`,
  `PmcSummaryDto`, `PmcResponse`, `AnalyticsRangeRequest` (+ validator), `IAnalyticsService`/`AnalyticsService`.
- `IWorkoutRepository.GetFirstWorkoutDateAsync` (additive `MIN(CompletedDate)` query) + impl.
- `AnalyticsController` (`GET /api/v1/analytics/daily-load`, `/pmc`); one DI registration in `Program.cs`.
- UI: `types/analytics.ts`, `services/analytics.ts` (incl. `getDailyLoad` for Phase 15), `stores/analytics.ts`,
  `components/dashboard/FormCard.vue`, ACWR chip on `WeeklyLoadCard.vue`, additive `MetricTile` `signed` prop.

## Known gaps / carry-forward

- **`getDailyLoad` service fn is unused by Phase 14** — added for Phase 15's charts; harmless until then.
- **No charts** — the PMC/Load charts are Phase 15 (it consumes `/analytics/daily-load` + `/pmc`).
- **`current` only carries today's-equivalent ACWR**, not a per-day ACWR track — Phase 15 can subdivide
  without changing the contract (ADR-0006 §6).
- **Seed first workout is ~18 days back** → ACWR shows "—" in the stock dev dashboard. Back-date a seed
  row (or wait for the relative anchor to age) to demo the numeric chip in-UI.
- **CLAUDE.md tech-debt list** (DbUpdateException→409, NotImplemented→501, ProblemDetails,
  per-version SwaggerDoc) untouched by Phase 14.

## Next — Phase 15 (Progress page)

`ProgressView` at `/progress`: port the PMC chart (CTL/ATL lines + daily-load bars) and the 8-week Load
chart from the design export, time-in-zone (honestly labeled), and session-level peaks. Strictly after
14 (consumes `/analytics/daily-load` + `/pmc`) and 13 (history + list conventions). Needs the
optimal-band definition decision (ROADMAP Phase 15 *Decisions needed*). Authentication (Phase 12) remains
the declared approval-gated phase and is still deferred.

## Session-start checklist

1. Read this handoff + ADR-0006 + `md/Tasks-14-*.md` and the ROADMAP Phase 15 entry.
2. `git status` clean; `git log --oneline -12`.
3. Backend: `dotnet test api/Bryk.sln` (expect 119). Frontend (from `ui/`): `pnpm run build` +
   `pnpm exec vitest run --no-file-parallelism` (expect 97; re-run plain `pnpm test` once if the
   transient worker crash appears with all tests passing).
4. `dotnet user-secrets list` (from `api/Bryk.API/`) shows `ConnectionStrings:DefaultConnection` +
   `DevAuth:CurrentAthleteId`. Seed: paste the athlete id into `db/dev-seed.sql` and run it.
5. Dev stack: API (`dotnet run` from `api/Bryk.API`, https://localhost:60129 / http://localhost:60130);
   `pnpm dev` from `ui/` (vite proxies `/api` → 60129).
