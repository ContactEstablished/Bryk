# Task 8-3 — Retire onboarding summary-card band-aid

## Goal
Remove the read-only summary cards that shipped during Phase 5 closeout as a footgun-mitigation band-aid. With the Profile surface from Task 8-2 now offering the proper edit path, the onboarding stepper can safely allow click-to-revisit on completed steps — re-submitting will upsert through the same code path the Profile editor uses.

Frontend-only task. Depends on Task 8-2 (the `/profile` route must exist for the "Manage your profile" link to land somewhere real).

## Current code/status

- During Phase 5 closeout (commit `a774e09`), each of `RequiredStep.vue`, `RecommendedStep.vue`, and `GoalsStep.vue` gained a `v-if="store.<flag>Complete"` branch rendering a `CheckCircle2` icon + "X saved" heading + a one-liner pointing at the future edit surface. The `<template v-else>` wraps the existing form.
- The stepper in `OnboardingView.vue` currently allows clicking on completed and "next incomplete" steps; future-locked steps are disabled (`canJumpTo()` logic). Revisiting a completed step renders the summary card (not an editable form), so the no-edit experience is consistent within the wizard.
- After this task, revisiting a completed step renders the editable form pre-filled with whatever the user originally submitted — but the wizard does NOT load that data; the form starts empty. The athlete editing a value in the wizard would overwrite with whatever they retype (upsert semantics). That's acceptable because the proper edit path is now `/profile`. The wizard's "Continue" button still submits the current form values — same upsert.
- The "All set" panel in `OnboardingView.vue` currently shows "You're all set!" + "Go to home" button. Adds a "Manage your profile" affordance.

## Acceptance criteria

**Step components — remove the summary-card branches:**

- `ui/src/components/onboarding/RequiredStep.vue`:
  - Delete the `<div v-if="store.requiredComplete">` block at the top of the template (the `CheckCircle2` + "Identity saved" card).
  - Delete the `<template v-else>` wrapper around the form (and its matching `</template>` closing tag).
  - Remove the `import { CheckCircle2 } from 'lucide-vue-next'` line.
- Same edits in `ui/src/components/onboarding/RecommendedStep.vue` (heading was "Thresholds saved").
- Same edits in `ui/src/components/onboarding/GoalsStep.vue` (heading was "Goals saved").

**Stepper — no change required.** The stepper already allows revisiting completed steps via `canJumpTo()`. Removing the summary card means revisits show the empty editable form. The footgun (re-submitting empty values would overwrite) is intentionally accepted in this task because:
1. The athlete who wants to edit values has `/profile` now.
2. The athlete who clicks back accidentally has no submit-by-default — they have to actively re-fill and click Continue.
3. The Continue button is the explicit confirmation.

If user feedback later reveals the empty-on-revisit is still confusing, the right fix is to prefill the wizard step from `/profile` GETs — but that's its own task (Phase 8.5 if needed), not bundled here.

**"All set" panel — add a "Manage your profile" affordance:**

- `ui/src/views/OnboardingView.vue`, in the `<div v-else-if="store.status && currentStep === 'done'">` block, add a second `Button` alongside "Go to home":
  ```vue
  <Button variant="outline" @click="goToProfile">
    Manage your profile
  </Button>
  ```
  Plus a `goToProfile()` function that does `void router.push('/profile')`. Place the button below "Go to home" or beside it — pick whichever reads cleanest with the existing centered layout.

**Tests:**

The existing step component specs (`ui/src/components/onboarding/__tests__/{Required,Recommended,Goals}Step.spec.ts`) mock the store with all completion flags `false`, so they exercise the form path. They should still pass unchanged. **Verify, don't assume.**

If any spec relies on the summary-card path or imports, update or delete the relevant assertions.

**Build / test:**
- `pnpm run build` from `ui/` green.
- `pnpm test` from `ui/` green; test count unchanged (or down by any summary-card-specific assertions you delete).

## Files likely to change/add

- `ui/src/components/onboarding/RequiredStep.vue` — remove summary card branch + import.
- `ui/src/components/onboarding/RecommendedStep.vue` — same.
- `ui/src/components/onboarding/GoalsStep.vue` — same.
- `ui/src/views/OnboardingView.vue` — add "Manage your profile" button + handler.

## What NOT to modify

- Do not modify the stepper's `canJumpTo` / `isLocked` logic — the existing behavior (allow revisits to completed steps; lock future steps) is now correct.
- Do not modify any backend code — Task 8-3 is pure frontend.
- Do not pre-fill the wizard step forms from `/profile` GETs — deferred to a future task if needed.
- Do not modify any `/profile`-related code from Task 8-2.
- Do not modify dashboard components — Tasks 8-4 and 8-5 own those.

## Test plan

1. `pnpm run build` green.
2. `pnpm test` green — no regression in existing 14 tests (or whatever count exists post-Task 8-2).
3. Manual smoke (assumes Tasks 8-1 and 8-2 landed):
   - Fresh `DevAuth:CurrentAthleteId` GUID → walk the wizard end-to-end → land on "All set" view.
   - See both "Go to home" and "Manage your profile" buttons. Click "Manage your profile" → land on `/profile`.
   - Edit and save a Required field in `/profile` → confirm it persists.
   - Navigate back to `/onboarding` → click Required circle in the stepper → see the editable form (now EMPTY, because Phase 5 store doesn't carry form state across mounts). This is the expected behavior post-band-aid-removal.
   - Confirm dashboard sidebar Profile link still works (carry-over from Task 8-2).

## Suggested commit

Single commit:

```
refactor: retire onboarding summary-card band-aid; add profile link

The summary cards shipped during Phase 5 closeout were a footgun
mitigation pending the proper edit-my-profile surface. With Task 8-2's
/profile route shipped, the cards come off and the stepper allows
click-to-revisit on completed steps. The "All set" panel gets a
"Manage your profile" button alongside "Go to home".

Existing onboarding step specs continue to pass — they mock the store
with completion flags false, exercising the form path that now always
renders.
```
