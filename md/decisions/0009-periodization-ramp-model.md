# ADR-0009 — Periodization ramp model (weekly targets, cadence, taper)

**Date:** 2026-07-26
**Status:** Accepted (2026-07-26) — baseline = trailing 4-week mean actual load; ramp = +7 % per build
week; `BuildWeeks : RecoveryWeeks` cadence with recovery weeks at `RecoveryWeekPercentage` % of the
build target they interrupt; two-week 75 % / 50 % taper into a linked in-window event; compute-on-read
(no `WeeklyTarget` table, no migration); a plan-window shrink that strands planned workouts is
rejected with 400.

## Context

Phase 18 ("ATP / periodization engine") activates three columns that have sat dormant on `TrainingPlan`
since ADR-0003: `BuildWeeks`, `RecoveryWeeks`, and `RecoveryWeekPercentage` — all three nullable, all
three documented as "forward-looking", and none of them read by any code path shipped so far. Turning
them into weekly load targets requires product decisions (how fast may load rise, what a recovery week
actually costs, when a taper begins) that get quietly wrong if they are settled inside an
implementation PR.

The ROADMAP Phase 18 entry therefore flags the ramp model under *Decisions needed* and requires the ADR
**before any code task**, exactly as ADR-0008 preceded Phase 16. Two of the inputs are already locked
elsewhere and are not re-litigated here:

- **ADR-0007 §1** fixed the optimal band as `[0.8, 1.3] × A`, where `A` is the trailing-4-week mean
  *actual* weekly load, and states explicitly that "Phase 18 reuses `A` as its baseline and `1.3 × A` as
  its ceiling." That ceiling is what this ADR's ramp rate is derived from.
- **ADR-0008 §2** fixed the plan window as authoritative: a reschedule outside
  `[StartDate, EndDate]` is rejected with 400, because "Phase 18's ramp targets are computed against it."

This ADR resolves:

1. **Baseline source + ramp rate** — what a week-0 target is anchored to, and how fast it may climb.
2. **Recovery cadence + taper** — how `BuildWeeks : RecoveryWeeks : RecoveryWeekPercentage` shapes the
   series, and what a linked event does to the last two weeks.
3. **Plan-window shrink that orphans planned workouts** — reject vs warn (the other side of ADR-0008 §2's
   invariant), plus the compute-on-read confirmation and the `RecoveryWeekPercentage` scale correction.

### Conventions this ADR follows

Grounded in `TrainingPlan`, `AnalyticsService`, `ThisWeekService`, `WeeklyLoadCalculator`,
`ComplianceClassifier`, `GoalProgress`, `LoadCalculator`:

- **"Today"** is `DateOnly.FromDateTime(DateTime.UtcNow)`, the same source `ThisWeekService`,
  `EventDtoValidator`, and the analytics range validator use. There is **no `IClock`**. Pure
  calculators take the date in as a parameter — `GoalProgress.Compute(…, today)` and
  `ComplianceClassifier.Classify(input)` (whose `ComplianceInput` carries `Today`) are the precedent.
- **ISO weeks are Monday-anchored**, `((int)DayOfWeek + 6) % 7`, exactly as `AnalyticsService.WeekStart`
  and `ThisWeekService.CurrentWeek` compute them.
- **Actual weekly load** = Σ (`LoadOverride ?? ComputedLoad ?? 0`) grouped by `WeekStart(CompletedDate)`;
  **planned weekly load** = Σ (`PlannedLoad ?? LoadCalculator.ComputePlannedLoad(…) ?? 0`) grouped by
  `WeekStart(ScheduledDate)` — both verbatim from `AnalyticsService.GetWeeklyLoadAsync`. Phase 18
  introduces no third aggregation rule.
- **No migration, no new package.** Every field this engine reads already exists.
- Athlete identity always via `ICurrentUserService` — never from a query or body (Phase 12 remains
  deferred and approval-gated).
- **Honesty rule (normative).** With no usable baseline the engine emits **no targets at all** — it never
  fabricates a ramp from zero, exactly as `WeeklyLoadCalculator` returns a `null` band rather than a
  `[0, 0]` band for a fresh athlete. An empty target series is an honest answer; a ramp from nothing is not.

## Decision

