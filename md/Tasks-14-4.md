# Task 14-4 — End-to-end verification pass (seed data + fresh-athlete)

## Surface
No new feature code — a verification pass that proves the Phase 14 engine behaves correctly against
**real seed data** and a **fresh (empty) athlete**, plus any small wiring polish the pass surfaces
(empty-state copy, a missed store load, a rounding mismatch). Anything bigger than a one-line polish that
the pass uncovers is its own fix commit referencing the task it really belongs to.

## Why
The ROADMAP Phase 14 success criteria are behavioural: "Form tile shows a real TSB that changes after
logging/deleting a workout and matches `current`; ACWR renders '—' under 28 days of history." Those are
only provable end-to-end against the running app + DB, not by unit tests alone. This task is the gate.

## Depends on
- **Tasks 14-1, 14-2, 14-3** complete and committed.
- Seed data: `db/dev-seed.sql` loaded against the dev SQL Server (paste the user-secrets
  `DevAuth:CurrentAthleteId` in first, per the Phase 13 handoff session-start notes). Seed has ~9
  completed workouts.

## Required reading
- `md/handoffs/2026-06-11-phase-13-complete.md` — dev-stack start (API on `https://localhost:60129`,
  `pnpm dev` proxying `/api`), seed loading, the transient Vitest worker note.
- ROADMAP Phase 14 *Success criteria*.
- `md/decisions/0006-pmc-computation.md` — to check observed numbers against the formulas.

## Acceptance criteria (verify, record results in the handoff)

### Against seed data (real SQL Server)
1. **`pmc` endpoint sanity.** `GET /api/v1/analytics/pmc?from=<90d-ago>&to=<today>` returns a contiguous
   day-per-date `series` (length == inclusive day count), a non-null `current` with `date == today`, and
   CTL/ATL/TSB that match a hand/needle-check against the seed loads (spot-check one day's EWMA step).
2. **`daily-load` zero-fill.** `GET /api/v1/analytics/daily-load?from=&to=` — gap days between seeded
   workouts are present at load 0; seeded workout days sum their `EffectiveLoad`. No skipped dates.
3. **Tile matches `current`.** The dashboard Form (TSB) tile value equals `current.tsb` (sign included),
   and the interpretation label matches the band.
4. **TSB moves on log/delete (Phase 13 endpoints).** Note the tile's TSB; `POST /api/v1/workouts` a hard
   session today; reload the dashboard → TSB drops (ATL jumps). `DELETE` that workout; reload → TSB
   returns to the prior value. Confirms the engine reads live `EffectiveLoad`.
5. **ACWR band.** With ≥ 28 days between the first seeded workout and today, the Weekly Load ACWR chip
   shows a real ratio with correct in/out-of-band styling. (If the seed's first workout is < 28 days back,
   note it and verify the "—" path instead, then add a back-dated seed row to exercise the numeric path —
   record whichever was done.)

### Against a fresh athlete (empty database / unseeded athlete id)
6. **No fabricated numbers.** With zero workouts: `pmc.current == null`; the Form tile renders "—" (no
   number, no band label); the ACWR chip renders "—". The dashboard still renders without console errors
   (other cards show their own empty states).
7. **daily-load fresh.** `daily-load` returns an all-zero contiguous series (honest — zero load is
   defined), no error.

### Regression
8. `dotnet test api/Bryk.sln` green (full count, up from the 99 baseline by the 14-1/14-2 additions).
9. `pnpm run build` + `pnpm test` green (up from the 87 baseline by the 14-3 specs).
10. `git status` clean after any polish commits; diffs read cleanly.

## What NOT to modify
- No new features, endpoints, or fields — verification + at most one-line polish.
- Don't paper over a real bug with UI conditionals — if a number is wrong, fix the calculator/service
  (14-1/14-2) and note it.
- Don't bump the seed past its current size except the single back-dated row item 5 may need (record it).

## Deliverable
- A green end-to-end pass with results recorded in `md/handoffs/<today>-phase-14-complete.md` (written in
  Step 3 / phase exit), plus any polish commit(s). If the pass is clean with no polish needed, this task
  contributes only the verification evidence captured in the handoff (no code commit).

## Suggested commit (only if polish was needed)
```
fix(ui): <one-line polish surfaced by the Phase 14 verification pass>
```
