# Task 18-1 — ADR-0009 (ramp model) + `WeeklyTargetCalculator` + unit tests

## Surface
Backend only, and **pure**. One new ADR (`md/decisions/0009-periodization-ramp-model.md`), one pure
static calculator in a new folder `api/Bryk.Application/Training/Periodization/`, its input/output
DTOs, and one xUnit file. **No service, no repository, no controller, no `Program.cs` line, no UI, no
migration, no new package.** Nothing in this task is reachable over HTTP — 18-3 wires it up.

## Why
`BuildWeeks` / `RecoveryWeeks` / `RecoveryWeekPercentage` have sat dormant on `TrainingPlan` since
ADR-0003 (all three are nullable, "forward-looking", never read by any code path). Phase 18 makes them
mean something. The ROADMAP's *Decisions needed* line says the ramp ADR is written **before any code
task**, exactly as ADR-0008 preceded Phase 16 — because baseline source, ramp rate, and taper rule are
product decisions that get quietly wrong if they are settled inside an implementation PR. The
calculator ships in the same task as the ADR so the ADR's worked example is executable: it is a pure
function of (plan window, baseline, cadence fields, optional event date) with no I/O and no
`DateTime.UtcNow`, mirroring `PmcCalculator` / `WeeklyLoadCalculator` / `ComplianceClassifier` /
`GoalProgress`. Every number in the ROADMAP's success criterion ("3-build/1-recovery/60 % on a 12-week
linked plan yields a visible ramp with every 4th week dipped and a race-week taper, reproducible via
pinned unit tests") is pinned here.

## Depends on
- **ADR-0003 §1** — the `TrainingPlan` field list: `StartDate`/`EndDate` (`DateOnly`), `EventId`
  (`Guid?`), `BuildWeeks`/`RecoveryWeeks` (`int?`), `RecoveryWeekPercentage` (`decimal?`,
  `HasPrecision(5,2)`, "e.g. `60.0`" — **percent scale**).
- **ADR-0007 §1** — the optimal band `[0.8, 1.3] × A` where `A` = trailing-4-week mean actual load.
  `A` is this phase's baseline and `1.3 × A` is the ceiling the ramp rate is chosen against;
  `WeeklyLoadCalculator.cs:11` already carries the comment "Phase 18's ramp cap".
- **ADR-0008** — the format to mirror for the new ADR, the Monday-anchored ISO week, the
  calculators-take-`today` convention, and the plan-window-is-authoritative contract (§2) that
  decision 5 of the new ADR extends from PATCH to PUT.
- **Phase 17 handoff** (`md/handoffs/2026-07-25-phase-17-complete.md`) — Phase 18's stated
  prerequisite set is met; the plan↔event link is display-only until this phase's PUT.
- Nothing in this task depends on 18-2 … 18-5. It can land first and alone.

## Required reading
- `md/decisions/0008-calendar-compliance.md` — **the format template**: title line, `**Date:**`,
  `**Status:** Accepted (date) — one-sentence summary`, `## Context` with a *Conventions this ADR
  follows* subsection, numbered decision sections, `## Consequences` with a *For Tasks 18-1 … 18-5*
  table, `## Alternatives considered`. Match it section-for-section.
- `md/decisions/0007-progress-analytics.md` §1 (lines 53–66) — the band definition and the explicit
  sentence that Phase 18 anchors its ramp cap on `1.3 × A`. Quote the anchor, don't re-derive it.
- `md/decisions/0003-trainingplan-domain-shape.md` line 59 — `RecoveryWeekPercentage` "e.g. `60.0`"
  (the percent-scale evidence that beats the ROADMAP prose).
- `api/Bryk.Application/Calendar/ComplianceClassifier.cs` — **the shape to mirror**: `public static
  class`, private `const decimal` thresholds, one public entry point, a positional
  `public sealed record` input type in the same file, XML `<summary>` naming the ADR section.
