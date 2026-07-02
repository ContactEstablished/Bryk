# Impl 17-4 — Build order: goal/event CRUD forms on the Goals page

**Executor:** the architect-implementer. **Acceptance contract:** `md/Tasks-17-4.md`. **Decision
lock:** ADR-0002 §"v1 is athlete-only" (no athlete-identity params in any service call).
**Scope:** Frontend only. No new package, no backend change, no migration.

## Step 0 — Pre-flight

- `git status` clean on `main`, Task 17-3 committed. `pnpm run build` + `pnpm test` green.
- Confirm 17-3's artifacts exist (this task is a pure extension of them — do not recreate):
  `ui/src/views/GoalsView.vue`, `ui/src/stores/goals.ts`, `ui/src/services/goals-events.ts`,
  `ui/src/types/goals.ts` (`EventListItem`, `GoalListItem`, `GoalStatus`, `LinkedPlan`),
  `ui/src/components/goals/GoalsEventCard.vue`, `ui/src/components/goals/GoalsGoalCard.vue`, the
  `/goals` route in `ui/src/router/index.ts`, and the live `Goals` entry in
  `ui/src/components/layout/AppSidebar.vue`'s `trainItems`. If any is missing or shaped differently
  than described below, **stop** — 17-3 is not actually done and must land first.
- Re-read `md/Tasks-17-4.md` in full.
- Open for reference (read-only, do not edit): `ui/src/components/profile/ProfileEventCard.vue`,
  `ui/src/components/profile/ProfileGoalCard.vue`, `ui/src/components/profile/ProfileGoalsSection.vue`
  (the draft-array pattern — `draftCounter` + `eventDrafts`/`goalDrafts` push/filter, "Add Event"/
  "Add Goal" buttons, `@remove`/`@created` wiring), `ui/src/stores/profile.ts` (the
  `createEvent`/`updateEvent`/`deleteEvent`/`createGoal`/`updateGoal`/`deleteGoal` → `loadGoals()`
  pattern to mirror against `loadAll()`), `ui/src/schemas/onboarding.ts` (`eventItemSchema`,
  `goalItemSchema` — reuse verbatim, do not fork), `ui/src/services/events.ts` /
  `ui/src/services/goals.ts` (existing write functions, unchanged), `ui/src/services/apiErrors.ts`
  (`extractApiValidationMessages`) + `ui/src/services/api.ts` (`ApiError`).
- Confirm in `ui/src/types/onboarding.ts` / `ui/src/types/profile.ts`: `EventDto`, `GoalDto`,
  `EventResponse`, `GoalResponse` shapes (no `TargetValue`/`Unit`/`CurrentValue` fields exist on
  `GoalDto` — confirms the "no quantitative goal fields" fence is a no-op restriction, not a gap to
  fill).

## Step 1 — `goals` store: CRUD actions

