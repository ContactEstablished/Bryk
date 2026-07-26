# Task 19-5 — upload entry, import review flow, "from file" badge (+ the `api.ts` FormData fix)

## Surface
Frontend (Vue) only. One **verified blocker fix** in `ui/src/services/api.ts`, a new service + types +
Pinia store for activity files, three new components under `ui/src/components/import/`, an upload
affordance on `WorkoutsView`, a source badge on `WorkoutDetailView`, and Vitest coverage for all of it.
**No backend change, no new route, no new sidebar item, no new npm package.**

## Why
Phase 19's endpoints are useless until an athlete can drop a file on the page, and the drop is blocked
today by one line: `apiFetch` hardcodes `'Content-Type': 'application/json'` (`ui/src/services/api.ts:25`)
on **every** request. A multipart upload must not carry an explicit `Content-Type` — the browser has to
supply the `multipart/form-data; boundary=…` value itself, and an overriding header makes the server
unable to find the parts. So the first deliverable here is a two-line guard, with its own regression
test, because every future upload surface in this app depends on it. The rest is the review flow the
two-step API was designed for: the athlete sees what was parsed, sees the load, picks the planned
session it satisfies, and only then commits — with a discard path that leaves nothing behind. Workouts
already exists in the nav (`AppSidebar.vue:37`) and `/workouts` + `/workouts/:id` already exist
(`router/index.ts:48,53`), so this phase adds **no route and no nav item**.

## Depends on
- **Task 19-4** — the four endpoints and their exact shapes: `POST /activityfiles` (201
  `ActivityFileUploadResponse`), `POST /activityfiles/{id}/commit` (201 `ActivityFileCommitResponse`),
  `DELETE /activityfiles/{id}` (204), `GET /activityfiles/by-workout/{workoutId}` (**200 with a `null`
  body** when the workout has no source file — not a 404).
- **ADR-0010 §4** — the badge is a reverse lookup; there is no `sourceFileId` on the workout read, and
  `WorkoutResponse` is unchanged this phase.
- **Phase 13** — `WorkoutsView` / `WorkoutDetailView` and the existing `useTrainingStore` reads this
  task extends but does not restructure.

## Required reading
- `ui/src/services/api.ts` — all 46 lines. **Line 25 is the blocker.** `BASE_URL` is
  `import.meta.env.VITE_API_BASE_URL ?? '/api/v1'`; `ApiError { status, statusText, body }`; a 204
  returns `null`.
- `ui/src/services/training.ts` — the service style to mirror: one exported async function per endpoint,
  `apiFetch<T>` + an explicit `if (result === null) throw new Error('Unexpected empty response from …')`,
  and a comment naming the task that introduced it (see `updatePlan`, L50–64, and `deleteWorkout`, L161).
- `ui/src/services/__tests__/training.spec.ts:1–50` — the service-test harness:
  `vi.spyOn(globalThis, 'fetch')`, a local `jsonResponse()` helper, asserting `fetchSpy.mock.calls[0]`'s
  url/method/body.
- `ui/src/stores/training.ts:33–75` — the store shape to mirror: `defineStore('…', () => { … })` with
  `ref` state, a `loading*` flag and an `error*` ref per read, and re-throwing writes so the caller can
  map validation errors.
- `ui/src/views/WorkoutsView.vue` — the filter bar (L100–142) and the `card-surface` history list
  (L145–202) the upload entry sits above. There is **no upload affordance today**.
- `ui/src/views/WorkoutDetailView.vue:154–190` — the header row (`TypePill` + date + Edit/Delete) and
  the `MetricTile` strip; the badge goes next to the sport pill.
- `ui/src/components/analytics/TimeInZoneSection.vue:68–72` (the badge markup to mirror) and
  `:92–108` (the stacked-bar + legend markup to mirror). **Read only — Task 19-6 owns this file.**
- `ui/src/components/common/MetricTile.vue` — props `label`, `value?: string | number | null`, `unit?`,
  plus a `footer` slot. The review strip uses `label` + `value` + `unit` only.
- `ui/src/components/layout/AppSidebar.vue:37` — the existing Workouts nav item. Confirm for yourself
  that no new entry is needed.
- `ui/src/router/index.ts:48,53` — `/workouts` and `/workouts/:id` already exist.
- `ui/src/lib/format.ts` — `formatHm` (used by `TimeInZoneSection` for zone seconds); reuse it.

## Acceptance criteria

### 1. `ui/src/services/api.ts` (edit — the blocker fix, and nothing else)