- `api/Bryk.Application/Analytics/WeeklyLoadCalculator.cs` — the rounding discipline
  (`Math.Round(x, 2)` per emitted value) and the "no fabricated output for a fresh athlete" rule
  (returns a `null` band rather than a zero band). **Read only — this file is not modified.**
- `api/Bryk.Application/Goals/GoalProgress.cs` — the smallest example of the pure-calculator
  convention (caller passes the date in; no `DateTime.UtcNow` inside).
- `api/Bryk.Application/Analytics/AnalyticsService.cs:186` — the Monday-anchored week start
  (`date.AddDays(-(((int)date.DayOfWeek + 6) % 7))`). The calculator needs the same math; duplicate the
  three-token expression as a private static local (it is already duplicated in `ThisWeekService.cs:44`
  — do **not** refactor the existing two copies into a shared helper in this task).
- `api/Bryk.Domain/Entities/TrainingPlan.cs` — confirm the field names/types the input record mirrors.
- `api/Bryk.Application.Tests/Calendar/ComplianceClassifierTests.cs` — the unit-test file layout to
  mirror (one `[Fact]` per pinned case, FluentAssertions, exact expected values, no stubs needed).

## Acceptance criteria

### 1. `md/decisions/0009-periodization-ramp-model.md` (write this **first**)

Header:

```
# ADR-0009 — Periodization ramp model (weekly targets, cadence, taper)

**Date:** 2026-07-26
**Status:** Accepted (2026-07-26) — baseline = trailing 4-week mean actual load; ramp = +7 % per build
week; `BuildWeeks : RecoveryWeeks` cadence with recovery weeks at `RecoveryWeekPercentage` % of the
build target they interrupt; two-week 75 % / 50 % taper into a linked in-window event; compute-on-read
(no `WeeklyTarget` table, no migration); a plan-window shrink that strands planned workouts is
rejected with 400.
```

`## Context` explains: Phase 18 activates three dormant ADR-0003 columns; the ROADMAP flags the ramp
model under *Decisions needed* and requires the ADR before code; ADR-0007 already fixed the ceiling
this ramp rate is chosen against; ADR-0008 already fixed the plan window as authoritative. Include a
**### Conventions this ADR follows** subsection stating, grounded in the files above:

- **"Today"** is `DateOnly.FromDateTime(DateTime.UtcNow)`; there is no `IClock`. Pure calculators take
  the date in as a parameter (`GoalProgress.Compute`, `ComplianceClassifier.Classify`).
- **ISO weeks are Monday-anchored**, `((int)DayOfWeek + 6) % 7`, as in `AnalyticsService`/`ThisWeekService`.
- **Actual weekly load** = Σ (`LoadOverride ?? ComputedLoad ?? 0`) grouped by `WeekStart(CompletedDate)`;
  **planned weekly load** = Σ (`PlannedLoad ?? LoadCalculator.ComputePlannedLoad(...) ?? 0`) grouped by
  `WeekStart(ScheduledDate)` — both verbatim from `AnalyticsService.GetWeeklyLoadAsync`.
- **No migration, no new package.** Every field already exists.
- Athlete identity always via `ICurrentUserService` (Phase 12 still deferred and approval-gated).
- **Honesty rule (normative).** With no usable baseline the engine emits **no targets at all** — it
  never fabricates a ramp from zero, exactly as `WeeklyLoadCalculator` returns a `null` band for a
  fresh athlete.

`## Decision` carries six numbered sections:

**§1 — Baseline + ramp rate.** Baseline = **trailing 4-week mean actual load** (the same 4-week window
ADR-0007 §1 defines as `A`; no second window length is introduced). Ramp = **+7 % per build week**,
compounding. Rationale to state explicitly: `1.07⁴ = 1.3108`, i.e. four uninterrupted build weeks land
at the ACWR ceiling ADR-0007 locked (`1.3 × A`) and a 3:1 cadence interrupts before that — the rate is
derived from the ceiling, not picked from the ROADMAP's "~5–8 %" range at random. Fallback chain when
there is no trailing actual load: **trailing-4-week mean actual → the plan's own first-week planned
load → no targets at all**. Resolving the chain is the *service's* job (18-3); the calculator receives
one nullable `Baseline` and returns an empty list when it is null or ≤ 0.

