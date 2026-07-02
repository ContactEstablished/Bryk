# Task 17-4 — Goal/event CRUD forms on the Goals page

## Surface
Frontend only. On-page vee-validate + zod create/edit/delete forms for events and goals inside
`GoalsView` (17-3), wrapping the **existing** POST/PUT/DELETE endpoints and services. Reuses the
onboarding per-row zod schemas and the Profile edit-card patterns; wires the new `goals` store to
re-fetch after each write. **No backend change, no new endpoint, no new package.**

## Why
Closes the Goals page loop: 17-3 shipped read/display + nav with a stubbed add affordance; 17-4 makes
the page fully self-service so a CRUD round-trip completes **without touching onboarding** (a ROADMAP
success criterion). The write endpoints and services already exist (Phase 8) — this task is purely the
on-page form UX + store wiring, reusing the exported `eventItemSchema`/`goalItemSchema` and the
`ProfileEventCard`/`ProfileGoalCard` draft-vs-existing pattern so validation stays consistent with
onboarding and Profile.

## Depends on
- **Task 17-3** — `GoalsView.vue`, the new `goals` store, `services/goals-events.ts` (the read layer the
  writes re-fetch through), and the stubbed add mount points this task fills.
- **Phase 8 write surface** — `services/events.ts` (`createEvent`/`updateEvent`/`deleteEvent`),
  `services/goals.ts` (`createGoal`/`updateGoal`/`deleteGoal`), `EventDto`/`GoalDto`, and the backend
  `POST`/`PUT`/`DELETE` on `EventsController`/`GoalsController` — **reused as-is, no change**.
- **Profile edit precedent** — `ProfileEventCard.vue`/`ProfileGoalCard.vue` (draft vs existing,
  `useForm` + `toTypedSchema`, `extractApiValidationMessages`, delete-with-confirm-ish, `justSaved`).

## Required reading
- `ui/src/components/profile/ProfileGoalCard.vue` — **the form pattern to mirror**: `useForm` +
  `toTypedSchema(goalItemSchema)`, `isDraft` computed, `onSubmit` (`store.createGoal`/`updateGoal`),
  `onDelete`, `setError` via `extractApiValidationMessages`/`ApiError`, `justSaved`/`deleting` flags,
  `remove`/`created` emits.
- `ui/src/components/profile/ProfileEventCard.vue` — the event-form equivalent (sport/priority/distance
  fields, `Notes`, `eventItemSchema`). Mirror its field set.
- `ui/src/schemas/onboarding.ts` — the **exported per-row schemas to reuse verbatim**: `eventItemSchema`
  (name, eventDate, sport, triathlonDistance, customDistanceName, priority, notes) and `goalItemSchema`
  (description, targetDate). Do not redefine validation.
- `ui/src/stores/goals.ts` (from 17-3) — extend with CRUD actions that call the existing write services
  and then `loadAll()` (mirror `stores/profile.ts`'s `createEvent`/`updateEvent`/... → `loadGoals()`
  pattern).
- `ui/src/services/events.ts`, `ui/src/services/goals.ts` — the write functions to call (unchanged).
- `ui/src/services/apiErrors.ts` (`extractApiValidationMessages`) + `ui/src/services/api.ts` (`ApiError`)
  — error mapping.
- `ui/src/components/ui/form/*`, `ui/src/components/ui/input`, `ui/src/components/ui/button` — the
  shadcn-vue form primitives used by the Profile cards.

## Acceptance criteria

### Store CRUD actions (`ui/src/stores/goals.ts`, extend)
- Add: `createEvent(dto: EventDto)`, `updateEvent(id: string, dto: EventDto)`, `deleteEvent(id: string)`,
  `createGoal(dto: GoalDto)`, `updateGoal(id: string, dto: GoalDto)`, `deleteGoal(id: string)`.
- Each calls the corresponding **existing** service function (`services/events.ts` / `services/goals.ts`),
  then `await loadAll()` so the lists reflect server truth (new rows pick up their id + computed
  `daysRemaining`/`status`/`linkedPlans`). Re-throw on error so the form maps field-level messages
  (mirror `stores/profile.ts`).

### Event form card (`ui/src/components/goals/GoalsEventForm.vue`, new)
- `defineProps<{ event?: EventListItem | null }>()`; `defineEmits<{ remove: []; created: [] }>()`.
  `event` present = existing row (Save → `updateEvent`, Delete → `deleteEvent`); absent = draft (Save →
  `createEvent` + emit `created`; Remove → emit `remove`, discard locally).
