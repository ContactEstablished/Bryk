# Task 5-2 — Resume-aware wizard navigation and completion gating

## Goal
Finish the wizard navigation behavior required by ROADMAP Phase 5: on mount, load onboarding status and land on the first incomplete step; let the athlete move forward through submissions and jump only to steps that are already complete.

Current code already complete:
- `ui/src/router/index.ts` lazy-loads `/onboarding`.
- `ui/src/views/OnboardingView.vue` calls `store.loadStatus()` on mount and initializes `currentStep` from `store.nextIncompleteStep`.
- The Pinia store exposes `requiredComplete`, `recommendedComplete`, `goalsComplete`, and `nextIncompleteStep`.

Current gap:
- The stepper is visual only. It does not let the user jump to any complete step.
- Navigation does not explicitly guard against jumping ahead to incomplete locked steps.
- Final completion currently lands on an inline “done” state; verify this remains acceptable or route to the post-onboarding landing if one already exists.

## Acceptance criteria
- Stepper items in `OnboardingView.vue` are keyboard/click accessible controls, not just static markup.
- A user can jump to:
  - any completed step;
  - the current first incomplete step.
- A user cannot jump ahead to locked/incomplete future steps.
- Locked steps are visually distinct and disabled.
- When all three status flags are true on mount/reload, the wizard lands in the completed/done state.
- After each successful step submit, the store reloads status and the wizard advances to the next incomplete step, not blindly to the next index if status says otherwise.
- Existing Back behavior remains, but cannot navigate to a future locked step.
- `npm run build` from `ui/` succeeds.

## Files likely to change
- `ui/src/views/OnboardingView.vue`
- Possibly `ui/src/stores/onboarding.ts` if a tiny derived helper makes the navigation cleaner.

## What NOT to modify
- Do not change route names or URLs.
- Do not replace the hand-rolled stepper with a new component library implementation.
- Do not change the API service layer.
- Do not add persistence of form draft values; Phase 5 resume is status-based only.

## Test plan
1. Run `npm run build` from `ui/`.
2. Manually verify status combinations:
   - no flags true → Required active, Recommended/Goals locked.
   - required true only → Recommended active, Required clickable, Goals locked.
   - required + recommended true → Goals active, Required/Recommended clickable.
   - all true → done state.
3. Confirm successful submits still emit forward progress and reload status through the store.
