# ADR-0006 — PMC computation strategy (CTL / ATL / TSB / ACWR)

**Date:** 2026-06-12
**Status:** Accepted (2026-06-12) — compute-on-read confirmed; `current` = range last day + null-for-fresh; TSB bands > +10 / ±10 / < −10.

## Context

Phase 14 ("Daily-load history & PMC engine") turns Phase 11's per-workout `EffectiveLoad`
(`LoadOverride ?? ComputedLoad`, [ADR-0005](0005-training-load-and-execution.md) §6) into the
Performance Management Chart series — daily load, CTL ("fitness"), ATL ("fatigue"), TSB ("form") —
plus the ACWR injury-risk ratio, and lights up the dashboard "Form (TSB)" tile and a "Weekly Load"
ACWR chip. It is the analytics spine that Phase 15 (Progress page charts) and Phase 18 (ATP targets
expressed against weekly load / CTL) consume, so the storage model, the seeding rule, and the band
thresholds need to be written down once and depended on, exactly as ADR-0004/0005 served Phases 10/11.

The math is **normative and already pinned** in `ROADMAP.md` *Math conventions* — this ADR carries
those formulas verbatim and resolves only the three genuinely-open questions the ROADMAP Phase 14
entry flags under *Decisions needed*:

1. **Compute-on-read vs a snapshot table** (and the lookback / seeding rule).
2. **Controller naming** (`AnalyticsController`).
3. **TSB interpretation band values.**

### Conventions this ADR follows

Grounded in `LoadCalculator`, `WorkoutService`, `WorkoutRepository`, `ThisWeekService`:

- **Daily load** = Σ `EffectiveLoad` (= `LoadOverride ?? ComputedLoad`) across the workouts sharing a
  `Workout.CompletedDate`. A day with no workout contributes **0**; a workout whose `EffectiveLoad`
  is null contributes **0**. Zero days are load-bearing for EWMA decay — never skipped.
- **"Today"** is `DateOnly.FromDateTime(DateTime.UtcNow)`, the same source `ThisWeekService.CurrentWeek`
  and the event validators already use. No `IClock` abstraction exists or is introduced.
- Pure calculators (no I/O, deterministic, unit-tested directly) mirror `LoadCalculator`; the service
  does the I/O (athlete resolution via `ICurrentUserService`, the repo reads) and delegates the math.
- Decimals round to **2 places** (`Math.Round(x, 2)`), as `LoadCalculator` does.
- **Honesty rule:** every rendered number traces to the athlete's actual workouts. ACWR with < 28
  days of history is `null` (renders "—"), never a fabricated ratio. A fresh athlete with no workouts
  gets a `null` `current` summary (tile renders "—"), not a 0/0/0 that reads as a real form.

## Decision

### 1. Compute-on-read — no `DailyLoadSnapshot` table

CTL/ATL/TSB/ACWR are **computed on every read** from the executed-`Workout` rows, exactly as
`LoadCalculator` computes load on read. There is **no** persisted daily-load / PMC snapshot table in
v1, no caching layer. Rationale:

- The inputs already live in `Workouts` (indexed by `AthleteId`); a date-bounded read + an in-memory
  EWMA pass over ≤ ~580 days (decision 2) is a millisecond-scale, single-table query.
- A snapshot table would need invalidation on every workout log / edit / delete (Phase 13 write paths)
  and a backfill job — staleness risk for no measured benefit at v1 data volumes, the same trap
  ADR-0005 avoided for planned load.

If profiling later proves compute-on-read too slow, a `DailyLoadSnapshot` table is a **future,
approval-gated migration** — explicitly out of Phase 14 scope. Discovering such a need mid-phase is a
**STOP-and-ask**, not a silent addition.

### 2. Seeding & lookback rule

To compute an accurate CTL/ATL at the start of the requested range we warm the EWMA up over prior
days, seeded from **0** and bounded. Let `Lookback = 180` days.

- **First-workout anchor.** `firstWorkoutDate` = the athlete's earliest `Workout.CompletedDate`
  (a single `MIN` query). With no workouts it is `null`.
- **Compute window start:**

  ```
  computeFrom = firstWorkoutDate is null
              ? from
              : max( min(firstWorkoutDate, from), from − Lookback )
  ```

  - `min(firstWorkoutDate, from)` guarantees the returned `[from, to]` is always fully covered and the
    first real load is included.
  - `max(…, from − Lookback)` caps the warm-up at 180 days. Seeding 0 there is accurate to ≈ `e^(−180/42)`
    ≈ **1.4 %** residual — 180 days is > 4 CTL time constants, so older history barely influences CTL
    at `from`.
- **Seed.** CTL = ATL = 0 immediately before `computeFrom`; iterate forward day-by-day to `to`,
  zero-filling every gap.
