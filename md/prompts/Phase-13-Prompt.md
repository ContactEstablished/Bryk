# Execution Prompt — Phase 13: Workout History & Plan Browser

> Paste this prompt into a fresh session rooted at the Bryk repo. It assumes nothing from prior conversations.

You are the Senior Solutions Architect for the Bryk project. You design, implement, and validate the work directly.

## Required reading, in order, before any code

1. `CLAUDE.md` — every convention applies (Clean Architecture layering, repository + IUnitOfWork pattern, `ValidateOrThrowAsync`, thin controllers, Vue Composition API rules, surgical-changes discipline, Sr. Dev approval gates).
2. `ROADMAP.md` → the **Phase 13** entry. That entry is your scope contract: backend scope, frontend scope, decisions, out-of-scope list, success criteria. Do not exceed it.
3. `md/handoffs/2026-06-08-phase-11-complete.md` and `md/decisions/0005-training-load-and-execution.md` — how the load engine and executed-workout capture work today.
4. Skim one existing task spec (e.g. `md/Tasks-11-4.md`) — your task docs must follow the same format: Surface, Why, Files to touch, Behavior, Do-not list, Success criteria.

## Session-start checklist

- `git status` clean; `git log --oneline -10` for context.
- Backend green: `dotnet build api/Bryk.sln` + `dotnet test api/Bryk.sln`.
- Frontend green (from `ui/`): `pnpm run build` + `pnpm test`. (Note: vitest occasionally exits 1 with a transient "Worker exited unexpectedly" while all tests pass — re-run once before investigating.)
- `dotnet user-secrets list` (from `api/Bryk.API/`) shows `ConnectionStrings:DefaultConnection` and `DevAuth:CurrentAthleteId`. Seed data: `db/dev-seed.sql` (paste the athlete id at the top, run via sqlcmd/SSMS).

## Important context

- **Phase 12 (auth) has NOT shipped.** This phase executes on the DevAuth stub. All athlete resolution must flow through `ICurrentUserService` — never accept athlete IDs from request bodies or query strings — so the later auth swap doesn't touch this phase's code.
- The UI uses the Bryk dark design system: `card-surface`/`eyebrow` utilities, primitives in `ui/src/components/common/` (`MetricTile`, `Sparkline`, `TypePill`, `DeltaChip`, `RpeSelector`), `useCountUp` composable, `AppShell` layout. Reuse them; match the established look (study `WorkoutDetail`-adjacent surfaces like `RecentActivityCard.vue` and `ThisWeekCard.vue`).
- Existing Vitest specs assert visible text, never CSS classes. Keep user-facing strings stable unless a task says otherwise; update specs in the same commit when text legitimately changes.

## Mission

Deliver **Phase 13 — Workout History & Plan Browser** end to end.

### Step 1 — write the task specs (one commit)

Create `md/Tasks-13-1.md` … `md/Tasks-13-5.md` per the ROADMAP entry's breakdown:

1. **Tasks-13-1** — `PUT /api/v1/workouts/{id}` (replace-style, recompute `ComputedLoad` via `LoadCalculator`, `LoadOverride` survives unless explicitly cleared) + `DELETE /api/v1/workouts/{id}` (hard delete, cascades step results, 204). 404 for missing/foreign. Validators mirror `LogWorkoutRequest`. xUnit + integration tests.
2. **Tasks-13-2** — extend `GET /api/v1/workouts` with `from`/`to`/`sport`/`skip`/`take` (capped, newest-first, all params optional — non-breaking). **Record the pagination convention** (skip/take + capped take) in the task doc; later phases follow it.
3. **Tasks-13-3** — `WorkoutsView.vue` at `/workouts`: flip the inert "Workouts" sidebar item (and mobile tab bar) live in `ui/src/components/layout/AppSidebar.vue`; list rows in the Recent Activity visual style with `TypePill` + filter bar + load-more pagination. Vitest spec.
4. **Tasks-13-4** — `WorkoutDetailView.vue` at `/workouts/:id`: `MetricTile` strip, per-step planned-vs-actual table (display `AvgPower`, `AvgPace`, `Workout.Notes` — captured but never shown until now), edit via `LogWorkoutForm` edit mode, delete with confirm. Compose planned structure from the existing `GET .../structure` endpoint rather than fattening `WorkoutResponse` (confirm; any response change must be additive).
5. **Tasks-13-5** — plan browser: plan list → detail → planned-workout rows with "Edit structure" reopening the existing `WorkoutStructureBuilder` against `GET/PUT .../structure`. Browse + structure-edit only.

Commit: `docs: add Phase 13 task specs`.

### Step 2 — implement, one task per commit

For each task in order: implement → `dotnet build` + `dotnet test` (backend tasks) / `pnpm run build` + `pnpm test` from `ui/` (frontend tasks) → read your own diff → manual smoke (dev API + `pnpm dev`) → commit with a conventional message (`feat:`/`fix:`).

**Commit messages:** plain conventional-commit messages only. Do NOT append a `Co-Authored-By:` trailer (or any AI co-author line) — it adds a second author and skews the GitHub contributor count. The commit author is already the repo's git user (Matthew Wilson).

**Approval gates for this phase:** none expected — **no migrations, no new packages**. If you discover a needed column or package, STOP and ask before proceeding. Hard delete is the decided default (soft delete would be a migration → ask first).

### Step 3 — phase exit

- Verify every success criterion in the ROADMAP Phase 13 entry against the running app (use the seed data; it includes 9+ completed workouts and a structured plan).
- Flip the Phase 13 ledger row in `ROADMAP.md` to ✅.
- Write `md/handoffs/<today>-phase-13-complete.md` (what shipped, decisions made, carry-forwards).
- Update the CLAUDE.md "Current phase" pointer.
- Commit: `docs: close out Phase 13 — ledger, handoff`.

## Scope guardrails (do NOT)

- No calendar rendering, no aggregates/analytics endpoints, no charts (Phases 14–16).
- No plan-metadata editing (`PUT /trainingplans/{id}` is Phase 18).
- No "save workout as template", no file upload.
- No auth code of any kind (Phase 12, approval-gated).
- Don't refactor adjacent code or "improve" things not named by a task. Every changed line traces to the task spec.
