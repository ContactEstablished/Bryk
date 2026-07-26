# Execution Prompt — Phase 18: ATP / periodization engine (weekly targets, ramp, taper)

> Paste this prompt into a fresh session rooted at the Bryk repo. Run only after Phases 14, 16, 17 and prior phases are complete (Phase 18 depends on load math, compliance bands, and the live event surface).

You are the Senior Solutions Architect for the Bryk project. You design, implement, and validate the work directly.

## Required reading, in order, before any code

1. `CLAUDE.md` — every convention applies.
2. `ROADMAP.md` → the **Phase 18** entry (lines ~500–526, goal + scope).
3. `md/handoffs/2026-07-25-phase-17-complete.md` — the latest handoff; its "Session-start checklist" and carry-forward list.
4. `md/decisions/0008-calendar-compliance.md` — §1 compliance bands (Phase 18 reuses them verbatim on the dashboard) and §2 the plan-window rule.
5. `md/decisions/0007-progress-analytics.md` §1 (lines 53–66) — the optimal band `[0.8, 1.3] × A`; `A` is Phase 18's baseline and `1.3 × A` its ceiling.
6. `md/decisions/0003-trainingplan-domain-shape.md` line 59 — `RecoveryWeekPercentage` "e.g. `60.0`" (percent scale, not the `0.3–0.9` fraction the ROADMAP prose originally said).
7. `md/Tasks-18-1.md` … `md/Tasks-18-5.md` — the task contracts (task scope, surface, dependencies, acceptance criteria, verification).
8. `md/Impl-18-1.md` … `md/Impl-18-5.md` — the step-by-step build orders (each step has a **Verify** gate to the next).

## Session-start checklist

Clean tree; `dotnet build`/`dotnet test api/Bryk.sln` green; `pnpm run build`/`pnpm test` green from `ui/`; user-secrets present; seed data loaded. Vitest's transient worker-crash-with-all-passing → re-run once.

## Important context

- **Phase 12 (auth) may not have shipped.** Execute on the DevAuth stub; athlete resolution through `ICurrentUserService` only.
- **No migration, no new package.** Every column already exists; targets compute on read. If any task appears to need either — **STOP and ask**.
- **`RecoveryWeekPercentage` is percent-scale (0–100).** The ROADMAP Phase 18 entry originally said `0.3–0.9` fraction; the code and ADR-0003 say percent (`decimal(5,2)`, "e.g. `60.0`"). The ROADMAP.md was corrected on 2026-07-26. The new PUT validator (18-2) bounds it to **30–90**; the existing POST validator is frozen (tightening it is a breaking change). Record the divergence in the 18-2 commit body.
- **`BuildWeeks` / `RecoveryWeeks` / `RecoveryWeekPercentage` have never been written by any UI.** `TrainingPlanRequest` deliberately omits them; the POST validator accepts them but no form has ever wired them. Phase 18's 18-4 panel is their first write path.
- **Compliance thresholds are locked.** Phase 16's ADR-0008 §1 defines green `[0.8, 1.2]`, yellow `[0.5, 0.8) ∪ (1.2, ∞)`, red `< 0.5`; Phase 18 reuses them verbatim on the dashboard target-vs-actual bar (18-5). Don't invent variants.
- **Plan window is authoritative.** ADR-0008 §2 locks the reschedule rule; ADR-0009 §5 extends it: a PUT that would strand planned workouts is rejected 400 with a `PlanWindow:`-prefixed message.

## Mission

Deliver **Phase 18 — ATP / periodization engine** end to end.

### Step 0 — verify the working tree and lock decisions

Before any code:

1. `git status` clean; `git log --oneline -5` shows Phase 17 as the latest complete phase.
2. `dotnet build api/Bryk.sln`, `dotnet test api/Bryk.sln` — expect 201 xUnit tests green, 16 known warnings.
3. `pnpm run build` from `ui/`, `pnpm exec vitest run --no-file-parallelism` from `ui/` — expect **229 tests / 53 files** green.
4. If `git status` shows modified files, they are likely the Phase 18 planning output (ROADMAP.md change + Tasks-18-1..5.md + Impl-18-1..5.md). See "Pre-existing changes" section below. Those files are **not errors**; they are the specs — commit them together before coding if they are uncommitted:
   ```
   docs: add Phase 18 task specs + correct RecoveryWeekPercentage scale
   ```
   If the tree has anything other than these eleven paths uncommitted, **STOP and ask the user before touching it.**

The **Decisions needed** from the ROADMAP entry are already locked in the planning output. Verify against the files you've read:

- **ADR-0009 will be written as Task 18-1's first step**, not in this preamble. Read it when you write it.
- **The ramp model's three key facts:** baseline = trailing 4-week mean actual load (ADR-0007's `A`); ramp = **+7% per build week** (1.07⁴ = 1.31, derived from the locked ACWR 1.3 ceiling); `BuildWeeks : RecoveryWeeks` cadence with recovery weeks at `RecoveryWeekPercentage%` of the build target.
- **Taper:** two weeks (event week = 50%, week before = 75% of the un-tapered build target) when the linked event's `EventDate` is inside `[StartDate, EndDate]`. Taper overrides recovery scaling.
- **Compute-on-read:** no `WeeklyTarget` table, no migration.
- **Orphan policy:** a PUT shrinking the window such that existing `PlannedWorkout.ScheduledDate` values fall outside is rejected 400.