- **Return.** The series is sliced to exactly `[from, to]`. A requested day earlier than `computeFrom`
  (possible only when the athlete's first workout falls after `from`) is a **true-zero day**
  (load 0, CTL/ATL/TSB 0) — honest, not fabricated.

Worst-case computed window ≈ `Lookback + 400` (range cap, decision 5) ≈ **580 days** of in-memory
iteration per request. Acceptable for compute-on-read.

### 3. Daily series construction (the service's job)

`AnalyticsService` groups the window's workouts by `CompletedDate`, sums `EffectiveLoad`
(`LoadOverride ?? ComputedLoad ?? 0`) per date into a dictionary, then materialises an **ordered,
contiguous, zero-filled** `DailyLoadDto (Date, Load)` series over `[computeFrom, to]` and hands it to
the pure calculators. The series is the single shared input — `PmcCalculator` and `AcwrCalculator` both
consume it and neither touches the database.

### 4. PMC math — carried verbatim from ROADMAP *Math conventions* (normative)

`PmcCalculator.Compute(series)` walks the zero-filled daily series once, seeding CTL = ATL = 0 before
the first element, and emits a `PmcPointDto (Date, Load, Ctl, Atl, Tsb)` per day:

- **CTL** (42-day EWMA): `CTL_today = CTL_yesterday + (load_today − CTL_yesterday) / 42`
- **ATL** (7-day EWMA): `ATL_today = ATL_yesterday + (load_today − ATL_yesterday) / 7`
- **TSB** ("form", *yesterday's* values by convention): `TSB_today = CTL_yesterday − ATL_yesterday`

So each emitted point carries that day's post-update CTL/ATL and a TSB derived from the **prior** day's
CTL/ATL. Day 1 (seeded): `TSB = 0 − 0 = 0`; `CTL = (load₁)/42`; `ATL = (load₁)/7`. Worked example — a
constant 100 TSS/day input drives CTL asymptotically toward 100 (and ATL toward 100 faster); pinned in
the 14-1 unit tests.

### 5. ACWR math + insufficiency

`AcwrCalculator.Compute(series, evaluationDay, firstWorkoutDate)` returns a `decimal?`:

- **Acute** = mean daily load over `[evaluationDay − 6, evaluationDay]` (7 days).
- **Chronic** = mean daily load over `[evaluationDay − 27, evaluationDay]` (28 days).
- **ACWR** = `acute / chronic`, rounded to 2 places.
- **Returns `null`** (renders "—", never a fake ratio) when **any** holds:
  - `firstWorkoutDate is null` (no history at all), **or**
  - `evaluationDay − firstWorkoutDate + 1 < 28` (fewer than 28 calendar days of history through the
    evaluation day — not enough for a chronic window), **or**
  - chronic mean = 0 (28 days of pure zeros → `0/0` undefined).

Sweet spot ≈ 0.8–1.3; > 1.5 is elevated risk (interpretation only — the calculator returns the raw
ratio; styling lives in the UI). The decision-2 window guarantees the 28-day span is in the series
whenever ACWR is *sufficient*, so the calculator can safely sum `[eval − 27, eval]`.

### 6. The `current` summary + honesty nullability

The PMC read returns the `[from, to]` series **plus** a `current` summary so the dashboard needs one
call. `current` is the PMC point at the **last day of the requested range (`to`)** — for the
dashboard's `to = today` request this is *today's* form, which is the contract the ROADMAP states.

`current` (`PmcSummaryDto (Date, Ctl, Atl, Tsb, Acwr)`) is **nullable**:

- `current = null` ⟺ the athlete has **no** workout with `CompletedDate ≤ to` (i.e. no history). The
  tile renders "—". This is the honest fresh-athlete state — a 0/0/0 summary would read as a real
  "neutral" form the athlete never earned.
- Otherwise `current` carries `to`'s CTL/ATL/TSB and `Acwr` (itself `null` under 28 days of history,
  decision 5).

The daily-load endpoint returns the bare zero-filled `[from, to]` series (`DailyLoadDto[]`) — all-zeros
for a fresh athlete is honest (zero load is defined, not fabricated); the *tile-level* "—" gating is
the `current = null` signal above, not the series.

### 7. Endpoints, controller naming, validation

A new **`AnalyticsController`** (additive surface — not a breaking change; settles the ROADMAP naming
nod) at `api/v1/analytics`:

| Endpoint | Returns |
|---|---|
| `GET /api/v1/analytics/daily-load?from=&to=` | `DailyLoadDto[]` — zero-filled `(date, load)` over `[from, to]`. |
| `GET /api/v1/analytics/pmc?from=&to=` | `PmcResponse { series: PmcPointDto[]; current: PmcSummaryDto? }`. |

Both share one range contract, validated in the service via the locked `ValidateOrThrowAsync`
extension (→ `ValidationException` → 400) over an `AnalyticsRangeRequest`:

- `from` and `to` are **required**.
- `from ≤ to`.
- `(to − from) ≤ 400` days.
- `to ≤ today` (no future `to`; `today = DateOnly.FromDateTime(DateTime.UtcNow)`).

Athlete resolution is always `ICurrentUserService` — never a request/query value (keeps the Phase 12
auth swap invisible to this code).

### 8. TSB interpretation bands (locked)

A single label on the Form tile, derived from `current.tsb`:

| TSB | Label |
|---|---|
| `> +10` | **Fresh** |
| `−10 … +10` (inclusive) | **Neutral** |
| `< −10` | **Fatigued** |

Three bands, not the finer 5-zone TrainingPeaks scheme (transition / fresh / grey / optimal / high-risk):
the tile shows one word, and the coarse split is unambiguous and honest at v1. The exact boundaries live
in `Tasks-14-3.md` and the tile component; if a later phase wants the finer zones for a chart legend it
can subdivide without re-litigating these.

## Consequences

**Closed by this decision:** the ROADMAP Phase 14 *Decisions needed* — compute-on-read (no snapshot),
the 180-day seeded lookback, `AnalyticsController` naming, and the TSB band values.

**Created by this decision:**

- `Bryk.Application/Analytics/`: `PmcCalculator` + `AcwrCalculator` (pure, like `LoadCalculator`),
  `IAnalyticsService`/`AnalyticsService`, the `DailyLoadDto` / `PmcPointDto` / `PmcSummaryDto` /
  `PmcResponse` shapes, and `AnalyticsRangeRequest` + its validator (14-1, 14-2).
- One additive repo read — `IWorkoutRepository.GetFirstWorkoutDateAsync` (a `MIN(CompletedDate)` query);
  the window read reuses the existing `GetByAthleteInRangeAsync`. **No migration, no new package.**
- `AnalyticsController` (14-2) + two DI registrations (`IAnalyticsService`, `Program.cs`).
- Dashboard: the "Form (TSB)" tile + a "Weekly Load" ACWR chip, a `services/analytics.ts` module, a
  Pinia analytics slice, and the `types/analytics.ts` mirrors (14-3).

**Phases 15 & 18 depend on this** being written down: 15's PMC/Load charts consume the same series and
`current`; 18's weekly targets are expressed against the CTL / weekly-load baseline this defines.

### For Tasks 14-1 … 14-4

| Task | Surface | Depends on | Pins from this ADR |
|---|---|---|---|
| **14-1** `PmcCalculator` + `AcwrCalculator` + shapes + unit tests | Backend | ADR-0006 | Decisions 4–5 (EWMA + ACWR formulas, insufficiency). Pure, no I/O. |
| **14-2** `IAnalyticsService`/`AnalyticsService` + `AnalyticsController` + range validator + integration tests | Backend | 14-1 | Decisions 1–3, 6–7 (compute-on-read, seeding window, series build, `current`, endpoints, validation). |
| **14-3** Form (TSB) tile + ACWR chip + service/store/types | Frontend | 14-2 | Decisions 6, 8 (`current` nullability, TSB bands; ACWR 0.8–1.3 styling). |
| **14-4** Seeded end-to-end verification pass | Both | 14-1…3 | Honesty rule (fresh-athlete "—", TSB moves on log/delete, tile matches `current`). |

## Alternatives considered

- **Persist a `DailyLoadSnapshot` table.** Rejected for v1 (decision 1) — invalidation cost on every
  Phase-13 write + backfill, for no measured benefit at v1 volumes; staleness is the exact trap ADR-0005
  avoided. Re-openable as an approval-gated migration if profiling demands.
- **Unbounded warm-up (seed at the athlete's very first workout however far back).** Rejected — a
  multi-year history would iterate thousands of days per request for < 1 % CTL accuracy past 180 days.
  The bounded lookback (decision 2) keeps the window ≤ ~580 days with negligible error.
- **`current` reflects "today" by extending the window past `to` with zero-fill.** Rejected — fabricates
  future-dated zero days for historical-range queries and complicates bounds. Defining `current` as the
  range's last day (`to`) is fully bounded by the request and, for the dashboard's `to = today` call, is
  exactly today (decision 6).
- **0/0/0 `current` for a fresh athlete.** Rejected — reads as a real "neutral" form. `current = null`
  (decision 6) is the honest empty state.
- **Per-day ACWR in the series.** Out of scope — ACWR is a single `current` figure for the tile; a
  per-day ACWR track (if Phase 15 wants it) subdivides later without changing this contract.
