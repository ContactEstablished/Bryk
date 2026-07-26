# Execution Prompt — Phase 20: Wellness metrics (sleep, RHR, weight, soreness, HRV)

> Paste this prompt into a fresh session rooted at the Bryk repo. Phase 19 is the latest complete phase. Phase 20 formally depends only on Phase 12 (auth) — which is **deliberately still deferred**; do not treat its absence as a blocker. Phase 20 is additive dashboard context: manual daily wellness entry, turning on the Sleep placeholder and giving Resting HR a real history trend.

You are the Senior Solutions Architect for the Bryk project. You design, implement, and validate the work directly.

## Required reading, in order, before any code

1. `CLAUDE.md` — every convention applies.
2. `ROADMAP.md` → the **Phase 20** entry (lines 557–581, goal + scope + the *Decisions needed* block now fully resolved).
3. `md/handoffs/2026-07-26-phase-19-complete.md` — the latest handoff; its "Session-start checklist" and carry-forward list.
4. `md/decisions/0006-pmc-computation.md` — the PMC stays a pure function of training load. Phase 20 must not change that.
5. `md/Tasks-20-1.md` … `md/Tasks-20-4.md` — the task contracts (task scope, surface, dependencies, acceptance criteria, verification).
6. `md/Impl-20-1.md` … `md/Impl-20-4.md` — the step-by-step build orders (each step has a **Verify** gate to the next).
7. Note: **ADR-0011 does NOT exist yet** — it is Task 20-1's first deliverable, written before any code.

## Session-start checklist

Clean tree; `dotnet build api/Bryk.sln` + `dotnet test api/Bryk.sln` green; `pnpm run build` + `pnpm exec vitest run --no-file-parallelism` green from `ui/`; user-secrets present (`ConnectionStrings:DefaultConnection`, `DevAuth:CurrentAthleteId` — from `api/Bryk.API/`); seed `db/dev-seed.sql`. Vitest's transient worker-fork crash reporting "Errors N" with every test passing → re-run once before debugging.

## Important context

- **Phase 12 (auth) has not shipped and stays deferred.** Execute on the DevAuth stub; athlete resolution through `ICurrentUserService` only. Writing auth code is an approval gate — do not.
- **One migration, one ADR, no new packages.** Migration: `DailyWellness` table only, with a **unique composite index on `{ AthleteId, Date }`**. **Any second migration or any new NuGet/npm package** → **STOP and ask**.
- **`DailyWellness` is independent of `Athlete`.** A wellness save **never** writes back to `Athlete.WeightKg` or `Athlete.RestingHr`. Verified safe: neither field feeds any load, zone or PMC math. The single concession is a **read-only fallback** — when an athlete has no wellness entries, the Resting HR tile shows `Athlete.RestingHr` so it never regresses to `—`. **No fallback for weight** (a trend tile cannot be seeded from a one-off onboarding self-report).
- **HRV does not blend into TSB, PMC or any readiness score.** ADR-0006 keeps the calculator pure. No readiness score, no "train/rest today" recommendation.
- **The scale input generalizes, it does not duplicate.** A new `ScaleSelector` (e.g., max + labels props); `RpeSelector.vue` becomes a thin wrapper passing 10 + Easy/Steady/Max. Soreness 1–10, sleep quality 1–5. `LogWorkoutForm.vue` and its three specs stay **untouched and passing** — that is Task 20-3's regression gate.
- **`DeltaChip` is not recoloured.** `ui/src/lib/weeklyTarget.ts:21–23` carries the standing instruction not to "fix" its colours. The `delta` prop is passed only for sleep hours and HRV (up = good). Resting HR, weight and soreness are inverted — a drop is good news — so they render their 7-day change in `MetricTile`'s **`#footer`** slot.
- **`Program.cs:32–33` sets `SuppressModelStateInvalidFilter = true`.** The automatic model-state 400 is **OFF**: a route value that fails to bind does not 400 — the parameter receives `default(DateOnly)` and the action still runs. An unguarded `PUT /api/v1/wellness/{date}` would upsert to 0001-01-01. The defence is **two mandatory layers**: the `{date:datetime}` route constraint (a non-date segment → **404** before binding) **and** the validator's `Date != default` guard (→ **400**). Neither alone is sufficient. **Do NOT "fix" this by turning `SuppressModelStateInvalidFilter` off** — that is cross-cutting and changes every endpoint.
- **`api/Bryk.API.Tests` runs on the EF InMemory provider**, which enforces **no unique index**. The `{AthleteId, Date}` uniqueness **cannot be proven by an integration test** — it is verified by reading the generated migration, while the *behaviour* is proven by a service-side "PUT twice, then count rows through the API" test. **No test may assert that a duplicate insert throws** — it would pass for the wrong reason here.
- **`MetricTile` renders numeric values through `useCountUp`, which defaults to `decimals = 0`** (`MetricTile.vue:34`; `useCountUp.ts:20`). A numeric `7.5` renders `"8"`. The one-decimal tiles (**Sleep**, **Weight**) must pass `average.toFixed(1)` — a **string**, which `MetricTile` returns verbatim — while whole-number tiles (**Resting HR**, **HRV**) pass numbers. This is the existing string-value path.

