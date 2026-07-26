# HANDOFF — Phase 20 complete (Wellness metrics)

**Date:** 2026-07-26
**Phase:** 20 — Wellness metrics (sleep, RHR, weight, soreness, HRV) (✅ COMPLETE)
**Decision:** **ADR-0011** — `md/decisions/0011-wellness-metrics.md` (Accepted 2026-07-26), written as
Task 20-1's first step, before any code, as the phase prompt required.
**Specs:** `md/Tasks-20-1.md` … `md/Tasks-20-4.md` plus `md/Impl-20-1.md` … `md/Impl-20-4.md`.
**Executed on the DevAuth stub** — Phase 12 (auth) is still deferred/approval-gated. All athlete
resolution flows through `ICurrentUserService`; no athlete id is read from a request/query/route.

Phase 20 turns the dashboard's Sleep placeholder into a real tile and gives Resting HR a history trend,
from manual daily entry. **Exactly one migration** (`AddDailyWellness`) and **zero** new packages —
neither NuGet nor npm.

## What shipped

| Task | Area | Scope | Commit |
|---|---|---|---|
| specs | Docs | `Tasks-20-1..4` + `Impl-20-1..4` + the phase prompt; ROADMAP Phase 20 decisions recorded | `da996fc` |
| 20-1 | Backend | **ADR-0011**; `DailyWellness` entity (7 nullable metrics, no navs, no FK); `IDailyWellnessRepository` (4 methods, complete surface) + `DailyWellnessRepository` (deliberately tracked per-day read); `ApplicationDbContext` `DbSet` + config with the unique composite index; migration `AddDailyWellness`; one `Program.cs` DI line; 7 repository facts | `5f92b6e` |
| 20-2 | Backend | 2 request DTOs + `WellnessResponses.cs` (4 shapes); `WellnessEntryRequestValidator` + `WellnessRangeRequestValidator`; pure `WellnessSummaryCalculator`; `IWellnessService` + `WellnessService` (the read-then-update upsert); `WellnessController` (3 actions, `{date:datetime}` constraint); one `Program.cs` DI line; 53 unit + 14 integration facts | `699718d` |
| 20-3 | Frontend | `types/wellness.ts`, `services/wellness.ts`, `schemas/wellness.ts`, `stores/wellness.ts`; **`ScaleSelector.vue`** (the extraction) + `RpeSelector.vue` rewritten as a wrapper; `WellnessQuickEntryCard.vue`; 25 Vitest specs across 4 files | `eeb968f` |
| 20-4 | Frontend | pure `lib/wellness.ts`; `SleepCard.vue`, `WeightCard.vue`, `HrvCard.vue`; `RestingHrCard.vue` upgraded to history + fallback; `HomeView.vue` composition (placeholder replaced, wellness row added); 24 Vitest specs across 5 files | `eb96784` |

## Verification state

- **Backend:** `dotnet build api/Bryk.sln --no-incremental` green, **0 errors, 16 warnings** — unchanged
  from the phase-start baseline (14 design-time `System.Security.Cryptography.Xml` NU1903 + the two
  pre-existing `WorkoutsControllerTests.cs:121,150` nullable warnings, deliberately not fixed).
  `dotnet test api/Bryk.sln` green — **417 tests** (249 `Bryk.Application.Tests` + 168 `Bryk.API.Tests`;
  was **343** at phase start, **+74**).
- **Frontend:** `pnpm run build` (`vue-tsc -b && vite build`) green. `pnpm exec vitest run
  --no-file-parallelism` green — **337 tests / 69 files** (was **288 / 61**, **+49 / +8 files**).
- **Migration applied** to the dev database (`Server=IRONMAN;Database=Bryk`) after review and explicit
  approval, per the CLAUDE.md Sr. Dev gate.

## Runtime gates — what was actually observed

Everything below was run against the **real dev stack** (API on `https://localhost:60129` with
`ASPNETCORE_ENVIRONMENT=Development`, Vite on `5273`), i.e. against **SQL Server with the unique index
live**, not the InMemory test provider.

### After 20-2 — HTTP smoke (19 rows)

