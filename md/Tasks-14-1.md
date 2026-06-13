# Task 14-1 — `PmcCalculator` + `AcwrCalculator` (pure analytics math)

## Surface
Backend only. New `Bryk.Application/Analytics/` namespace: two **pure, I/O-free** static calculators
(`PmcCalculator`, `AcwrCalculator`) plus the small data shapes they exchange, and an xUnit suite that
pins the formulas against hand-verifiable examples. Same shape and test style as
`Bryk.Application/Training/Load/LoadCalculator.cs` + `Bryk.Application.Tests/Training/LoadCalculatorTests.cs`.

## Why
The PMC engine's correctness lives or dies on this math (ADR-0005 said the same of `LoadCalculator`,
which 14-1's actual-load feeds). Isolating it as pure functions — no DB, no clock — makes every band of
the EWMA and the ACWR-insufficiency rule directly unit-testable, so the service layer (14-2) only has to
get the *data assembly* right.

## Depends on
- **ADR-0006** (decisions 4–5) — the formulas, the seeding convention, the ACWR insufficiency rule.
- **ROADMAP *Math conventions*** — normative source for CTL/ATL/TSB/ACWR; carry verbatim.

## Required reading
- `api/Bryk.Application/Training/Load/LoadCalculator.cs` — the pure-static pattern to mirror (rounding to
  2 dp, graceful degradation, no I/O).
- `api/Bryk.Application.Tests/Training/LoadCalculatorTests.cs` — the test style (FluentAssertions,
  hand-computed expected values in comments).
- `md/decisions/0006-pmc-computation.md` §4 (PMC), §5 (ACWR), §3 (the series is the shared input shape).

## Shapes (new, in `Bryk.Application/Analytics/`)
Plain data carriers (records or simple classes — match the codebase; LoadCalculator uses classes/DTOs):
- `DailyLoadDto { DateOnly Date; decimal Load; }` — one zero-filled day; the shared calculator input.
- `PmcPointDto { DateOnly Date; decimal Load; decimal Ctl; decimal Atl; decimal Tsb; }` — PMC per day.
- `PmcSummaryDto { DateOnly Date; decimal Ctl; decimal Atl; decimal Tsb; decimal? Acwr; }` — the `current` summary.
- `PmcResponse { List<PmcPointDto> Series; PmcSummaryDto? Current; }` — the pmc read shape (used by 14-2).

(Keep these in one or a few files under `Analytics/`; 14-2 maps the service output straight to them, so
no separate internal-vs-DTO layer.)

## Acceptance criteria

### `PmcCalculator`
- `public static IReadOnlyList<PmcPointDto> Compute(IReadOnlyList<DailyLoadDto> series)`.
- **Contract:** `series` is ordered, contiguous (one entry per calendar day), zero-filled — the **caller
  (14-2) guarantees this**; the calculator does not re-sort or gap-fill.
- Seeds `ctlPrev = atlPrev = 0` before the first element. For each day in order:
  - `tsb = round(ctlPrev − atlPrev, 2)` (**yesterday's** values — computed *before* updating).
  - `ctl = ctlPrev + (load − ctlPrev) / 42`; `atl = atlPrev + (load − atlPrev) / 7`.
  - Emit `{ Date, Load, Ctl = round(ctl,2), Atl = round(atl,2), Tsb }`; then `ctlPrev = ctl; atlPrev = atl`
    (carry **unrounded** values forward to avoid rounding drift across a long series).
- Empty input → empty list (no throw).

### `AcwrCalculator`
- `public static decimal? Compute(IReadOnlyList<DailyLoadDto> series, DateOnly evaluationDay, DateOnly? firstWorkoutDate)`.
- Returns `null` when **any** of (ADR-0006 §5):
  - `firstWorkoutDate is null`, or
  - `evaluationDay.DayNumber − firstWorkoutDate.Value.DayNumber + 1 < 28`, or
  - the 28-day chronic sum is 0.
- Else: `acute = mean(load over [evaluationDay−6, evaluationDay])` (7 days),
  `chronic = mean(load over [evaluationDay−27, evaluationDay])` (28 days), return `round(acute/chronic, 2)`.
- Read the window by date from `series` (which 14-2 guarantees covers `[evaluationDay−27, evaluationDay]`
  whenever ACWR is sufficient); a missing day in-window counts as 0 (defensive, shouldn't occur).

### Tests (`Bryk.Application.Tests/Analytics/PmcCalculatorTests.cs` + `AcwrCalculatorTests.cs`)
Pin at least:
- **Seeding / day 1.** Single 100-TSS day → `Ctl = round(100/42,2) = 2.38`, `Atl = round(100/7,2) = 14.29`,
  `Tsb = 0` (seeded yesterday 0−0).
- **Zero-day decay.** After a 100-day then a 0-day: CTL/ATL strictly decrease on the zero day, by
  `ctl/42` and `atl/7` respectively (verify the zero day isn't skipped).
- **TSB yesterday-offset.** A 2-day series proves day-2 `Tsb == day-1 Ctl − day-1 Atl` (yesterday's
  values, not today's).
- **Worked example — convergence.** Constant 100 TSS/day for ~120 days → final `Ctl` within a tight
  epsilon of 100 (e.g. ≥ 94 and < 100), and `Atl` converges faster (≥ 99). Demonstrates the EWMA limit.
- **ACWR insufficiency.** `firstWorkoutDate` null → null; 27 days of history (`eval − first + 1 = 27`) →
  null; exactly 28 days → a computed value. Boundary is at 28.
- **ACWR ratio.** A constructed series where acute ≠ chronic gives a known ratio (e.g. ramped load →
  acute > chronic → ACWR > 1; hand-compute the means).
- **ACWR zero chronic.** 28 days of zero load (but ≥ 28 days history) → null (no `0/0`).
- **LoadOverride respected** is exercised at the service layer (14-2), but a calculator test may feed a
  `DailyLoadDto` series whose loads already reflect overrides — note in a comment that override
  resolution is 14-2's job (`LoadOverride ?? ComputedLoad`).
- `dotnet build api/Bryk.sln` + `dotnet test api/Bryk.sln` green.

## What NOT to modify
- Don't touch `LoadCalculator`, `WorkoutService`, or any existing file — this task is purely additive.
- Don't add I/O, a clock, or `ICurrentUserService` to the calculators — they stay pure (the service owns
  "today", athlete resolution, and the DB reads).
- Don't build the controller, service, or DTOs beyond the shared shapes above — that's 14-2.
- Don't re-sort or gap-fill inside `PmcCalculator` — the contiguous zero-filled series is the caller's
  contract (keeps the math obviously correct and the cost in one place).

## Suggested commit
```
feat: PmcCalculator + AcwrCalculator (pure CTL/ATL/TSB/ACWR math)

New Bryk.Application/Analytics/: 42-day CTL / 7-day ATL EWMA with
yesterday-offset TSB, and 7:28 ACWR returning null under 28 days of
history. Pure (no I/O), per ADR-0006 §4-5 and the ROADMAP math
conventions. xUnit pins seeding, zero-day decay, the TSB offset, ACWR
insufficiency, and constant-100-TSS convergence toward CTL 100.
```
