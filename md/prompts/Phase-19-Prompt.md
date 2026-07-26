# Execution Prompt — Phase 19: Activity file import (.fit / .tcx / .gpx)

> Paste this prompt into a fresh session rooted at the Bryk repo. Phase 18 is the latest complete phase. Phase 19 formally depends only on Phase 13 (the workout detail/edit surface and match UX) and reuses Phase 14's load math and Phase 15's time-in-zone. **Phase 12 (auth) is deliberately still deferred** — do not treat its absence as a blocker.

You are the Senior Solutions Architect for the Bryk project. You design, implement, and validate the work directly.

## Required reading, in order, before any code

1. `CLAUDE.md` — every convention applies.
2. `ROADMAP.md` → the **Phase 19** entry (lines 527–553, goal + scope).
3. `md/handoffs/2026-07-26-phase-18-complete.md` — the latest handoff; its "Session-start checklist" and carry-forward list.
4. `md/decisions/0005-training-load-and-execution.md` §5 — `WorkoutStepResult.WorkoutStepId` is nullable (`Guid?`), which is what makes the synthetic-step-result design legal.
5. `md/decisions/0007-progress-analytics.md` §4 — the coarse time-in-zone model (structure / sessionAvg / unclassified) that Phase 19 upgrades by adding `samples` as a fourth method.
6. `md/Tasks-19-1.md` … `md/Tasks-19-6.md` — the task contracts (task scope, surface, dependencies, acceptance criteria, verification).
7. `md/Impl-19-1.md` … `md/Impl-19-6.md` — the step-by-step build orders (each step has a **Verify** gate to the next).
8. Note: **ADR-0010 does NOT exist yet** — it is Task 19-1's first deliverable. Read the ROADMAP's "Decisions needed" section carefully; Task 19-1 records all five in the ADR.

## Session-start checklist

Clean tree; `dotnet build`/`dotnet test api/Bryk.sln` green; `pnpm run build`/`pnpm test` green from `ui/`; user-secrets present; seed data loaded. Vitest's transient worker-crash-with-all-passing → re-run once with `--no-file-parallelism`.

## Important context

- **Phase 12 (auth) has not shipped and stays deferred.** Execute on the DevAuth stub; athlete resolution through `ICurrentUserService` only. Writing auth code is an approval gate — do not.
- **One migration, one package, both pre-approved in SHAPE.** Migration: `ActivityFile` table only. Package: `Garmin.FIT.Sdk` 21.205.0 for `Bryk.Infrastructure` only. **Any second migration or any other package** → **STOP and ask**.
- **`ActivityFile.Content` holds raw bytes in `varbinary(max)`, ~25 MB cap**, enforced by the validator (which produces the clean 400) behind `[RequestSizeLimit(ActivityFileLimits.HardCapBytes)]` + `[RequestFormLimits(MultipartBodyLengthLimit = ActivityFileLimits.HardCapBytes)]` set to a deliberately **higher** 32 MB, so the framework never trips first. NOT global Kestrel config. The pipeline's own over-limit exceptions have no case in `ExceptionHandlingMiddleware` — Phase 21 owns that contract. Above 32 MB the framework wins and the status is whatever it produces; that gap is accepted and documented.
- **Imported power/pace reach the load math via ONE synthetic `WorkoutStepResult`** (`WorkoutStepId = null`, `OrderIndex = 0`) carrying parsed avg/max values. **`LoadCalculator.cs` is FROZEN for this phase.** Do NOT add `Workout.AvgPower`, `Workout.AvgPace`, or `Workout.SourceFileId`. Do NOT emit one step result per lap or per zone. If the synthetic result looks wrong, the bug is almost certainly a missing field on the step result or a malformed payload — do NOT weaken `LoadCalculator.cs`.
- **Zone histogram is JSON on `ActivityFile`** (`ZoneHistogramJson`), read via the reverse link `ActivityFile.ParsedWorkoutId → Workout`. Normalizing into a real table is a Phase-21 candidate — record as tech debt in the handoff.
- **"From file" badge and duplicate-commit guard** use the reverse lookup. `GET /api/v1/activityfiles/by-workout/{id}` returns 200 with null body for hand-logged workouts (not 404).
- **`ui/src/services/api.ts` hardcodes `'Content-Type': 'application/json'` for all requests**, which breaks multipart uploads. Task 19-5 Step 1 fixes it with a `body instanceof FormData` guard + a regression spec.
- **Workouts routes and nav** already exist; Phase 19 adds no route, no nav item. Upload button lives on `WorkoutsView`; detail gains a "from file" badge.

## Mission

Deliver **Phase 19 — Activity file import** end to end.