| Check | Observed |
|---|---|
| `GET /summary` on a fresh athlete | **200**, `hasAnyEntries: false`, every `average: null` (**not** `0`), `days: []` |
| `PUT /{yesterday}` `{sleepHours:7.5,restingHr:48,soreness:3}` | **200**, body echoes all three, non-empty `id` |
| `PUT` same date `{sleepHours:8,restingHr:47}` | **200**, **same `id`**, and `soreness` now `null` — in-place update *and* whole-day replacement |
| `GET ?from=&to=` that date | **200**, **exactly one** entry, `sleepHours: 8`, `restingHr: 47`, `soreness: null` |
| `PUT` `{}` | **400** `"Entry: At least one metric is required."` |
| `PUT /{tomorrow}` | **400** `"Date: A wellness entry cannot be in the future."` |
| `PUT /not-a-date` | **404** — the `{date:datetime}` route constraint, before binding |
| `PUT /0001-01-01` | **400** `"Date: A valid date is required (yyyy-MM-dd)."` — the validator, the second layer |
| Each out-of-range metric (6 rows) | **400**, each with its own field-prefixed message (`SleepHours:`, `SleepQuality:`, `RestingHr:`, `WeightKg:`, `Soreness:`, `HrvMs:`) |
| `notes` at 1001 chars | **400** `"Notes: Notes must be 1000 characters or fewer."` |
| `GET` with no bounds | **400**, `errors` = `["from is required.","to is required."]` |
| `GET ?from>to` | **400** `"from must be on or before to."` |
| `GET /summary` after logging | **200**, `hasAnyEntries: true`, real average, `delta: null` (no prior week) |

### SQL-level fences (the only place uniqueness is genuinely exercised)

| Check | Observed |
|---|---|
| `Athletes` row before / after the whole smoke | `48\|74.50` → `48\|74.50` — **byte-identical** (ADR-0011 §1) |
| `DailyWellness` rows for the repeatedly-PUT date | **1**, after ~10 PUTs |
| `sys.indexes` on `DailyWellness` | `IX_DailyWellness_AthleteId_Date`, **`is_unique = 1`** |
| Direct duplicate `{AthleteId, Date}` INSERT | **rejected, error 2601** (duplicate key in unique index) |

### After 20-4 — dashboard, in the browser

Submitting the Today card updated **all four tiles plus the card itself from server truth, with no
reload** (the store re-fetches both reads):

| Tile | Before submit | After submit |
|---|---|---|
| Sleep Avg | `7.0h · 1 night logged` | **`7.3h · 2 nights logged`** — (7 + 7.5)/2 = 7.25 → `7.3` |
| Resting HR | `From profile · log RHR to see a trend` | **`46 bpm · 7-day average`** — the logged mean of 47/44, **not** the onboarding constant 48 |
| Weight | `—` + `Log weight to see a trend` | **`74.4 kg · 7-day average`** |
| HRV | `—` + `Log HRV to see a trend` | **`86 ms · 2 days logged`** |
| Today card | `No wellness logged today.` | `7.5 h · Q4 · 44 bpm · 74.2 kg · Sore 3 · HRV 88` + `Edit` |

Other observations: the expanded form renders a **5**-button sleep-quality scale and a **10**-button
soreness scale (the `max` prop is wired, not defaulted); the entry **survived a full page reload**;
console **clean** (zero errors); and the two-point sparkline rule was observed directly — with one
logged day the Resting HR / Weight / HRV tiles rendered a number and **no** line, and each grew a
sparkline only once a second day was logged.

**Tailwind literal-class guard, proven at build time rather than by eye:** the production CSS bundle
contains both `.grid-cols-10{grid-template-columns:repeat(10,minmax(0,1fr))}` and
`.grid-cols-5{...repeat(5...)}`. An interpolated `grid-cols-${max}` would have generated neither.

## Success criteria (ROADMAP Phase 20) — checked

| Criterion | Status |
|---|---|
| Today's entry persists, survives reload | ✅ Observed — reloaded and re-rendered from server state |
| Re-submit updates, not duplicates (upsert proven) | ✅ Observed — same `id` on second PUT; GET returns **one** row; DB `COUNT(*) = 1` after ~10 PUTs; **row count read, not inferred** |
| Sleep tile shows real 7-day avg + sparkline | ✅ Observed — `7.3h` + sparkline, no "soon" badge, placeholder gone |
| Resting HR reflects entries, not the onboarding constant | ✅ Observed — `46 bpm` from logged 47/44 with a sparkline; falls back to the profile value (and says so) only when nothing is logged |
| Out-of-range and future dates rejected with field messages | ✅ Observed — all six metrics + notes + future date + `0001-01-01`, each with its field prefix |

## Decisions held (ADR-0011)

- **§1 no write-back.** `WellnessService` takes no `IAthleteRepository` (asserted structurally by a
  test), the `Athletes` row was byte-identical across the whole runtime smoke, and
  `Athlete.cs` / `OnboardingService.cs` / `ProfileService.cs` are absent from the phase diff. The
  read-only Resting HR fallback shipped; **weight deliberately has none**, with the reasoning in a
  comment in `WeightCard.vue`.
- **§2 one wide nullable row, service-side upsert.** No test anywhere asserts that a duplicate insert
  throws; idempotency is proven by counting rows through the API. The index is verified by reading the
  migration and, at runtime, by SQL Server rejecting a hand-written duplicate.