**§2 — Recovery cadence.** The `BuildWeeks : RecoveryWeeks` pattern repeats over the plan window from
week 0 (`cycle = BuildWeeks + RecoveryWeeks`; week index `i` is a recovery week when
`i % cycle >= BuildWeeks`). A recovery week's target = `RecoveryWeekPercentage % × the build target it
interrupts`, and a recovery week **does not advance the ramp** — the next build week ramps from the
last build target. When **any** of the three fields is null the plan has **no cadence**: every week is
a build week ramping at the cap. Include the 12-week worked example table (identical numbers to the
test vector in §"Tests" below).

**§3 — Taper.** A two-week taper applies **only** when the plan has a linked event whose `EventDate`
falls inside `[StartDate, EndDate]` inclusive: the week containing the event = **50 %** of that week's
un-tapered build target, the week before = **75 %** of its own un-tapered build target. Taper
**overrides** recovery-week scaling on those weeks (a week is labelled taper *or* recovery, never
both). Degenerate plans shorter than three weeks need **no special branch** — the rule simply applies
(a two-week plan with an in-window event is 75 % / 50 %; a one-week plan is 50 %).

**§4 — Compute-on-read.** No `WeeklyTarget` table, no persisted per-week override, **no migration in
Phase 18**. Targets are a pure function of columns that already exist plus the athlete's workout
history; persisting them would add a staleness class (edit the plan → stale rows) for no read-cost
win, and the ROADMAP already recommends against it. Persisted overrides are a future migration and a
future ADR.

**§5 — Plan-window shrink orphans planned workouts → reject with 400.** `PUT /trainingplans/{id}`
(Task 18-2) rejects a `[StartDate, EndDate]` that would leave any existing `PlannedWorkout.ScheduledDate`
outside the window, with `Bryk.Application.Exceptions.ValidationException` → 400. This is ADR-0008 §2's
reschedule policy applied to the other side of the same invariant, so the plan window keeps exactly one
meaning across PATCH and PUT — and it is what makes these targets meaningful (a workout drifting
outside the window would be counted by no target week).

**§6 — `RecoveryWeekPercentage` is percent-scale (0–100), not a 0.3–0.9 fraction.** The ROADMAP Phase 18
entry says `0.3–0.9`; the code and ADR-0003 say percent (`HasPrecision(5,2)`, column `decimal(5,2)`,
"e.g. `60.0`", existing POST validator `InclusiveBetween(0m, 100m)`). **The code wins**; the ROADMAP
wording was corrected in `ROADMAP.md` on 2026-07-26 (at phase kickoff), and ADR-0009 §6 is the durable
record of why. The new PUT validator bounds the field to **30–90**; the
existing POST validator is frozen (tightening it would be an API breaking change — Sr. Dev gate).

`## Consequences` lists what is closed (the ROADMAP Phase 18 *Decisions needed* bullets: ramp model,
compute-on-read confirmation, reject-vs-warn on orphaning) and what is created — with an explicit
**"no migration, no new package"** line and this table:

| Task | Surface | Depends on | Pins from this ADR |
|---|---|---|---|
| **18-1** ADR + `WeeklyTargetCalculator` + xUnit | Backend (pure) | ADR-0007 §1 | §1 §2 §3 (the whole algorithm) |
| **18-2** `TrainingPlanUpdateRequest` + validator + `PUT /trainingplans/{id}` | Backend | ADR-0008 §2 | §5 (400 on orphaning), §6 (30–90 percent) |
| **18-3** `IPeriodizationService` + `GET .../weekly-targets` | Backend | 18-1, 18-2 | §1 (baseline chain), §4 (compute-on-read) |
| **18-4** Periodization panel on plan detail | Frontend | 18-2, 18-3 | §5 (surface the 400), §6 (30–90 in the form) |
| **18-5** `ThisWeekCard` target-vs-actual | Backend + Frontend | 18-1, 18-3 | §1 (targets), ADR-0008 §1 bands reused verbatim |

