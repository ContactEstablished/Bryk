# Task 6-2 — Vue test infrastructure (Vitest)

## Goal
Stand up Vitest under `ui/` so the frontend has a real safety net before any Phase 7 UI work. Land a passing smoke test for `useOnboardingStore`, a passing smoke test for `src/services/onboarding.ts`, and one passing component test per onboarding step (three total). Coverage breadth comes later as new code lands — this task is about establishing the surface.

## Current code/status
- `ui/package.json` shows Vite 8, Vue 3.5, TypeScript ~6.0, vee-validate + zod, Pinia 3, Vue Router 4, Tailwind 4 + shadcn-vue + reka-ui. Package manager is `pnpm@10.33.2`. No test runner is currently installed; no `test` script in `package.json`.
- Existing source surfaces:
  - `ui/src/services/onboarding.ts` — typed methods for the four Phase 4 onboarding endpoints, routed through `ui/src/services/api.ts` (`apiFetch` wrapper, shared `ApiError`).
  - `ui/src/stores/onboarding.ts` — Pinia store exposing `loadStatus`, `nextIncompleteStep`, and the per-step submit actions.
  - `ui/src/components/onboarding/RequiredStep.vue`, `RecommendedStep.vue`, `GoalsStep.vue` — all `<script setup lang="ts">` with vee-validate + zod schemas.
  - `ui/src/schemas/onboarding.ts` — zod schemas aligned with Phase 4 FluentValidation rules (Task 5-3).
- Server validation error mapping helper (from Task 5-1) lives in `ui/src/services/` or `ui/src/lib/` — confirm exact path when writing the test; the mapper is a natural unit-test target but not required by this task.
- `pnpm build` (`vue-tsc -b && vite build`) is the only command currently exercised in CI-equivalent flows; Task 6-3 will wire a real CI.

## Acceptance criteria
- Vitest installed via pnpm with companion deps: `vitest`, `@vue/test-utils`, `@vitest/coverage-v8` (coverage optional but recommended — flag in the approval request), `jsdom` (or `happy-dom` — pick one and document why), `@pinia/testing`. New dependencies are an approval gate per CLAUDE.md — list them and the rationale when requesting Sr. Dev sign-off.
- `vitest.config.ts` (or merged into `vite.config.ts`) under `ui/` configures the chosen DOM environment, sets up Vue plugin, and points at `src/**/*.{test,spec}.ts` (or `.test.ts` colocated with source — pick one convention and document it).
- `ui/package.json` gains `"test": "vitest run"` and `"test:watch": "vitest"` scripts. Do not remove the existing `build` script.
- At least one passing test per surface:
  - `ui/src/stores/__tests__/onboarding.spec.ts` (or colocated) — verifies `nextIncompleteStep` derives correctly from each combination of the three status flags (no flags / required only / required+recommended / all complete).
  - `ui/src/services/__tests__/onboarding.spec.ts` — uses `vi.fn()` or `vi.spyOn(globalThis, 'fetch')` to stub `apiFetch`'s underlying transport and verifies that each of the four onboarding methods calls the correct URL/verb and returns the typed shape. No real HTTP.
  - `ui/src/components/onboarding/__tests__/RequiredStep.spec.ts` — mounts the component with `@vue/test-utils` + Pinia testing harness, asserts the form renders and that submitting empty fields surfaces at least one client-side validation error.
  - `RecommendedStep.spec.ts` and `GoalsStep.spec.ts` — analogous one-test smoke each, focused on render + one validation/interaction assertion. Do not chase coverage; prove the harness mounts the component cleanly.
- `pnpm test` from `ui/` runs and reports five passing tests, 0 failures. Test count and timing captured in the commit message.
- Coverage tooling (if installed) wired but not gated — Task 6-3 decides whether CI enforces a coverage floor.
- No real HTTP, no real router navigation, no real backend dependency. Tests must run offline.

## Files likely to change/add
- `ui/package.json` — devDependency additions, `test` / `test:watch` scripts.
- `ui/pnpm-lock.yaml` — regenerated from the install.
- `ui/vitest.config.ts` (new) — or extend `ui/vite.config.ts` via the `test` block.
- `ui/src/test-setup.ts` (new, if needed) — global test setup (e.g., `createPinia`, DOM matchers if any are adopted).
- `ui/src/stores/__tests__/onboarding.spec.ts` (new).
- `ui/src/services/__tests__/onboarding.spec.ts` (new).
- `ui/src/components/onboarding/__tests__/RequiredStep.spec.ts` (new).
- `ui/src/components/onboarding/__tests__/RecommendedStep.spec.ts` (new).
- `ui/src/components/onboarding/__tests__/GoalsStep.spec.ts` (new).

## What NOT to modify
- Do not change production store, service, component, or schema logic.
- Do not introduce a CI workflow file — Task 6-3 owns that.
- Do not touch the .NET test surface — Task 6-1 owns that.
- Do not touch tech-debt items or the Mesocycle layer fix — Task 6-4 owns those.
- Do not touch `appsettings.development.json` or secret hygiene — Task 6-5 owns that.
- Do not add component tests beyond the three required smoke tests; broader coverage is for Phase 7+ as new components land.
- Do not change the existing `build` script behavior or vee-validate/zod schema files.
- Do not pull in a UI snapshot library or component-level visual regression tooling — out of scope for this phase.

## Approval gates / open questions
- **Approval gate:** new devDependencies (`vitest`, `@vue/test-utils`, `@pinia/testing`, `jsdom`/`happy-dom`, optional `@vitest/coverage-v8`). Confirm the full list with Sr. Dev before `pnpm install`.
- **Decision question:** `jsdom` vs `happy-dom` for the DOM environment. `happy-dom` is faster and lighter; `jsdom` is the de-facto Vue Test Utils default and has fewer rough edges with third-party libs. Recommendation to discuss: start with `jsdom` for fewer surprises; revisit if test suite latency becomes annoying.
- **Decision question:** colocated `*.spec.ts` next to source vs `__tests__/` subfolders. Recommendation: `__tests__/` subfolders to keep `ls src/components/onboarding/` clean for a UI-heavy project. Either is fine — pick and document.
- **Decision question:** install `@vitest/coverage-v8` now or defer to Task 6-3? Recommendation: install now so CI in Task 6-3 has the option without a follow-up dependency PR.

## Test plan
1. `pnpm install` from `ui/` succeeds with the new devDependencies.
2. `pnpm test` from `ui/` runs all five tests green.
3. `pnpm build` from `ui/` still succeeds — no regression from the test config additions.
4. Manually break one assertion locally and confirm `pnpm test` fails red, to prove the harness actually executes the code.
5. Confirm `git diff` touches only `ui/` test infrastructure files, new test files, and `package.json` / `pnpm-lock.yaml`.