### 1. Baseline = trailing 4-week mean actual load; ramp = +7 % per build week

**Baseline.** The week-0 target is the **trailing 4-week mean actual weekly load** — the same window and
the same quantity ADR-0007 §1 calls `A`. No second window length is introduced: if the athlete's chart
says the safe ceiling is `1.3 × A`, the ramp must start from the same `A` or the two surfaces disagree.

**Ramp rate = +7 % per build week, compounding.** The rate is *derived* from the locked ceiling, not
picked from the ROADMAP's "~5–8 %" range at random:

```
1.07⁴ = 1.3108
```

Four uninterrupted build weeks land essentially exactly on the ACWR ceiling ADR-0007 locked (`1.3 × A`),
and any real cadence (3:1, 2:1) interrupts the climb before the fourth build week — so a plan with a
cadence never reaches the ceiling at all, and a cadence-less plan reaches it only at week 3 and then
knowingly continues past it. 7 % is the largest round rate with that property.

**Baseline fallback chain** (resolving it is the **service's** job — Task 18-3 — not the calculator's):

1. trailing 4-week mean actual load, →
2. the plan's own first-week **planned** load, →
3. **no targets at all**.

The calculator receives one nullable `Baseline` and returns an **empty list** when it is `null` or `≤ 0`.
Keeping the chain in the service keeps the calculator pure and keeps every I/O decision (which weeks to
read, whose workouts) on the side of the boundary that already owns repositories.

### 2. Recovery cadence — `i % cycle >= BuildWeeks`, and recovery does not advance the ramp

The `BuildWeeks : RecoveryWeeks` pattern repeats over the plan window from week 0:

```
cycle = BuildWeeks + RecoveryWeeks
week index i is a recovery week  ⟺  i % cycle >= BuildWeeks
```

A recovery week's target = `RecoveryWeekPercentage % × the build target it interrupts`, and a recovery
week **does not advance the ramp** — the next build week ramps from the last *build* target, not from
the recovery-scaled value. (This is why the implementation is two-pass: pass 1 walks the ramp
taper-blind and lets recovery weeks record the unchanged running value; pass 2 applies scaling on top.
A one-pass rewrite that ramps off the scaled value produces a visibly different, and wrong, series.)

When **any** of the three fields is null the plan has **no cadence**: every week is a build week ramping
at the cap. A partial cadence is not a cadence — `BuildWeeks = 3, RecoveryWeeks = 1,
RecoveryWeekPercentage = null` has no defined recovery volume, so there is nothing to scale to.

**Worked example** — 12-week plan, 3 build : 1 recovery @ 60 %, race in the final week:

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

Input: `Start = 2026-01-05`, `End = 2026-03-29`, `Baseline = 200.00`, `BuildWeeks = 3`,
`RecoveryWeeks = 1`, `RecoveryWeekPercentage = 60.0`, `EventDate = 2026-03-28`.

**If this table and the unit test ever disagree, the test wins and the ADR is corrected.** The pin lives
in `Bryk.Application.Tests/Training/Periodization/WeeklyTargetCalculatorTests.cs`
(`Compute_TwelveWeekThreeBuildOneRecoveryWithRaceWeek_MatchesAdrWorkedExample`).

