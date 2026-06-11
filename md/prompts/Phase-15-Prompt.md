# Execution Prompt — Phase 15: Progress Page

> Paste this prompt into a fresh session rooted at the Bryk repo. Run only after Phases 13 and 14 are complete (this page consumes their endpoints).

You are the Senior Solutions Architect for the Bryk project. You design, implement, and validate the work directly.

## Required reading, in order, before any code

1. `CLAUDE.md` — every convention applies.
2. `ROADMAP.md` → the **Phase 15** entry (scope contract) + the **Math conventions** section (time-in-zone honesty rules are normative) + `md/decisions/0006-pmc-computation.md` (Phase 14's ADR).
3. `md/handoffs/<latest>-phase-14-complete.md` — the analytics endpoint shapes you'll consume.
4. **Chart porting references:** the SVG chart designs live in the Claude Design export `Bryk UI.zip` (file `charts.jsx`: `PMCChart` — CTL/ATL lines + daily load bars; `LoadChart` — weekly bars with planned hatch, optimal band, 4-week rolling trend). Ask the user for the zip if it isn't in the repo. `ui/src/components/common/Sparkline.vue` demonstrates the established porting pattern: Vue SFC, `computed` path math, `useId()` for gradient ids, CSS-variable colors (`var(--bryk-accent-hi)` etc.), draw-in animation classes neutralized by the global reduced-motion rule. **No chart library — hand-rolled SVG only.**

## Session-start checklist

Clean tree; `dotnet build`/`dotnet test api/Bryk.sln` green; `pnpm run build`/`pnpm test` green from `ui/`; user-secrets present; seed data loaded. Vitest's transient worker-crash-with-all-passing → re-run once.

## Important context

- **Phase 12 (auth) may not have shipped.** Execute on the DevAuth stub; athlete resolution through `ICurrentUserService` only.
- **Time-in-zone honesty (normative):** classification order per workout — (1) linked planned structure: distribute per-step durations into step zone targets; (2) else session-level `AvgHr` classified via `AthleteSportZone`; (3) else "unclassified". The response must carry a per-method breakdown (`structure`/`sessionAvg`/`unclassified` seconds) and the UI must show an "estimated" badge whenever non-structure data is in range. Real samples arrive in Phase 19 — do not pretend otherwise.
- Zone colors must match `ZonesView` (`--chart-1..5` tokens).

## Mission

Deliver **Phase 15 — Progress Page** end to end.

### Step 0 — lock the optimal-band decision (in task spec, before chart code)

Define the LoadChart "optimal band" as the ACWR-safe range: **0.8–1.3 × trailing 4-week average actual load**, computed server-side in the weekly-load response. Record it in the Tasks-15-1 spec (and note that Phase 18's ramp model must agree with it). If you argue for a different definition, write the alternative + rationale and ask the user before coding.

### Step 1 — write the task specs (one commit)

Create `md/Tasks-15-1.md` … `md/Tasks-15-5.md` per the ROADMAP entry:

1. **Tasks-15-1** — `GET /api/v1/analytics/weekly-load?weeks=8` (per ISO week `{weekStart, plannedLoad, actualLoad}`, 4-week rolling average, optimal band; weeks capped 1–26) + `GET /api/v1/analytics/peaks?sport=` (session-level records only: highest single-workout load, longest duration, longest distance, best session avg pace for run/swim, highest session AvgPower for bike). Validators + integration tests.
2. **Tasks-15-2** — `GET /api/v1/analytics/time-in-zone?from=&to=&sport=` with the three-tier classification + per-method breakdown. xUnit covers each classification path and the "seconds sum to total classified time" invariant.
3. **Tasks-15-3** — port `PMCChart.vue` into `ui/src/components/common/`: CTL line + fill, ATL dashed line, daily-load bars, end markers; fed by `/analytics/pmc`; 6w/3m/6m range toggle (segmented buttons styled like the ZonesView tab switcher). Vitest on the data-transform composable.
4. **Tasks-15-4** — port `LoadChart.vue`: actual bars (current week highlighted), planned hatch bars behind, optimal band rect, 4-week trend line with dots; fed by `/analytics/weekly-load`. Vitest on the transform.
5. **Tasks-15-5** — `ProgressView.vue` at `/progress`: assemble PMC card, weekly LoadChart card, time-in-zone stacked bars (+ "estimated" badge logic), peaks `MetricTile` grid (`TypePill` per sport, `DeltaChip` for in-range records); flip the inert "Progress" sidebar item (and mobile tab bar) live in `AppSidebar.vue`; `stagger-in` entrance like the dashboard.

Commit: `docs: add Phase 15 task specs`.

### Step 2 — implement, one task per commit

Build + test + diff-read + visual smoke per task (this phase is chart-heavy — screenshot `/progress` against seed data at desktop and <820px widths), conventional commits.

**Approval gates:** none expected — **no migrations, no new packages** (hand-rolled SVG; peaks are compute-on-read). If anything seems to need either, STOP and ask.

### Step 3 — phase exit

Verify every ROADMAP Phase 15 success criterion (including: zero console errors, no chart library in `package.json`, charts move when workouts are added/edited via Phase 13's endpoints); flip the ledger row to ✅; write `md/handoffs/<today>-phase-15-complete.md`; update the CLAUDE.md phase pointer. Commit: `docs: close out Phase 15`.

## Scope guardrails (do NOT)

- No sample-based analytics (power curves, HR/power decoupling, lap splits) — those need Phase 19 file import.
- No per-sport PMC tabs, no chart export/share, no customizable dashboard layouts.
- No duration-curve peaks (5s/1min/20min power) — session-level records only, honestly labeled.
- No chart libraries. No auth code. No refactoring outside the named files.