## Mission

Deliver **Phase 20 — Wellness metrics** end to end.

### Step 0 — verify the working tree and lock decisions

Before any code:

1. `git status` clean; `git log --oneline -5` shows Phase 19 as the latest complete phase.
2. `dotnet build api/Bryk.sln`, `dotnet test api/Bryk.sln` — expect 343 xUnit tests green and **16** warnings on a **clean** (`--no-incremental`) compile. An *incremental* build reports 14 because it skips recompiling `Bryk.API.Tests`; compare like for like before concluding a warning was added or removed.
3. `pnpm run build` from `ui/`, `pnpm exec vitest run --no-file-parallelism` from `ui/` — expect **288 tests across 61 files** green.
4. If `git status` shows modified files, they are likely the Phase 20 planning output (ROADMAP.md change + Tasks-20-1..4.md + Impl-20-1..4.md). See "Pre-existing changes" section below. Those files are **not errors**; they are the specs — commit them together before coding if they are uncommitted:
   ```
   docs: add Phase 20 task specs + record the resolved Phase 20 decisions
   ```
   If the tree has anything other than these **ten paths** uncommitted (the nine above plus this prompt file), **STOP and ask the user before touching it.**

The **Decisions needed** from the ROADMAP entry are all locked and will be written into ADR-0011 by Task 20-1. All six are already resolved by the Sr. Dev on 2026-07-26:

- **D1 — `DailyWellness` is independent of `Athlete`.** Never writes back to `Athlete.WeightKg`/`RestingHr`. Read-only fallback for Resting HR; no fallback for weight.
- **D2 — One wide, mostly-nullable row per athlete per day.** Uniqueness enforced by a unique composite index **and** a service-side read-then-update upsert.
- **D3 — HRV does not blend into TSB/PMC/readiness.** ADR-0006 keeps the calculator pure.
- **D4 — The scale input generalizes, not duplicates.** `ScaleSelector`, leaving `RpeSelector.vue` and `LogWorkoutForm.vue` untouched.
- **D5 — `DeltaChip` is not recoloured.** Standing convention holds; inverted metrics report change in `MetricTile`'s footer slot.
- **D6 — One migration, `DailyWellness` alone.** No `Athlete` change, no FK, no second table, no new package.

### Step 1 — implement, one task per commit (strict order)

**The task specs already exist — you are not writing them.** `md/Tasks-20-1.md` … `Tasks-20-4.md` and `md/Impl-20-1.md` … `Impl-20-4.md` are on disk (see *Pre-existing changes*); Step 0 commits them if they are still uncommitted. Their scopes:

- **20-1** — ADR-0011 + `DailyWellness` entity + `IDailyWellnessRepository`/`DailyWellnessRepository` + `ApplicationDbContext` DbSet & config + migration + DI line + 7 repository tests.
- **20-2** — DTOs + 2 validators + `WellnessSummaryCalculator` + `WellnessService` + `WellnessController` (3 actions) + DI line + ~40 tests.
- **20-3** — `types/wellness.ts` + `services/wellness.ts` + `schemas/wellness.ts` + `stores/wellness.ts` + `ScaleSelector.vue` + `RpeSelector` rewrap + `WellnessQuickEntryCard.vue`. **Owns no view file.**
- **20-4** — `lib/wellness.ts` + `SleepCard.vue` + `RestingHrCard` upgrade + `WeightCard.vue` + `HrvCard.vue` + `HomeView.vue` (**sole owner**).

Task dependency chain is strict: **20-1 → 20-2 → 20-3 → 20-4.** Do not parallelize; later tasks depend on earlier ones' files.