`## Alternatives considered` — at minimum: a persisted `WeeklyTarget` table (rejected, §4); a 5 % ramp
(rejected — not derivable from the locked 1.3 ceiling; 7 % is); ramping from the plan's first-week
*planned* load as the primary baseline (rejected — planned is the prescription, not the dose, mirroring
ADR-0007's own rejection of a planned-load band; it survives only as fallback #2); a single-week taper
or a taper percentage per event priority (rejected for v1 — one rule, two weeks, no `EventPriority`
coupling); treating a taper week that is also a cadence recovery week as both (rejected — one label per
week keeps the UI chip and the tests unambiguous).

### 2. `api/Bryk.Application/Training/Periodization/WeeklyTargetCalculator.cs`

New folder `Training/Periodization/`. Namespace `Bryk.Application.Training.Periodization`.

- `public static class WeeklyTargetCalculator` with private constants:
  `RampMultiplier = 1.07m`, `TaperEventWeekMultiplier = 0.50m`, `TaperPriorWeekMultiplier = 0.75m`,
  `PercentDivisor = 100m`.
- One public entry point:
  `public static IReadOnlyList<WeeklyTargetDto> Compute(WeeklyTargetInput input)`.
- XML `<summary>` naming ADR-0009 §1–§3 and stating "pure: no I/O, no `DateTime.UtcNow`".
- Algorithm — implement exactly this walk:
  1. `if (input.Baseline is not { } baseline || baseline <= 0m) return [];`
  2. `if (input.EndDate < input.StartDate) return [];`
  3. `firstWeekStart = WeekStart(input.StartDate)` (private static, Monday anchor).
     `weekCount = ((input.EndDate.DayNumber - firstWeekStart.DayNumber) / 7) + 1`.
  4. `hasCadence = input.BuildWeeks is > 0 && input.RecoveryWeeks is > 0 && input.RecoveryWeekPercentage is not null;`
     `cycle = hasCadence ? BuildWeeks.Value + RecoveryWeeks.Value : 0`.
  5. Taper index: `taperWeek = -1`; when `input.EventDate is { } ev && ev >= input.StartDate && ev <= input.EndDate`,
     `taperWeek = (WeekStart(ev).DayNumber - firstWeekStart.DayNumber) / 7`.
  6. **Pass 1 (ramp walk, taper-blind).** `current = baseline`, `seenFirstBuild = false`. For each week
     `i`: `isRecovery[i] = hasCadence && (i % cycle) >= BuildWeeks.Value`. When `!isRecovery[i]`:
     if `seenFirstBuild` then `current = Math.Round(current * RampMultiplier, 2)`; set
     `seenFirstBuild = true`. Then `ramp[i] = current` (recovery weeks record the unchanged `current`).
  7. **Pass 2 (emit).** For each week `i`:
     `isTaper = taperWeek >= 0 && (i == taperWeek || i == taperWeek - 1)`;
     `target = isTaper ? Math.Round(ramp[i] * (i == taperWeek ? TaperEventWeekMultiplier : TaperPriorWeekMultiplier), 2)
              : isRecovery[i] ? Math.Round(ramp[i] * (input.RecoveryWeekPercentage!.Value / PercentDivisor), 2)
              : ramp[i];`
     Emit `new WeeklyTargetDto { WeekStart = firstWeekStart.AddDays(7 * i), TargetLoad = target,
     IsRecoveryWeek = isRecovery[i] && !isTaper, IsTaperWeek = isTaper }`.
  - The list is ordered oldest → newest, one entry per ISO week that overlaps `[StartDate, EndDate]`.
    The first `WeekStart` may **precede** `StartDate` when the plan starts mid-week; the last `WeekStart`
    is always ≤ `EndDate`.
  - `Math.Round` is called with default (banker's) rounding — do **not** pass `MidpointRounding`; every
    pinned vector below avoids midpoints, so the two agree.

### 3. DTOs (same folder)

- `public sealed record WeeklyTargetInput(DateOnly StartDate, DateOnly EndDate, decimal? Baseline,
  int? BuildWeeks, int? RecoveryWeeks, decimal? RecoveryWeekPercentage, DateOnly? EventDate);`
  — positional record in the calculator's file, mirroring `ComplianceInput`. XML `<summary>` stating
  that `Baseline` is resolved by the caller (ADR-0009 §1 chain), `RecoveryWeekPercentage` is
  **percent-scale** (`60.0` = 60 %), and `EventDate` is ignored unless it falls inside the window.
- `api/Bryk.Application/Training/Periodization/WeeklyTargetDto.cs`:
  ```csharp
  public class WeeklyTargetDto
  {
      public DateOnly WeekStart { get; set; }
      public decimal TargetLoad { get; set; }
      public bool IsRecoveryWeek { get; set; }
      public bool IsTaperWeek { get; set; }
  }
  ```
  A class with settable properties (the `*Dto` convention used by `WeeklyLoadWeekDto`/`DailyLoadDto`),
  **not** a record — it is serialized by 18-3's response.
- **No** `PlannedLoad`/`ActualLoad`/`Baseline` fields on `WeeklyTargetDto` — merging actuals is 18-3's
  response concern; the calculator stays a pure function of its input.

## Non-goals
- **No migration.** No `WeeklyTarget` table, no column, no `DbContext`/`ApplicationDbContext` edit, no
  `dotnet ef` invocation. If any part of this task appears to need one — **STOP and ask** (Sr. Dev gate).
- **No new NuGet or npm package.**
- **Do not** create a service, repository method, controller action, `Program.cs` `AddScoped` line, DTO
  outside `Training/Periodization/`, or any HTTP surface. This calculator is unreferenced until 18-3;
  that is expected and is not dead code to "wire up while we're here".
- **Do not modify** `api/Bryk.Application/Analytics/WeeklyLoadCalculator.cs`,
  `api/Bryk.Application/Calendar/ComplianceClassifier.cs`, `ui/src/components/charts/LoadChart.vue`, or
  `ui/src/lib/charts/load.ts`.
- **Do not modify** `TrainingPlanRequest` or `TrainingPlanRequestValidator` (frozen for all of Phase 18
  — tightening POST bounds is an API breaking change requiring Sr. Dev approval).
- **Do not** refactor the two existing duplicated Monday-week expressions (`AnalyticsService.cs:186`,
  `ThisWeekService.cs:44`) into a shared helper. Duplicate the expression locally; note the third copy
  in the handoff as tech debt.
- **Do not fix** the two pre-existing nullable warnings in
  `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs:121,150` — they predate this phase and are
  out of scope.
- **Do not** revert, stash, or commit unrelated working-tree changes.
- **No auth code** — Phase 12 stays deferred and approval-gated; athlete identity is always
  `ICurrentUserService` (and this task touches neither).
- No auto-generation of planned *workouts* from targets (targets are numbers; authoring stays manual),
  no multi-event season ATP, no per-sport target split, no coach overrides.
- No `IClock` abstraction, no `DateTime.UtcNow` anywhere in the calculator.

## Test expectations

`api/Bryk.Application.Tests/Training/Periodization/WeeklyTargetCalculatorTests.cs` (new folder). Every
case is a named `[Fact]` with FluentAssertions and **exact** decimal expectations — no tolerance
ranges, no recomputation of the expected value inside the test.

Shared fixture dates: `Mon = new DateOnly(2026, 1, 5)` (a Monday; 2026-01-01 is a Thursday).

1. `Compute_TwelveWeekThreeBuildOneRecoveryWithRaceWeek_MatchesAdrWorkedExample`
   — input `Start = 2026-01-05`, `End = 2026-03-29`, `Baseline = 200.00m`, `BuildWeeks = 3`,
   `RecoveryWeeks = 1`, `RecoveryWeekPercentage = 60.0m`, `EventDate = 2026-03-28`.
   Expect **12** entries, `WeekStart[0] = 2026-01-05`, `WeekStart[11] = 2026-03-23`, and
   `TargetLoad` exactly:

   | i | WeekStart | Target | Recovery | Taper |
   |---|---|---|---|---|
   | 0 | 2026-01-05 | `200.00` | no | no |
   | 1 | 2026-01-12 | `214.00` | no | no |
   | 2 | 2026-01-19 | `228.98` | no | no |
   | 3 | 2026-01-26 | `137.39` | **yes** | no |
   | 4 | 2026-02-02 | `245.01` | no | no |
   | 5 | 2026-02-09 | `262.16` | no | no |
   | 6 | 2026-02-16 | `280.51` | no | no |
   | 7 | 2026-02-23 | `168.31` | **yes** | no |
   | 8 | 2026-03-02 | `300.15` | no | no |
   | 9 | 2026-03-09 | `321.16` | no | no |
   | 10 | 2026-03-16 | `257.73` | no | **yes** |
   | 11 | 2026-03-23 | `171.82` | no | **yes** |

   These are the numbers ADR-0009 §2's worked example must contain — if the two ever disagree, the
   test wins and the ADR is corrected.
2. `Compute_EventWeekThatIsAlsoACadenceRecoveryWeek_TapersInsteadOfScaling` — same input as (1);
   assert `result[11].IsTaperWeek == true`, `result[11].IsRecoveryWeek == false`,
   `result[11].TargetLoad == 171.82m` and explicitly `.Should().NotBe(206.18m)` (the value the 60 %
   recovery rule alone would have produced) — this is the ADR §3 "taper overrides recovery" pin.
3. `Compute_NullBaseline_ReturnsEmpty` — `Baseline = null` → `Should().BeEmpty()`.
4. `Compute_ZeroBaseline_ReturnsEmpty` — `Baseline = 0m` → `Should().BeEmpty()` (no fabricated ramp).
5. `Compute_NoCadenceFields_RampsEveryWeekAtTheCap` — `Start = 2026-01-05`, `End = 2026-02-01`,
   `Baseline = 200.00m`, all three cadence fields `null`, `EventDate = null` → exactly
   `[200.00, 214.00, 228.98, 245.01]`, every `IsRecoveryWeek == false`, every `IsTaperWeek == false`.
6. `Compute_RecoveryPercentageNull_TreatsEveryWeekAsBuild` — same window/baseline as (5) with
   `BuildWeeks = 3`, `RecoveryWeeks = 1`, `RecoveryWeekPercentage = null` → identical output to (5)
   (a partial cadence is no cadence).
7. `Compute_NoLinkedEvent_ProducesNoTaperWeeks` — `Start = 2026-01-05`, `End = 2026-02-08`,
   `Baseline = 100.00m`, `3 : 1 @ 60.0m`, `EventDate = null` →
   `[100.00, 107.00, 114.49, 68.69, 122.50]`; `IsRecoveryWeek` true only at index 3; no taper anywhere.
8. `Compute_EventOutsideThePlanWindow_ProducesNoTaperWeeks` — same as (7) with
   `EventDate = 2026-02-09` (one day past `End`) → byte-identical result to (7).
9. `Compute_TwoWeekPlanWithEventInFinalWeek_IsAllTaper` — `Start = 2026-01-05`, `End = 2026-01-18`,
   `Baseline = 200.00m`, no cadence fields, `EventDate = 2026-01-17` → `[150.00, 107.00]`, both weeks
   `IsTaperWeek == true`, both `IsRecoveryWeek == false`.
10. `Compute_SingleWeekPlanWithEvent_HalvesTheOnlyWeek` — `Start = 2026-01-05`, `End = 2026-01-11`,
    `Baseline = 200.00m`, `EventDate = 2026-01-07` → `[100.00]`, `IsTaperWeek == true` (the
    "week before" index −1 simply does not exist; no branch, no throw).
11. `Compute_MidWeekStartDate_AnchorsTheFirstWeekOnThePrecedingMonday` — `Start = 2026-01-07` (Wed),
    `End = 2026-01-20` (Tue), `Baseline = 200.00m`, no cadence, no event → 3 entries with
    `WeekStart` `2026-01-05 / 2026-01-12 / 2026-01-19` and targets `[200.00, 214.00, 228.98]`;
    assert every `WeekStart.DayOfWeek == DayOfWeek.Monday`.
12. `Compute_FourConsecutiveBuildWeeks_StayUnderTheAcwrCeiling` — reuse (5)'s result; assert
    `result[3].TargetLoad == Math.Round(result[2].TargetLoad * 1.07m, 2)` and
    `result[3].TargetLoad.Should().BeLessThanOrEqualTo(262.00m)` (= `1.31 × 200`), the ADR §1 rationale
    made executable.
13. `Compute_EndDateBeforeStartDate_ReturnsEmpty` — `End = Start.AddDays(-1)` → `Should().BeEmpty()`.

No integration tests in this task — there is no endpoint yet (18-3 adds them). No service-level tests —
there is no service yet. State that explicitly in the commit body rather than inventing a stub host.

## Verification commands
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```
Backend baseline to beat: **201 xUnit tests** green before this task; after it, 201 + the new facts, no
failures. Frontend baseline **229 tests / 53 files** must be **unchanged** (this task touches no UI).
The build's 16 warnings (design-time `System.Security.Cryptography.Xml` NU1903 + the two pre-existing
`WorkoutsControllerTests.cs` nullable warnings) must not grow.

## Review checklist
- [ ] ADR-0009 exists, is numbered/dated/`Accepted`, and matches ADR-0008's section skeleton including
      the *Conventions this ADR follows* subsection and the *For Tasks 18-1 … 18-5* table.
- [ ] ADR §1 states the `1.07⁴ ≈ 1.31` derivation and cites ADR-0007 §1 as the ceiling's origin.
- [ ] ADR §6 records the percent-scale correction (the ROADMAP prose was already fixed on 2026-07-26).
- [ ] `WeeklyTargetCalculator` is `static`, has zero `DateTime.UtcNow` / repository / `async` usage, and
      lives in `Bryk.Application/Training/Periodization/`.
- [ ] `WeeklyTargetInput` is a positional `sealed record`; `WeeklyTargetDto` is a settable-property class.
- [ ] A week is never both `IsRecoveryWeek` and `IsTaperWeek`.
- [ ] Null / zero baseline returns an **empty list**, not zeros.
- [ ] The 12-week vector in the tests is character-identical to the ADR's worked-example table.
- [ ] No file outside `md/decisions/0009-*.md`, `Bryk.Application/Training/Periodization/`, and
      `Bryk.Application.Tests/Training/Periodization/` appears in `git diff --stat`.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

## Suggested commit
```
feat: ADR-0009 periodization ramp model + pure WeeklyTargetCalculator

Write the ramp ADR before any Phase 18 code, as the ROADMAP requires:
baseline = trailing 4-week mean actual load (ADR-0007's A), ramp = +7% per
build week (1.07^4 = 1.31, i.e. derived from the locked ACWR 1.3 ceiling),
BuildWeeks:RecoveryWeeks cadence with recovery weeks at
RecoveryWeekPercentage% of the build target they interrupt, and a two-week
75%/50% taper into a linked in-window event that overrides recovery
scaling. Targets compute on read — no WeeklyTarget table, no migration.
The ADR also records that RecoveryWeekPercentage is percent-scale (the
ROADMAP's 0.3-0.9 wording is wrong; ADR-0003 and the decimal(5,2) column
win) and extends ADR-0008's plan-window rule to the Phase 18 plan PUT.

WeeklyTargetCalculator is pure and unreferenced until Task 18-3: it takes
the plan window, a nullable baseline, the cadence fields and an optional
event date, and returns the ordered per-ISO-week targets with recovery and
taper flags. A null or zero baseline yields no targets rather than a
fabricated ramp. xUnit pins the ADR's 12-week 3:1/60% race-week example
week by week, plus the no-event, partial-cadence, mid-week-start,
one-week, two-week and ACWR-ceiling cases.
```
