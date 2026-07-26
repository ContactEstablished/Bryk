# HANDOFF — Phase 19 complete (Activity file import)

**Date:** 2026-07-26
**Phase:** 19 — Activity file import (.fit / .tcx / .gpx) (✅ COMPLETE, with two documented carry-forwards)
**Decision:** **ADR-0010** — `md/decisions/0010-activity-file-import.md` (Accepted 2026-07-26), written as
Task 19-1's first step, before any code, as the ROADMAP required.
**Specs:** `md/Tasks-19-1.md` … `md/Tasks-19-6.md` plus `md/Impl-19-1.md` … `md/Impl-19-6.md`.
**Executed on the DevAuth stub** — Phase 12 (auth) is still deferred/approval-gated. All athlete
resolution flows through `ICurrentUserService`; no athlete id is read from a request/query/route.

Phase 19 turns a device file into a `Workout` with real actuals, matched to a planned session, and
upgrades time-in-zone from "estimated" to measured for imported workouts. **Exactly one migration**
(`AddActivityFile`) and **exactly one new NuGet package** (`Garmin.FIT.Sdk` 21.205.0, `Bryk.Infrastructure`
only). No npm package.

## What shipped

| Task | Area | Scope | Commit |
|---|---|---|---|
| specs | Docs | `Tasks-19-1..6` + `Impl-19-1..6` + the phase prompt; corrected Phase 19 roadmap entry | `73d28b1` |
| 19-1 | Backend | **ADR-0010**; `ActivityFile` + `ActivityFileFormat`; `IActivityFileRepository` (4 methods, complete surface) + `ActivityFileRepository` (scalar-only reverse lookup); `ApplicationDbContext` `DbSet` + config; migration `AddActivityFile`; one `Program.cs` DI line; 5 repository facts | `b2265c0` |
| 19-2 | Backend (mostly pure) | `IActivityFileParser` + `ParsedActivity`/`ActivitySample` + `ZoneHistogramEntry`; pure `ZoneHistogramCalculator`; `ActivitySampleBounds`; `TcxActivityParser` + `GpxActivityParser` (`System.Xml.Linq`, no package); 3 committed XML fixtures + csproj glob; 8 + 10 + 7 facts | `1e8b228` |
| 19-3 | Backend | `Garmin.FIT.Sdk` 21.205.0 (`Bryk.Infrastructure` only); `FitActivityParser`; 3 fixture-independent facts | `c4d0fed` |
| 19-4 | Backend | `ActivityFileLimits`/DTOs/2 validators/`IActivityFileService` + `ActivityFileService`; `ActivityFilesController` (4 actions, per-route size cap); 4 `Program.cs` DI lines; **the synthetic `WorkoutStepResult` commit path**; 6 unit + 25 integration facts | `39c7fa2` |
| 19-5 | Frontend | `api.ts` FormData guard (the multipart blocker); `types/activityFiles.ts`, `services/activityFiles.ts`, `stores/activityFiles.ts`; `ImportReviewCard` + `ZoneHistogramBars` + `MatchCandidateList`; `WorkoutsView` drop zone; `WorkoutDetailView` "from file" badge; ~32 Vitest specs | `10a43cc` |
| 19-6 | Backend + Frontend | `SampleSeconds` on `ZoneTimeMethodBreakdownDto`; fourth (winning) provenance in `TimeInZoneCalculator`; `AnalyticsService` histogram load + tolerant JSON; `types/analytics.ts`; `TimeInZoneSection` badge + provenance rewrite; 9 unit + 2 integration + 4 Vitest facts | `0d81368` |

## Verification state

- **Backend:** `dotnet build api/Bryk.sln` green, **0 errors**. `dotnet test api/Bryk.sln` green —
  **337 tests** (196 `Bryk.Application.Tests` + 141 `Bryk.API.Tests`; was **262** at phase start, **+75**).
- **Frontend:** `pnpm run build` (`vue-tsc -b`) green. `pnpm exec vitest run --no-file-parallelism`
  green — **61 test files, 288 tests** (was 56/252 at phase start, **+36**).