Every emitted value is `Math.Round(x, 2)` with default (banker's) rounding, matching
`WeeklyLoadCalculator` and `ComplianceClassifier`. The series is ordered oldest → newest, one entry per
ISO week overlapping `[StartDate, EndDate]`; the first `WeekStart` may **precede** `StartDate` when the
plan starts mid-week, and the last `WeekStart` is always ≤ `EndDate`.

### 3. Taper — two weeks, 75 % / 50 %, overriding recovery scaling

A two-week taper applies **only** when the plan has a linked event whose `EventDate` falls inside
`[StartDate, EndDate]` **inclusive**:

- the week containing the event → **50 %** of that week's un-tapered build target;
- the week before it → **75 %** of its own un-tapered build target.

An event outside the window (or no linked event at all) produces **no taper weeks** — the event is
ignored entirely, not clamped to the nearest edge.

**Taper overrides recovery-week scaling.** A week is labelled taper **or** recovery, never both. Row 11
of §2's table is the pin: by the `i % cycle` math it *is* a cadence recovery week, but taper wins and it
reports `171.82` (= `343.64 × 0.50`), not the `206.18` (= `343.64 × 0.60`) a bare recovery scaling would
have produced. `IsRecoveryWeek` is false on that row.

Degenerate plans shorter than three weeks need **no special branch** — the rule simply applies. A
two-week plan with an in-window event is 75 % / 50 %; a one-week plan is 50 % with no "week before" (the
index `taperWeek − 1` is `−1`, which no week matches — no branch, no throw).

### 4. Compute-on-read — no `WeeklyTarget` table, no migration

Targets are a pure function of columns that already exist (`StartDate`, `EndDate`, `EventId`,
`BuildWeeks`, `RecoveryWeeks`, `RecoveryWeekPercentage`) plus the athlete's workout history, which the
analytics surface already reads. There is **no `WeeklyTarget` table, no persisted per-week override, and
no migration in Phase 18.**

Persisting the series would buy nothing on the read path (the math is a dozen decimal multiplications
over ≤ 52 rows) while adding a whole staleness class: edit the plan's dates, cadence, or event link — or
simply log a workout that moves the trailing-4-week mean — and every persisted row is wrong until
something invalidates it. The ROADMAP already recommends against it. Persisted per-week **overrides** (an
athlete hand-editing one week's target) are a genuinely different feature; they are a future migration
and a future ADR.

### 5. A plan-window shrink that orphans planned workouts → reject with 400

`PUT /api/v1/trainingplans/{id}` (Task 18-2) rejects a `[StartDate, EndDate]` that would leave any
existing `PlannedWorkout.ScheduledDate` outside the window, raising
`Bryk.Application.Exceptions.ValidationException` → 400.

This is ADR-0008 §2's reschedule policy applied to the other side of the same invariant. ADR-0008 stops a
*workout* from moving out of a fixed window; this stops the *window* from moving out from under a fixed
workout. Together they give the plan window exactly one meaning across PATCH and PUT — and that is what
makes these targets meaningful, because a workout that drifts outside the window is counted by no target
week at all and silently vanishes from the ramp's actual-vs-target comparison.

The athlete's recourse is the same one ADR-0008 named: reschedule the stranded workouts first, then
shrink the plan.

### 6. `RecoveryWeekPercentage` is percent-scale (0–100), not a 0.3–0.9 fraction

The ROADMAP Phase 18 entry originally specified validation as `RecoveryWeekPercentage` `0.3–0.9`. That
wording is wrong. The code says percent:

- `TrainingPlan.RecoveryWeekPercentage` is `decimal?`, configured `HasPrecision(5, 2)` → column
  `decimal(5,2)`.
- **ADR-0003** documents it as "Recovery-week volume as % of a build week (e.g. `60.0`)".
- The shipped `TrainingPlanRequestValidator` accepts `InclusiveBetween(0m, 100m)`.

**The code wins.** `ROADMAP.md` was corrected on 2026-07-26 at phase kickoff; this section is the durable
record of why. The calculator therefore divides by 100 (`RecoveryWeekPercentage / 100m`), and `60.0m`
means 60 %.

The **new PUT validator** (Task 18-2) bounds the field to **30–90** — the useful product range, and what
the ROADMAP's `0.3–0.9` was reaching for. The **existing POST validator stays frozen at 0–100** for the
whole of Phase 18: tightening the bounds of a shipped endpoint is an API breaking change and requires
Sr. Dev approval. The resulting POST/PUT divergence is known and accepted, and is recorded in Task 18-2's
commit body.

## Consequences

**Closed by this decision:** the ROADMAP Phase 18 *Decisions needed* bullets — the ramp model (baseline
source, ramp cap, taper rule), the compute-on-read confirmation, and reject-vs-warn when shrinking plan
dates orphans planned workouts. Plus the `RecoveryWeekPercentage` scale ambiguity between the ROADMAP
prose and the code.

**Created by this decision — no migration, no new package:**

- `Bryk.Application/Training/Periodization/`: pure `WeeklyTargetCalculator` (the two-pass ramp walk of
  §1–§3) with its positional `WeeklyTargetInput` record in the same file, mirroring
  `ComplianceClassifier`/`ComplianceInput`; and the settable-property `WeeklyTargetDto`
  (`WeekStart`, `TargetLoad`, `IsRecoveryWeek`, `IsTaperWeek`).
- `TrainingPlanUpdateRequest` + validator and `PUT /api/v1/trainingplans/{id}` (18-2), using the existing
  and currently-unused `ITrainingPlanRepository.Update`.
- `IPeriodizationService`/`PeriodizationService` + `GET /api/v1/trainingplans/{id}/weekly-targets` (18-3)
  — resolves §1's baseline chain, merges actuals, and commits nothing.
- UI: a Periodization panel on plan detail reusing Phase 15's `LoadChart` unforked (18-4), and a
  target-vs-actual bar + `DeltaChip` on `ThisWeekCard` reusing ADR-0008 §1's compliance bands
  verbatim (18-5).

**ADR-0007 and ADR-0008 are consumed, not amended.** `1.3 × A` and the plan-window rule keep exactly the
meanings they were given; this ADR only extends the window rule from PATCH to PUT (§5).

### For Tasks 18-1 … 18-5

| Task | Surface | Depends on | Pins from this ADR |
|---|---|---|---|
| **18-1** ADR + `WeeklyTargetCalculator` + xUnit | Backend (pure) | ADR-0007 §1 | §1 §2 §3 (the whole algorithm) |
| **18-2** `TrainingPlanUpdateRequest` + validator + `PUT /trainingplans/{id}` | Backend | ADR-0008 §2 | §5 (400 on orphaning), §6 (30–90 percent) |
| **18-3** `IPeriodizationService` + `GET .../weekly-targets` | Backend | 18-1, 18-2 | §1 (baseline chain), §4 (compute-on-read) |
| **18-4** Periodization panel on plan detail | Frontend | 18-2, 18-3 | §5 (surface the 400), §6 (30–90 in the form) |
| **18-5** `ThisWeekCard` target-vs-actual | Backend + Frontend | 18-1, 18-3 | §1 (targets), ADR-0008 §1 bands reused verbatim |

## Alternatives considered

- **A persisted `WeeklyTarget` table.** Rejected (§4) — it requires a migration Phase 18 does not need,
  and buys a read-cost win that does not exist against a dozen decimal multiplications. Its real cost is
  a staleness class with several independent invalidation triggers (plan dates, cadence fields, event
  link, and every logged workout that shifts the trailing mean). The ROADMAP itself recommends against
  it for v1. Revisit only when per-week *overrides* become a feature — a different thing, needing its
  own ADR.
- **A 5 % ramp.** Rejected (§1) — it is not derivable from anything already locked: `1.05⁴ = 1.2155`,
  comfortably under the ceiling but chosen by taste. 7 % is the rate the locked `1.3 × A` ceiling
  *implies*, so the two surfaces stay one decision instead of two.
- **Ramping from the plan's own first-week planned load as the primary baseline.** Rejected (§1) — planned
  load is the prescription, not the dose, and anchoring the ramp to it would let an over-optimistic plan
  bootstrap its own escalation. This mirrors ADR-0007's own rejection of a planned-load optimal band. It
  survives as fallback #2, where its weakness is acceptable because the alternative is no targets at all.
- **A single-week taper, or a taper percentage keyed to `EventPriority`.** Rejected for v1 (§3) — one
  rule, two weeks, no `EventPriority` coupling. A priority-keyed taper multiplies the test matrix by the
  A/B/C axis and forces a product answer ("how much less do you taper for a C race?") that nothing in the
  roadmap needs yet. Two weeks at 75 %/50 % is the conventional default and is trivially replaceable.
- **Treating a week that is both a taper week and a cadence recovery week as both.** Rejected (§3) — the
  UI chip would need a compound label, the target would need a defined composition order (`0.5 × 0.6`?
  `min`?), and every test touching the final weeks would have to encode that order. One label per week
  keeps the chip, the DTO's two booleans, and the tests unambiguous.
- **Warn-but-allow the orphaning plan shrink (200 + a `warnings` field).** Rejected (§5) — for the same
  reason ADR-0008 §2 rejected warn-but-allow on the reschedule: it needs a warnings channel on the
  response shape, and it silently produces exactly the state the targets cannot represent. Rejecting
  keeps the plan window's single meaning and is a plain validator rule.
