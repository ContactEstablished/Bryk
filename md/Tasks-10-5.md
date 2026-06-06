# Task 10-5 — Structured-workout builder UI (Vue)

## Goal
The structured-workout builder: from the `/training` plan-authoring surface, open a planned workout
and compose its blocks and steps — an interval grid for cardio (zone picker that pre-fills raw
power/HR/pace ranges, duration/distance, repeats) and a sets/reps/load table for strength. Backed by
Task 10-4's structure endpoints and Task 10-1/10-2's zones. Frontend only. No backend changes.

## Scope discipline — read first
This is the builder ADR-0003 §2 and Task 9-6 deferred to Phase 10. It is bounded by ADR-0004:
**two-level blocks** (a block has a repeat count and ordered steps — no recursive block nesting), and
**no load/TSS display** (Phase 11). If you find yourself building nested blocks or a TSS readout, stop.

## Depends on
- **Task 10-4** — structure read/write endpoints.
- **Task 10-1 / 10-2** — the athlete's computed zones, to pre-fill a step's raw range from a picked zone.
- Pairs with **Task 9-6** (`/training`) — extend its `training` store/service/types; do not fork a second store.

## Required reading
- `md/decisions/0004-structured-workout-and-zones.md` §2, §3.
- `ui/src/views/TrainingView.vue` + `ui/src/components/onboarding/GoalsStep.vue` — the `useForm` + `useFieldArray` field-array pattern (blocks = an array; each block's steps = a nested array).
- `ui/src/components/profile/ProfileEventCard.vue` — per-item card with `FormField`/`Select` stack + dependent-field reveal (mirror for sport-discriminated step fields).
- `ui/src/schemas/training.ts` — extend with block/step zod schemas (`toTypedSchema`).
- `ui/src/stores/training.ts` / `services/training.ts` / `types/training.ts` — extend with `getStructure`/`saveStructure` + the block/step request types; `ui/src/stores/zones.ts` (Task 10-2) for the zone picker.

## Acceptance criteria
- **Types/service/store**: add `WorkoutBlockDto`/`WorkoutStepDto` (+ responses) and `getStructure(planId, pwId)` / `saveStructure(...)` actions (POST then reload), mirroring 9-6's request/store shape.
- **Builder**: open a planned workout → add/remove/reorder **blocks** (each with a repeat count) → within a block add/remove **steps**. Per step: an `Intent` select; duration **or** distance toggle; a **zone picker** (from `stores/zones`, scoped to the workout's sport) that pre-fills the raw power/HR/pace range (editable); for `Strength` workouts the step shows sets/reps/load/RPE instead. Submit calls `saveStructure`.
- **Validation** (zod, mirroring Task 10-4's server rules): exactly one of duration/distance; `repeats ≥ 1`; range `high ≥ low`; sport-discriminated fields. Field errors via `FormMessage`; server errors via the global banner / `extractApiValidationMessages`.
- **Components**: Composition API, `<script setup lang="ts">`, one per file; extract a `WorkoutBlockCard.vue` / `WorkoutStepRow.vue` if the markup is non-trivial.
- **Tests** (≥2): "Add block" then "Add step" append rows; a strength workout renders sets/reps (not a power zone); (optional) picking a zone pre-fills the range.
- `pnpm run build` + `pnpm test` green; count up by ≥2.

## What NOT to modify
- Do not build recursive/nested blocks — two-level only (ADR-0004 §2).
- Do not show or compute TSS/load — Phase 11.
- Do not build device export (`.fit`/Garmin) — post-v1.
- Do not change the dashboard cards or the Phase-9 plan-level form beyond launching the builder.
- Do not call `fetch`/`axios` directly from components.

## Suggested commit
```
feat: add structured-workout builder UI

Compose a planned workout's blocks and steps from /training: cardio
interval grid with a zone picker that pre-fills power/HR/pace ranges and a
strength sets/reps/load table, with repeat counts per block. Two-level
blocks only; no TSS readout (Phase 11). Backed by the 10-4 structure API.
```