- **Warnings:** **16** on a *clean* compile (`--no-incremental`) — exactly the documented baseline,
  unchanged. 14 are the design-time `System.Security.Cryptography.Xml` NU1903 advisory; the other two are
  the pre-existing `WorkoutsControllerTests.cs:121` (CS8604) and `:150` (CS8602), deliberately not fixed.
  **Zero warnings from any file this phase added**, and the FIT SDK restore introduced no new audit
  warning. (Reminder, as in Phase 18: an *incremental* build reports 14 because it skips recompiling
  `Bryk.API.Tests`. Compare like for like.)
- **One migration.** `20260726155712_AddActivityFile` — reviewed `Up`/`Down` before applying: exactly one
  `CreateTable("ActivityFiles")`, exactly two `CreateIndex` (`AthleteId`, `ParsedWorkoutId`), **zero**
  `AddForeignKey`, `Content` as `varbinary(max)`, `Down` a single `DropTable`. Snapshot diff is purely
  additive (48 insertions, 0 deletions, one entity, no FK). Applied to the dev DB.
- **One package.** `Garmin.FIT.Sdk` 21.205.0 resolved exactly, **no transitive dependency**, and
  `git grep "Garmin.FIT.Sdk" -- "*.csproj"` returns exactly one line. No `Dynastream` type appears in
  `Bryk.Domain`, `Bryk.Application` or `Bryk.API`.
- **Frozen files untouched across all six commits:** `LoadCalculator.cs`, `Workout.cs`,
  `WorkoutStepResult.cs`, `WorkoutService.cs`, `WorkoutsController.cs`, `ExceptionHandlingMiddleware.cs`,
  `router/index.ts`, `AppSidebar.vue`.

## Runtime gates — what was actually observed

Run against the dev stack (API on SQL Server `IRONMAN\Bryk` with `db/dev-seed.sql`, UI on Vite 5273).
**The seed was left exactly as found** — 9 workouts, 8 step results, 0 `ActivityFiles` before and after.

**After 19-4 — HTTP smoke on `/api/v1/activityfiles`:**

| Gate | Observed |
|---|---|
| Upload `sample-ride.tcx` | `201`; parsed Bike / 2026-06-02 / 3600 s / 30000 m / avgHr 141 / avgPower 210 / 4 samples; `computedLoad 70.56`; five zone buckets (Z3 120 s, Z4 60 s) |
| Commit | `201` → `{workoutId, plannedWorkoutId: null, computedLoad: 70.56}` |
| Duplicate commit | `409` |
| Discard a committed file | `409` |
| `by-workout` on the import | `200` + `{fileName: "sample-ride.tcx", format: "Tcx", …}` |
| `by-workout` on a hand-logged workout | `200` with body `null` (**not** 404) |
| Workout read | **exactly one** step result: `workoutStepId null`, `orderIndex 0`, `avgPower 210`, `avgHr 141` |
| Corrupt XML as `.tcx` | `400` — `"File: The .tcx file could not be parsed."`, `ActivityFiles` count **0** |
| TCX bytes announced as `.fit` | `400` — `"File: The file's contents do not match its extension."` (magic-byte sniff) |
| Unsupported extension | `400` — `"FileName: Only .fit, .tcx and .gpx files are supported."` |
| **Oversized 26 MB upload** | `400` — `"Content: The file exceeds the 25 MB limit."` — the **validator**, not a pipeline 500. The 32 MB route attribute sitting above the 25 MB validator cap is what makes this clean. |

**The headline load gate (ADR-0010 §3) — two independent observations:**

- **Pinned in test, exact:** `Commit_BikeFileWithPower_ComputesLoadThroughThePowerIfBranch` seeds a
  200 W bike FTP, uploads `sample-ride.tcx` (210 W over 3600 s) and asserts the commit's
  `computedLoad` is **exactly `110.25m`** — `IF = 210/200 = 1.05`, `3600 × 1.05² / 3600 × 100 = 110.25`.
  It passes. The same test also asserts the *preview* promised `110.25` before the commit persisted it.
