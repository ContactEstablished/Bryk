# HANDOFF — Phase 18 complete (ATP / periodization engine)

**Date:** 2026-07-26
**Phase:** 18 — ATP / periodization engine (weekly targets, ramp, taper) (✅ COMPLETE)
**Decision:** **ADR-0009** — `md/decisions/0009-periodization-ramp-model.md` (Accepted 2026-07-26), written
as Task 18-1's first step, before any code, as the ROADMAP required.
**Specs:** `md/Tasks-18-1.md` … `md/Tasks-18-5.md` plus `md/Impl-18-1.md` … `md/Impl-18-5.md`.
**Executed on the DevAuth stub** — Phase 12 (auth) is still deferred/approval-gated. All athlete
resolution flows through `ICurrentUserService`; no athlete id is read from a request/query.

Phase 18 brings `BuildWeeks` / `RecoveryWeeks` / `RecoveryWeekPercentage` — dormant on `TrainingPlan`
since ADR-0003 and never written by any UI — to life: weekly load targets that ramp toward a linked
event, dip on recovery weeks, taper into race week, and are graded against the athlete's real load on
both the plan-detail panel and the dashboard. **No migration, no new NuGet package, no new npm package.**

## What shipped

| Task | Area | Scope | Commit |
|---|---|---|---|
| specs | Docs | `Tasks-18-1..5` + `Impl-18-1..5` + the phase prompt; ROADMAP `RecoveryWeekPercentage` scale correction | `4b10c51` |
| 18-1 | Backend (pure) | **ADR-0009**; `Bryk.Application/Training/Periodization/`: `WeeklyTargetCalculator` (two-pass ramp walk) + positional `WeeklyTargetInput` + `WeeklyTargetDto`; 13 xUnit facts pinning the ADR's 12-week worked example week-by-week plus the no-event, partial-cadence, mid-week-start, one-week, two-week, zero/null-baseline and ACWR-ceiling cases | `1e7463e` |
| 18-2 | Backend | `TrainingPlanUpdateRequest` + validator (1–8 / ≥1 / 30–90); `ITrainingPlanService.UpdateAsync` + impl (orphan guard → 400 `PlanWindow:`, event-ownership guard → 400 `EventId:`, fresh nav-free staging, children re-attached for projection); `PUT /api/v1/trainingplans/{id}`; 13 unit + 8 integration tests | `fa7b578` |
| 18-3 | Backend | `TargetBaselineSource` / `WeeklyTargetWeekDto` / `WeeklyTargetsResponse`; `IPeriodizationService` + `PeriodizationService` (ADR-0009 §1 baseline chain, planned/actual merge lifted verbatim from `AnalyticsService`); `GET /api/v1/trainingplans/{id}/weekly-targets`; one `Program.cs` line; 12 unit + 6 integration tests | `10070ef` |
| 18-4 | Frontend | `TrainingPlanUpdateRequest`/`WeeklyTarget*` types, `updatePlan`/`getWeeklyTargets` services, store slice (`weeklyTargets`, `loadWeeklyTargets`, `updatePlan`), `planMetadataSchema`, `PeriodizationPanel.vue` (edit form + target ramp reusing `LoadChart` unforked + week strip), `PlanDetailView` swap; 9 + 2 + 1 Vitest | `86d25d2` |
| 18-5 | Backend + Frontend | `ThisWeekResponse.TargetLoad`/`ActualLoad`, `ThisWeekService` active-plan selection + actual-load sum + target lookup; pure `lib/weeklyTarget.ts` (ADR-0008 §1 bands verbatim); target-vs-actual bar + `DeltaChip` on `ThisWeekCard`; 7 + 2 xUnit, 8 + 3 Vitest | `6b14ca3` |

## Verification state

- **Backend:** `dotnet build api/Bryk.sln` green, **0 errors**. `dotnet test api/Bryk.sln` green —
  **262 tests** (173 `Bryk.Application.Tests` + 89 `Bryk.API.Tests`; was **201** at phase start, +61).