### Step 0 — verify the working tree and lock decisions

Before any code:

1. `git status` clean; `git log --oneline -5` shows Phase 18 as the latest complete phase.
2. `dotnet build api/Bryk.sln`, `dotnet test api/Bryk.sln` — expect 262 xUnit tests green, 16 known warnings.
3. `pnpm run build` from `ui/`, `pnpm exec vitest run --no-file-parallelism` from `ui/` — expect **252 tests across 56 files** green.
4. If `git status` shows modified files, they are likely the Phase 19 planning output (ROADMAP.md change + Tasks-19-1..6.md + Impl-19-1..6.md). See "Pre-existing changes" section below. Those files are **not errors**; they are the specs — commit them together before coding if they are uncommitted:
   ```
   docs: add Phase 19 task specs + correct Phase 19 roadmap entry
   ```
   If the tree has anything other than these **fourteen paths** uncommitted, **STOP and ask the user before touching it.**

The **Decisions needed** from the ROADMAP entry are all locked and will be written into ADR-0010 by Task 19-1:

- **D1 — Garmin FIT SDK: `Garmin.FIT.Sdk` 21.205.0 approved** for `Bryk.Infrastructure` only. Publisher-verified Garmin International, netstandard2.0 (net10.0-compatible), license Garmin proprietary royalty-free FIT Protocol License (not OSI). **This is the ONLY new package permitted in Phase 19.**
- **D2 — Raw file storage: `varbinary(max)` on `ActivityFile.Content`**, ~25 MB cap via validator + per-route attribute. No filesystem path, no upload-root config.
- **D3 — Imported power/pace reach load via ONE synthetic `WorkoutStepResult`** (`WorkoutStepId = null`, `OrderIndex = 0`). **`LoadCalculator.cs` is FROZEN.** Zero changes to the calculator.
- **D4 — Migration: `ActivityFile` table ONLY.** No `Workout.SourceFileId`, no `Workout.AvgPower`, no `WorkoutZoneDuration` child table. "From file" badge and duplicate guard use reverse link `ActivityFile.ParsedWorkoutId`.
- **D5 — Zone histogram: JSON on `ActivityFile` (`ZoneHistogramJson`)**, read via `ActivityFile.ParsedWorkoutId → Workout`. Normalization to a real table is Phase-21 debt.

### Step 1 — implement, one task per commit (strict order)

Task dependency chain is strict. Do not parallelize; later tasks depend on earlier ones' files:

**Order: 19-1 → 19-2 → 19-3 → 19-4 → then 19-5 and 19-6 (either order).**

- **19-1 first:** everything depends on the `ActivityFile` entity + ADR-0010.
- **19-2 defines `IActivityFileParser`** (abstraction); 19-3 implements it for FIT. Both are independent of the API.
- **19-4 depends on 19-1 + 19-2.** It wires the service and endpoints. **Shared file:** `api/Bryk.API/Program.cs` (19-1 adds repo DI at L106; 19-4 appends service DI; never reorder).
- **19-5 and 19-6 both depend on 19-4** and own disjoint files: 19-5 handles `WorkoutsView`, `WorkoutDetailView`, `api.ts`, `types/activityFiles.ts`; 19-6 handles `TimeInZoneSection.vue`, `types/analytics.ts`, and the backend time-in-zone trio. Because they share no files, **the order between these two is free** — but still run them **sequentially, one commit each**, like every other task. "No file conflict" is not a licence to run two tasks at once.

For each task:

1. Read the task's `Tasks-19-N.md` (the contract) and `Impl-19-N.md` (step-by-step walkthrough).
2. Follow `Impl-19-N.md` top-to-bottom, treating each **Verify** gate as a hard stop before the next step.
3. Build + test + diff-read after the code.
4. Surface the commit message from the `## Suggested commit` section of the Tasks doc.

**Commit message discipline:** Plain conventional-commit messages only. Do NOT append `Co-Authored-By:` or any AI co-author trailer — it skews the GitHub contributor count. The commit author is already the repo git user (Matthew Wilson).

**Approval gates:** The migration and the FIT SDK are **pre-approved in SHAPE**. The migration must still be **generated, reviewed Up/Down, and only then applied** (CLAUDE.md gate). A **second migration or any other package** → **STOP and ask** (Sr. Dev gate).

### Step 2 — phase exit

Verify every ROADMAP Phase 19 success criterion:

- **Committed test fixtures (.fit ride, .tcx run, .gpx activity) upload→preview→commit→appear in history with correct load.** → Pinned by the 19-4 integration test asserting the powered-bike fixture commits to **exactly `110.25` TSS** — a value reachable only through the synthetic step result (the regression guard on ADR-0010 §3). Report the observed number.
- **Import against a seeded same-day planned workout offers + links the match.** → 19-4 integration test + 19-5 smoke.
- **Calendar shows real compliance for imports.** → Manual smoke at `/calendar`: find the day of a matched import and verify its cell shows the correct ADR-0008 §1 compliance band.
- **Progress shows `samples` method for imports.** → 19-6 manual smoke at `/progress`: on a range containing an import, the Time in Zone **badge** (the one hardcoded to `estimated` at `TimeInZoneSection.vue:68–72`) and the provenance sentence beneath the bars both reflect measured samples. It is a badge + sentence, **not** a tooltip.
- **Corrupt/oversized files fail clean with nothing persisted.** → 19-4 test + smoke.

Flip the ROADMAP Phase 19 heading to ✅; write `md/handoffs/<today>-phase-19-complete.md` (follow the Phase 18 template); update the CLAUDE.md phase pointer to "Phase 19 complete"; index **ADR-0010** in CLAUDE.md's decision list. Final commit: `docs: close out Phase 19`.

## Scope guardrails (do NOT)

- **No second migration.** `ActivityFile` table only, one reviewed set. If a task appears to need another — **STOP and ask**.
- **No package other than `Garmin.FIT.Sdk` 21.205.0.** Add nothing else to `Bryk.Infrastructure` or elsewhere.
- **Do not modify** `Workout.cs` — no `SourceFileId`, no `AvgPower`, no `AvgPace`. It is untouched by this phase.
- **Do not create** a `WorkoutZoneDuration` child table or any other schema change.
- **Do not modify `LoadCalculator.cs`** (frozen). If it looks like the calculator is wrong, the bug is almost certainly a missing/malformed synthetic `WorkoutStepResult` — do NOT edit the calculator.
- **Do not modify** `ExceptionHandlingMiddleware.cs` (Phase 21 owns the error contract). The upload cap is per-route attribute + validator, not global Kestrel config.
- **Do not fix** the two pre-existing nullable warnings at `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs:121,150` — they predate this phase.
- **No vendor OAuth, auto-sync, per-second sample persistence, power curves, lap deep-dives, push-to-device, bulk/multi-file backfill.** File import only, hard line.
- **No auth code.** Phase 12 remains deferred and approval-gated. Athlete identity always via `ICurrentUserService`.
- **Do not revert, stage, or commit unrelated working-tree changes.**

## Verified code facts (checked 2026-07-26)

Read these during the relevant task:

- `api/Bryk.Application/Training/Load/LoadCalculator.cs:74–83` — `ComputeActualLoad`'s StepResults branch; synthetic result routes here and reaches the real power/pace IF branches.
- `api/Bryk.Application/Training/Load/LoadCalculator.cs:88` — session-only path hardcodes power and pace to `null`. Without the synthetic step result, import can only reach HR branch. **This is the phase's headline risk.**
- `api/Bryk.Domain/Entities/Workout.cs` — has **no** session-level `AvgPower`, `AvgPace`, or `SourceFileId`, and none is being added.
- `api/Bryk.Domain/Entities/WorkoutStepResult.cs:14` — `WorkoutStepId` is `Guid?` (nullable).
- `api/Bryk.API/Program.cs:35` — `AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>()`; validators auto-register, so never add a DI line for a validator. `:99–107` is the Repositories block (19-1 appends one line after L106).
- `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` — 233 lines; last `DbSet` at L24, `OnModelCreating` closes at L231.
- `api/Bryk.API/Middleware/ExceptionHandlingMiddleware.cs` — maps `ValidationException`→400, `KeyNotFoundException`→404, `InvalidOperationException`→409, aborted→499, else 500. Has **no** case for `InvalidDataException`/`BadHttpRequestException`, which is why upload cap is per-route attribute + validator, not global config. Changing this is Phase 21's job — **STOP and ask**.
- `ui/src/services/api.ts:24–27` — `apiFetch` hardcodes `'Content-Type': 'application/json'` on every request, breaking multipart. Task 19-5 Step 1 fixes it with a `body instanceof FormData` guard + regression spec.
- `ui/src/components/layout/AppSidebar.vue:37` — `Workouts` nav item already exists; `ui/src/router/index.ts:48,53` — `/workouts` and `/workouts/:id` already exist. Phase 19 adds no route, no nav item.
- `api/Bryk.Application/Analytics/TimeInZoneCalculator.cs` — `Compute(...)` has exactly ONE production call site (`AnalyticsService.cs:142`) and SIX test call sites (`TimeInZoneCalculatorTests.cs` lines 47, 63, 93, 107, 123, 142). Task 19-6 adds a required 5th parameter; every existing assertion must stay unchanged as the regression guard.
- `api/Bryk.API.Tests` uses **EF InMemory provider** — no real constraints, no unique-index enforcement. Do not write tests depending on DB-level constraint violations.