- **Observed live:** against the dev athlete's real FTP of **250 W**, the same file commits at
  **`70.56` TSS** = `100 × (210/250)²`. That is the power IF branch arithmetic exactly; an HR fallback
  would not produce it (and with no `Lt2` set it would have produced `0`). Different FTP, same branch.

**After 19-5 — `/workouts` in the browser:**

- Drop zone renders (`Drop a .fit, .tcx or .gpx file here` + `Import file`).
- Dropping a ride file renders the review card live:
  `Review import · Tcx · smoke-ride.tcx · 1 KB`, metric strip `Load 71 TSS` (MetricTile rounds — the
  app-wide convention), `Duration 1:00:00`, `Distance 30.0 km`, `Avg HR 141 bpm`, zone bars
  `Z3 · 2m  Z4 · 1m`, the match list with its `No planned workout` option, and Confirm / Discard.
- Confirm committed the workout; `/workouts/:id` shows the **`from file`** badge with `title="smoke-ride.tcx"`,
  and the planned-vs-actual table renders the synthetic step result (`210` power, `141` HR, `1:00:00`).
- **Console clean** (zero errors) across load, drop, parse, commit and detail.
- Caveat, unchanged from Phase 18: the preview pane freezes `requestAnimationFrame`, so the SPA
  `router.push` after Confirm did not repaint; the detail page was reached by a full navigation instead.
  This is a preview-harness artifact, not an app bug — see carry-forward 5.

**After 19-6 — `/progress`, before and after an import (the ROADMAP criterion, observed not inferred):**

| | BEFORE import | AFTER importing the ride into the window |
|---|---|---|
| `sampleSeconds` | `0` | `180` |
| `structureSeconds` | `6300` | `6300` (unchanged) |
| `sessionAvgSeconds` | `25200` | `25200` (unchanged) |
| `unclassifiedSeconds` | `3000` | `6420` (+3420 = 3600 − 180) |
| `totalSeconds` | `34500` | `38100` (+3600, the full session) |
| badge | `estimated` (`text-warn`) | **`mixed`** (`text-warn`) |
| provenance line | "Estimated from planned structure … · session HR …" | "**Measured from device samples (3m)** · planned structure (1h 45m) · session HR (7h 0m) · unclassified (1h 47m)." |

The four fields sum exactly to `totalSeconds` in both states, and the import **raised** total training
time rather than shrinking it. A range containing only hand-logged workouts (`2026-06-10..14`) still
reports `sampleSeconds 0` with the old chain byte-identical — the regression guard, live.

**Calendar compliance for a matched import (ADR-0008 §1):**

Uploaded a ride dated `2026-06-19`, where the seed has an unmatched planned **Bike — Endurance 90'**
(planned 75.0 TSS). The upload offered exactly one candidate — `Bike — Endurance 90'`, `dayOffset 0`,
`plannedLoad 75.0`. Committing with that link produced a calendar item on `2026-06-19` reading
`load 70.56 | plannedLoad 75.0 | compliance Green | isUnplanned false` — ratio `0.941`, inside
ADR-0008 §1's `[0.8, 1.2]` band. Real compliance from a real import.

**`.gpx` end to end:** upload `201` → parsed `Run / 2026-06-03 / 600 s / 2000 m` (haversine) `/ avgHr 140
/ pace 300 / 3 samples`; commit `201`, `computedLoad 12.04`.

## Success criteria (ROADMAP Phase 19) — checked

- **Committed fixtures upload→preview→commit→appear in history with correct load** — ✅ for `.tcx`
  (run + ride) and `.gpx`, observed live above and pinned at exactly `110.25` TSS in test.
  ⚠️ **`.fit` is partial** — the parser ships and is exercised against a real 966 KB Garmin file during
  development (11 160 records, HR + power, decoded correctly), but no committed `.fit` fixture exists.
  See carry-forward 1.
- **Import against a seeded same-day planned workout offers + links the match** — ✅ observed live
  (`dayOffset 0`, single candidate, linked on commit) plus 5 integration facts covering the ±1 window
  boundary, sport mismatch and already-linked exclusion.