### Step 1 — implement, one task per commit (strict order)

Task dependency chain is strict. Do not parallelize; later tasks depend on earlier ones' files:

**Order: 18-1 → 18-2 → 18-3 → 18-4 → 18-5.**

- **18-1 and 18-2 are genuinely independent** (no shared files); land 18-1 first so ADR-0009 exists before anything cites it.
- **18-3 depends on both.** It consumes 18-1's `WeeklyTargetCalculator`, and it shares `api/Bryk.API/Controllers/TrainingPlansController.cs` with 18-2 — never edit that file from two sessions or in two concurrent tasks.
- **18-4 depends on 18-2 + 18-3** (it calls both endpoints).
- **18-5 depends on 18-3**, and shares `ui/src/types/training.ts` with 18-4 — land 18-4 first.

For each task:

1. Read the task's `Tasks-18-N.md` (the contract) and `Impl-18-N.md` (step-by-step walkthrough).
2. Follow `Impl-18-N.md` top-to-bottom, treating each **Verify** gate as a hard stop before the next step.
3. Build + test + diff-read after the code.
4. Surface the commit message from the `## Suggested commit` section of the Tasks doc.

**Commit message discipline:** Plain conventional-commit messages only. Do NOT append `Co-Authored-By:` or any AI co-author trailer — it skews the GitHub contributor count. The commit author is already the repo git user (Matthew Wilson).

**Approval gates:** None expected — no migration, no new package are in scope. If either becomes necessary, **STOP and ask** (Sr. Dev gate).

### Step 2 — phase exit

Verify every ROADMAP Phase 18 success criterion:

- **3-build/1-recovery/60% on a 12-week linked plan yields a visible ramp** with every 4th week dipped and a race-week taper, reproducible via pinned unit tests. → Demonstrated by Task 18-1 xUnit vector + 18-3 integration test.
- **This Week shows target vs actual flipping state on log.** → Manual smoke at the end of 18-5: log a workout, reload, watch the bar's colour and `DeltaChip` direction change.
- **Plan PUT round-trips from the UI.** → 18-4 smoke: open a plan, edit the metadata, save, verify the ramp redraws.
- **Foreign plan 404s.** → Integration tests in 18-2/18-3.

Flip the ROADMAP Phase 18 heading to ✅; write `md/handoffs/<today>-phase-18-complete.md` (follow the Phase 17 template); update the CLAUDE.md phase pointer to "Phase 18 complete"; index ADR-0009 in CLAUDE.md's decision list. Final commit: `docs: close out Phase 18`.

## Scope guardrails (do NOT)

- **No migration.** Every column already exists. If a task appears to need one — **STOP and ask**. Same for snapshot tables, caching, or any schema change.
- **No new NuGet or npm package** (no chart lib, no date-picker, no new shadcn primitive).
- **Do not modify** `TrainingPlanRequest` / `TrainingPlanRequestValidator` (frozen for the whole phase — tightening a shipped POST is a breaking change requiring Sr. Dev approval). The POST/PUT bounds divergence is known and accepted; record it in the commit body of 18-2.
- **Do not modify** `WeeklyLoadCalculator.cs`, `ComplianceClassifier.cs`, `LoadChart.vue`, `lib/charts/load.ts`, or `DeltaChip.vue`.
- **Do not refactor the duplicated Monday-week expressions** (`AnalyticsService.cs:186`, `ThisWeekService.cs:44`) into a shared helper. Duplicate them locally; the third copy (in 18-1 and 18-3) will be noted as tech debt in the handoff.
- **Do not fix** the two pre-existing nullable warnings at `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs:121,150` — they predate this phase.
- **Do not** add the weekly target to calendar week headers (ROADMAP marks it "optionally"). That's a follow-up.
- **Do not** auto-generate planned workouts from targets (targets are numbers; authoring stays manual).
- **No auth code.** Phase 12 remains deferred and approval-gated. Athlete identity always via `ICurrentUserService`.
- No multi-event season ATP, no per-sport target split, no coach overrides, no `IClock`, no `DateTime.UtcNow` inside calculators.
- **Do not revert, stage, or commit unrelated working-tree changes.**

## Verified code facts (checked 2026-07-26)

Read these during the relevant task:

- `api/Bryk.Domain/Entities/TrainingPlan.cs:14-16` — `BuildWeeks int?`, `RecoveryWeeks int?`, `RecoveryWeekPercentage decimal?` already exist. `EventId Guid?` at L10.
- `api/Bryk.API/Controllers/TrainingPlansController.cs` — has no plan-metadata PUT (verified gap; 18-2 closes it).
- `api/Bryk.Domain/Interfaces/ITrainingPlanRepository.cs:65` — `void Update(TrainingPlan entity)` exists and is currently unused; 18-2 uses it. No repository change needed.
- `api/Bryk.Application/Analytics/AnalyticsService.cs:57-107` — `GetWeeklyLoadAsync`, the reference aggregation both 18-3 and 18-5 mirror. `:186` is the Monday-anchored `WeekStart` helper.
- `api/Bryk.Application/Analytics/WeeklyLoadCalculator.cs:11` — comment already reads "Phase 18's ramp cap".
- `api/Bryk.API/Program.cs:35` — `AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>()` (validators auto-register). `:100-120` — manual `AddScoped` service list; Phase 18 adds exactly ONE line (18-3's `IPeriodizationService`).
- `ui/src/views/PlanDetailView.vue:66-74` — the read-only plan header block 18-4 replaces (its comment literally says "metadata editing is Phase 18").
- `ui/src/components/charts/LoadChart.vue` props are `{ weeks: WeeklyLoadWeek[]; optimalBand: OptimalBand | null }`; 18-4 adapts targets onto this WITHOUT modifying the chart or `ui/src/lib/charts/load.ts`.
- `ui/src/components/common/DeltaChip.vue` props `{ dir: 'up' | 'down' | 'flat' }` + slot — reused unchanged by 18-5.

## Pre-existing changes — critical reading

As of prompt generation, `git status` contains:

```
 M ROADMAP.md                      (corrected RecoveryWeekPercentage scale)
?? md/Tasks-18-1.md … md/Tasks-18-5.md
?? md/Impl-18-1.md … md/Impl-18-5.md
```

**These are Phase 18's planning output. Do NOT discard, revert, or ignore them.** The `ROADMAP.md` modification is a deliberate factual correction.

**Step 0 action:** If `git status` still shows these eleven paths as uncommitted after you read this prompt, commit them together before any code, as the specs commit:
```
docs: add Phase 18 task specs + correct RecoveryWeekPercentage scale
```

If they are already committed, confirm the tree is clean and proceed.

If the tree contains anything OTHER than these eleven paths, **STOP and ask the user before touching it.**

## Verification commands (runnable from repo root)

```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```

**Baselines at phase start:** 201 xUnit tests, 229 Vitest tests across 53 files, 16 known build warnings (design-time `System.Security.Cryptography.Xml` NU1903 + two pre-existing `WorkoutsControllerTests.cs` nullable warnings). Both suites must RISE with zero failures; warning count must not grow. Vitest transient worker-fork crash reports "Errors N" with every test passing — re-run once with `--no-file-parallelism` before debugging.

**Runtime gates (do not just compile; run the app):** Dev stack: API via `dotnet run` from `api/Bryk.API` (https://localhost:60129); UI via `pnpm dev` from `ui/` (vite proxies `/api` → 60129).

- After **18-2**: HTTP-smoke the PUT — happy path 200, unknown id 404, orphan-stranding window 400 with a `PlanWindow:` error, foreign `eventId` 400 with an `EventId:` error.
- After **18-3**: GET weekly-targets — a fresh athlete returns **200 with an empty `weeks` array and `baselineSource: "None"`** (NOT 404); a seeded athlete returns a ramping series.
- After **18-4**: open a seeded plan at `/plans/:id` — the summary renders, Edit opens the form, a valid save round-trips and the ramp redraws, a window shrink that strands workouts surfaces the server's `PlanWindow:` text verbatim. Console clean.
- After **18-5** (the ROADMAP success criterion — **observe it, do not infer it**): open the dashboard, note the bar's colour and `DeltaChip` direction, log a workout that pushes the week's actual across a band boundary, reload, confirm both change. **Record the observed before/after values in the handoff.**

## Failure honesty clause

If a verification command fails for an unrelated environment reason (SQL Server unavailable, missing user-secrets, port in use, the known Vitest worker crash), capture the exact output verbatim, explain what it was and why it is unrelated, and **do not claim success**. Never report a phase or task as complete on a red or unrun gate. If a ROADMAP success criterion cannot be observed, say so explicitly and mark it partial — the Phase 17 handoff's honest "⚠️ partial" entry is the precedent.

## Final reporting requirements

End with a status from **DONE / DONE_WITH_CONCERNS / NEEDS_CONTEXT / BLOCKED**, then:

- Files changed (grouped by task) with brief surface summary.
- Build + test results with actual counts, not "green".
- What was actually observed at each runtime gate (before/after values where the ROADMAP asks for them).
- Review outcomes (all non-goals held? frozen files untouched?).
- Explicit confirmation that no migration and no new package landed.
- Residual risks, known artifacts (the `NOW` label in load.ts), and carry-forward items.
- Final `git status`.

## Known carry-forward for the handoff

`lib/charts/load.ts:65` labels the last bar `· NOW`, which in the periodization panel is the plan's final week rather than the current week — a documented cosmetic artifact of reusing the chart unforked. Note it in the Phase 18 handoff; do not "fix" it mid-phase.