Replace the unconditional header block (currently L24–27) with:
```ts
  // A multipart body must NOT carry an explicit Content-Type: the browser has to set
  // 'multipart/form-data; boundary=…' itself, and overriding it makes the server unable to
  // locate the parts. Only default the JSON header for non-FormData bodies.
  const headers: HeadersInit =
    init?.body instanceof FormData
      ? { ...init?.headers }
      : { 'Content-Type': 'application/json', ...init?.headers }
```
- Everything else in the file is untouched: `BASE_URL`, `ApiError`, the 204 → `null` branch, the
  `!response.ok` branch, the final `response.json()` cast.
- An explicitly-passed header still wins in both branches (spread order unchanged).

### 2. `ui/src/types/activityFiles.ts` (new)

Mirrors 19-4's DTOs. `Sport` values are the serialized enum names (`JsonStringEnumConverter`), so reuse
`PlannedSport` from `@/types/training` for the sport fields, and dates are `'YYYY-MM-DD'` strings.

```ts
export type ActivityFileFormat = 'Fit' | 'Tcx' | 'Gpx'

export interface ZoneHistogramEntry {
  zoneNumber: number
  seconds: number
}

export interface ParsedActivity {
  sport: PlannedSport
  completedDate: string
  startTimeUtc: string
  durationSeconds: number | null
  distanceMeters: number | null
  avgHr: number | null
  maxHr: number | null
  avgPower: number | null
  avgPace: number | null
  sampleCount: number
}

export interface MatchCandidate {
  plannedWorkoutId: string
  trainingPlanId: string
  title: string
  sport: PlannedSport
  scheduledDate: string
  plannedLoad: number | null
  dayOffset: number
}

export interface ActivityFileUploadResponse {
  id: string
  fileName: string
  format: ActivityFileFormat
  byteSize: number
  parsed: ParsedActivity
  computedLoad: number | null
  zoneSeconds: ZoneHistogramEntry[]
  matchCandidates: MatchCandidate[]
}

export interface ActivityFileCommitResponse {
  workoutId: string
  plannedWorkoutId: string | null
  computedLoad: number | null
}

export interface ActivityFileSource {
  id: string
  fileName: string
  format: ActivityFileFormat
  uploadedAt: string
}
```

### 3. `ui/src/services/activityFiles.ts` (new)

```ts
export const ACCEPTED_EXTENSIONS = '.fit,.tcx,.gpx'
export const MAX_UPLOAD_BYTES = 25 * 1024 * 1024

export async function uploadActivityFile(file: File): Promise<ActivityFileUploadResponse>
export async function commitActivityFile(id: string, plannedWorkoutId: string | null): Promise<ActivityFileCommitResponse>
export async function discardActivityFile(id: string): Promise<void>
export async function getWorkoutSource(workoutId: string): Promise<ActivityFileSource | null>
```
- `uploadActivityFile` builds `const form = new FormData(); form.append('file', file)` and calls
  `apiFetch<ActivityFileUploadResponse>('/activityfiles', { method: 'POST', body: form })`. **It must not
  set any header** — that is the whole point of the `api.ts` fix. The part name is `'file'`, matching
  the controller's `IFormFile? file` parameter.
- `commitActivityFile` POSTs `JSON.stringify({ plannedWorkoutId })` to `/activityfiles/${id}/commit`.
- `discardActivityFile` DELETEs; a 204 yields `null` from `apiFetch` and the function returns `void`.
- `getWorkoutSource` GETs `/activityfiles/by-workout/${workoutId}` and returns the result **or `null`**
  — a null body is the normal answer for a manually-logged workout and must **not** throw the
  "Unexpected empty response" error the other services use. Comment that divergence explicitly.
- `MAX_UPLOAD_BYTES` mirrors the server cap so the UI can refuse an oversized file before spending an
  upload; the server remains the authority.

### 4. `ui/src/stores/activityFiles.ts` (new — a new store, not an addition to `stores/training.ts`)

`export const useActivityFilesStore = defineStore('activityFiles', () => { … })` with:
- state: `preview: ref<ActivityFileUploadResponse | null>(null)`, `uploading: ref(false)`,
  `uploadError: ref<string | null>(null)`, `committing: ref(false)`,
  `commitError: ref<string | null>(null)`, `selectedPlannedWorkoutId: ref<string | null>(null)`,
  `source: ref<ActivityFileSource | null>(null)`.
- `async function upload(file: File)` — clears errors, sets `uploading`, calls the service, assigns
  `preview` and resets `selectedPlannedWorkoutId` to the **first** candidate's id when there is exactly
  one same-day candidate, otherwise `null`. On `ApiError`, map `body.errors?.[0]` (the middleware's
  `{status, error, errors[], traceId}` shape) into `uploadError`, falling back to
  `"Couldn't read that file."`. Does **not** re-throw — the drop zone renders the message.
