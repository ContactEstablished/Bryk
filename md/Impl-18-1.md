# Impl 18-1 — Build order: ADR-0009 periodization ramp model + pure `WeeklyTargetCalculator`

**Executor:** architect-implementer (per CLAUDE.md).
**Acceptance contract:** `md/Tasks-18-1.md`.
**Decision lock:** ADR-0009 (`md/decisions/0009-periodization-ramp-model.md`, written in Step 1 of this
build order and **reviewed before any code is written**) + ADR-0007 §1 (the `[0.8, 1.3] × A` optimal
band — `1.3 × A` is the ceiling the new ramp rate is chosen against) + ADR-0008 (the ADR format template,
the Monday-anchored ISO week convention, and the plan-window-is-authoritative contract that ADR-0009 §5
extends from PATCH to PUT).
**Scope:** Backend only, pure. No migration, no new package, no service/repository/controller/`Program.cs`
change. `WeeklyTargetCalculator` is unreferenced until Task 18-3 — that is expected, not dead code to
wire up early.

This is the step-by-step build order. Execute top-to-bottom; each step's verification is the gate to the
next. **Step 1 carries a hard stop** — do not write any `.cs` file until ADR-0009 has been read and
accepted. One commit at the end with the message in `Tasks-18-1.md`.

## Step 0 — Pre-flight

- `git status` clean on `main`. `dotnet build api/Bryk.sln` green — confirm the stated baseline:
  **201 xUnit tests**, **16 warnings** (design-time `System.Security.Cryptography.Xml` NU1903 + the two
  pre-existing `WorkoutsControllerTests.cs:121,150` nullable warnings — do not fix these, they predate
  this phase). Run `dotnet test api/Bryk.sln` once to confirm 201 passing before touching anything.
- Confirm `api/Bryk.Application/Training/Periodization/` and
  `api/Bryk.Application.Tests/Training/Periodization/` do not yet exist (fresh surface — this task only
  adds files, it modifies none).
- Re-read `md/Tasks-18-1.md` in full. Open in editor:
  `md/decisions/0008-calendar-compliance.md` (format template),
  `md/decisions/0007-progress-analytics.md` (§1, lines 53–66 — the band/ceiling definition),
  `md/decisions/0003-trainingplan-domain-shape.md` (line 59 — `RecoveryWeekPercentage` "e.g. `60.0`"),
  `api/Bryk.Application/Calendar/ComplianceClassifier.cs` (shape to mirror),
  `api/Bryk.Application/Analytics/WeeklyLoadCalculator.cs` (rounding + "no fabricated output" — **read
  only, do not modify**),
  `api/Bryk.Application/Goals/GoalProgress.cs` (smallest pure-calculator example),
  `api/Bryk.Application/Analytics/AnalyticsService.cs:186` and
  `api/Bryk.Application/Training/ThisWeekService.cs:44` (the duplicated Monday-week expression —
  duplicate it a third time here, do **not** refactor the existing two),
  `api/Bryk.Domain/Entities/TrainingPlan.cs` (field names/types the input record mirrors),
  `api/Bryk.Application.Tests/Calendar/ComplianceClassifierTests.cs` (unit-test layout to mirror).
- Confirm the two Monday-week expressions read exactly
  `date.AddDays(-(((int)date.DayOfWeek + 6) % 7))` (`AnalyticsService.cs:186`) and
  `today.AddDays(-(((int)today.DayOfWeek + 6) % 7))` (`ThisWeekService.cs:44`) — the calculator's private
  `WeekStart` duplicates this exact three-token math.

## Step 1 — Write ADR-0009 first (`md/decisions/0009-periodization-ramp-model.md`)

New file. Section-for-section skeleton matches ADR-0008: title line, `**Date:**`, `**Status:**`,
`## Context` (with a `### Conventions this ADR follows` subsection), `## Decision` (six numbered
sections), `## Consequences` (with a *For Tasks 18-1 … 18-5* table), `## Alternatives considered`.

### Header — transcribe verbatim