- **Calendar shows real compliance for imports** — ✅ observed: `Green` at ratio `0.941`.
- **Progress shows `samples` method for imports** — ✅ **partial in the literal badge sense.**
  `sampleSeconds` is reported, the provenance line leads with device samples, and the badge moves off
  `estimated` → `mixed`. The literal `samples` badge requires `sampleSeconds == totalSeconds`, which
  needs a dense per-second file; the committed 4-point TCX fixture can only measure 180 s of a 3600 s
  session. The `samples` state is covered by a unit fact and a Vitest spec. See carry-forward 2.
- **Corrupt/oversized files fail clean with nothing persisted** — ✅ observed: `400` in every case, with
  `ActivityFiles` count `0` after the corrupt upload.

## Decisions held (ADR-0010)

- **§1 FIT SDK:** `Garmin.FIT.Sdk` 21.205.0, `Bryk.Infrastructure` only, behind `IActivityFileParser`.
  Proprietary royalty-free FIT Protocol License (not OSI) — recorded, accepted, not to be reopened.
- **§2 Raw bytes in the DB** as `varbinary(max)`, 25 MB validator cap behind a 32 MB per-route attribute.
  **No global Kestrel/FormOptions change** — deliberately, because the pipeline's own over-limit
  exceptions have no case in `ExceptionHandlingMiddleware` and would surface as 500.
- **§3 One synthetic `WorkoutStepResult`** (`WorkoutStepId = null`, `OrderIndex = 0`) carries imported
  power/pace/HR into `LoadCalculator`'s StepResults branch. **The calculator was not edited.** No
  per-lap results.
- **§4 One migration, `ActivityFile` only.** No `Workout.SourceFileId`, no `WorkoutZoneDuration`. The
  badge and the duplicate guard both read the reverse link.
- **§5 Histogram as JSON** on `ActivityFile`, method `samples`, precedence over structure/sessionAvg.
- **§6 No per-second sample persistence.** Parsers materialise samples in memory only.

## Known gaps / carry-forward

1. **The `.fit` fixture and its six pinned parser tests are deferred.** `Tasks-19-3.md` requires a *real
   device-written* file and explicitly forbids hand-crafting one or generating one with the SDK encoder.
   No such file (≤ 200 KB, ride with HR + power) was available: the machine held one 966 KB Garmin **run**
   and two small Zwift files carrying no records. The user elected to supply a file later.
   `FitActivityParserTests.cs` ships with the three facts that need no fixture — `Format_IsFit`, garbage
   bytes → `ThrowExactly<ValidationException>`, and TCX bytes → `ValidationException` — which cover the
   contract that matters most (a corrupt upload must be a clean 400, never a 500). **To finish:** drop the
   file at `api/Bryk.API.Tests/Fixtures/ActivityFiles/sample-ride.fit` (the csproj glob already covers it),
   add the six fixture-pinned facts from `Tasks-19-3.md`, promote the observed `AvgPower` to a
   `private const`, and delete the `PENDING FIXTURE` header comment.
2. **The live `samples` badge needs a dense-sample file.** `samples` requires every second in the window
   to be measured. The 4-point TCX fixture yields 180 measured seconds out of 3600, so the highest live
   state reachable today is `mixed`. This is correct behaviour (ADR-0010 §5's honesty rule — unmeasured
   seconds go to `unclassified`, never silently into a zone), not a defect. A real 1-second `.fit` will
   produce `samples`. Resolving carry-forward 1 also resolves this.
3. **The zone histogram is JSON on `ActivityFile`, not a normalized table** (ADR-0010 §5). Read whole,
   never queried per-zone. **Normalizing it is an explicit Phase-21 candidate.**
4. **Above 32 MB the framework wins and the status is whatever it produces.** `RequestSizeLimit` /
   `RequestFormLimits` are set to 32 MB so the 25 MB validator produces the clean 400; beyond that the
   pipeline aborts with `InvalidDataException`/`BadHttpRequestException`, neither of which
   `ExceptionHandlingMiddleware` has a case for, so it would surface as 500. **Accepted and documented;
   Phase 21 owns the error contract.**
