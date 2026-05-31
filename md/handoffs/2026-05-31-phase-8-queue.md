# TRACKING NOTE — Bryk Phase 8 work queue

**Date:** 2026-05-31
**Phase:** 8 — Profile editing + dashboard warmup cards (🟡 IN PROGRESS)
**Purpose:** Single durable queue for the rest of Phase 8 — what's merged, what's left,
the recommended order, the per-task gotchas surfaced during 8-2, and two deferred
`/profile` polish items that have no formal task doc yet.

## Status at a glance

| Item | Scope | Status |
|---|---|---|
| Task 8-1 | Profile read endpoints (`GET /profile/{required,recommended,goals}`) | ✅ merged (`92154f4`) |
| Task 8-2 Part A | Event/Goal CRUD endpoints (`POST/PUT/DELETE /events|/goals`) | ✅ merged (`f36e374`) |
| Task 8-2 Part B | `/profile` Vue surface (3 editable sections) | ✅ merged (`a1d2988`, `9662352`, `b3fd47d`) + hotfix (`ec4f492`) |
| **Task 8-3** | Retire onboarding summary-card band-aid + "Manage your profile" link | ⏳ **next** |
| Task 8-4 | Wire Primary Goal dashboard card | ⏳ queued |
| Task 8-5 | Wire Resting HR card + finalize sidebar Profile active state | ⏳ queued (**closes Phase 8**) |
| Polish P1 | Imperial/metric toggle in `ProfileRequiredSection` | 🔵 deferred (no task doc — spec below) |
| Polish P2 | Kill the pre-load content "flash" on the 3 profile sections | 🔵 deferred (no task doc — spec below) |

## What just shipped (Task 8-2 Part B)

The `/profile` route with three editable sections: **Required** and **Recommended** save
through the existing onboarding upsert POSTs; **Goals** does real per-row add/save/delete
against the Part A event/goal CRUD endpoints, reading Id-bearing items from
`GET /profile/goals`. New types/services/store/view/section+card components under
`ui/src/{types,services,stores,views,components}/profile/`, the sidebar Profile/Dashboard
nav made navigable, and the `eventItemSchema`/`goalItemSchema` exported for per-row reuse
(no rule changes). Full spec in `md/Tasks-8-2.md`.

**Hotfix `ec4f492`:** `ProfileRecommendedSection` was missing `initialValues`, so
`form.values.sportThresholds` was `undefined` on first paint and the template's
`sportThresholds[index]` access threw, blanking `/profile`. Seeded the fixed Bike/Run/Swim
rows + added a mount regression spec.

**Frontend baseline (at `ec4f492`):** `pnpm run build` green; `pnpm test` green —
**31 tests / 12 files**. Working tree clean.

## Recommended order

`8-3 → 8-4 → 8-5 → (optional) P2 → P1`

- 8-3 first: it's the direct unblock of 8-2 (the read-only "X saved" cards can come off now
  that `/profile` is the real edit path), smallest/lowest-risk, and clears the wizard.
- 8-4 before 8-5: 8-5's card is told to **match 8-4's `PrimaryGoalCard` loading aesthetic**.
- P2 before P1: gating each section to render only once its data is loaded gives P1's
  imperial toggle a single deterministic moment to seed its display fields.

