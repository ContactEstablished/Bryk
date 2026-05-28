# Task 5-3 — Align client schemas and payloads with Phase 4 validators

## Goal
Tighten the Vue-side zod schemas and submit payloads so they match the Phase 4 FluentValidation rules and DTO shapes as closely as practical before the Phase 5 smoke test.

Current code already complete:
- `ui/src/types/onboarding.ts` mirrors the server DTOs and enums, including `Sport.Triathlon` and `customDistanceName`.
- `ui/src/schemas/onboarding.ts` defines zod schemas for all three steps.
- `RequiredStep.vue`, `RecommendedStep.vue`, and `GoalsStep.vue` use `<script setup lang="ts">`, vee-validate, and zod.
- `GoalsStep.vue` supports event and goal field arrays plus triathlon custom distance UI.

Known gaps to verify/fix:
- Goal form currently sends every goal as `type: 'General'`; if the UI should support `GoalType.EventDriven`, add a minimal selector or explain why `General` is the intended Phase 5 surface.
- Goals/event client validation should match server constraints where practical: required names/descriptions, max lengths, and date-not-in-past rules.
- Event triathlon rules should not send `triathlonDistance` or `customDistanceName` unless applicable.
- Recommended step should preserve generic `thresholdValue` while keeping per-sport labels in the UI.

## Acceptance criteria
- Required-step client validation still matches server validator ranges and required fields.
- Recommended-step schema continues to allow optional HR and optional per-sport thresholds, with `maxHr > restingHr` when both are provided.
- Goals-step schema validates:
  - event name required and max 200;
  - event date today or later;
  - event notes max 2000 when provided;
  - goal description required and max 2000;
  - target date today or later when provided;
  - goal `type` is included in the payload and intentionally chosen by the UI or intentionally documented as `General` only for Phase 5.
- Triathlon custom distance payload behavior remains correct.
- `npm run build` from `ui/` succeeds.

## Files likely to change
- `ui/src/schemas/onboarding.ts`
- `ui/src/components/onboarding/GoalsStep.vue`
- Possibly `ui/src/types/onboarding.ts` only if a type mismatch is found.

## What NOT to modify
- Do not change backend validators or DTOs.
- Do not add new goal/event persistence behavior.
- Do not add equipment management or edit-profile UI.
- Do not change the route or global wizard layout.

## Test plan
1. Run `npm run build` from `ui/`.
2. Manually check that invalid past dates and overlong text show client-side errors before submission.
3. Inspect `GoalsStep.vue` payload construction to confirm it matches `OnboardingGoalsRequest` exactly.