5. **Preview-pane rAF caveat (unchanged from Phase 18, now with a second symptom).** The in-app Browser
   pane freezes `requestAnimationFrame`, so Vue's route `<Transition>` stalls and, additionally, an
   in-app `router.push` after a successful commit does not repaint. Workaround: shim
   `requestAnimationFrame` → `setTimeout`, then remove the stuck `*-leave-active` wrapper and call its
   `__vnode.transition.afterLeave()`. Screenshots time out; read `document.body.textContent`.
6. **Zone-bar markup is duplicated** between `components/import/ZoneHistogramBars.vue` and
   `components/analytics/TimeInZoneSection.vue` (~15 lines). Deliberate: 19-5 and 19-6 were separate
   tasks and 19-6 rewrote the analytics file in the same phase. Extracting a shared component is a clean
   standalone follow-up.
7. **The `%HRmax` scheme and band predicate are duplicated** between `ZoneHistogramCalculator` (19-2) and
   `TimeInZoneCalculator` (19-6) — ~10 lines, duplicated on `Tasks-19-2.md`'s explicit instruction so the
   two tasks would not share a file. They must stay commensurable; a change to one requires a change to
   the other. Worth a shared helper now that both files are settled.
8. **`Compute_PowerSamples_BucketByBand`'s spec vector was internally inconsistent** and was corrected in
   19-2: the doc asked for four bands of 60 s from four samples, which the last-sample-contributes-zero
   rule makes unreachable. The fixture gained a trailing sample (exactly how the sibling `%HRmax` case is
   built) so both the stated expectation and the rule hold.
9. **`GET /activityfiles/by-workout/{id}` returns `JsonResult`, not `Ok()`.** `Ok(null)` is turned into a
   204 by `HttpNoContentOutputFormatter`, and the client needs a 200 with an explicit `null` body. Noted
   because it looks like an inconsistency next to the other controllers.
10. **POST/PUT periodization validator bounds still diverge** (carried from Phase 18, still open).
    `TrainingPlanRequestValidator` accepts `BuildWeeks > 0` / `RecoveryWeeks > 0` /
    `RecoveryWeekPercentage` 0–100; the PUT validator bounds them 1–8 / ≥ 1 / 30–90. **Needs a Sr. Dev
    decision**; deferred twice now.
11. **`lib/charts/load.ts:65` labels the last bar `· NOW`** — the known cosmetic artifact from Phase 18.
    Untouched.
12. **ROADMAP doc drift (pre-existing):** the Phase 16 *heading* reads `⏳` although its ledger row reads
    `✅`. Carried from Phase 17/18; still outside scope.
13. **`.claude/launch.json` gained an `api` entry** so the API could be started through the preview
    tooling rather than a raw shell. It is gitignored, so it does not appear in any commit — recreate it
    if a future session needs the same gate.

## Files added by Phase 19

| File | Purpose |
|---|---|
| `md/decisions/0010-activity-file-import.md` | The import ADR: SDK, storage, load routing, migration scope, histogram, samples. |
| `api/Bryk.Domain/Entities/ActivityFile.cs`, `Entities/Enums/ActivityFileFormat.cs` | The upload row + format enum. |
| `api/Bryk.Domain/Interfaces/IActivityFileRepository.cs` | Four-method contract, complete as shipped. |
| `api/Bryk.Infrastructure/Repositories/ActivityFileRepository.cs` | Scalar-only reverse lookup (never loads `Content`). |
| `api/Bryk.Infrastructure/Migrations/20260726155712_AddActivityFile*.cs` | The one approved migration. |
| `api/Bryk.Application/ActivityFiles/ParsedActivity.cs`, `IActivityFileParser.cs`, `ZoneHistogramEntry.cs`, `ZoneHistogramCalculator.cs` | The parsing boundary + pure bucket math. |
| `api/Bryk.Infrastructure/ActivityFiles/ActivitySampleBounds.cs`, `TcxActivityParser.cs`, `GpxActivityParser.cs`, `FitActivityParser.cs` | Sample sanity + the three format parsers. |
| `api/Bryk.Application/ActivityFiles/ActivityFileLimits.cs`, `ActivityFileUploadRequest.cs`, `CommitActivityFileRequest.cs`, `ActivityFileResponses.cs`, `Validators/*`, `IActivityFileService.cs`, `ActivityFileService.cs` | The service slice. |
| `api/Bryk.API/Controllers/ActivityFilesController.cs` | Four actions + the per-route cap. |
| `api/Bryk.API.Tests/Fixtures/ActivityFiles/sample-run.tcx`, `sample-ride.tcx`, `sample-activity.gpx` | The committed fixtures every pinned number derives from. |
| `ui/src/types/activityFiles.ts`, `services/activityFiles.ts`, `stores/activityFiles.ts` | The frontend slice. |
| `ui/src/components/import/ImportReviewCard.vue`, `ZoneHistogramBars.vue`, `MatchCandidateList.vue` | The review flow. |
| `ui/src/services/__tests__/api.spec.ts` | The multipart regression guard on `apiFetch`. |

