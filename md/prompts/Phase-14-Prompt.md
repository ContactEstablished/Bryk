# Execution Prompt — Phase 14: Daily-Load History & PMC Engine

> Paste this prompt into a fresh session rooted at the Bryk repo. Run only after Phase 13 is complete (its date-range workout query is this phase's data source).

You are the Senior Solutions Architect for the Bryk project. You design, implement, and validate the work directly.

## Required reading, in order, before any code

1. `CLAUDE.md` — every convention applies.
2. `ROADMAP.md` → the **Phase 14** entry (your scope contract) **and the "Math conventions" section directly above Phase 13** — the EWMA/TSB/ACWR formulas there are normative; carry them into code and tests verbatim.
3. `md/decisions/0005-training-load-and-execution.md` — `EffectiveLoad = LoadOverride ?? ComputedLoad` semantics.
4. `md/handoffs/<latest>-phase-13-complete.md` — what Phase 13 shipped (list endpoint shape, pagination convention).
5. Study `api/Bryk.Application/Training/Load/LoadCalculator*` (pure-calculator pattern + its test style) — `PmcCalculator` must follow the same shape.

## Session-start checklist

Same as every phase: clean tree, `dotnet build`/`dotnet test api/Bryk.sln` green, `pnpm run build`/`pnpm test` green from `ui/`, user-secrets present, seed data loaded (`db/dev-seed.sql`). Vitest's transient "Worker exited unexpectedly" with all tests passing → re-run once.

## Important context

- **Phase 12 (auth) may not have shipped.** Execute on the DevAuth stub; all athlete resolution through `ICurrentUserService`.
- **Honesty rule:** no fake numbers anywhere. ACWR with <28 days of history returns null and renders "—". Zero-load days are real data (EWMA decay) — never skip them when building the daily series.

## Mission

Deliver **Phase 14 — Daily-Load History & PMC Engine** end to end.

### Step 0 — ADR first (one commit)

Write `md/decisions/0006-pmc-computation.md` **before any code**: compute-on-read (no snapshot table), the lookback/seeding rule (recommend: series starts at the athlete's first workout date seeded from 0; bounded lookback ≤ ~180 days beyond the requested range), and the TSB interpretation bands (recommend: > +10 fresh / −10..+10 neutral / < −10 fatigued — adjust if you argue better, then lock). Status: Proposed → mark Accepted once the user confirms. Phases 15 and 18 depend on this being written down.

### Step 1 — write the task specs (one commit)

Create `md/Tasks-14-1.md` … `md/Tasks-14-4.md` per the ROADMAP entry:

1. **Tasks-14-1** — `PmcCalculator` + `AcwrCalculator` in `Bryk.Application/Analytics/` (pure, no I/O, like `LoadCalculator`). Formulas exactly per ROADMAP math conventions: CTL 42-day EWMA, ATL 7-day EWMA, TSB = yesterday's CTL − yesterday's ATL, ACWR = 7-day acute ÷ 28-day chronic (null under 28 days). xUnit: seeding, zero-day decay, TSB yesterday-offset, ACWR insufficiency, and the worked example (constant 100 TSS/day → CTL converges toward 100).
2. **Tasks-14-2** — `IAnalyticsService`/`AnalyticsService` (group workouts by `CompletedDate`, sum `EffectiveLoad`, zero-fill gaps, delegate to calculators) + new `AnalyticsController`: `GET /api/v1/analytics/daily-load?from=&to=` and `GET /api/v1/analytics/pmc?from=&to=` (series + `current` summary with today's CTL/ATL/TSB/ACWR). Validation: range required, ≤ 400 days, `from <= to`, no future `to`. Integration tests.
3. **Tasks-14-3** — dashboard wiring: replace the "Form (TSB)" `PlaceholderCard` in `ui/src/views/HomeView.vue` with a live card (`MetricTile` + `useCountUp`, signed TSB, `DeltaChip` vs 7 days ago, interpretation label per the ADR bands); add an ACWR chip to `WeeklyLoadCard.vue` (styled in/out of the 0.8–1.3 band; "—" when null). New `ui/src/services/analytics.ts` + Pinia store slice + types. Vitest specs.
4. **Tasks-14-4** — end-to-end verification pass against seed data: TSB changes after logging/deleting a workout (Phase 13 endpoints), `current` matches the tile, dashboard still renders with an empty database (fresh-athlete empty states).

Commit: `docs: add Phase 14 task specs`.

### Step 2 — implement, one task per commit

Build + test + diff-read + smoke per task, conventional commits.

**Commit messages:** plain conventional-commit messages only. Do NOT append a `Co-Authored-By:` trailer (or any AI co-author line) — it adds a second author and skews the GitHub contributor count. The commit author is already the repo's git user (Matthew Wilson).

**Approval gates:** none expected — **no migrations, no new packages** (a `DailyLoadSnapshot` table is explicitly out of scope; if performance argues for it, STOP and ask). The new `AnalyticsController` is additive API surface, not a breaking change.

### Step 3 — phase exit

Verify every ROADMAP Phase 14 success criterion; flip the ledger row to ✅; write `md/handoffs/<today>-phase-14-complete.md`; update the CLAUDE.md phase pointer; mark ADR-0006 Accepted. Commit: `docs: close out Phase 14`.

## Scope guardrails (do NOT)

- No charts — the PMC/load charts are Phase 15. This phase ships the engine + two dashboard tiles only.
- No per-sport PMC split, no snapshot/caching table, no wellness inputs into form.
- No fake/sample data: every rendered number must come from the athlete's actual workouts.
- No auth code. No refactoring outside the named files.
