# Task 10-2 — Zones configuration UI (Vue)

## Goal
A `/zones` (or a Profile section) surface where the athlete sees their computed training zones per
sport and can override/reset individual zone bounds, backed by Task 10-1's `GET/PUT /api/v1/zones`.
Frontend only. No backend changes.

## Depends on
- **Task 10-1** — `GET /api/v1/zones` (effective zones) + the override write endpoint.

## Required reading
- `md/decisions/0004-structured-workout-and-zones.md` §1, §4 (full config UI in scope).
- `ui/src/components/profile/ProfileRecommendedSection.vue` — the closest reference: per-sport rows, number inputs, `useForm` + zod, save-then-reload.
- `ui/src/schemas/onboarding.ts` (`optionalNumber`, `toTypedSchema`), `ui/src/services/profile.ts` (`apiFetch`), `ui/src/stores/profile.ts` (setup-store + `saveX` → reload).
- `ui/src/router/index.ts` — lazy route registration (mirror `/training`).

## Acceptance criteria
- **Types** (`ui/src/types/zones.ts`): mirror the 10-1 response (per-sport zone arrays: number, metric, lower, upper).
- **Service** (`ui/src/services/zones.ts`): `getZones()`, `saveZones(...)` via `apiFetch`.
- **Store** (`ui/src/stores/zones.ts`): `zones` ref, `loadZones()`, `saveZones()` (POST then reload), loading/error flags — mirror `stores/profile.ts`.
- **View/section**: per-sport zone tables (bike 7 rows power, run/swim 5 rows pace; strength omitted), showing computed defaults; editable bounds with a "Reset to computed" per sport; zod validation (`upper > lower`, positive); global error banner; loading gated on data presence (the /profile flash lesson).
- **Route**: lazy `/zones` (or a tab in `ProfileView`) — pick one and note why; default to a dedicated `/zones` route.
- **Tests** (≥2): renders computed zones for a seeded store; editing + save calls the store action; (optional) reset restores computed.
- `pnpm run build` + `pnpm test` green; count up by ≥2.

## What NOT to modify
- Do not change the workout builder (Task 10-5) or dashboard cards.
- Do not recompute zones client-side — the server is the source of truth (10-1).
- Do not call `fetch`/`axios` directly from components.

## Suggested commit
```
feat: add training-zones configuration UI

A /zones surface listing computed per-sport zones (7-zone bike power,
5-zone run/swim pace) with editable overrides and reset-to-computed,
backed by /api/v1/zones via a new zones store/service.
```