- **§3 no HRV in the PMC.** `PmcCalculator.cs`, `AcwrCalculator.cs`, `LoadCalculator.cs`,
  `TimeInZoneCalculator.cs` and `AnalyticsService.cs` are absent from the phase diff.
- **§4 generalize, don't duplicate.** `RpeSelector.vue` is a 20-line wrapper; `LogWorkoutForm.vue` and
  `RpeSelector.spec.ts` were never edited and their tests pass unchanged — the regression gate.
- **§5 `DeltaChip` not recoloured.** `DeltaChip.vue`, `MetricTile.vue` and `Sparkline.vue` are absent
  from the phase diff. Sleep and HRV pass `delta`; Resting HR and Weight pass none and use the footer,
  each with a spec asserting `DeltaChip` is **absent**.
- **§6 one migration.** `AddDailyWellness` only: one `CreateTable`, one unique `CreateIndex`, zero
  `AddForeignKey`, `Down` = a single `DropTable`. No `Athlete` change.

## Known gaps / carry-forward

1. **`PUT /wellness/{date}` accepts a date-with-time segment.** `…/2026-07-25T10:00:00` returns **200**
   and writes to `2026-07-25`, where `Impl-20-2.md`'s optional smoke row 19 predicted 400/404. The
   spec's premise was wrong, not the code: ASP.NET Core's `DateOnly` model binder accepts an ISO
   datetime and truncates the time, so the URL canonicalises to a valid, non-future date. **No wrong
   date reaches the database** and both mandatory layers still hold (`not-a-date` → 404,
   `0001-01-01` → 400). Tightening it would need a stricter route constraint, which is outside the
   Phase 20 contract — left as-is deliberately.
2. **`HrvCard` says "1 days logged".** No singular/plural handling, unlike `SleepCard`'s
   "night/nights". The string is pinned verbatim by `Tasks-20-4.md` and its spec, so it was left alone
   rather than deviating from the contract mid-phase. Cosmetic; a one-line fix whenever that file is
   next touched.
3. **`PlaceholderCard.vue` is now unused.** `HomeView` was its only importer and Phase 20 removed that
   import. The file was **deliberately kept** — deleting a pre-existing component is outside the task
   fence (CLAUDE.md "surgical changes"). Delete it in a standalone cleanup if nothing else claims it.
4. **The `WellnessQuickEntryCard` submit specs poll with `vi.waitFor`, not a fixed flush budget.** The
   spec docs prescribed ~6 `flushPromises()`; that proved **flaky across runs** for a refined zod
   schema (a valid submit re-validates the whole object, and each refine adds a microtask hop). The
   repo's own precedent (`LogWorkoutForm.spec.ts:35`, `GoalsGoalForm.spec.ts:33`) is `vi.waitFor`, and
   the file now uses it throughout — verified stable over three consecutive runs. **Prefer `vi.waitFor`
   over tick-counting for any future refined-schema submit spec.**
5. **Soreness has no dashboard tile.** Captured and shown in the Today card's collapsed summary only.
   If it earns a tile, ADR-0011 §5 puts its change in the footer like the other inverted metrics.
6. **No wellness history view.** There is no `/wellness` route; the dashboard is the whole surface. A
   history/table view is a later phase.
7. **Carried from earlier phases, still open:** the **Phase 18 POST/PUT periodization validator bounds
   divergence** (`TrainingPlanRequestValidator` accepts `BuildWeeks > 0` / `RecoveryWeeks > 0` /
   `RecoveryWeekPercentage` 0–100 while `TrainingPlanUpdateRequestValidator` bounds them 1–8 / ≥ 1 /
   30–90) — **needs a Sr. Dev decision, deferred three times now**; the Phase 19 zone histogram being
   JSON on `ActivityFile` rather than a normalized table (Phase-21 candidate);
   `ui/src/lib/charts/load.ts:65` labelling the last bar `· NOW`; and the duplicated zone-bar markup
   between `ZoneHistogramBars.vue` and `TimeInZoneSection.vue` plus the duplicated `%HRmax` scheme
   between `ZoneHistogramCalculator` and `TimeInZoneCalculator`.
8. **ROADMAP doc drift (pre-existing, now the only one left):** the Phase 16 *heading* reads `⏳`
   although its ledger row reads `✅`. Carried from Phases 17–19; Phase 20 flipped **both** of its own
   markers, so this is the sole remaining mismatch.

## Files added by Phase 20