- **20-1 first:** everything depends on the `DailyWellness` entity + ADR-0011.
- **20-2 depends on 20-1.** It wires the service and endpoints. **Shared file:** `api/Bryk.API/Program.cs` (20-1 adds repo DI after L107; 20-2 appends service DI after L126; never reorder).
- **20-3 depends on 20-2.** It builds the frontend layer and **must not edit any view file** (that is 20-4's job).
- **20-4 depends on 20-3.** It owns `HomeView.vue`, the dashboard tiles, and their wiring.

For each task:

1. Read the task's `Tasks-20-N.md` (the contract) and `Impl-20-N.md` (step-by-step walkthrough).
2. Follow `Impl-20-N.md` top-to-bottom, treating each **Verify** gate as a hard stop before the next step.
3. Build + test + diff-read after the code.
4. Surface the commit message from the `## Suggested commit` section of the Tasks doc.

**Commit message discipline:** Plain conventional-commit messages only. Do NOT append `Co-Authored-By:` or any AI co-author trailer — it skews the GitHub contributor count. The commit author is already the repo git user (Matthew Wilson).

**Approval gate:** The `AddDailyWellness` migration is **pre-approved in SHAPE only**. It must still be **generated, its `Up`/`Down` read in full, and explicitly approved before `dotnet ef database update`** (CLAUDE.md gate). Required checks before approval: exactly one `CreateTable("DailyWellness")`, exactly one `CreateIndex` named `IX_DailyWellness_AthleteId_Date` with `unique: true`, **zero** `AddForeignKey`, and a `Down` that is a single `DropTable`. If the migration touches any other table — including `Athletes` — the model has drifted: **STOP and ask**.

### Step 2 — phase exit

Verify every ROADMAP Phase 20 success criterion:

- **Today's entry persists, survives reload.** → Manual smoke at `/` (the Today card): submit a day, reload, observe it re-renders from server state.
- **Re-submit updates not duplicates (upsert proven).** → HTTP smoke after 20-2: PUT a valid day → **200**; PUT the same day again with different values → **200**, and GET returns **one** row carrying the second call's values (observe the row count, do not infer it).
- **Sleep tile shows real 7-day avg + sparkline.** → Manual smoke at `/`: the Sleep tile displays a numeric average + sparkline, not the "soon" placeholder.
- **Resting HR sparkline reflects entries, not the onboarding constant.** → Manual smoke: with no wellness entries, the tile shows the `Athlete.RestingHr` fallback; after submitting an entry, it updates from server state.
- **Out-of-range and future dates rejected with field messages.** → HTTP smoke after 20-2: PUT future date → **400** with field message; each out-of-range metric → **400** with its field-prefixed message; `PUT /api/v1/wellness/not-a-date` → **404** (the route constraint).

Flip **both** the ROADMAP Phase 20 heading (`⏳` → `✅`) **and** its ledger row (`🚧 Specs ready` → `✅ Complete`) — they are two separate edits, and letting them drift is exactly the pre-existing Phase 16 defect listed under carry-forward 4. Then write `md/handoffs/<today>-phase-20-complete.md` (follow the Phase 19 template); update the CLAUDE.md phase pointer to "Phase 20 complete"; index **ADR-0011** in CLAUDE.md's decision list. Final commit: `docs: close out Phase 20`.

## Scope guardrails (do NOT)

- **No second migration.** `DailyWellness` table only, one reviewed set.
- **No new NuGet or npm package.**
- **Do not modify** `api/Bryk.Domain/Entities/Athlete.cs` — no `ICollection<DailyWellness>`, no FK, no column, no nullability change. It must not appear in `git diff`.
- **Do not feed wellness into the PMC:** `PmcCalculator.cs`, `AcwrCalculator.cs`, `LoadCalculator.cs`, `TimeInZoneCalculator.cs` and `AnalyticsService.cs` must not appear in `git diff`.
- **Do not edit** `ui/src/components/common/DeltaChip.vue` (D5).
- **Do not edit** `ui/src/components/training/LogWorkoutForm.vue` or `ui/src/components/common/__tests__/RpeSelector.spec.ts` (D4 regression gate).
- **Do not delete** `ui/src/components/dashboard/PlaceholderCard.vue` — Task 20-4 removes only the `HomeView` import its change orphaned; deleting a pre-existing component is out of scope.
- **Do not modify** `api/Bryk.API/Middleware/ExceptionHandlingMiddleware.cs` (Phase 21 owns the error contract).
- **Do not use** FluentValidation's `ValidateAndThrowAsync` — it throws the wrong exception type. Use `ValidateOrThrowAsync`.
- **Do not fix** the two pre-existing nullable warnings at `WorkoutsControllerTests.cs:121,150`.
- **No device/health sync, readiness scores, hydration, nutrition, menstruation fields, logging reminders.**
- **No auth code.** Phase 12 remains deferred and approval-gated. Athlete identity always via `ICurrentUserService`.
- **Do not revert, stage, or commit unrelated working-tree changes.**

## Pre-existing changes — critical reading

As of prompt generation, `git status` contains:

```
 M ROADMAP.md
?? md/Tasks-20-1.md  ?? md/Tasks-20-2.md  ?? md/Tasks-20-3.md  ?? md/Tasks-20-4.md
?? md/Impl-20-1.md   ?? md/Impl-20-2.md   ?? md/Impl-20-3.md   ?? md/Impl-20-4.md
?? md/prompts/Phase-20-Prompt.md  (this file)
```

**These are Phase 20's planning output. Do NOT discard, revert, or ignore them.** The `ROADMAP.md` modification records the resolved decisions and flips the ledger row to "🚧 Specs ready".

**Step 0 action:** If `git status` still shows these ten paths as uncommitted after you read this prompt, commit them together before any code, as the specs commit:
```
docs: add Phase 20 task specs + record the resolved Phase 20 decisions
```

If they are already committed, confirm the tree is clean and proceed.

If the tree contains anything OTHER than these ten paths, **STOP and ask the user before touching it.**

## Verification commands (runnable from repo root)

```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```

**Baselines at phase start:** 343 xUnit tests (**196** `Bryk.Application.Tests` + **147** `Bryk.API.Tests`), 288 Vitest tests across 61 files, 16 known build warnings (14 design-time `System.Security.Cryptography.Xml` NU1903 + 2 pre-existing `WorkoutsControllerTests.cs` nullable warnings). Both suites must RISE with zero failures; warning count must not grow past 16.

**Runtime gates (do not just compile; run the app):** Dev stack: API via `dotnet run` from `api/Bryk.API` with **`ASPNETCORE_ENVIRONMENT=Development`** (https://localhost:60129); UI via `pnpm dev` from `ui/` (vite proxies `/api` → 60129). Stop the API before rebuilding — a running app locks the DLL.

- After **20-2**: HTTP-smoke a valid wellness day → **200**; re-submit the same day → **200** with **one row** in GET (the upsert gate); future date → **400**; all-metrics-null → **400** `"Entry: At least one metric is required."`; each out-of-range metric → **400** with its field-prefixed message; `GET /summary` on a fresh athlete → `hasAnyEntries: false` with **null** averages, not zeros.
- After **20-3**: Build green; test green; no console errors.
- After **20-4**: Open `/` in the browser; the Today entry card submits and updates all four tiles from server truth without reload; the Sleep tile shows a real 7-day average + sparkline (not "soon"); console clean. **Note the preview-pane caveat (carried from Phases 18–19):** if the in-app Browser pane is used, `requestAnimationFrame` freezes, route transitions stall, and numeric tiles render **blank** (not `0`, and not `—`). Shim `window.requestAnimationFrame = (cb) => setTimeout(() => cb(performance.now()), 16)` before testing in-app navigation.

## Failure honesty clause

If a verification command fails for an unrelated environment reason (SQL Server unavailable, missing user-secrets, port in use, the known Vitest worker crash), capture the exact output verbatim, explain what it was and why it is unrelated, and **do not claim success**. Never report a phase or task as complete on a red or unrun gate. If a ROADMAP success criterion cannot be observed, say so explicitly and mark it partial.

## Final reporting requirements

End with a status from **DONE / DONE_WITH_CONCERNS / NEEDS_CONTEXT / BLOCKED**, then:

- Files changed (grouped by task) with brief surface summary.
- Build + test results with actual counts, not "green".
- What was actually observed at each runtime gate (including the row count after duplicate PUT and the `not-a-date` status code).
- Review outcomes (all non-goals held? `Athlete.cs`, `DeltaChip.vue`, `LogWorkoutForm.vue`, the PMC calculators untouched?).
- Explicit confirmation that exactly **one** migration and **zero** new packages landed.
- Residual risks and carry-forward items.
- Final `git status`.

## Known carry-forward to record in the handoff

1. The **POST/PUT periodization validator bounds divergence** from Phase 18 is still open and needs a Sr. Dev decision (deferred twice now): `TrainingPlanRequestValidator` accepts `BuildWeeks > 0` / `RecoveryWeeks > 0` / `RecoveryWeekPercentage` 0–100, while `TrainingPlanUpdateRequestValidator` bounds them 1–8 / ≥ 1 / 30–90.
2. The Phase 19 zone histogram is JSON on `ActivityFile` rather than a normalized table — a Phase-21 candidate.
3. `ui/src/lib/charts/load.ts:65` labels the last bar `· NOW` — a known cosmetic artifact; do not fix mid-phase.
4. **ROADMAP doc drift (pre-existing):** the Phase 16 *heading* reads `⏳` although its ledger row reads `✅`. Carried from Phases 17–19; outside Phase 20's scope.
5. Zone-bar markup is duplicated between `components/import/ZoneHistogramBars.vue` and `components/analytics/TimeInZoneSection.vue`, and the `%HRmax` scheme is duplicated between `ZoneHistogramCalculator` and `TimeInZoneCalculator` — both deliberate Phase 19 artifacts, both clean standalone follow-ups.