- `async function commit(): Promise<string | null>` — commits `preview!.id` with
  `selectedPlannedWorkoutId`, returns the new `workoutId` on success (the caller routes to it), sets
  `commitError` and returns `null` on failure.
- `async function discard()` — DELETEs `preview!.id` and clears `preview`.
- `function reset()` — clears `preview`, both errors and the selection.
- `async function loadSource(workoutId: string)` — assigns `source` (may be `null`); swallows errors
  into `source = null` (a missing badge must never break the detail page).

### 5. `ui/src/components/import/ImportReviewCard.vue` (new)

`<script setup lang="ts">`, Composition API. Props: none (reads the store);
emits `{ committed: [workoutId: string]; cancelled: [] }`.
- Renders a `card-surface` panel with: the file name + format + size; a `MetricTile` strip
  (`Load`/TSS, `Duration`, `Distance`, `Avg HR`) built from `preview.parsed` + `preview.computedLoad`,
  formatted with the same `formatDuration`/`formatDistance` helpers `WorkoutsView.vue:82–93` uses
  (copy them locally — do **not** refactor the view's helpers into a shared module in this task);
  a `ZoneHistogramBars` preview; a `MatchCandidateList`; and Confirm / Discard buttons using the
  existing `@/components/ui/button` `Button` (`variant="outline"` / `variant="ghost"`).
- Confirm calls `store.commit()` and emits `committed` with the returned id; Discard calls
  `store.discard()` and emits `cancelled`. Both are disabled while `store.committing`.
- `store.commitError` renders in the destructive style already used at `WorkoutDetailView.vue:180–182`.

### 6. `ui/src/components/import/ZoneHistogramBars.vue` (new)

Props `{ zones: ZoneHistogramEntry[] }`. A stacked bar + legend that **mirrors**
`TimeInZoneSection.vue:92–108`: `flex h-5 w-full overflow-hidden rounded-md`, one `div` per non-zero
zone with `width: <pct>%` and `background: var(--chart-N)`, and a `font-mono text-[11px]` legend using
`formatHm`. Renders nothing when every bucket is 0.
**Accepted duplication, called out on purpose:** extracting a shared bar component would mean editing
`TimeInZoneSection.vue`, which Task 19-6 owns. Duplicate the ~15 lines here and record the duplication
in the phase handoff as tech debt. Do **not** touch `TimeInZoneSection.vue`.

### 7. `ui/src/components/import/MatchCandidateList.vue` (new)

Props `{ candidates: MatchCandidate[]; modelValue: string | null }`, emits
`{ 'update:modelValue': [value: string | null] }`.
- A **radio list** (`<input type="radio" name="match-candidate">` with a label per row) showing the
  title, `TypePill` sport (reuse `@/components/common/TypePill.vue` + `sportToPillKind`), the scheduled
  date, `plannedLoad` when present, and a day chip reading `Same day` / `−1 day` / `+1 day` from
  `dayOffset`.
- Always includes a final `No planned workout` option that emits `null` — an unplanned import is
  first-class and must be one click away.
- When `candidates` is empty, render only that option with the hint
  `No planned session within a day of this file.`

### 8. `ui/src/views/WorkoutsView.vue` (edit — additive)

- Above the filter bar, a dashed `card-surface border-dashed` drop zone with an `Import file` `Button`
  and a hidden `<input type="file" :accept="ACCEPTED_EXTENSIONS" class="sr-only">` (`ref` + click
  forwarding). Handle `@dragover.prevent`, `@dragleave.prevent` and `@drop.prevent`, taking
  `event.dataTransfer?.files?.[0]`; a highlight class while dragging.
- Client-side pre-checks before calling the store: extension must be one of `.fit`/`.tcx`/`.gpx`
  (case-insensitive) and `file.size <= MAX_UPLOAD_BYTES`; otherwise set the store's `uploadError`
  locally without an HTTP call. The server is still the authority — these only avoid a wasted upload.
- While `store.preview` is set, the drop zone is replaced by `<ImportReviewCard>`.
- `@committed` → `router.push('/workouts/' + workoutId)` (the view gains `useRouter`).
  `@cancelled` → `store.reset()` and re-run the existing `apply()` so the history list is fresh.
- `store.uploading` renders `Parsing…` in place of the button; `store.uploadError` renders in the
  destructive style.
- **Do not** change the existing filter bar, the history list, `apply()`, `selectSport()`,
  `clearFilters()`, or the local formatting helpers.

### 9. `ui/src/views/WorkoutDetailView.vue` (edit — additive)

- `onMounted`'s existing `load()` also calls `activityFilesStore.loadSource(id.value)`.
- When `source` is non-null, render next to the sport pill (header row at L156–160):
  ```html
  <span
    v-if="source"
    class="rounded border border-border px-1.5 py-px font-mono text-[9px] uppercase tracking-[0.08em] text-muted-foreground"
    :title="source.fileName"
  >
    from file
  </span>
  ```
  Markup mirrors the badge at `TimeInZoneSection.vue:68–72`; the file name is the tooltip so the header
  never wraps.
- **Do not** change the metric strip, the edit form wiring, the delete flow, or the planned-vs-actual
  table. **Do not** add a `sourceFileId` field to `WorkoutResponse` or `@/types/training` — the badge
  comes from the reverse-lookup endpoint (ADR-0010 §4).

### 10. No routing or nav change

`/workouts` and `/workouts/:id` already exist (`router/index.ts:48,53`) and the sidebar already has a
Workouts item (`AppSidebar.vue:37`). **Do not edit `ui/src/router/index.ts` or
`ui/src/components/layout/AppSidebar.vue`.**

## Non-goals
- **No backend change of any kind.** Nothing under `api/` may appear in `git diff` — no DTO tweak, no
  "one more field on `WorkoutResponse`", no controller edit. If the UI seems to need a shape 19-4 did
  not ship, **STOP and ask**.
- **No migration, no new npm package.** `FormData`, `File` and `DataTransfer` are browser built-ins and
  jsdom provides them. If a task step seems to need a package (a dropzone library, a file-type sniffer)
  — **STOP and ask** (Sr. Dev gate).
- **Do not add `sourceFileId` (or any import field) to `@/types/training`'s `WorkoutResponse`** — the
  badge is a reverse lookup. If you find yourself wanting a column on the workout, **STOP and ask**.
- **Do not edit `ui/src/components/analytics/TimeInZoneSection.vue`, `ui/src/types/analytics.ts`, or
  `ui/src/components/analytics/__tests__/TimeInZoneSection.spec.ts`** — Task 19-6 owns all three,
  including the `estimated`→`samples` badge change. Duplicating the bar markup is the intended answer.
- **Do not refactor `apiFetch`** beyond the FormData guard: no retry, no interceptor, no separate
  `uploadFetch` helper, no `AbortController` plumbing.
- **Do not restructure `stores/training.ts`** — this task adds a new store and reads the existing one.
- **Do not** add a route, a sidebar item, a modal/dialog system, a progress bar with real byte progress
  (`fetch` gives none — do not reach for XHR), multi-file/bulk upload, drag-to-reorder, or a re-parse
  affordance.
- Do not write files owned by siblings: anything under `api/` (19-1 … 19-4, 19-6),
  `ui/src/types/analytics.ts` and `ui/src/components/analytics/*` (19-6).
- **No auth code** — Phase 12 stays deferred and approval-gated.
- **Do not** revert, stash, or commit unrelated working-tree changes.

## Test expectations

**`ui/src/services/__tests__/api.spec.ts` (new file — the blocker's regression test).**
- `sets application/json for a plain object body` — `apiFetch('/x', { method: 'POST', body: '{}' })`;
  assert `fetchSpy.mock.calls[0][1].headers['Content-Type'] === 'application/json'`.
- `omits Content-Type entirely for a FormData body` — the same call with `body: new FormData()`;
  assert the resolved headers object has **no** `Content-Type` key (`expect('Content-Type' in headers).toBe(false)`).
  This is the test that keeps `api.ts:25` from regressing.
- `still honours an explicitly passed header alongside FormData`.
- `returns null for a 204` and `throws ApiError with the parsed body for a 400` — cheap guards that the
  edit did not disturb the rest of the function.

**`ui/src/services/__tests__/activityFiles.spec.ts` (new).** `vi.spyOn(globalThis, 'fetch')`, mirroring
`training.spec.ts`.
- `uploadActivityFile posts multipart to /activityfiles with the part named "file"` — assert the url is
  `/api/v1/activityfiles`, `init.method === 'POST'`, `init.body instanceof FormData`, and
  `(init.body as FormData).get('file')` is the `File` passed in.
- `commitActivityFile posts the plannedWorkoutId body to /activityfiles/{id}/commit` — assert
  `JSON.parse(String(init.body))` equals `{ plannedWorkoutId: 'pw1' }`, and a second case with `null`.
- `discardActivityFile deletes and resolves on 204`.
- `getWorkoutSource returns null for a null body` — a 200 whose body is the JSON literal `null` resolves
  to `null` and **does not throw**.

**`ui/src/stores/__tests__/activityFiles.spec.ts` (new).** `createPinia`/`setActivePinia` with the
service module mocked via `vi.mock('@/services/activityFiles')`.
- `upload stores the preview and preselects a single same-day candidate`.
- `upload with two candidates leaves the selection null`.
- `upload maps an ApiError's first errors[] entry into uploadError and does not throw`.
- `commit returns the new workoutId and passes the selected plannedWorkoutId`.
- `discard clears the preview`.
- `loadSource swallows an error and leaves source null`.

**`ui/src/components/import/__tests__/ImportReviewCard.spec.ts` (new).**
- `renders load, duration, distance and avg HR from the preview`.
- `Confirm emits committed with the workout id returned by the store`.
- `Discard emits cancelled`.
- `disables both buttons while committing`.
- `renders commitError when the store has one`.

**`ui/src/components/import/__tests__/MatchCandidateList.spec.ts` (new).**
- `renders one radio per candidate plus the "No planned workout" option`.
- `labels dayOffset as Same day / −1 day / +1 day`.
- `emits update:modelValue with the candidate id on select, and null for the no-match option`.
- `renders only the no-match option with a hint when candidates is empty`.

**`ui/src/views/__tests__/WorkoutsView.spec.ts` (extend — this task owns the file).**
- `renders the import drop zone when there is no preview`.
- `rejects an unsupported extension without calling upload`.
- `rejects a file over the size cap without calling upload`.
- `renders ImportReviewCard instead of the drop zone when a preview exists`.
- The existing specs (history rows, filters, load-more) must keep passing untouched.

**`ui/src/views/__tests__/WorkoutDetailView.spec.ts` (extend — this task owns the file).**
- `renders the "from file" badge when the workout has a source file`.
- `renders no badge for a manually logged workout` (store `source` is `null`).

## Verification commands
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```
Vitest must rise from the **252 / 56 files** baseline (this task adds roughly five files and ~25 specs),
with zero failures, and `pnpm run build` (`vue-tsc -b && vite build`) must stay green — the new types are
type-checked there. xUnit stays exactly where 19-1 … 19-4 left it (baseline **262** plus their additions)
because this task touches no backend file. Backend warnings must not exceed **16**.

## Review checklist
- [ ] `api.ts` sets `Content-Type: application/json` for plain bodies and **omits it entirely** for
      `FormData`, with a test that fails if line 25's behaviour returns.
- [ ] `uploadActivityFile` sets no headers at all and names the part `'file'`.
- [ ] `getWorkoutSource` returns `null` for a null body instead of throwing.
- [ ] Confirm navigates to `/workouts/{workoutId}`; Discard leaves no `ActivityFile` behind (the store
      calls DELETE) and refreshes the history list.
- [ ] The "from file" badge comes from `GET /activityfiles/by-workout/{id}`; `@/types/training`'s
      `WorkoutResponse` is unchanged in `git diff`.
- [ ] `git diff --stat` shows **nothing under `api/`**, no `router/index.ts`, no `AppSidebar.vue`, and
      no file under `ui/src/components/analytics/` or `ui/src/types/analytics.ts`.
- [ ] Every new SFC is `<script setup lang="ts">` with typed `defineProps`/`defineEmits`; every HTTP call
      goes through `src/services/`; state lives in Pinia.
- [ ] No new npm package in `ui/package.json`.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

## Suggested commit
```
feat(ui): activity-file upload, import review and "from file" badge

Unblock uploads first: apiFetch hardcoded Content-Type: application/json on
every request, which breaks multipart because the browser must supply the
boundary itself. Guard on body instanceof FormData and leave the JSON default
for everything else - with a regression test, since every future upload
surface depends on it.

The review flow follows the two-step API. WorkoutsView gains a drop zone and
an Import file button (no new route and no new sidebar entry - Workouts
already exists), which POSTs the file and renders the parsed preview: a metric
strip, the five-bucket zone histogram, and a radio list of the planned
sessions within a day of the file's date, same sport, still unlinked, with a
first-class "No planned workout" option. Confirm commits and routes to the new
workout; Discard deletes the stored file so an abandoned import leaves nothing
behind.

Workout detail gains a "from file" badge fed by the reverse lookup rather than
a field on the workout - there is no Workout.SourceFileId and none was added
(ADR-0010 4). The zone bars are duplicated rather than extracted from
TimeInZoneSection, which Task 19-6 is rewriting in the same phase; the
duplication is recorded as tech debt in the handoff.
```