**Edit** `ui/src/stores/goals.ts` (17-3's file). Import the existing write services and add six
actions that mirror `stores/profile.ts`'s pattern exactly, but re-fetching via `loadAll()` instead of
`loadGoals()`:

```ts
import {
  createEvent as createEventApi,
  updateEvent as updateEventApi,
  deleteEvent as deleteEventApi,
} from '@/services/events'
import {
  createGoal as createGoalApi,
  updateGoal as updateGoalApi,
  deleteGoal as deleteGoalApi,
} from '@/services/goals'
import type { EventDto, GoalDto } from '@/types/onboarding'

// ...inside the store setup function, alongside loadAll:

async function createEvent(dto: EventDto) {
  await createEventApi(dto)
  await loadAll()
}

async function updateEvent(id: string, dto: EventDto) {
  await updateEventApi(id, dto)
  await loadAll()
}

async function deleteEvent(id: string) {
  await deleteEventApi(id)
  await loadAll()
}

async function createGoal(dto: GoalDto) {
  await createGoalApi(dto)
  await loadAll()
}

async function updateGoal(id: string, dto: GoalDto) {
  await updateGoalApi(id, dto)
  await loadAll()
}

async function deleteGoal(id: string) {
  await deleteGoalApi(id)
  await loadAll()
}
```

Add all six to the store's `return { ... }`. No try/catch here — let errors propagate to the caller
(the form component maps them), same as `stores/profile.ts`.

**Do NOT** touch `stores/profile.ts` — its own `createEvent`/etc. stay wired to `loadGoals()`
for the Profile page; this is a separate, parallel set of actions on the `goals` store only.

**Verify:** `pnpm run build` green (type-check on the new imports/actions). No test yet — covered in
Step 4's store extension.

## Step 2 — `GoalsEventForm.vue`

**New file** `ui/src/components/goals/GoalsEventForm.vue`. Copy the structure of
`ProfileEventCard.vue` (`useForm` + `toTypedSchema(eventItemSchema)`, `isDraft` computed, field set,
`setError`, `justSaved`/`deleting`, the `dirty` watch clearing `justSaved`) with three changes:

1. Props/types use the Goals-page shapes: `defineProps<{ event?: EventListItem | null }>()` (from
   `@/types/goals`), not `EventResponse`.
2. The store is `useGoalsStore()` (`@/stores/goals`), not `useProfileStore()`. Calls become
   `store.createEvent` / `store.updateEvent` / `store.deleteEvent` (the Step 1 actions).
3. `toFormItem`/`emptyEvent` and the `EventDto` construction in `onSubmit` are unchanged — the field
   set (name, eventDate, sport, triathlonDistance, customDistanceName, priority, notes) and the
   sport/distance conditional rendering rules mirror `ProfileEventCard.vue` exactly.

`defineEmits<{ remove: []; created: [] }>()` unchanged. Confirm `EventListItem` (17-3's type,
`EventResponse & { linkedPlans: LinkedPlan[] }`) is assignable to the card's internal `EventFormItem`
via `toFormItem` — the extra `linkedPlans` field is simply not read here (the card doesn't map it into
the form; the read-display card owns rendering `linkedPlans`).

**Do NOT** add a plan-link field/control to this form — `linkedPlans` stays display-only, rendered
only by `GoalsEventCard.vue` (17-3), never edited here.

**Verify:** `pnpm run build` green (component type-checks in isolation; `GoalsView` doesn't import it
yet).

## Step 3 — `GoalsGoalForm.vue`

**New file** `ui/src/components/goals/GoalsGoalForm.vue`. Copy the structure of
`ProfileGoalCard.vue` with the same three changes as Step 2:

1. `defineProps<{ goal?: GoalListItem | null }>()` (from `@/types/goals`).
2. `useGoalsStore()`; calls become `store.createGoal` / `store.updateGoal` / `store.deleteGoal`.
3. Field set (description, targetDate) and the `GoalDto` construction are unchanged — **`type` stays
   hardcoded to `'General'`** exactly as `ProfileGoalCard.vue` does it:

```ts
const dto: GoalDto = {
  type: 'General',
  description: values.description,
  targetDate: values.targetDate || null,
}
```

`GoalsView` does not add a `GoalType` selector (confirmed absent from 17-3's acceptance criteria) —
if a future reviewer asks for one, that is new scope, not this task.

**STOP-and-flag condition:** if at any point this step seems to need `TargetValue`/`Unit`/
`CurrentValue` fields (quantitative goals), stop and surface it — those fields don't exist on `GoalDto`
today and are explicitly deferred per `Tasks-17-4.md`. Do not add a client-only field for them.

**Verify:** `pnpm run build` green.

## Step 4 — Store unit tests: CRUD actions

**Edit** `ui/src/stores/__tests__/goals.spec.ts` (17-3's file — extend, don't replace). Add cases
mirroring the shape of any equivalent test in `stores/profile.ts`'s coverage (create/update/delete →
re-fetch):

- `createEvent(dto)` calls the mocked `events.ts#createEvent`, then re-invokes `loadAll()` (assert
  `getEvents`/`getGoalsList` were called again after the create — i.e. call counts go from 1 to 2 for
  the paired reads).
- `updateEvent(id, dto)` calls `events.ts#updateEvent` with `(id, dto)`, then re-fetches.
- `deleteEvent(id)` calls `events.ts#deleteEvent` with `(id)`, then re-fetches.
- `createGoal`/`updateGoal`/`deleteGoal` — same three assertions against `goals.ts`'s functions.
- An error thrown by the underlying service (e.g. `createEventApi` rejects) propagates out of
  `store.createEvent` (i.e. the store re-throws, does not swallow) — `await expect(store.createEvent(dto)).rejects.toThrow()`.

Mock `@/services/events` and `@/services/goals` with `vi.mock`, mock `@/services/goals-events`'s
`getEvents`/`getGoalsList` as in 17-3's existing spec setup.

**Verify:** `pnpm exec vitest run ui/src/stores/__tests__/goals.spec.ts --no-file-parallelism` green.

## Step 5 — `GoalsEventForm.spec.ts`

**New file** `ui/src/components/goals/__tests__/GoalsEventForm.spec.ts`. Use `createTestingPinia`
(`createSpy: () => vi.fn()`ish, or `createSpy: vi.fn` per the existing profile-section test's
`createSpy: () => () => {}` pattern — but here we need to assert calls, so use
`createSpy: vi.fn` so `store.createEvent` etc. are spies) over `@pinia/testing`, same shape as
`ProfileGoalsSection.spec.ts`'s `mountSection` helper but mounting `GoalsEventForm` directly.

Cases:
- **Draft submit (valid input):** mount with `event` prop absent. Fill `name` + a future `eventDate`
  (`>= utcTodayIso()`, e.g. `'2099-01-01'`) + `priority` (default `'B'` satisfies the schema). Trigger
  submit. Poll with `vi.waitFor(() => expect(useGoalsStore().createEvent).toHaveBeenCalledTimes(1))`
  rather than a fixed `flushPromises()` count (per the repo's memory note on valid-submit specs over
  refined zod schemas needing more microtask hops than the empty/invalid case — `vi.waitFor` is the
  robust form, not a guessed loop count). Assert the call argument is the mapped `EventDto` (name,
  eventDate, sport, triathlonDistance/customDistanceName nulled per the Triathlon/Custom conditionals,
  priority, notes). Assert `emitted('created')` has length 1.
- **Existing-row submit:** mount with `event` set to a sample `EventListItem`. Change a field, submit.
  `vi.waitFor(() => expect(store.updateEvent).toHaveBeenCalledWith(event.id, expect.objectContaining({...})))`.
  Assert `justSaved` renders ("Saved" text) after the poll resolves.
- **Delete:** mount with `event` set. Click "Delete". `vi.waitFor(() => expect(store.deleteEvent).toHaveBeenCalledWith(event.id))`.
- **404 on save:** make the spied `store.updateEvent` (or `createEvent`) reject with
  `new ApiError(404, 'Not Found', null)`. Submit. `vi.waitFor` until the "no longer exists" text is
  present (mirror `ProfileEventCard`'s exact copy: `'This event no longer exists — it may have been removed.'`).
- **Invalid input blocks submit:** empty `name` — submit, assert `store.createEvent` is never called
  (`vi.waitFor`-poll a short timeout is unnecessary here; assert after one `flushPromises()` that the
  call count is still 0 and a `FormMessage` renders "Event name is required"). Past `eventDate` (e.g.
  yesterday's date) — same pattern, asserts the "Event date must be today or later" message and no
  submit call.

**Verify:** `pnpm exec vitest run ui/src/components/goals/__tests__/GoalsEventForm.spec.ts --no-file-parallelism` green.

## Step 6 — `GoalsGoalForm.spec.ts`

**New file** `ui/src/components/goals/__tests__/GoalsGoalForm.spec.ts`. Same harness as Step 5,
mounting `GoalsGoalForm`.

Cases:
- **Draft submit (valid):** fill `description`, leave `targetDate` empty (schema allows null/empty).
  Submit, `vi.waitFor(() => expect(store.createGoal).toHaveBeenCalledTimes(1))`, assert the call arg
  is `{ type: 'General', description: ..., targetDate: null }`, assert `emitted('created')`.
- **Update:** mount with `goal` set, change `description`, submit,
  `vi.waitFor(() => expect(store.updateGoal).toHaveBeenCalledWith(goal.id, expect.objectContaining({ type: 'General' })))`.
- **Delete:** click "Delete", `vi.waitFor(() => expect(store.deleteGoal).toHaveBeenCalledWith(goal.id))`.
- **Empty description blocked:** submit with `description` blank — assert `store.createGoal` not
  called, `FormMessage` shows "Description is required".
- **`justSaved` appears after a successful save:** after the update case's `vi.waitFor` resolves,
  assert the "Saved" text (with the `CheckCircle2` icon) is present; assert it disappears once the
  form is made dirty again (matches the existing `watch(() => form.meta.value.dirty, ...)` behavior —
  edit a field, assert "Saved" text is gone).

**Verify:** `pnpm exec vitest run ui/src/components/goals/__tests__/GoalsGoalForm.spec.ts --no-file-parallelism` green.

## Step 7 — `GoalsView.vue` wiring

**Edit** `ui/src/views/GoalsView.vue` (17-3's file). Replace the stubbed "Add event"/"Add goal"
affordances with the `ProfileGoalsSection.vue` draft-array pattern, adapted to render forms (not just
read cards) so the create/edit loop is on-page:

```ts
import GoalsEventForm from '@/components/goals/GoalsEventForm.vue'
import GoalsGoalForm from '@/components/goals/GoalsGoalForm.vue'

let draftCounter = 0
const eventDrafts = ref<number[]>([])
const goalDrafts = ref<number[]>([])

function addEventDraft() {
  eventDrafts.value.push((draftCounter += 1))
}
function removeEventDraft(key: number) {
  eventDrafts.value = eventDrafts.value.filter((k) => k !== key)
}
function addGoalDraft() {
  goalDrafts.value.push((draftCounter += 1))
}
function removeGoalDraft(key: number) {
  goalDrafts.value = goalDrafts.value.filter((k) => k !== key)
}
```

For each existing row (`store.events` / `store.upcomingEvents` etc.), render the 17-3 read-display
card (`GoalsEventCard`/`GoalsGoalCard`) plus an inline "Edit" toggle (a local `ref<Set<string>>` of
expanded ids, or a per-row `ref(false)` via `v-for` index — pick whichever the existing codebase leans
toward; `ProfileGoalsSection` doesn't need this toggle because Profile renders forms unconditionally,
but the Goals page already has a read card from 17-3, so an explicit toggle is the correct minimal
addition) that reveals `GoalsEventForm`/`GoalsGoalForm` bound to that row's data. Collapse the form
back to the read card on `@created` (not applicable for existing rows — `created` only fires for
drafts) — for existing rows, collapse back to read-display on a successful save (watch the form's
`justSaved` via a local state flip, or simply collapse on click of a "Done"/close affordance; keep this
minimal — do not over-engineer a two-way sync). For draft rows, replace the stubbed button with
`addEventDraft`/`addGoalDraft`, render `GoalsEventForm`/`GoalsGoalForm` with no `event`/`goal` prop for
each draft key, wire `@remove="removeEventDraft(key)"` and `@created="removeEventDraft(key)"` (mirror
`ProfileGoalsSection.vue` verbatim for the draft plumbing).

**Do NOT** wire a plan-link editor into either read card or form — `linkedPlans` remains display-only
per 17-3, untouched by this task.

**Verify:** `pnpm run build` green (vue-tsc clean on the new imports/refs).

## Step 8 — `GoalsView.spec.ts` extension

**Edit** `ui/src/views/__tests__/GoalsView.spec.ts` (17-3's file — extend). Add cases:
- Clicking "Add Event" reveals a `GoalsEventForm` (find by component, assert count goes from 0 to 1).
- Clicking "Add Goal" reveals a `GoalsGoalForm`.
- A successful create (mock the store's `createEvent`/`createGoal` to resolve, trigger the draft
  form's submit, `vi.waitFor` the emit) removes the draft form from the DOM (count back to 0) — mirror
  the assertion style of `ProfileGoalsSection.spec.ts`'s "Add Event" appends a draft card" test, but add
  the completion half (`@created` removing it) since Profile's stub never had a full submit test.

**Verify:** `pnpm exec vitest run ui/src/views/__tests__/GoalsView.spec.ts --no-file-parallelism` green.

## Step 9 — Full suite + manual smoke

- `pnpm run build` (vue-tsc) green.
- `pnpm exec vitest run --no-file-parallelism` green — full suite, not just the new files (confirms no
  regression in `ProfileGoalsSection`, `ProfileEventCard`/`ProfileGoalCard` behavior, which stay
  untouched). If the known transient worker-fork crash appears with all tests reporting passed, re-run
  once before treating it as a real failure (see the repo's vitest-worker-crash-transient note).
- Manual smoke against the dev seed: `/goals` → "Add Event" → fill a valid row → Save → draft
  disappears, a new read card appears with the server-issued id and computed `status`/
  `daysRemaining`/`linkedPlans`. Edit an existing event's name → Save → "Saved" flag appears, card
  reflects new name. Delete an event → card disappears. Repeat for a goal (create/edit/delete).
  Confirm zero console errors. Confirm the Profile page's own Goals section (`/profile`) is unaffected
  — the onboarding flow and `ProfileGoalsSection`/`ProfileEventCard`/`ProfileGoalCard` are untouched by
  this diff.

## Step 10 — Commit

- `git diff --stat` — expect only: `ui/src/stores/goals.ts` (edit, Step 1), new
  `ui/src/components/goals/GoalsEventForm.vue`, `ui/src/components/goals/GoalsGoalForm.vue`,
  `ui/src/views/GoalsView.vue` (edit, Step 7), new test files
  (`ui/src/components/goals/__tests__/GoalsEventForm.spec.ts`,
  `ui/src/components/goals/__tests__/GoalsGoalForm.spec.ts`), and the extended
  `ui/src/stores/__tests__/goals.spec.ts` / `ui/src/views/__tests__/GoalsView.spec.ts`. No
  `package.json` change, no router change, no `AppSidebar.vue` change, no backend (`api/`) change, no
  touch to `ProfileGoalsSection.vue`/`ProfileEventCard.vue`/`ProfileGoalCard.vue`/`schemas/onboarding.ts`.
- Commit with the message from `Tasks-17-4.md`:

```
feat(ui): goal/event CRUD forms on the Goals page

On-page vee-validate + zod create/edit/delete for events and goals in
GoalsView, wrapping the existing Phase-8 POST/PUT/DELETE services (no
backend change). Reuses the exported eventItemSchema/goalItemSchema and
the Profile edit-card draft-vs-existing pattern; new GoalsEventForm /
GoalsGoalForm cards map field-level validation + 404 errors and flip a
justSaved flag. The goals store gains CRUD actions that re-fetch loadAll
after each write so lists reflect server truth (new rows pick up computed
status/daysRemaining/linkedPlans). No plan-link control (display-only in
Phase 17) and no quantitative goal fields (deferred). Round-trips without
touching onboarding. Vitest covers both forms + the view wiring.
```
