# Task 7-1 — Phase 5 completion handoff

## Goal
Write the formal Phase 5 closeout document and flip the ROADMAP ledger from 🟡 to ✅. This task is purely documentation — no code changes.

## Current code/status
- Phase 5 (Vue onboarding wizard) is functionally complete on `main`:
  - Wizard at `/onboarding` with Required / Recommended / Goals steps using vee-validate + zod (`ui/src/views/OnboardingView.vue`, `ui/src/components/onboarding/{Required,Recommended,Goals}Step.vue`).
  - vee-validate server-error mapping via `ui/src/services/apiErrors.ts`.
  - Resume-aware stepper navigation with locked future steps.
  - Read-only summary cards on completed steps — band-aid for missing edit-my-profile surface; will be replaced by Phase 8.
  - HomeView gates on status flags and renders a dashboard shell when all three flags are true (`ui/src/views/HomeView.vue`, `ui/src/components/dashboard/{DashboardSidebar,PlaceholderCard}.vue`). Dashboard cards are placeholders — Phase 8 lights up the first two.
- Manual end-to-end smoke walked 2026-05-26 with a fresh `DevAuth:CurrentAthleteId` GUID — wizard happy path, resume-after-Required-only landing on Recommended, summary card on revisit, dashboard shell on completion.
- Vitest suite green (14 tests). `npm run build` green. `dotnet test api/Bryk.sln` green.
- Two architectural decisions landed during Phase 7 closeout (Tasks 7-2 and 7-3): ADR-0001 (Mesocycle superseded) and ADR-0002 (coaches v2). Both relevant to what Phase 8 / 9 will do first.
- The dashboard shell that landed during Phase 5 closeout (2026-05-26 commits) is technically out of original Phase 5 scope. It's mentioned in the handoff for the historical record; population is owned by Phases 8 / 9 / 11 / 13.

## Acceptance criteria
- New file: `md/handoffs/2026-05-26-phase-5-complete.md`, structured in the same shape as `md/handoffs/2026-04-29-phase-4-complete.md`. Sections to cover:
  - **Context** — Phase 5 scope as defined; what shipped; what slipped or was added.
  - **Final shape of the wizard** — 3 steps, vee-validate + zod, resume-aware stepper, summary-card band-aid, HomeView dashboard gate.
  - **Inter-phase work that landed** — dashboard shell with sidebar + placeholder cards. Brief; Phase 8 owns the population work.
  - **Manual smoke matrix walked 2026-05-26** — happy path, resume flow, summary card on revisit, dashboard "Welcome back" / shell, "Get started" flow with fresh GUID.
  - **Test status** — Vitest suite count and result; backend test count and result; CI workflow status.
  - **Known follow-ups owned by Phase 8** — edit-my-profile surface (retires summary-card band-aid), Primary Goal card population, Resting HR card population.
  - **Known follow-ups deferred** — server-validation surfacing matrix not fully exercised (tracked); RFC 7807 ProblemDetails (tech-debt item 9) still parked.
- `ROADMAP.md` Phase 5 ledger row flipped from 🟡 to ✅ — one cell change in the table at the top, one header change in the Phase 5 entry, and the "Why still 🟡" sub-section removed.
- Single commit, message: `docs: Phase 5 complete — handoff and ledger flip`.

## Files likely to change/add
- `md/handoffs/2026-05-26-phase-5-complete.md` (new).
- `ROADMAP.md` (two surgical edits: ledger row, phase entry header + remove the "Why still 🟡" sub-section).

## What NOT to modify
- No code changes.
- No edits to Phase 6 or later entries in `ROADMAP.md`.
- Do not change the wizard, the dashboard shell, the onboarding service, or any endpoint.
- Do not pre-emptively start Phase 8 work — the handoff describes follow-ups, doesn't implement them.

## Test plan
1. `git diff` covers only the two files above.
2. `dotnet build api/Bryk.sln` still green (sanity check even though no code changed).
3. `pnpm test` from `ui/` still green.
4. Read the handoff out loud — if a fresh contributor can read it and know what Phase 8 should do first without reading the wizard source, it's done.