- **Frontend:** `pnpm run build` (`vue-tsc -b`) green. `pnpm exec vitest run --no-file-parallelism`
  green — **56 test files, 252 tests** (was 53/229 at phase start, +23).
- **Warnings:** **16** on a clean compile — exactly the documented baseline, unchanged. 14 are the known
  design-time `System.Security.Cryptography.Xml` NU1903 advisory in `Bryk.Infrastructure`; the other two
  are the pre-existing `WorkoutsControllerTests.cs:121` (CS8604) and `:150` (CS8602) nullable warnings,
  deliberately not fixed. **Zero warnings from any file this phase added.** (Note for future sessions:
  an *incremental* `dotnet build` reports 14, because it skips recompiling `Bryk.API.Tests`; only a
  clean compile shows the full 16. Compare like for like before concluding the count moved.)
- **No migration.** No `DbContext` edit, no `dotnet ef` invocation, no column, no `WeeklyTarget` table.
- **No package change.** No `*.csproj` and no `package.json` diff across all five tasks.

## Runtime gates — what was actually observed

Run against the dev stack (API on SQL Server `IRONMAN\Bryk` with `db/dev-seed.sql`, UI on Vite 5273).
The seed plan is "Indian Wells 70.3 Build" (`2026-06-01 → 2026-07-27`, 3 : 1 @ 70 %).

**After 18-2 — `PUT /trainingplans/{id}` HTTP smoke:**

| Gate | Observed |
|---|---|
| Happy path (widen window + change cadence) | `200`, new values echoed, **15** planned workouts present in the PUT response (the re-attach-for-projection trap holds) |
| Unknown id | `404` |
| Orphan-stranding window `[2026-07-01, 2026-07-10]` | `400` — `"PlanWindow: 15 planned workout(s) fall outside the requested window (2026-07-01 to 2026-07-10); reschedule or remove them first (earliest 2026-06-01, latest 2026-06-24)."` |
| Foreign/unknown `eventId` | `400` — `"EventId: The selected event does not exist or belongs to another athlete."` |

**After 18-3 — `GET /trainingplans/{id}/weekly-targets`:**

- **Fresh plan** (window 180 days out, no history, no planned work) → `200` with
  `{"baseline":null,"baselineSource":"None","weeks":[]}`. **Not** a 404, **not** a row of zeros.