**Backend (11 new, 3 modified)**
- `md/decisions/0011-wellness-metrics.md`
- `api/Bryk.Domain/Entities/DailyWellness.cs`, `api/Bryk.Domain/Interfaces/IDailyWellnessRepository.cs`
- `api/Bryk.Infrastructure/Repositories/DailyWellnessRepository.cs`
- `api/Bryk.Infrastructure/Migrations/20260726191032_AddDailyWellness.cs` (+ `.Designer.cs`)
- `api/Bryk.Application/Wellness/` — `WellnessEntryRequest.cs`, `WellnessRangeRequest.cs`,
  `WellnessResponses.cs`, `WellnessSummaryCalculator.cs`, `IWellnessService.cs`, `WellnessService.cs`,
  `Validators/WellnessEntryRequestValidator.cs`, `Validators/WellnessRangeRequestValidator.cs`
- `api/Bryk.API/Controllers/WellnessController.cs`
- *modified:* `ApplicationDbContext.cs` (two additive blocks), `ApplicationDbContextModelSnapshot.cs`
  (regenerated), `Program.cs` (**two** lines total — one repo, one service)

**Backend tests (4 new)** — `api/Bryk.API.Tests/Wellness/DailyWellnessRepositoryTests.cs`,
`WellnessControllerTests.cs`; `api/Bryk.Application.Tests/Wellness/WellnessEntryRequestValidatorTests.cs`,
`WellnessSummaryCalculatorTests.cs`, `WellnessServiceTests.cs`

**Frontend (9 new, 2 modified)** — `types/wellness.ts`, `services/wellness.ts`, `schemas/wellness.ts`,
`stores/wellness.ts`, `lib/wellness.ts`, `components/common/ScaleSelector.vue`,
`components/wellness/WellnessQuickEntryCard.vue`, `components/dashboard/SleepCard.vue`,
`WeightCard.vue`, `HrvCard.vue`; *modified:* `components/common/RpeSelector.vue` (now a wrapper),
`components/dashboard/RestingHrCard.vue`, `views/HomeView.vue`

**Frontend tests (8 new, 1 extended)** — `ScaleSelector.spec.ts`, `services/wellness.spec.ts`,
`stores/wellness.spec.ts`, `WellnessQuickEntryCard.spec.ts`, `lib/wellness.spec.ts`,
`SleepCard.spec.ts`, `WeightCard.spec.ts`, `HrvCard.spec.ts`; *extended:* `RestingHrCard.spec.ts`
(3 original cases untouched, 4 added)

## Phase 20 closeout checklist

- [x] ROADMAP Phase 20 **heading** flipped `⏳` → `✅`
- [x] ROADMAP Phase 20 **ledger row** flipped `🚧 Specs ready` → `✅ Complete` (both, deliberately — the
      Phase 16 drift is exactly this going wrong)
- [x] ROADMAP Phase 20 entry gained a **Delivered 2026-07-26** paragraph
- [x] `md/handoffs/2026-07-26-phase-20-complete.md` written (this file)
- [x] CLAUDE.md phase pointer updated to "Phase 20 complete"
- [x] **ADR-0011** indexed in CLAUDE.md's decision list
- [x] Exactly **one** migration, **zero** new packages
- [x] No AI co-author trailer on any commit

## Next — Phase 21 (Production hardening) or Phase 12 (Auth)

**Phase 21** is the last planned phase: ProblemDetails (RFC 9457) on every path, `DbUpdateException` →
409, `NotImplementedException` → 501, observability, containerization, a deployment target, and
tech-debt burn-down. Note it inherits the CLAUDE.md tech-debt list, several items of which Phase 20
touched the edges of but deliberately did not change (the error contract is frozen until then).

**Phase 12 (Auth)** remains deferred and **approval-gated**. Nothing in Phase 20 writes auth code; every
wellness read and write resolves the athlete through `ICurrentUserService`, so the swap to a real
`ClaimsPrincipal` implementation needs no change in this phase's surface.

## Session-start checklist

Clean tree; `dotnet build api/Bryk.sln` + `dotnet test api/Bryk.sln` green (**417**: 249 + 168, **16**
warnings on a clean compile — an *incremental* build reports 14 because it skips recompiling
`Bryk.API.Tests`, so compare like for like); `pnpm run build` + `pnpm exec vitest run
--no-file-parallelism` green from `ui/` (**337 / 69 files**); user-secrets present
(`ConnectionStrings:DefaultConnection`, `DevAuth:CurrentAthleteId` — from `api/Bryk.API/`); seed
`db/dev-seed.sql`. Vitest's transient worker-fork crash reporting "Errors N" with every test passing →
re-run once before debugging. If you use the in-app Browser pane, **shim
`window.requestAnimationFrame`** before reading any numeric tile — `useCountUp` drives them, the pane
freezes rAF, and the tiles render **blank** (not `0`, not `—`); screenshots time out there too, so read
`document.body.innerText` instead.
