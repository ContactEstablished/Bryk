# Task 5-4 — Phase 5 verification, handoff, and build hygiene

## Goal
Finish Phase 5 with a clean frontend build, documented smoke coverage, and a handoff note that records what is complete, what was manually verified, and any follow-up needed before Phase 6.

Current code already complete:
- Vue onboarding wizard exists at `/onboarding`.
- Service layer, Pinia store, three step components, DTO types, schemas, and route are present.
- Phase 5 roadmap says recent `main` landings include `GoalsStep` field arrays and polish.

## Acceptance criteria
- Install/use frontend dependencies in the intended package manager flow without committing `node_modules`.
- `npm run build` or the project-preferred equivalent succeeds from `ui/`.
- If the API can be run locally, perform or document the manual smoke matrix:
  - full happy path creates/updates Required, Recommended sport profiles, and at least one Goal/Event;
  - reload after partial completion lands on the correct first incomplete step;
  - server validation errors surface sanely in the UI.
- Write a dated handoff under `docs/handoffs/` summarizing:
  - Phase 5 tasks completed;
  - code files changed;
  - build/test commands run and results;
  - manual smoke results or blockers;
  - explicit follow-ups for Phase 6 if any.
- Do not mark Phase 5 complete in `ROADMAP.md` unless the smoke checklist has actually been verified. If smoke cannot be completed in this environment, leave the roadmap status unchanged and state why in the handoff.

## Files likely to change
- `docs/handoffs/YYYY-MM-DD-phase-5-vue-onboarding-wizard.md`
- Possibly `ROADMAP.md` only if Phase 5 is genuinely complete and manually verified.
- Possibly small build-hygiene fixes discovered while running the build.

## What NOT to modify
- Do not start Phase 6 work.
- Do not add automated test infrastructure; that belongs to Phase 6.
- Do not commit local environment files, dependency caches, `node_modules`, or generated DB artifacts.

## Test plan
1. Run the frontend build command from `ui/`.
2. Run backend build if API changes were made; otherwise note that Phase 5 changed UI/docs only.
3. Capture manual smoke results or environment blockers in the handoff.
4. Verify `git status --short` contains only intentional Phase 5 files before commit.