- **Seeded plan** → `200`, `baseline 162.56`, `baselineSource "FirstWeekPlanned"` (the seed's own
  trailing four weeks are empty, so the chain falls through to fallback #2 — a real exercise of it),
  9 weeks, ramping `162.56 → 173.94 → 186.12` with the recovery dip at `130.28` (= `186.12 × 0.70`)
  and again at `159.61` (= `228.01 × 0.70`). No taper weeks — the linked event's date is outside the
  plan window, and an out-of-window event is ignored rather than clamped.

**After 18-4 — `/plans/:id` in the browser:**

- Summary renders: name, methodology, `Jun 1, 2026 – Jul 27, 2026`, the linked event name
  ("Indian Wells 70.3"), and `3 build : 1 recovery · 70% recovery volume`.
- **Edit** opens the form pre-populated from the plan, with the field bounds live
  (`min=1 max=8`, `min=1`, `min=30 max=90`).
- **Valid save round-trips and the ramp redraws:** changing recovery volume `70 → 50` returned 200 and
  the panel re-rendered with the cadence line at `50%`, the Jun 22 week `130.28 → 93.06` and the
  Jul 20 week `159.61 → 114` (both `= build target × 0.50`), **build weeks unchanged** — the pin that
  a recovery week does not advance the ramp.
- **Window shrink that strands workouts** surfaces the server's text verbatim in the form's error
  banner: `"PlanWindow: 15 planned workout(s) fall outside the requested window (2026-07-01 to
  2026-07-27); reschedule or remove them first (earliest 2026-06-01, latest 2026-06-24)."`, and the
  form stays open so the athlete can correct it.
- Console clean (zero errors) across load, edit, save, and the rejection.

**After 18-5 — the ROADMAP success criterion, observed in the browser, not inferred:**

| | BEFORE | AFTER logging a 140 TSS run |
|---|---|---|
| label | `0 / 159.61 TSS` | `140 / 159.61 TSS` |
| bar class | `bg-bad` | `bg-good` |
| bar width / `aria-valuenow` | `0%` / `0` | `88%` / `88` |
| `DeltaChip` text | `-160 TSS` | `-20 TSS` |
| `DeltaChip` direction | `down` (`text-bad` + ArrowDown) | `flat` (`text-muted`, no icon) |

Both the bar colour **and** the chip direction flipped on log (ratio `0.00 → 0.877`, crossing the
ADR-0008 §1 `bad → good` boundary). The smoke workout was deleted afterwards and the seed plan restored
to its original values (`2026-06-01 → 2026-07-27`, 3 : 1 @ 70 %); `this-week` reads
`targetLoad 159.61 / actualLoad 0` again, and the DB holds exactly the one seeded plan it started with.

## Success criteria (ROADMAP Phase 18) — checked

- **3-build/1-recovery/60 % on a 12-week linked plan yields a visible ramp with every 4th week dipped
  and a race-week taper, reproducible via pinned unit tests** — ✅
  `WeeklyTargetCalculatorTests.Compute_TwelveWeekThreeBuildOneRecoveryWithRaceWeek_MatchesAdrWorkedExample`
  pins all 12 values, and `PeriodizationServiceTests.GetWeeklyTargetsAsync_ThreeBuildOneRecoverySixtyPercent_MatchesTheAdrVector`
  reproduces the identical vector end-to-end through the service.
- **This Week shows target vs actual flipping state on log** — ✅ observed live, table above.
- **Plan PUT round-trips from the UI** — ✅ observed live (70 % → 50 %, ramp redrew).
- **Foreign plan 404s** — ✅ `Update_ForeignPlan_Returns404`, `WeeklyTargets_ForeignPlan_Returns404`,
  plus the unit-level `KeyNotFoundException` tests on both services.

## Decisions held (ADR-0009)

- **Baseline = trailing 4-week mean actual load**, anchored on the **plan's first week**, not on today —
  so a plan's targets don't silently reshape every Monday. Divisor is a fixed **4**: empty weeks are
  load-bearing zeros. Chain: trailing actual → the plan's own first-week planned load → **no targets**.
- **Ramp = +7 %/build week**, derived from ADR-0007's locked `1.3 × A` ceiling (`1.07⁴ = 1.3108`), not
  picked from the ROADMAP's "~5–8 %" range.
- **Recovery weeks do not advance the ramp** — hence the two-pass algorithm. A one-pass rewrite that
  ramps off the scaled value produces a different, wrong series.
- **Taper overrides recovery** — a week is labelled taper *or* recovery, never both. Pinned by the
  `171.82` vs `206.18` assertion.
- **Compute-on-read** — no `WeeklyTarget` table, no migration, no cache.
- **`RecoveryWeekPercentage` is percent-scale (0–100)**; the ROADMAP's `0.3–0.9` prose was wrong and was
  corrected in `ROADMAP.md` on 2026-07-26. ADR-0009 §6 is the durable record.
- **Plan-window shrink that orphans planned workouts → 400**, extending ADR-0008 §2 to the PUT.

## Known gaps / carry-forward

1. **POST/PUT validator bounds diverge, deliberately.** `TrainingPlanRequestValidator` (POST) still
   accepts `BuildWeeks > 0`, `RecoveryWeeks > 0`, `RecoveryWeekPercentage` 0–100; the new PUT validator
   is 1–8 / ≥ 1 / 30–90. Tightening a shipped endpoint is an API breaking change and needs Sr. Dev
   approval — it was explicitly out of Phase 18 scope. **Tech debt; needs a decision.**
2. **`lib/charts/load.ts:65` labels the last bar `· NOW`**, which in the periodization panel is the
   plan's *final* week rather than the current week. A documented cosmetic artifact of reusing the
   Phase-15 chart unforked; visible live in the panel. Fixing it means either a chart prop or a fork —
   neither was in scope.
3. **Third copy of the Monday-week expression.** `date.AddDays(-(((int)date.DayOfWeek + 6) % 7))` now
   lives in `AnalyticsService.cs:186`, `ThisWeekService.cs`, `WeeklyTargetCalculator.cs`,
   `PeriodizationService.cs` and `WeeklyTargetsControllerTests.cs` — five copies. The task fence
   forbade refactoring the existing two; extracting all five into one helper is a clean standalone task.
4. **The compliance bands are now duplicated across the language boundary.** `ComplianceClassifier.cs`
   (C#) and `ui/src/lib/weeklyTarget.ts` (TS) each carry `[0.8, 1.2] / 0.5`. Intentional per Task 18-5's
   fence (copy, don't extract), but the two can now drift silently. Worth a shared constant or a
   contract test if a third consumer appears.
5. **`ThisWeekService` costs an extra plan + workout read per dashboard call** (it delegates to
   `IPeriodizationService` rather than duplicating the ramp math). Accepted for v1 and commented in the
   code; revisit only if the dashboard call shows up in profiling.
6. **`PeriodizationService`'s `PlannedLoad` is plan-scoped but `ActualLoad` is athlete-wide** — a
   completed `Workout` carries no plan attribution (ADR-0005/0007). Documented on `WeeklyTargetWeekDto`
   rather than papered over. Real attribution would need a schema change.
7. **Calendar week headers do not show the weekly target.** The ROADMAP marks it "optionally"; it was
   not in the task split. Clean follow-up.
8. **No store spec for `stores/training.ts`** — it has none today, and the `PeriodizationPanel` spec
   asserts the store contract through `createTestingPinia` spies. Flagged rather than added silently.
9. **The event picker's clear option uses a sentinel (`__none__`), not `''`** — reka-ui rejects a
   `SelectItem` whose value is the empty string. Both the sentinel and `''` map to `null` on submit.
   The Impl doc specified `''`; this is the working equivalent.
10. **Preview-pane caveat for future sessions:** the in-app Browser pane does not composite frames, so
    `requestAnimationFrame` is frozen and Vue's route `<Transition>` stalls in `page-leave-active`,
    leaving the page blank (and `innerText` empty). Screenshots time out for the same reason. The
    working recipe is to grab the stuck wrapper's `__vnode.transition`, `el.remove()`, then call
    `transition.afterLeave()`, and read `document.body.textContent`.
11. **ROADMAP doc drift (pre-existing, still open):** the Phase 16 *heading* reads `⏳` although its
    ledger row reads `✅`. Carried over from the Phase 17 handoff; still outside scope.

## Files added by Phase 18

| File | Purpose |
|---|---|
| `md/decisions/0009-periodization-ramp-model.md` | The ramp ADR: baseline, +7 %, cadence, taper, compute-on-read, orphan policy, percent scale. |
| `api/Bryk.Application/Training/Periodization/WeeklyTargetCalculator.cs` | Pure two-pass ramp walk + `WeeklyTargetInput`. |
| `…/Periodization/WeeklyTargetDto.cs` | `{ WeekStart, TargetLoad, IsRecoveryWeek, IsTaperWeek }`. |
| `…/Periodization/TargetBaselineSource.cs` | `None` / `TrailingActual` / `FirstWeekPlanned`. |
| `…/Periodization/WeeklyTargetWeekDto.cs` | Per-week merge (+ the planned/actual asymmetry comment). |
| `…/Periodization/WeeklyTargetsResponse.cs` | `{ PlanId, StartDate, EndDate, Baseline, BaselineSource, Weeks }`. |
| `…/Periodization/IPeriodizationService.cs`, `PeriodizationService.cs` | The I/O half: baseline chain + merges. |
| `api/Bryk.Application/Training/TrainingPlanUpdateRequest.cs` + `Validators/TrainingPlanUpdateRequestValidator.cs` | The metadata-only PUT body. |
| `api/Bryk.Application.Tests/Training/Periodization/WeeklyTargetCalculatorTests.cs`, `PeriodizationServiceTests.cs` | 13 + 12 facts. |
| `api/Bryk.API.Tests/Training/WeeklyTargetsControllerTests.cs` | 6 integration facts. |
| `ui/src/lib/weeklyTarget.ts` (+ spec) | Pure `buildTargetProgress`, ADR-0008 §1 bands. |
| `ui/src/components/training/PeriodizationPanel.vue` (+ spec) | Edit form + target ramp + week strip. |
| `ui/src/services/__tests__/training.spec.ts` | Pins both new URLs. |

## Phase 18 closeout checklist

- [x] ADR-0009 written **before** any code (Task 18-1 Step 1).
- [x] Pure `WeeklyTargetCalculator` + 13 pinned facts (18-1).
- [x] `PUT /trainingplans/{id}` with orphan + event-ownership guards (18-2).
- [x] `GET /trainingplans/{id}/weekly-targets` + baseline chain (18-3).
- [x] Periodization panel on plan detail, `LoadChart` reused unforked (18-4).
- [x] This Week target-vs-actual bar + `DeltaChip` (18-5).
- [x] xUnit: 262 tests. Vitest: 56 files, 252 tests. Both builds green, warnings flat.
- [x] All four runtime gates observed live; seed left as found.
- [x] Handoff doc written (`md/handoffs/2026-07-26-phase-18-complete.md`).
- [x] ROADMAP.md updated (Phase 18 → ✅; ledger + heading; status date; *Decisions needed* → closed).
- [x] CLAUDE.md phase pointer refreshed + ADR-0009 indexed.

## Next — Phase 19 (Activity file import) or Phase 12 (Auth)

**Phase 19 — Activity file import (.fit / .tcx / .gpx)** is the declared next feature phase. It
**requires a migration** (`ActivityFile`, `Workout.SourceFileId?`, `WorkoutZoneDuration`) and therefore
one reviewed migration set under the Sr. Dev gate, plus a parser dependency decision — the first new
NuGet package in several phases. It pays off Phase 15's time-in-zone honesty caveat.

**Phase 12 (Auth)** remains eligible and **approval-gated**: it needs an ADR evaluating ASP.NET Core
Identity vs hand-rolled, a table-layout decision, migration approval, OAuth wiring, and a
cookie-or-JWT decision. **All auth code requires approval before it is written.**

Worth considering before either: carry-forward **1** (the POST/PUT bounds divergence) is a small,
self-contained decision that has been deferred once and will keep resurfacing.

## Session-start checklist

1. Read this handoff + the ROADMAP Phase 19 entry (or Phase 12 if auth is next) + ADR-0009.
2. `git status` clean; `git log --oneline -8`.
3. Backend: `dotnet build api/Bryk.sln` + `dotnet test api/Bryk.sln` (expect **262**).
4. Frontend: `pnpm run build` + `pnpm exec vitest run --no-file-parallelism` (expect **252 / 56**);
   the transient worker-fork crash with all tests passing → re-run once before debugging.
5. `dotnet user-secrets list` (from `api/Bryk.API/`) shows `ConnectionStrings:DefaultConnection` +
   `DevAuth:CurrentAthleteId`. Seed: `db/dev-seed.sql`.
6. Dev stack: API from `api/Bryk.API` with **`ASPNETCORE_ENVIRONMENT=Development`** (the DevAuth stub
   throws outside Development, and `dotnet run --no-launch-profile` defaults to Production);
   `pnpm dev` from `ui/` (vite proxies `/api` → 60129).
