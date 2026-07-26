# Task 18-5 — Dashboard tie-in: This Week target vs actual

## Surface
Backend **and** frontend. `ThisWeekResponse` gains `TargetLoad` (nullable) + `ActualLoad`;
`ThisWeekService` gains the plan-selection rule, the completed-workout read and the periodization
service; its unit tests are extended (existing stubs must grow). On the client: the mirrored type
fields, one pure helper `ui/src/lib/weeklyTarget.ts`, a target-vs-actual progress bar + `DeltaChip` in
`ThisWeekCard.vue`, and specs. **No migration, no new package, no new endpoint, no new component.**

## Why
This is the phase's payoff on the surface an athlete looks at daily. Today `ThisWeekCard` shows
"`N` sessions · `X` TSS planned" and has **no actual-load source at all** — logging a workout changes
nothing on the dashboard. The ROADMAP's success criterion is explicit: "This Week shows target vs actual
flipping state on log." Reusing ADR-0008 §1's compliance bands verbatim for the bar's state is the
cross-phase contract that ADR-0008 itself anticipated ("Phase 18 reuses this"): one rule, one set of
thresholds, whether you are looking at a calendar day or a dashboard week. Computing the target
server-side (rather than having the card fetch weekly-targets and pick a row) keeps the card a single
round-trip and keeps the plan-selection rule in one testable place.

## Depends on
- **Task 18-1** — the ramp math (indirectly, via 18-3).
- **Task 18-3** — `IPeriodizationService.GetWeeklyTargetsAsync` and its DI registration. Hard
  dependency: `ThisWeekService` will not resolve without it.
- **ADR-0008 §1** — the bands reused verbatim: green `[0.8, 1.2]`, yellow `[0.5, 0.8) ∪ (1.2, ∞)`,
  red `< 0.5`, and the "planned 0 ⇒ ratio 1.0, don't div-by-zero" degenerate rule.
- **ADR-0009 §1** — no baseline ⇒ no targets ⇒ the card renders exactly as it does today.
- **Shares `ui/src/types/training.ts` with Task 18-4.** Land 18-4 first.

## Required reading
- `api/Bryk.Application/Training/ThisWeekService.cs` — the whole file (71 lines): the primary ctor
  `(ICurrentUserService, ITrainingPlanRepository, IAthleteRepository, IZoneService)`, `CurrentWeek()`
  (L41–46, the Monday anchor + `DateOnly.FromDateTime(DateTime.UtcNow)`), and the planned-load `Map`.
- `api/Bryk.Application/Training/ThisWeekResponse.cs` — the four existing properties and the comment
  style to extend.
- `api/Bryk.Application/Analytics/AnalyticsService.cs:86–93` — the actual-load aggregation
  (`GetByAthleteInRangeAsync` + `LoadOverride ?? ComputedLoad ?? 0m`) to reuse for a single week.
- `api/Bryk.Domain/Interfaces/ITrainingPlanRepository.cs:23` — `GetByAthleteIdAsync` (all the athlete's
  plans, `StartDate` asc, entity only, no-tracking) — the plan-selection input; **no new repo read**.
- `api/Bryk.Application/Calendar/ComplianceClassifier.cs` — the band constants and their exact
  inclusivity (`>= 0.8 && <= 1.2` green; `>= 0.5` yellow; else red) that the client helper mirrors.
- `api/Bryk.Application.Tests/Training/ThisWeekServiceTests.cs` — the file to extend; note that
  `StubTrainingPlanRepository.GetByAthleteIdAsync` currently **throws** `NotImplementedException`
  (L112) and must start returning a configurable list, and that new ctor dependencies mean new stubs.
- `api/Bryk.API.Tests/Training/ThisWeekControllerTests.cs` — the integration harness for the endpoint.
- `ui/src/components/dashboard/ThisWeekCard.vue` — the header at L59–67 (`{{ store.thisWeek.weeklyLoad
  ?? 0 }} TSS planned`), the `onMounted` load, and the session list. The new bar goes **between** the
  header and the session list, inside the `p-6` body.