## Pre-existing changes — critical reading

As of prompt generation, `git status` contains:

```
 M ROADMAP.md                      (corrected Phase 19 entry + scope clarifications)
?? md/Tasks-19-1.md … md/Tasks-19-6.md
?? md/Impl-19-1.md … md/Impl-19-6.md
?? md/prompts/Phase-19-Prompt.md   (this file)
```

**These are Phase 19's planning output. Do NOT discard, revert, or ignore them.** The `ROADMAP.md` modification is a deliberate factual correction (the phase previously claimed imported power "finally exercises the top IF branch" without the synthetic-step design, which was incorrect — corrected in coordination with ADR-0010 §3).

**Step 0 action:** If `git status` still shows these fourteen paths as uncommitted after you read this prompt, commit them together before any code, as the specs commit:
```
docs: add Phase 19 task specs + correct Phase 19 roadmap entry
```

If they are already committed, confirm the tree is clean and proceed.

If the tree contains anything OTHER than these fourteen paths, **STOP and ask the user before touching it.**

## Verification commands (runnable from repo root)

```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```

**Baselines at phase start:** 262 xUnit tests, 252 Vitest tests across 56 files, 16 known build warnings (design-time `System.Security.Cryptography.Xml` NU1903 × 9 + two pre-existing `WorkoutsControllerTests.cs` nullable warnings). Both suites must RISE with zero failures; warning count must not grow past 16. Vitest transient worker-fork crash reports "Errors N" with every test passing — re-run once with `--no-file-parallelism` before debugging.

**Runtime gates (do not just compile; run the app):** Dev stack: API via `dotnet run` from `api/Bryk.API` (https://localhost:60129); UI via `pnpm dev` from `ui/` (vite proxies `/api` → 60129).

- After **19-4**: HTTP-smoke the upload → 201 preview with parsed actuals, load, zone histogram and match candidates; commit → 201; a corrupt file → 400 with **nothing persisted**; an oversized file → clean rejection; a duplicate commit → rejected.
- After **19-4** (the headline gate — **observe it, do not infer it**): the powered-bike fixture commits to **exactly `110.25` TSS** (210 W over a 200 W FTP: IF = 1.05, TSS = 3600 × 1.05² / 3600 × 100). If this is wrong, the synthetic `WorkoutStepResult` is missing or malformed — do NOT weaken the assertion and do NOT touch `LoadCalculator.cs`.
- After **19-5**: open `/workouts`, drop a fixture file, confirm the review card shows parsed metrics + zone bars + match candidates, commit, land on `/workouts/:id` with the "from file" badge visible. Console clean.
- After **19-6**: open `/progress` — a range containing an imported workout reports the `samples` method and the "estimated" badge softens/disappears for sample-covered ranges; a range with only hand-logged workouts still shows the old estimate chain unchanged.

## Failure honesty clause

If a verification command fails for an unrelated environment reason (SQL Server unavailable, missing user-secrets, port in use, the known Vitest worker crash), capture the exact output verbatim, explain what it was and why it is unrelated, and **do not claim success**. Never report a phase or task as complete on a red or unrun gate. If a ROADMAP success criterion cannot be observed, say so explicitly and mark it partial — the precedent is `md/handoffs/2026-07-25-phase-17-complete.md:52`, which marks the ProgressRing criterion "⚠️ **partial, see carry-forward**" rather than claiming it.

## Final reporting requirements

End with a status from **DONE / DONE_WITH_CONCERNS / NEEDS_CONTEXT / BLOCKED**, then:

- Files changed (grouped by task) with brief surface summary.
- Build + test results with actual counts, not "green".
- What was actually observed at each runtime gate (including the exact TSS value; before/after zone-method labels for Progress).
- Review outcomes (all non-goals held? frozen files untouched? LoadCalculator unchanged?).
- Explicit confirmation that exactly ONE migration and exactly ONE new package landed.
- Residual risks, known artifacts, and carry-forward items.
- Final `git status`.

## Known carry-forward for the handoff

(1) The zone histogram is JSON on `ActivityFile` rather than a normalized table — a Phase-21 candidate; record in the handoff and leave as tech debt. (2) The POST/PUT periodization validator bounds divergence from Phase 18 is still open and needs a Sr. Dev decision. (3) `lib/charts/load.ts:65` labels the last bar `· NOW` — a known cosmetic artifact (the periodization panel reuses the chart unforked), do not fix mid-phase.