```
# ADR-0009 — Periodization ramp model (weekly targets, cadence, taper)

**Date:** 2026-07-26
**Status:** Accepted (2026-07-26) — baseline = trailing 4-week mean actual load; ramp = +7 % per build
week; `BuildWeeks : RecoveryWeeks` cadence with recovery weeks at `RecoveryWeekPercentage` % of the
build target they interrupt; two-week 75 % / 50 % taper into a linked in-window event; compute-on-read
(no `WeeklyTarget` table, no migration); a plan-window shrink that strands planned workouts is
rejected with 400.
```

### `## Context`

State: Phase 18 activates three dormant `TrainingPlan` columns (`BuildWeeks`/`RecoveryWeeks`/
`RecoveryWeekPercentage`, nullable and unread since ADR-0003); the ROADMAP's *Decisions needed* line
requires this ADR before any Phase 18 code task, exactly as ADR-0008 preceded Phase 16; ADR-0007 already
fixed the ceiling (`1.3 × A`) this ramp rate is chosen against; ADR-0008 already fixed the plan window as
authoritative. List the three things this ADR resolves (mirror ADR-0008's numbered list): ramp
rate/baseline, recovery cadence + taper, plan-window-shrink rejection.

**`### Conventions this ADR follows`** — state each, grounded in the files read in Step 0:

- **"Today"** is `DateOnly.FromDateTime(DateTime.UtcNow)`; there is no `IClock`. Pure calculators take
  the date in as a parameter (cite `GoalProgress.Compute`, `ComplianceClassifier.Classify`).
- **ISO weeks are Monday-anchored**, `((int)DayOfWeek + 6) % 7`, as in `AnalyticsService`/
  `ThisWeekService`.
- **Actual weekly load** = Σ (`LoadOverride ?? ComputedLoad ?? 0`) grouped by `WeekStart(CompletedDate)`;
  **planned weekly load** = Σ (`PlannedLoad ?? LoadCalculator.ComputePlannedLoad(...) ?? 0`) grouped by
  `WeekStart(ScheduledDate)` — both verbatim from `AnalyticsService.GetWeeklyLoadAsync`.
- **No migration, no new package.** Every field already exists.
- Athlete identity always via `ICurrentUserService` (Phase 12 still deferred and approval-gated).
- **Honesty rule (normative).** With no usable baseline the engine emits **no targets at all** — it
  never fabricates a ramp from zero, exactly as `WeeklyLoadCalculator` returns a `null` band for a fresh
  athlete.

### `## Decision` — six numbered sections

**§1 — Baseline + ramp rate.** Baseline = trailing 4-week mean actual load (the same window ADR-0007 §1
calls `A` — do not introduce a second window length). Ramp = **+7 % per build week**, compounding. State
the rationale explicitly: `1.07⁴ = 1.3108`, i.e. four uninterrupted build weeks land at the ACWR ceiling
ADR-0007 locked (`1.3 × A`), and a 3:1 cadence interrupts before that — the rate is *derived* from the
ceiling, not picked from the ROADMAP's "~5–8 %" range at random. State the fallback chain **as the
service's job (18-3), not the calculator's**: trailing-4-week mean actual → the plan's own first-week
planned load → no targets at all. The calculator itself receives one nullable `Baseline` and returns an
empty list when it is null or ≤ 0.

**§2 — Recovery cadence.** The `BuildWeeks : RecoveryWeeks` pattern repeats over the plan window from
week 0 (`cycle = BuildWeeks + RecoveryWeeks`; week index `i` is a recovery week when
`i % cycle >= BuildWeeks`). A recovery week's target = `RecoveryWeekPercentage % × the build target it
interrupts`, and a recovery week **does not advance the ramp** — the next build week ramps from the last
build target. When **any** of the three fields is null, the plan has **no cadence**: every week is a
build week ramping at the cap. Include the 12-week worked-example table below **exactly as given** — it
must be character-identical to the pinned unit-test vector (Step 4):

```
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
```

State immediately below the table: input is `Start = 2026-01-05`, `End = 2026-03-29`,
`Baseline = 200.00`, `BuildWeeks = 3`, `RecoveryWeeks = 1`, `RecoveryWeekPercentage = 60.0`,
`EventDate = 2026-03-28`; and "if this table and the unit test ever disagree, the test wins and the ADR
is corrected."

**§3 — Taper.** A two-week taper applies **only** when the plan has a linked event whose `EventDate`
falls inside `[StartDate, EndDate]` inclusive: the week containing the event = **50 %** of that week's
un-tapered build target, the week before = **75 %** of its own un-tapered build target. Taper
**overrides** recovery-week scaling on those weeks (a week is labelled taper *or* recovery, never both —
row 11 in the table above is the pin: it is a cadence recovery week by the `%cycle` math, but taper wins,
reporting `171.82`, not the `206.18` a bare 60 % recovery scaling would produce). State: degenerate plans
shorter than three weeks need no special branch — the rule simply applies (a two-week plan with an
in-window event is 75 % / 50 %; a one-week plan is 50 % with no "week before").

**§4 — Compute-on-read.** No `WeeklyTarget` table, no persisted per-week override, no migration in
Phase 18. Targets are a pure function of columns that already exist plus the athlete's workout history;
persisting them would add a staleness class (edit the plan → stale rows) for no read-cost win, and the
ROADMAP already recommends against it. Persisted overrides are a future migration and a future ADR.

**§5 — Plan-window shrink orphans planned workouts → reject with 400.** `PUT /trainingplans/{id}`
(Task 18-2) rejects a `[StartDate, EndDate]` that would leave any existing `PlannedWorkout.ScheduledDate`
outside the window, with `Bryk.Application.Exceptions.ValidationException` → 400. This is ADR-0008 §2's
reschedule policy applied to the other side of the same invariant, so the plan window keeps exactly one
meaning across PATCH and PUT — and it is what makes these targets meaningful (a workout drifting outside
the window would be counted by no target week).

**§6 — `RecoveryWeekPercentage` is percent-scale (0–100), not a 0.3–0.9 fraction.** The ROADMAP Phase 18
entry says `0.3–0.9`; the code and ADR-0003 say percent (`HasPrecision(5,2)`, column `decimal(5,2)`,
"e.g. `60.0`", existing POST validator `InclusiveBetween(0m, 100m)`). **The code wins**; the ROADMAP
wording is corrected at phase wrap-up. State the new PUT validator (Task 18-2) bounds the field to
**30–90**; the existing POST validator is frozen (tightening it is an API breaking change — Sr. Dev
gate, out of scope here).

### `## Consequences`

State what is closed (the ROADMAP Phase 18 *Decisions needed* bullets: ramp model, compute-on-read
confirmation, reject-vs-warn on orphaning) and what is created, with an explicit **"no migration, no new
package"** line, then this table verbatim:

```
| Task | Surface | Depends on | Pins from this ADR |
|---|---|---|---|
| **18-1** ADR + `WeeklyTargetCalculator` + xUnit | Backend (pure) | ADR-0007 §1 | §1 §2 §3 (the whole algorithm) |
| **18-2** `TrainingPlanUpdateRequest` + validator + `PUT /trainingplans/{id}` | Backend | ADR-0008 §2 | §5 (400 on orphaning), §6 (30–90 percent) |
| **18-3** `IPeriodizationService` + `GET .../weekly-targets` | Backend | 18-1, 18-2 | §1 (baseline chain), §4 (compute-on-read) |
| **18-4** Periodization panel on plan detail | Frontend | 18-2, 18-3 | §5 (surface the 400), §6 (30–90 in the form) |
| **18-5** `ThisWeekCard` target-vs-actual | Backend + Frontend | 18-1, 18-3 | §1 (targets), ADR-0008 §1 bands reused verbatim |
```

### `## Alternatives considered`

At minimum, four entries: a persisted `WeeklyTarget` table (rejected, §4); a 5 % ramp (rejected — not
derivable from the locked 1.3 ceiling; 7 % is); ramping from the plan's first-week *planned* load as the
**primary** baseline (rejected — planned is the prescription, not the dose, mirroring ADR-0007's own
rejection of a planned-load band; it survives only as fallback #2); a single-week taper or a taper
percentage per event priority (rejected for v1 — one rule, two weeks, no `EventPriority` coupling);
treating a taper week that is also a cadence recovery week as both (rejected — one label per week keeps
the UI chip and the tests unambiguous).

**Verify (docs-only step — no compiler gate):**
- File exists at `md/decisions/0009-periodization-ramp-model.md`, matches ADR-0008's section skeleton
  including the *Conventions this ADR follows* subsection and the *For Tasks 18-1 … 18-5* table.
- §1 states `1.07⁴ = 1.3108` and cites ADR-0007 §1 as the ceiling's origin.
- §6 records the percent-scale correction and that the ROADMAP wording is fixed at phase wrap-up.
- The §2 worked-example table is **character-identical**, row for row, to the table above (which is
  itself character-identical to the Step 4 test vector — verify this by eye now, and again at Step 4).
- A week is never described as both recovery and taper anywhere in the ADR text.

**STOP — Sr. Dev / reviewer gate.** Per the ROADMAP and this task's own framing ("the ramp ADR is
written before any code task, exactly as ADR-0008 preceded Phase 16"), do not create, edit, or stage any
`.cs` file until ADR-0009 has been read and accepted by the reviewer. Do not proceed to Step 2 on your
own authority.

## Step 2 — `WeeklyTargetDto.cs`

**New file** `api/Bryk.Application/Training/Periodization/WeeklyTargetDto.cs` (new folder):

```csharp
namespace Bryk.Application.Training.Periodization;

public class WeeklyTargetDto
{
    public DateOnly WeekStart { get; set; }
    public decimal TargetLoad { get; set; }
    public bool IsRecoveryWeek { get; set; }
    public bool IsTaperWeek { get; set; }
}
```

A settable-property class (the `*Dto` convention used by `WeeklyLoadWeekDto`/`DailyLoadDto`), **not** a
record — 18-3 serializes it directly. No `PlannedLoad`/`ActualLoad`/`Baseline` fields — merging actuals
is 18-3's response concern.

**Verify:** `dotnet build api/Bryk.sln` green (no logic yet — this is a shape-only file).

## Step 3 — `WeeklyTargetCalculator.cs` + `WeeklyTargetInput`

**New file** `api/Bryk.Application/Training/Periodization/WeeklyTargetCalculator.cs`. Namespace
`Bryk.Application.Training.Periodization`. This is the two-pass algorithm from `Tasks-18-1.md` §2,
transcribed exactly — a one-pass rewrite produces different numbers (recovery weeks must **not** advance
the ramp; the next build week must ramp from the **last build week's** rounded value, not from a
recovery-scaled value).

```csharp
namespace Bryk.Application.Training.Periodization;

/// <summary>
/// Pure weekly-target ramp math (ADR-0009 §1–§3). Pure: no I/O, no <see cref="DateTime.UtcNow"/> — the
/// caller resolves <see cref="WeeklyTargetInput.Baseline"/> (ADR-0009 §1's fallback chain, a service
/// concern) and passes the plan window in. Two-pass: pass 1 walks the ramp taper-blind to establish each
/// week's build target (recovery weeks record the unchanged running value and do not advance the ramp);
/// pass 2 applies recovery-week scaling or taper scaling on top of it — never both on the same week.
/// </summary>
public static class WeeklyTargetCalculator
{
    private const decimal RampMultiplier = 1.07m;
    private const decimal TaperEventWeekMultiplier = 0.50m;
    private const decimal TaperPriorWeekMultiplier = 0.75m;
    private const decimal PercentDivisor = 100m;

    public static IReadOnlyList<WeeklyTargetDto> Compute(WeeklyTargetInput input)
    {
        if (input.Baseline is not { } baseline || baseline <= 0m) return [];
        if (input.EndDate < input.StartDate) return [];

        var firstWeekStart = WeekStart(input.StartDate);
        var weekCount = ((input.EndDate.DayNumber - firstWeekStart.DayNumber) / 7) + 1;

        var hasCadence = input.BuildWeeks is > 0 && input.RecoveryWeeks is > 0 && input.RecoveryWeekPercentage is not null;
        var cycle = hasCadence ? input.BuildWeeks!.Value + input.RecoveryWeeks!.Value : 0;

        var taperWeek = -1;
        if (input.EventDate is { } ev && ev >= input.StartDate && ev <= input.EndDate)
        {
            taperWeek = (WeekStart(ev).DayNumber - firstWeekStart.DayNumber) / 7;
        }

        // Pass 1 — ramp walk, taper-blind. Recovery weeks record the unchanged running build value; the
        // next build week ramps from the last build target, never from a recovery-scaled value.
        var isRecovery = new bool[weekCount];
        var ramp = new decimal[weekCount];
        var current = baseline;
        var seenFirstBuild = false;
        for (var i = 0; i < weekCount; i++)
        {
            isRecovery[i] = hasCadence && (i % cycle) >= input.BuildWeeks!.Value;
            if (!isRecovery[i])
            {
                if (seenFirstBuild)
                {
                    current = Math.Round(current * RampMultiplier, 2);
                }
                seenFirstBuild = true;
            }
            ramp[i] = current;
        }

        // Pass 2 — emit. Taper overrides recovery scaling on the same week; never both (ADR-0009 §3).
        var result = new List<WeeklyTargetDto>(weekCount);
        for (var i = 0; i < weekCount; i++)
        {
            var isTaper = taperWeek >= 0 && (i == taperWeek || i == taperWeek - 1);
            var target = isTaper
                ? Math.Round(ramp[i] * (i == taperWeek ? TaperEventWeekMultiplier : TaperPriorWeekMultiplier), 2)
                : isRecovery[i]
                    ? Math.Round(ramp[i] * (input.RecoveryWeekPercentage!.Value / PercentDivisor), 2)
                    : ramp[i];

            result.Add(new WeeklyTargetDto
            {
                WeekStart = firstWeekStart.AddDays(7 * i),
                TargetLoad = target,
                IsRecoveryWeek = isRecovery[i] && !isTaper,
                IsTaperWeek = isTaper
            });
        }

        return result;
    }

    // Monday-based ISO week start — same math as AnalyticsService.WeekStart / ThisWeekService.CurrentWeek.
    // Duplicated locally per Tasks-18-1 (do not refactor the existing two copies into a shared helper).
    private static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}

/// <summary>
/// Inputs to <see cref="WeeklyTargetCalculator.Compute"/> (ADR-0009). <see cref="Baseline"/> is resolved
/// by the caller via ADR-0009 §1's fallback chain (trailing 4-week mean actual load → plan's first-week
/// planned load → null); a null or non-positive baseline yields no targets, never a fabricated ramp.
/// <see cref="RecoveryWeekPercentage"/> is percent-scale (<c>60.0m</c> = 60 %, ADR-0009 §6).
/// <see cref="EventDate"/> is ignored unless it falls inside <c>[StartDate, EndDate]</c> inclusive.
/// </summary>
public sealed record WeeklyTargetInput(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal? Baseline,
    int? BuildWeeks,
    int? RecoveryWeeks,
    decimal? RecoveryWeekPercentage,
    DateOnly? EventDate);
```

Notes for the transcription:
- `input.BuildWeeks!.Value` / `input.RecoveryWeeks!.Value` / `input.RecoveryWeekPercentage!.Value` use
  the null-forgiving operator because `hasCadence`/`isTaper`/`isRecovery[i]` already prove non-null at
  each use site — the same pattern `AnalyticsService.BuildSeriesAsync` uses (`from!.Value` after
  `ValidateOrThrowAsync`). Do not add redundant null checks.
- `Math.Round(x, 2)` is called with **no** `MidpointRounding` argument — default banker's rounding. Every
  pinned vector in Step 4 avoids exact midpoints, so this agrees with `MidpointRounding.AwayFromZero`
  too; use the default because that is what `ComplianceClassifier`/`WeeklyLoadCalculator` both do.
- `WeeklyTargetInput` lives in **this file**, not a separate one — mirrors `ComplianceInput` living in
  `ComplianceClassifier.cs`.

**Verify:** `dotnet build api/Bryk.sln` green, 0 new warnings (still 16 total).

## Step 4 — Unit tests: `WeeklyTargetCalculatorTests.cs`

**New file** `api/Bryk.Application.Tests/Training/Periodization/WeeklyTargetCalculatorTests.cs` (new
folder). One `[Fact]` per pinned case from `Tasks-18-1.md` — 13 total, exact decimal expectations, no
tolerance ranges, no recomputation of an expected value inside a test (pin the literal, don't re-derive
it via a second `Compute` call or a formula).

```csharp
using Bryk.Application.Training.Periodization;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Training.Periodization;

public class WeeklyTargetCalculatorTests
{
    // Fixture Monday (2026-01-01 is a Thursday) — shared across the 12-week worked example (ADR-0009 §2)
    // and its derived cases (Tasks-18-1).
    private static readonly DateOnly Mon = new(2026, 1, 5);

    [Fact]
    public void Compute_TwelveWeekThreeBuildOneRecoveryWithRaceWeek_MatchesAdrWorkedExample()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 3, 29),
            Baseline: 200.00m,
            BuildWeeks: 3,
            RecoveryWeeks: 1,
            RecoveryWeekPercentage: 60.0m,
            EventDate: new DateOnly(2026, 3, 28));

        var result = WeeklyTargetCalculator.Compute(input);

        result.Should().HaveCount(12);
        result[0].WeekStart.Should().Be(new DateOnly(2026, 1, 5));
        result[11].WeekStart.Should().Be(new DateOnly(2026, 3, 23));

        result.Select(r => r.TargetLoad).Should().Equal(
            200.00m, 214.00m, 228.98m, 137.39m, 245.01m, 262.16m, 280.51m,
            168.31m, 300.15m, 321.16m, 257.73m, 171.82m);

        result.Select(r => r.IsRecoveryWeek).Should().Equal(
            false, false, false, true, false, false, false, true, false, false, false, false);

        result.Select(r => r.IsTaperWeek).Should().Equal(
            false, false, false, false, false, false, false, false, false, false, true, true);
    }

    [Fact]
    public void Compute_EventWeekThatIsAlsoACadenceRecoveryWeek_TapersInsteadOfScaling()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 3, 29),
            Baseline: 200.00m,
            BuildWeeks: 3,
            RecoveryWeeks: 1,
            RecoveryWeekPercentage: 60.0m,
            EventDate: new DateOnly(2026, 3, 28));

        var result = WeeklyTargetCalculator.Compute(input);

        result[11].IsTaperWeek.Should().BeTrue();
        result[11].IsRecoveryWeek.Should().BeFalse();
        result[11].TargetLoad.Should().Be(171.82m);
        result[11].TargetLoad.Should().NotBe(206.18m); // the 60% recovery rule alone would have produced this
    }

    [Fact]
    public void Compute_NullBaseline_ReturnsEmpty()
    {
        var input = new WeeklyTargetInput(Mon, Mon.AddDays(7), null, 3, 1, 60.0m, null);

        WeeklyTargetCalculator.Compute(input).Should().BeEmpty();
    }

    [Fact]
    public void Compute_ZeroBaseline_ReturnsEmpty()
    {
        var input = new WeeklyTargetInput(Mon, Mon.AddDays(7), 0m, 3, 1, 60.0m, null);

        WeeklyTargetCalculator.Compute(input).Should().BeEmpty();
    }

    [Fact]
    public void Compute_NoCadenceFields_RampsEveryWeekAtTheCap()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 2, 1),
            Baseline: 200.00m,
            BuildWeeks: null,
            RecoveryWeeks: null,
            RecoveryWeekPercentage: null,
            EventDate: null);

        var result = WeeklyTargetCalculator.Compute(input);

        result.Select(r => r.TargetLoad).Should().Equal(200.00m, 214.00m, 228.98m, 245.01m);
        result.Should().OnlyContain(r => !r.IsRecoveryWeek && !r.IsTaperWeek);
    }

    [Fact]
    public void Compute_RecoveryPercentageNull_TreatsEveryWeekAsBuild()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 2, 1),
            Baseline: 200.00m,
            BuildWeeks: 3,
            RecoveryWeeks: 1,
            RecoveryWeekPercentage: null,
            EventDate: null);

        var result = WeeklyTargetCalculator.Compute(input);

        result.Select(r => r.TargetLoad).Should().Equal(200.00m, 214.00m, 228.98m, 245.01m);
        result.Should().OnlyContain(r => !r.IsRecoveryWeek && !r.IsTaperWeek);
    }

    [Fact]
    public void Compute_NoLinkedEvent_ProducesNoTaperWeeks()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 2, 8),
            Baseline: 100.00m,
            BuildWeeks: 3,
            RecoveryWeeks: 1,
            RecoveryWeekPercentage: 60.0m,
            EventDate: null);

        var result = WeeklyTargetCalculator.Compute(input);

        result.Select(r => r.TargetLoad).Should().Equal(100.00m, 107.00m, 114.49m, 68.69m, 122.50m);
        result.Select(r => r.IsRecoveryWeek).Should().Equal(false, false, false, true, false);
        result.Should().OnlyContain(r => !r.IsTaperWeek);
    }

    [Fact]
    public void Compute_EventOutsideThePlanWindow_ProducesNoTaperWeeks()
    {
        // Same window/baseline/cadence as the previous case; EventDate is one day past End — byte-identical
        // result (the same literals pinned above), proving the out-of-window event is fully ignored.
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 2, 8),
            Baseline: 100.00m,
            BuildWeeks: 3,
            RecoveryWeeks: 1,
            RecoveryWeekPercentage: 60.0m,
            EventDate: new DateOnly(2026, 2, 9));

        var result = WeeklyTargetCalculator.Compute(input);

        result.Select(r => r.TargetLoad).Should().Equal(100.00m, 107.00m, 114.49m, 68.69m, 122.50m);
        result.Select(r => r.IsRecoveryWeek).Should().Equal(false, false, false, true, false);
        result.Should().OnlyContain(r => !r.IsTaperWeek);
    }

    [Fact]
    public void Compute_TwoWeekPlanWithEventInFinalWeek_IsAllTaper()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 1, 18),
            Baseline: 200.00m,
            BuildWeeks: null,
            RecoveryWeeks: null,
            RecoveryWeekPercentage: null,
            EventDate: new DateOnly(2026, 1, 17));

        var result = WeeklyTargetCalculator.Compute(input);

        result.Select(r => r.TargetLoad).Should().Equal(150.00m, 107.00m);
        result.Should().OnlyContain(r => r.IsTaperWeek && !r.IsRecoveryWeek);
    }

    [Fact]
    public void Compute_SingleWeekPlanWithEvent_HalvesTheOnlyWeek()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 1, 11),
            Baseline: 200.00m,
            BuildWeeks: null,
            RecoveryWeeks: null,
            RecoveryWeekPercentage: null,
            EventDate: new DateOnly(2026, 1, 7));

        var result = WeeklyTargetCalculator.Compute(input);

        result.Should().ContainSingle();
        result[0].TargetLoad.Should().Be(100.00m);
        result[0].IsTaperWeek.Should().BeTrue();
    }

    [Fact]
    public void Compute_MidWeekStartDate_AnchorsTheFirstWeekOnThePrecedingMonday()
    {
        var input = new WeeklyTargetInput(
            StartDate: new DateOnly(2026, 1, 7),
            EndDate: new DateOnly(2026, 1, 20),
            Baseline: 200.00m,
            BuildWeeks: null,
            RecoveryWeeks: null,
            RecoveryWeekPercentage: null,
            EventDate: null);

        var result = WeeklyTargetCalculator.Compute(input);

        result.Select(r => r.WeekStart).Should().Equal(
            new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 12), new DateOnly(2026, 1, 19));
        result.Select(r => r.TargetLoad).Should().Equal(200.00m, 214.00m, 228.98m);
        result.Should().OnlyContain(r => r.WeekStart.DayOfWeek == DayOfWeek.Monday);
    }

    [Fact]
    public void Compute_FourConsecutiveBuildWeeks_StayUnderTheAcwrCeiling()
    {
        var input = new WeeklyTargetInput(
            StartDate: Mon,
            EndDate: new DateOnly(2026, 2, 1),
            Baseline: 200.00m,
            BuildWeeks: null,
            RecoveryWeeks: null,
            RecoveryWeekPercentage: null,
            EventDate: null);

        var result = WeeklyTargetCalculator.Compute(input);

        result[3].TargetLoad.Should().Be(Math.Round(result[2].TargetLoad * 1.07m, 2));
        result[3].TargetLoad.Should().BeLessThanOrEqualTo(262.00m); // = 1.31 × 200, ADR-0009 §1 made executable
    }

    [Fact]
    public void Compute_EndDateBeforeStartDate_ReturnsEmpty()
    {
        var input = new WeeklyTargetInput(Mon, Mon.AddDays(-1), 200.00m, 3, 1, 60.0m, null);

        WeeklyTargetCalculator.Compute(input).Should().BeEmpty();
    }
}
```

No integration tests in this task (no endpoint exists yet — 18-3 adds it). No service-level tests (no
service yet). This is stated explicitly in the commit body, not worked around with a stub host.

**Verify:**
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln --filter FullyQualifiedName~WeeklyTargetCalculatorTests
```
Build green, 0 new warnings. All 13 facts pass by name:
`Compute_TwelveWeekThreeBuildOneRecoveryWithRaceWeek_MatchesAdrWorkedExample`,
`Compute_EventWeekThatIsAlsoACadenceRecoveryWeek_TapersInsteadOfScaling`,
`Compute_NullBaseline_ReturnsEmpty`, `Compute_ZeroBaseline_ReturnsEmpty`,
`Compute_NoCadenceFields_RampsEveryWeekAtTheCap`,
`Compute_RecoveryPercentageNull_TreatsEveryWeekAsBuild`,
`Compute_NoLinkedEvent_ProducesNoTaperWeeks`, `Compute_EventOutsideThePlanWindow_ProducesNoTaperWeeks`,
`Compute_TwoWeekPlanWithEventInFinalWeek_IsAllTaper`,
`Compute_SingleWeekPlanWithEvent_HalvesTheOnlyWeek`,
`Compute_MidWeekStartDate_AnchorsTheFirstWeekOnThePrecedingMonday`,
`Compute_FourConsecutiveBuildWeeks_StayUnderTheAcwrCeiling`, `Compute_EndDateBeforeStartDate_ReturnsEmpty`.
By eye, re-confirm test 1's `TargetLoad`/`IsRecoveryWeek`/`IsTaperWeek` sequences are character-identical
to ADR-0009 §2's table (Step 1) — this is the cross-check the review checklist requires.

## Step 5 — Final verification + commit

Run the full command set from `Tasks-18-1.md`:
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```

- `dotnet build` — 0 errors, **16 warnings** (unchanged from the Step 0 baseline — the design-time
  `System.Security.Cryptography.Xml` NU1903 plus the two pre-existing `WorkoutsControllerTests.cs`
  nullable warnings; no new warning introduced by this task's files).
- `dotnet test api/Bryk.sln` — **214 tests** (201 baseline + the 13 new `WeeklyTargetCalculatorTests`
  facts), all green, no failures, nothing else broke.
- `pnpm run build` — green (this task touches no UI file; sanity check only).
- `pnpm exec vitest run --no-file-parallelism` — **229 tests / 53 files**, unchanged from baseline (this
  task touches no UI file — if this number moved, something outside this task's scope changed; stop and
  investigate before committing).
- `git status` / `git add -A && git diff --cached --stat` — confirm **only** these four new files appear,
  all additions, **zero modified files** (this task is purely additive — no existing file changes):
  - `md/decisions/0009-periodization-ramp-model.md`
  - `api/Bryk.Application/Training/Periodization/WeeklyTargetCalculator.cs`
  - `api/Bryk.Application/Training/Periodization/WeeklyTargetDto.cs`
  - `api/Bryk.Application.Tests/Training/Periodization/WeeklyTargetCalculatorTests.cs`
  If the diff shows any other file touched, or any migration/`*.csproj`/`Program.cs` change — **STOP**,
  that is scope creep beyond `Tasks-18-1.md` (the task's own "What NOT to modify" / Non-goals fence).
- Confirm no `WeeklyTarget` table, no `DbContext`/`ApplicationDbContext` edit, and no `dotnet ef`
  invocation occurred anywhere in this task — if any step appeared to need one, it should already have
  stopped at that step per the Non-goals fence; re-verify here as the final gate.
- Commit with the message from `Tasks-18-1.md` (no AI co-author trailer — project convention):

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
