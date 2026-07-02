# Execution Prompt — Phase 17: Goals & Events Surface

> Paste this prompt into a fresh session rooted at the Bryk repo. Run only after Phases 16 and prior phases are complete (this phase floats but surfaces the dormant plan↔event link that Phase 18 will write to).

You are the Senior Solutions Architect for the Bryk project. You design, implement, and validate the work directly.

## Required reading, in order, before any code

1. `CLAUDE.md` — every convention applies.
2. `ROADMAP.md` → the **Phase 17** entry (scope contract) + the **Math conventions** section (time-in-zone honesty for reference) + any prior ADRs on plan/event linking.
3. `md/handoffs/<latest>-phase-16-complete.md` — the calendar endpoint shapes and compliance classifier you'll reference in this phase (Phase 18 reuses the same bands).
4. **Design reference:** the `ProgressRing` component in the Claude Design export. If the export (`%TEMP%\bryk-design\` or `Bryk UI.zip`) is not present, **STOP and ask for it** rather than inventing the ring geometry — but the geometry contract in Task 17-2 is authoritative regardless. `ui/src/components/common/Sparkline.vue` demonstrates the established porting pattern: Vue SFC, `computed` path math, `useId()` for gradient ids, CSS-variable colors, draw-in animation classes.

## Session-start checklist

Clean tree; `dotnet build`/`dotnet test api/Bryk.sln` green; `pnpm run build`/`pnpm test` green from `ui/`; user-secrets present; seed data loaded. Vitest's transient worker-crash-with-all-passing → re-run once.

## Important context

- **Phase 12 (auth) may not have shipped.** Execute on the DevAuth stub; athlete resolution through `ICurrentUserService` only.
- **Quantitative goal progress is deferred** (no `TargetValue/Unit/CurrentValue` columns). Ship date-based only; if a task appears to need these fields, **STOP and flag it as blocked** — it's a product decision, not a blocker for Phase 17.
- **Plan↔event link is display-only in Phase 17** (17-1 and 17-3 surface it read-only). The write path waits for Phase 18's plan PUT. If a form tries to let athletes edit the link, **STOP** — it's deferred.
- **`%TEMP%\bryk-design\` ProgressRing reference may not be present locally.** Task 17-2 proceeds from the geometry contract in the task spec regardless — hand-rolled SVG, `pathLength` draw-in, no chart lib. If you need the export to verify the look, ask the user.
- **Compliance thresholds** (green `[0.8, 1.2]`, yellow `[0.5, 0.8) ∪ (1.2, ∞)`, red `< 0.5`) are locked from Phase 16's ADR-0008; Phase 18 reuses them. Don't invent variants.

## Mission

Deliver **Phase 17 — Goals & Events Surface** end to end.

### Step 0 — lock task order (before coding)

The task dependency chain is strict:
1. **17-1 (backend GET endpoints + linked-plan lookup)** — foundation for 17-3 and 17-4.
2. **17-2 (ProgressRing port + PrimaryGoalCard refactor)** — dashboard card must work before 17-3 reuses the ring.
3. **17-3 (GoalsView read/display + nav)** — consume 17-1 endpoints and 17-2 ring; mount point for 17-4 forms.
4. **17-4 (CRUD forms on GoalsView)** — completes the round-trip, re-fetches through 17-1's endpoints.

Each task ships with its own commit; later tasks review the prior commit's output before diving in.

### Step 1 — write the task specs (one commit)

Create `md/Tasks-17-1.md` … `md/Tasks-17-4.md` per the ROADMAP entry:

1. **Tasks-17-1** — `GET /api/v1/events` (ordered by date, `upcoming=true` filter; includes `Notes` + linked plan ids), `GET /api/v1/events/{id}`, `GET /api/v1/goals` (computed days-remaining + status). New `LinkedPlanDto` + `EventListItemResponse` + `GoalListItemResponse`. Pure `GoalProgress.Compute` helper. New `ITrainingPlanRepository.GetByEventIdsAsync` reverse `EventId` lookup. No migration, no new package; the link is read-only (write path deferred to Phase 18).
2. **Tasks-17-2** — Port `ProgressRing.vue` from the design export (hand-rolled SVG: ticks + gradient + draw-in animation); a pure `buildRingGeometry` helper (Vitest-covered). Refactor dashboard `PrimaryGoalCard` to render its countdown through the ring — one shared implementation, two surfaces (rolling-horizon fraction on the card; true plan [start, target] window on the page).
3. **Tasks-17-3** — `GoalsView.vue` at `/goals` (read/display only — no forms yet) with read-display card components (`GoalsEventCard.vue`, `GoalsGoalCard.vue`) showing notes, linked-plan chips, status pills. New `goals` Pinia store + `services/goals-events.ts` read layer. Goals sidebar item goes **live** (was inert "soon"). Stubbed "Add Event" / "Add Goal" affordances for 17-4. No new package.
4. **Tasks-17-4** — On-page vee-validate + zod create/edit/delete forms for events and goals, wrapping the **existing** Phase-8 write services (POST/PUT/DELETE). Reuse `eventItemSchema`/`goalItemSchema`. Goals store gains CRUD actions re-fetching after each write. No backend endpoint, no new package, no auth code.

Commit: `docs: add Phase 17 task specs`.

### Step 2 — implement, one task per commit

Build + test + diff-read per task (no migration expected; flag any if discovered). Conventional commits.

**Commit messages:** plain conventional-commit messages only. Do NOT append a `Co-Authored-By:` trailer (or any AI co-author line) — it adds a second author and skews the GitHub contributor count. The commit author is already the repo's git user (Matthew Wilson).

**Approval gates:** none expected — **no migrations, no new packages** (hand-rolled SVG; endpoints are additive). If anything seems to need either, STOP and ask.

### Step 3 — phase exit

Verify every ROADMAP Phase 17 success criterion:
- `/goals` lists seeded data from the new 17-1 GETs.
- CRUD round-trips without touching onboarding.
- ProgressRing animates with correct elapsed fraction (rolling-horizon on dashboard; true plan window on page).
- Dashboard PrimaryGoalCard renders identically via the shared ring (behavioral parity check).
- Linked events navigate to plan detail (`/plans/:id`).
- Event Notes are visible.
- Zero console errors.

Flip the ledger row to ✅; write `md/handoffs/<today>-phase-17-complete.md`; update the CLAUDE.md phase pointer. Commit: `docs: close out Phase 17`.

## Scope guardrails (do NOT)

- No quantitative goal progress fields (`TargetValue/Unit/CurrentValue`) — **STOP and flag** if a task appears to need a migration for them. That's a product decision deferred to Phase 18 or later.
- No write surface for the plan↔event link — display-only. If a form tries to expose "link to plan" control, that's deferred to Phase 18's plan PUT.
- No auth code. No new packages. No refactoring outside the named files.
- No goal/event reminders, auto-prioritization, or goal↔workout attribution — all out of scope per ROADMAP.
- Don't touch onboarding, the Profile page, or the Phase-8 write services (`events.ts`, `goals.ts`) — the CRUD round-trip must complete **without touching onboarding** (a success criterion). Reuse the schemas and patterns, don't modify them.