8-3/8-4/8-5 are all unblocked (each reuses Task 8-2's `useProfileStore`). The two polish
items are optional and independent of the rest of Phase 8.

## Remaining tasks — pointers + gotchas

### Task 8-3 — Retire onboarding summary-card band-aid → `md/Tasks-8-3.md`
Frontend-only. Remove the `v-if="store.<flag>Complete"` summary-card block + its
`<template v-else>` wrapper + the `CheckCircle2` import from each of `RequiredStep.vue`,
`RecommendedStep.vue`, `GoalsStep.vue`; add a "Manage your profile" button → `goToProfile()`
→ `/profile` to the OnboardingView "All set" panel. **Do not** touch the stepper
`canJumpTo`/`isLocked` logic, backend, or `/profile` code. The 3 existing step specs mock
completion flags `false` (form path) and should pass unchanged — verify, don't assume.

### Task 8-4 — Wire Primary Goal dashboard card → `md/Tasks-8-4.md`
Add a `primaryEvent` computed to `useProfileStore` (export it); new
`PrimaryGoalCard.vue` + spec; swap the HomeView placeholder. Selection: exclude past
events, then priority A<B<C, then earliest date. Gotchas:
- **Type:** the doc says the computed returns `EventDto`, but `store.goals.events` are
  `EventResponse[]` (Id-bearing; `EventResponse extends EventDto`). Type it
  `EventResponse | null` — verify against the actual store types, not the doc wording.
- Enums are **string unions** (`'A'|'B'|'C'`, dates `'YYYY-MM-DD'`) → priority and date
  sort as plain string compares; "today" in UTC (see `utcTodayIso` in `schemas/onboarding.ts`).
- **Test:** the empty-state uses a `router-link` → stub it (`RouterLinkStub`) or mount with a
  memory router, or the bare `mount()` throws.
- **Decision to close:** client-side selection (recommended (a)) vs a new
  `GET /profile/primary-event` (b). Confirm (a) before coding.

### Task 8-5 — Wire Resting HR card + sidebar polish → `md/Tasks-8-5.md` (closes Phase 8)
New `RestingHrCard.vue` + spec reading `useProfileStore().recommended.restingHr`; swap the
HomeView placeholder. Sidebar is **confirm/polish only** (8-2 already made Profile
navigable — verify the active treatment matches Dashboard's; likely a no-op). Match
`PrimaryGoalCard`'s loading aesthetic. Same `router-link` test gotcha as 8-4. Leave the
other three top-row cards (Weekly Load / Sleep Avg / Form-TSB) as placeholders.

## Deferred `/profile` polish (no task doc — full mini-spec here)

### Polish P1 — Imperial/metric toggle in `ProfileRequiredSection`
**Goal:** parity with `onboarding/RequiredStep.vue`'s metric/imperial toggle. Today the
profile editor shows Height/Weight as **cm/kg only**; an athlete who onboarded in imperial
sees metric on `/profile`.
- **Source of truth:** `RequiredStep.vue` — the `unitSystem` ref + localStorage key
  `'bryk:unitSystem'`, `heightFeet`/`heightInches`/`weightLb` refs, `CM_PER_INCH`/`KG_PER_LB`,
  the `suppressImperialSync` guard, the two `flush: 'sync'` watches, `setUnitSystem()`, and the
  toggle button group + conditional cm-vs-ft/in / kg-vs-lb inputs.
- **The easy-to-miss bit:** the profile form is **seeded** from `store.required` (unlike the
  wizard, which starts empty). When the active unit is imperial you must derive
  `heightFeet/heightInches/weightLb` FROM the seeded `heightCm/weightKg` at load time (reuse
  the `setUnitSystem('imperial')` conversion), in the same watch/resetForm seed step — else an
  imperial user sees empty height/weight despite data being present.
- Reuse the same `'bryk:unitSystem'` localStorage key (shared preference). Keep the
  `heightCm`/`weightKg` `FormField` wrappers so `FormMessage` errors still surface. Stored/
  submitted values stay cm/kg — the toggle is display-only. No zod rule changes.
- **Decision to close:** (a) duplicate the toggle logic into `ProfileRequiredSection`
  (honors Task 8-2 Decision 2 "accept duplication", smallest footprint, but two copies of the
  conversion math) vs (b) extract a `useImperialUnits` composable used by both `RequiredStep`
  and `ProfileRequiredSection` (DRY, supersedes Decision 2, modifies `RequiredStep`). Lean (b).
- **Files:** `ProfileRequiredSection.vue` (+ a new `composables/useImperialUnits.ts` and
  `RequiredStep.vue` if (b)) + spec.

### Polish P2 — Kill the pre-load "flash" on the profile sections
**Problem:** each section gates its body with `v-if="loadingX && !X"` / `v-else-if XError && !X`
/ `v-else content`. Because `loadingX` starts `false`, first paint falls through to the
content branch and briefly shows default/empty content before flipping to "Loading…".
- **Fix:** in all three sections (`ProfileRequiredSection`, `ProfileRecommendedSection`,
  `ProfileGoalsSection`) replace the gate with `v-if="store.<X>"` → content /
  `v-else-if="store.<X>Error"` → the existing error+Retry / `v-else` → "Loading…".
  (`loadX()` clears its error at the start, so retry correctly shows Loading→content.)
- **Spec updates this forces** (the "render before data" path goes away):
  - `ProfileRequiredSection.spec.ts` — its "submit empty" case mounts unseeded; the form
    won't render. Seed `initialState:{ profile:{ required:<valid> } }`, then clear the Name
    input before submitting.
  - `ProfileRecommendedSection.spec.ts` — its "renders rows before data loads" case won't
    render. Replace with: (1) unseeded → assert "Loading…"; (2) seed `recommended` → assert
    Bike/Run/Swim rows.
  - `ProfileGoalsSection.spec.ts` — already seeds `goals`; `v-if="store.goals"` renders fine —
    verify, no change expected.
- **Files:** the three section components + the two updated specs.

## What the next session should do first

1. Read this note + the relevant task doc (`md/Tasks-8-3.md` for the next task).
2. `git status` clean; `git log --oneline -10` for context; from `ui/` run `pnpm run build`
   + `pnpm test` and confirm the green baseline (31/12 at the time of writing — will have
   grown if later tasks merged first).
3. For any **backend** run, confirm `dotnet user-secrets list` shows your local
   `ConnectionStrings:DefaultConnection` + `DevAuth:CurrentAthleteId` (required since Task 7-5;
   note 8-3/8-4/8-5 and both polish items are frontend-only and don't need it).
4. Open the next task.

## Notes / known states carried forward

- `ProfileRequiredSection` is **metric-only** until Polish P1 lands.
- The brief pre-load content flash affects **all three** profile sections until Polish P2.
- The two polish items have no formal task doc; if formalized, the next numbers are
  `md/Tasks-8-6.md` / `md/Tasks-8-7.md`.
- `ROADMAP.md` Phase 8 row stays 🟡 until Task 8-5 merges (don't flip early).