- `ui/src/components/common/DeltaChip.vue` — props are exactly `{ dir: 'up' | 'down' | 'flat' }` plus a
  default slot; `up` renders `ArrowUp` + `text-good`, `down` renders `ArrowDown` + `text-bad`,
  `flat` renders no icon + muted. **Reused as-is.**
- `ui/src/lib/progressRing.ts` — the precedent for a small pure geometry/state helper in `lib/` with
  its own spec.
- `ui/src/style.css:147–149` — `--color-good` / `--color-warn` / `--color-bad` are registered in the
  Tailwind theme, so `bg-good` / `bg-warn` / `bg-bad` / `text-good` utilities exist. Use them; do not
  add CSS variables.
- `ui/src/types/training.ts:29–35` — `ThisWeekResponse`, where `weeklyLoad?: number` is **optional**;
  the new fields follow suit (existing specs construct this object without `weeklyLoad`).
- `ui/src/components/dashboard/__tests__/ThisWeekCard.spec.ts` — the mount harness to extend; its
  three existing tests must keep passing untouched.

## Acceptance criteria

### `api/Bryk.Application/Training/ThisWeekResponse.cs` (additive)
```csharp
// The week's load target from the athlete's active plan (ADR-0009). Null when no plan covers today,
// or when the plan has no usable baseline — the card then renders exactly as it did before Phase 18.
public decimal? TargetLoad { get; set; }
// Σ EffectiveLoad (LoadOverride ?? ComputedLoad) of the athlete's completed workouts in the week.
public decimal ActualLoad { get; set; }
```
`WeekStart`, `WeekEnd`, `WeeklyLoad`, `PlannedWorkouts` keep their current names, types and meaning —
`WeeklyLoad` stays the **planned** sum. This is an additive response change (not breaking).

### `api/Bryk.Application/Training/ThisWeekService.cs`
- Primary ctor gains two dependencies:
  `(ICurrentUserService currentUser, ITrainingPlanRepository planRepo, IAthleteRepository athleteRepo,
  IZoneService zoneService, IWorkoutRepository workoutRepo, IPeriodizationService periodization)`.
  Both are already DI-registered (`IWorkoutRepository` at `Program.cs:106`; `IPeriodizationService` by
  Task 18-3).
- After the existing planned-workout work, in `GetThisWeekAsync`:
  1. **Actual load.** `completed = await workoutRepo.GetByAthleteInRangeAsync(athleteId, weekStart, weekEnd, ct);`
     `actualLoad = Math.Round(completed.Sum(w => w.LoadOverride ?? w.ComputedLoad ?? 0m), 2);`
  2. **Active-plan selection (ADR-0009 / Phase 18 decision).** `plans = await planRepo.GetByAthleteIdAsync(athleteId, ct);`
     `active = plans.Where(p => p.StartDate <= today && today <= p.EndDate).OrderByDescending(p => p.StartDate).FirstOrDefault();`
     — the plan whose window **contains today**, ties broken by the **latest `StartDate`** (the most
     recently begun plan wins an overlap). `today` comes from the same `CurrentWeek()` computation;
     lift it out so the method computes `DateOnly.FromDateTime(DateTime.UtcNow)` exactly once.
     None → `TargetLoad = null`.
  3. **Target lookup.** When `active` is not null:
     `var targets = await periodization.GetWeeklyTargetsAsync(active.Id, ct);`
     `targetLoad = targets.Weeks.FirstOrDefault(w => w.WeekStart == weekStart)?.TargetLoad;`
     — a plan whose targets are empty (no baseline) or whose window does not include the current ISO
     week yields `null`. Do **not** interpolate, clamp, or fall back to `WeeklyLoad`.
  4. Populate `TargetLoad` + `ActualLoad` on the response.
- The service stays read-only: no `IUnitOfWork`, no writes. Do not change `CurrentWeek()`'s Monday math
  or the existing `Map`.
- Add a one-line comment noting that reusing `IPeriodizationService` costs an extra plan/workout read
  on the dashboard call and is accepted for v1 over duplicating the ramp math.