## Phase 19 closeout checklist

- [x] ADR-0010 written **before** any code (Task 19-1 Step 1).
- [x] `ActivityFile` entity/repo/migration; migration `Up`/`Down` reviewed before apply (19-1).
- [x] Parser boundary + TCX/GPX + pure histogram math (19-2).
- [x] FIT parser + the one approved package (19-3) — fixture-pinned tests deferred, see carry-forward 1.
- [x] Upload/commit/discard/by-workout endpoints + the synthetic `WorkoutStepResult` (19-4).
- [x] Upload + review UI + "from file" badge + the `api.ts` FormData fix (19-5).
- [x] `samples` time-in-zone, precedence and honest badge (19-6).
- [x] xUnit: 337 tests. Vitest: 61 files, 288 tests. Both builds green, warnings flat at 16.
- [x] Runtime gates observed live; dev seed left exactly as found.
- [x] Handoff doc written (`md/handoffs/2026-07-26-phase-19-complete.md`).
- [x] ROADMAP.md updated (Phase 19 → ✅; ledger + heading; delivered note).
- [x] CLAUDE.md phase pointer refreshed + ADR-0010 indexed.

## Next — Phase 20 (Wellness metrics) or Phase 12 (Auth)

**Phase 20 — Wellness metrics (sleep, RHR, weight, soreness, HRV)** is the declared next feature phase.
It **requires a migration** (`DailyWellness`, with a unique composite index on `AthleteId + Date`) and
therefore one reviewed migration set under the Sr. Dev gate. No new package expected.

**Phase 12 (Auth)** remains eligible and **approval-gated**: ADR evaluating ASP.NET Core Identity vs
hand-rolled, a table-layout decision, migration approval, OAuth wiring, cookie-or-JWT.
**All auth code requires approval before it is written.**

Small and worth clearing first: carry-forward **1** (drop in the `.fit` fixture and finish 19-3's test
list — a 20-minute task once a file exists) and carry-forward **10** (the POST/PUT bounds divergence,
now deferred twice).

## Session-start checklist

1. Read this handoff + the ROADMAP Phase 20 entry (or Phase 12 if auth is next) + ADR-0010.
2. `git status` clean; `git log --oneline -10`.
3. Backend: `dotnet build api/Bryk.sln` + `dotnet test api/Bryk.sln` (expect **337**).
   Use `--no-incremental` when checking the warning count (**16**); an incremental build reports 14.
4. Frontend: `pnpm run build` + `pnpm exec vitest run --no-file-parallelism` (expect **288 / 61**);
   the transient worker-fork crash with all tests passing → re-run once before debugging.
5. `dotnet user-secrets list` (from `api/Bryk.API/`) shows `ConnectionStrings:DefaultConnection` +
   `DevAuth:CurrentAthleteId`. Seed: `db/dev-seed.sql`.
6. Dev stack: API from `api/Bryk.API` with **`ASPNETCORE_ENVIRONMENT=Development`** (the DevAuth stub
   throws outside Development); `pnpm dev` from `ui/` (vite proxies `/api` → 60129). Stop the API before
   rebuilding — a running `Bryk.API` locks `Bryk.Infrastructure.dll` and the build fails with MSB3027.