- `useForm({ validationSchema: toTypedSchema(eventItemSchema), initialValues: ... })`. Fields: name,
  eventDate (`type="date"`), sport (select over `Sport`, nullable), triathlonDistance +
  customDistanceName (conditional on `Triathlon`, matching the Profile card / onboarding rules),
  priority (A/B/C select), notes (textarea). Submit builds an `EventDto` and calls the store; map
  validation/404 errors via `extractApiValidationMessages`/`ApiError` into a `globalError`; `justSaved`
  + `deleting` flags like the Profile card.
- **Read-only link note:** the form does **not** expose a plan link control (plan↔event write is deferred
  to Phase 18). `linkedPlans` is display-only, shown by the read card, not editable here.

### Goal form card (`ui/src/components/goals/GoalsGoalForm.vue`, new)
- `defineProps<{ goal?: GoalListItem | null }>()`; same draft/existing pattern. `useForm` +
  `toTypedSchema(goalItemSchema)`. Fields: description, targetDate (`type="date"`, nullable). Submit
  builds a `GoalDto` — set `type` consistently with the Profile card (which fixes `type: 'General'`
  because the editor doesn't surface `GoalType`; match that unless the Goals page deliberately adds a
  type selector — if it does, use the selected `GoalType`, else default `General`). Error mapping +
  `justSaved`/`deleting` as above.

### `GoalsView` wiring (fill the 17-3 mount points)
- Replace the stubbed "Add event" / "Add goal" affordances with the draft-card pattern from
  `ProfileGoalsSection.vue`: a `draftCounter` + `eventDrafts`/`goalDrafts` ref arrays; "Add Event" /
  "Add Goal" buttons push a draft; each existing item renders both the read-display card (17-3) **and**
  an edit affordance (e.g. an inline "Edit" toggle revealing `GoalsEventForm`/`GoalsGoalForm`, or render
  the form card directly — match whichever the Profile section does most cleanly). A draft is dropped on
  `created` (its real row arrives via `loadAll`) or `remove`.
- After any create/update/delete, the store's `loadAll()` refresh flows the new/updated/removed row (and
  any changed `linkedPlans`/`status`) back into the read cards.

### Tests
- `ui/src/components/goals/__tests__/GoalsEventForm.spec.ts` — draft submit calls `store.createEvent`
  with the mapped `EventDto` and emits `created`; existing-row submit calls `updateEvent`; delete calls
  `deleteEvent`; a 404 on save shows the "no longer exists" message; invalid input (empty name / past
  event date per `eventItemSchema`) blocks submit with a field message. Use `@pinia/testing`. (Valid
  array-submit specs over refined zod may need ~6 `flushPromises` — see the memory note.)
- `ui/src/components/goals/__tests__/GoalsGoalForm.spec.ts` — draft submit calls `store.createGoal`;
  update/delete paths; empty description blocked; `justSaved` appears after a successful save.
- `ui/src/views/__tests__/GoalsView.spec.ts` (extend 17-3's) — "Add Event"/"Add Goal" reveal a draft
  form; a successful create removes the draft.
- `pnpm run build` (vue-tsc) green; `pnpm test` green (`--no-file-parallelism` for a clean exit if the
  known transient worker crash appears with all tests passing).

## What NOT to modify
- **No backend change, no new endpoint, no new package.** This is UI + store wiring over the existing
  Phase-8 POST/PUT/DELETE and their services.
- **Do not** modify the onboarding schemas — reuse `eventItemSchema`/`goalItemSchema` as-is (do not fork
  them). If a Goals-specific rule is genuinely needed, compose/extend rather than editing the shared
  schema, and flag it.
- **Do not** touch the onboarding flow, `OnboardingController`, or `ProfileGoalsSection`/`ProfileEventCard`/
  `ProfileGoalCard` — the CRUD round-trip must complete **without touching onboarding** (success
  criterion). The Profile cards are the *reference*, not the edit target.
- **Do not** add a plan↔event link control — display-only in Phase 17 (write path is Phase 18).
- **Do not** add quantitative goal fields (target value/unit/current) — deferred; if the form seems to
  need them, **STOP and flag** rather than adding a column-less client field.
- **Do not** read athlete identity in any service — the API scopes to the current athlete.

## Suggested commit
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