### `ui/src/types/training.ts` (additive, on `ThisWeekResponse`)
```ts
  targetLoad?: number | null
  actualLoad?: number
```
Both **optional**, matching the existing `weeklyLoad?: number`, so the fixtures in the current
`ThisWeekCard.spec.ts` still type-check unchanged.

### `ui/src/lib/weeklyTarget.ts` (new, pure)
```ts
export type TargetState = 'good' | 'warn' | 'bad'

export interface TargetProgress {
  ratio: number
  state: TargetState
  dir: 'up' | 'down' | 'flat'
  deltaLabel: string
  widthPct: number
}

export function buildTargetProgress(actual: number, target: number): TargetProgress
```
Rules — ADR-0008 §1 bands, verbatim:
- `ratio = target === 0 ? 1 : actual / target` (the degenerate div-by-zero guard, same as the classifier).
- `state`: `ratio >= 0.8 && ratio <= 1.2` → `'good'`; else `ratio >= 0.5` → `'warn'`; else `'bad'`.
  (`ratio > 1.2` therefore lands in `'warn'` — the classifier's upper tail, unchanged.)
- `dir`: `ratio > 1.2` → `'up'`; `ratio < 0.8` → `'down'`; else `'flat'`. `DeltaChip` colours `up` green
  and `down` red — it reports the **direction of the delta**; the bar carries the honest band colour.
  Add that sentence as a comment so nobody "fixes" the chip's colours.
- `deltaLabel`: `const d = Math.round(actual - target)` → `` `${d > 0 ? '+' : ''}${d} TSS` `` (ASCII
  minus comes from the number itself; no Unicode minus).
- `widthPct`: `Math.round(Math.min(100, Math.max(0, ratio * 100)))` — clamped, integer.
No imports, no Vue, no `Date`.

### `ui/src/components/dashboard/ThisWeekCard.vue`
- Add a `targetProgress` computed: `null` when `store.thisWeek?.targetLoad == null`, else
  `buildTargetProgress(store.thisWeek.actualLoad ?? 0, store.thisWeek.targetLoad)`.
- When `targetProgress` is `null`, the card renders **exactly** as before (same header text, same list,
  same empty state — no placeholder bar, no "—", no dash row). Assert this in a spec.
- Otherwise, render between the header and the session list:
  - a label row: `"{actualLoad} / {targetLoad} TSS"` (mono, the card's existing `text-[11px]` scale) on
    the left and `<DeltaChip :dir="targetProgress.dir">{{ targetProgress.deltaLabel }}</DeltaChip>` on
    the right;
  - a track (`h-1.5 rounded-full bg-muted overflow-hidden`) containing a fill whose
    `:style="{ width: targetProgress.widthPct + '%' }"` and whose class is
    `bg-good` / `bg-warn` / `bg-bad` from `targetProgress.state`;
  - `role="progressbar"` with `aria-valuenow` = `widthPct`, `aria-valuemin="0"`, `aria-valuemax="100"`,
    and an `aria-label` such as `"Weekly load: {actual} of {target} TSS"` (the visual bar alone is not
    accessible).
- The existing header keeps saying `… TSS planned` (planned ≠ target; do not repurpose that string).
- No new component, no new import beyond `DeltaChip` and the helper.

## Non-goals
- **No migration.** No column, no `WeeklyTarget` table. If it looks needed — **STOP and ask**.
- **No new NuGet or npm package.**
- **Do not** add a new endpoint or query parameter; This Week stays one GET.
- **Do not modify** `WeeklyLoadCalculator.cs`, `ComplianceClassifier.cs` (the bands are **copied** into
  the client helper, not refactored into a shared module), `LoadChart.vue`, or `lib/charts/load.ts`.
- **Do not modify** `DeltaChip.vue` — including its `up`/`down` colour mapping.
- **Do not modify** `TrainingPlanRequest`/`TrainingPlanRequestValidator`, 18-2's update DTO/validator,
  18-1's calculator, or 18-3's `PeriodizationService`. If a target looks wrong, fix it in the owning
  task's file **with a new pinned test** and say so in the commit body.
- **Do not** rename or repurpose `ThisWeekResponse.WeeklyLoad` (it is the planned sum and other callers
  read it).
- **Do not** add the weekly target to the calendar week headers — the ROADMAP marks that "optionally",
  and it is not in this phase's task split. Note it as a follow-up instead.
- **Do not** touch `PeriodizationPanel.vue`, `PlanDetailView.vue`, or the Progress page.
- **Do not fix** the two pre-existing nullable warnings in `WorkoutsControllerTests.cs:121,150`.
- **Do not** revert, stash, or commit unrelated working-tree changes.
- **No auth code** — Phase 12 stays deferred and approval-gated.
- No per-sport split, no multi-event ATP, no coach overrides, no auto-generated planned workouts.

## Test expectations

**Unit — `api/Bryk.Application.Tests/Training/ThisWeekServiceTests.cs` (extend).**
`NewService(...)` gains a `StubWorkoutRepository` (range-filtering a configured completion list; other
members `NotImplementedException`) and a `StubPeriodizationService` returning a configured
`WeeklyTargetsResponse`; `StubTrainingPlanRepository.GetByAthleteIdAsync` starts returning a
configurable plan list instead of throwing. The four existing tests must keep passing.
- `GetThisWeekAsync_NoPlanCoversToday_TargetLoadIsNull` — plans that ended yesterday / start tomorrow →
  `TargetLoad == null`, and the periodization stub was **never** called.
- `GetThisWeekAsync_PlanCoveringToday_ReturnsThisWeeksTarget` — the stub returns weeks including
  `WeekStart == this week's Monday` with `TargetLoad = 320.00m` → `result.TargetLoad == 320.00m`.
- `GetThisWeekAsync_OverlappingPlans_PicksTheLatestStartDate` — two plans both containing today; assert
  the periodization stub was called with the id of the one whose `StartDate` is later.
- `GetThisWeekAsync_PlanWithNoTargets_TargetLoadIsNull` — stub returns `Weeks = []` → `null`
  (no fallback to `WeeklyLoad`).
- `GetThisWeekAsync_TargetsMissingTheCurrentWeek_TargetLoadIsNull`.
- `GetThisWeekAsync_ActualLoad_SumsEffectiveLoadOfTheWeeksCompletions` — two completions in the week
  (`LoadOverride = 40m`; `ComputedLoad = 25m`, `LoadOverride = null`) plus one 10 days ago →
  `ActualLoad == 65.00m`.
- `GetThisWeekAsync_NoCompletions_ActualLoadIsZero` — `0m`, not null.

**Integration — `api/Bryk.API.Tests/Training/ThisWeekControllerTests.cs` (extend).**
- `GetThisWeek_FreshAthlete_ReturnsNullTargetAndZeroActual` — `targetLoad` null, `actualLoad` `0`.
- `GetThisWeek_WithAnActivePlanAndHistory_ReturnsATarget` — seed four past completions with
  `loadOverride`, a plan whose window contains today, then GET → `targetLoad` non-null and
  `actualLoad` reflecting any in-week completion. Keep the seeded dates relative to
  `DateOnly.FromDateTime(DateTime.UtcNow)` (`CompletedDate` may not be in the future).

**Frontend — `ui/src/lib/__tests__/weeklyTarget.spec.ts` (new).** Pin every boundary:
| actual | target | state | dir | deltaLabel | widthPct |
|---|---|---|---|---|---|
| 80 | 100 | `good` | `flat` | `-20 TSS` | 80 |
| 79 | 100 | `warn` | `down` | `-21 TSS` | 79 |
| 100 | 100 | `good` | `flat` | `0 TSS` | 100 |
| 120 | 100 | `good` | `flat` | `+20 TSS` | 100 |
| 121 | 100 | `warn` | `up` | `+21 TSS` | 100 |
| 50 | 100 | `warn` | `down` | `-50 TSS` | 50 |
| 49 | 100 | `bad` | `down` | `-51 TSS` | 49 |
| 0 | 0 | `good` | `flat` | `0 TSS` | 100 |
(the last row pins the div-by-zero guard: ratio 1 ⇒ good, full width).

**`ui/src/components/dashboard/__tests__/ThisWeekCard.spec.ts` (extend).**
- `renders the target-vs-actual bar when a target is present` — fixture `targetLoad: 300`,
  `actualLoad: 240` → text contains `240 / 300 TSS` and `-60 TSS`; the fill element carries `bg-good`
  and `width: 80%`; `role="progressbar"` with `aria-valuenow="80"` exists.
- `flips the bar state when the athlete falls behind` — `actualLoad: 100`, `targetLoad: 300` → the fill
  carries `bg-bad` and the `DeltaChip` `dir` prop is `down`.
- `renders no bar at all when targetLoad is null` — the three existing fixtures (which omit the new
  fields) must produce **no** `role="progressbar"` element and no `DeltaChip`; the existing three tests
  stay green unmodified.

## Verification commands
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```
Both suites must rise from their post-18-4 counts with zero failures (baselines at phase start:
**201** xUnit, **229 / 53 files** Vitest). Warning count must not grow past the known 16. Re-run Vitest
once before debugging a worker crash with all tests passing (known transient fork quirk).

**Manual smoke (the ROADMAP success criterion — do this, don't infer it):** with the dev seed, open the
dashboard, note the bar's state, log a workout that pushes the week's actual across a band boundary,
reload, and confirm the bar's colour and the `DeltaChip` direction change. Record the observed values
in the phase handoff.

## Review checklist
- [ ] `ThisWeekResponse` change is additive; `WeeklyLoad` still means planned.
- [ ] Plan selection is "window contains today, latest `StartDate` wins"; no plan ⇒ `TargetLoad` null
      and the periodization service is not called.
- [ ] `TargetLoad` is never faked from `WeeklyLoad` when targets are unavailable.
- [ ] `ActualLoad` is `0` (not null) with no completions, and uses `LoadOverride ?? ComputedLoad ?? 0`.
- [ ] The client helper's thresholds are character-identical to `ComplianceClassifier`'s
      (`[0.8, 1.2]` good, `[0.5, 0.8) ∪ (1.2, ∞)` warn, `< 0.5` bad, target 0 ⇒ ratio 1).
- [ ] With no target the card's DOM is unchanged from Phase 17 (the three original specs pass untouched).
- [ ] The bar exposes `role="progressbar"` + `aria-valuenow` + a label; the colour is not the only cue
      (the delta text carries the number).
- [ ] `DeltaChip.vue` is absent from `git diff`.
- [ ] `ThisWeekServiceTests`' pre-existing four tests still pass with the widened stubs.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

## Suggested commit
```
feat: This Week target vs actual (ADR-0008 bands on the dashboard)

ThisWeekResponse gains TargetLoad (nullable) and ActualLoad, closing the
card's long-standing gap: it had no actual-load source at all, so logging a
workout changed nothing on the dashboard. ThisWeekService now sums the
week's completed EffectiveLoad and resolves the week's target through the
Phase 18 periodization service, selecting the plan whose window contains
today (ties to the latest StartDate). No plan, no baseline, or a plan that
does not cover the current ISO week all yield a null target - never a
target faked from the planned sum - and the card then renders exactly as it
did before.

The card grows a target-vs-actual bar plus a DeltaChip. Its state comes from
a pure buildTargetProgress helper that reuses ADR-0008 1's compliance bands
verbatim ([0.8, 1.2] good, [0.5, 0.8) and (1.2, inf) warn, below 0.5 bad,
zero target guarded to ratio 1), so a dashboard week and a calendar day are
graded by one rule. The bar is a labelled progressbar, not colour alone.

No migration, no new endpoint, no new package; DeltaChip and
ComplianceClassifier are reused unchanged. xUnit pins plan selection,
overlap, the missing-week and empty-target cases and the actual-load sum;
Vitest pins every band boundary and that a null target leaves the card's
markup untouched.
```
