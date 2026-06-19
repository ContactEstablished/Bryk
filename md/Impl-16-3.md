# Impl 16-3 — Build order: calendar grid rendering (month view, chips, toggle)

**Executor:** GLM 5.2. **Acceptance contract:** `md/Tasks-16-3.md`. **Decision lock:** ADR-0008 §1, §3.
**Scope:** Frontend only. No new npm package, no drag/tap (16-4), no popover (16-5).

## Step 0 — Pre-flight

- `git status` clean (16-1 + 16-2 committed). `pnpm run build` (from `ui/`) green; `pnpm test` green.
- Re-read `md/Tasks-16-3.md` + ADR-0008 §1, §3. Open: `ui/src/services/analytics.ts`,
  `ui/src/services/training.ts`, `ui/src/services/api.ts` + `apiErrors.ts`, `ui/src/stores/analytics.ts`,
  `ui/src/views/ProgressView.vue` + `WorkoutsView.vue`, `ui/src/components/charts/PMCChart.vue` /
  `LoadChart.vue`, `ui/src/components/layout/AppSidebar.vue`, `ui/src/router/index.ts`,
  `ui/src/types/analytics.ts`, `ui/src/lib/charts/pmc.ts` (the pure-transform pattern).
- **Verify the 16-1 feed shape live** by booting the dev API + hitting `GET /api/v1/calendar` — confirm
  the enum serialization (`ComplianceBucket`/`CalendarItemKind` as strings or ints?) so the TS types
  match. The .NET `JsonStringEnumConverter` should serialize them as strings; if not, switch the TS
  types to number unions. Pin this before writing types.

## Step 1 — Types (`ui/src/types/calendar.ts`)

Mirror the 16-1 DTOs. Assuming string-enum serialization (confirm in Step 0):

```ts
export type ComplianceBucket = 'Grey' | 'Green' | 'Yellow' | 'Red'
export type CalendarItemKind = 'Planned' | 'Completed' | 'Event'

export interface CalendarItemDto {
  id: string
  kind: CalendarItemKind
  sport?: string  // the Sport enum name; mirror PlannedSport in types/training.ts
  title: string
  load?: number | null
  plannedLoad?: number | null
  compliance?: ComplianceBucket | null
  isUnplanned: boolean
  plannedWorkoutId?: string | null
  workoutId?: string | null
  priority?: string | null  // EventPriority enum name; only on events
  notes?: string | null     // events only
  trainingPlanId?: string | null  // on planned items — needed by 16-4 for the PATCH route
}

export interface CalendarDayDto {
  date: string  // 'YYYY-MM-DD'
  items: CalendarItemDto[]
}

export interface CalendarFeedResponse {
  rangeStart: string
  rangeEnd: string
  days: CalendarDayDto[]
}
```

**Note for 16-1:** if the `CalendarItemDto` on the backend doesn't currently carry `TrainingPlanId`
for planned items, **amend 16-1** to add it before 16-3 ships — 16-4's PATCH route needs
`/trainingplans/{planId}/plannedworkouts/{pwId}/schedule`. Flag this as a 16-1 amendment in the commit
message if you have to go back and add it.

## Step 2 — Service (`ui/src/services/calendar.ts`)

```ts
import { apiFetch } from '@/services/api'
import type { CalendarFeedResponse } from '@/types/calendar'

export async function getCalendarFeed(from?: string, to?: string): Promise<CalendarFeedResponse> {
  const qs = new URLSearchParams()
  if (from) qs.set('from', from)
  if (to) qs.set('to', to)
  const query = qs.toString()
  const result = await apiFetch<CalendarFeedResponse>(`/calendar${query ? `?${query}` : ''}`)
  if (result === null) throw new Error('Unexpected empty response from /calendar')
  return result
}

// Declared here so the service module is complete; 16-4 wires the interaction to call it.
export async function reschedulePlannedWorkout(
  planId: string,
  plannedWorkoutId: string,
  scheduledDate: string,
): Promise<void> {
  await apiFetch<void>(
    `/trainingplans/${planId}/plannedworkouts/${plannedWorkoutId}/schedule`,
    { method: 'PATCH', body: JSON.stringify({ scheduledDate }) },
  )
}
```

## Step 3 — Pure transforms (`ui/src/lib/calendar.ts`)

```ts
import type { CalendarDayDto, CalendarFeedResponse, CalendarItemDto, ComplianceBucket } from '@/types/calendar'

export interface CalendarDayCell {
  date: string
  items: CalendarItemDto[]
  isInMonth: boolean
  isToday: boolean
}

export function groupItemsByDay(feed: CalendarFeedResponse): Map<string, CalendarDayDto> {
  const map = new Map<string, CalendarDayDto>()
  for (const day of feed.days) map.set(day.date, day)
  return map
}

export function buildMonthMatrix(
  days: CalendarDayDto[],
  anchorMonth: { year: number; month: number },  // month: 1-12
  today: string,
): CalendarDayCell[][] {
  const byDate = new Map(days.map((d) => [d.date, d]))
  const firstOfMonth = new Date(anchorMonth.year, anchorMonth.month - 1, 1)
  // Monday-anchored: ((int)DayOfWeek + 6) % 7, mirroring ThisWeekService.
  const leadingBlanks = (firstOfMonth.getDay() + 6) % 7
  const gridStart = new Date(firstOfMonth)
  gridStart.setDate(gridStart.getDate() - leadingBlanks)

  const cells: CalendarDayCell[][] = []
  let cursor = new Date(gridStart)
  for (let week = 0; week < 6; week++) {
    const row: CalendarDayCell[] = []
    for (let dow = 0; dow < 7; dow++) {
      const dateStr = isoDate(cursor)
      const dayDto = byDate.get(dateStr)
      row.push({
        date: dateStr,
        items: dayDto?.items ?? [],
        isInMonth: cursor.getMonth() + 1 === anchorMonth.month && cursor.getFullYear() === anchorMonth.year,
        isToday: dateStr === today,
      })
      cursor = new Date(cursor.getFullYear(), cursor.getMonth(), cursor.getDate() + 1)
    }
    cells.push(row)
    // Stop after the week that contains the last day of the anchor month (5-week months).
    if (week === 4 && cells.every((r) => r.every((c) => !c.isInMonth || new Date(c.date).getMonth() + 1 !== anchorMonth.month))) {
      break
    }
  }
  return cells
}

export function isoDate(d: Date): string {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

export function complianceColor(bucket: ComplianceBucket | null | undefined): { dot: string; chip?: string } {
  switch (bucket) {
    case 'Green': return { dot: 'bg-emerald-500' }
    case 'Yellow': return { dot: 'bg-amber-400' }
    case 'Red': return { dot: 'bg-rose-500' }
    case 'Grey': return { dot: 'bg-slate-400' }
    default: return { dot: '' }  // events / null — no dot
  }
}

export function sportColor(sport?: string | null): string {
  switch (sport) {
    case 'Bike': return 'bg-sky-500'
    case 'Run': return 'bg-emerald-500'
    case 'Swim': return 'bg-teal-500'
    case 'Strength': return 'bg-orange-500'
    default: return 'bg-slate-400'
  }
}
```

**Verify:** `pnpm run build` green (type-checks).

## Step 4 — Transform tests (`ui/src/lib/calendar.spec.ts`)

Mirror `lib/charts/pmc.spec.ts` (or `load.spec.ts`) — Vitest + `@vue/test-utils` if needed, but pure
functions need no component harness.

Tests:
- `isoDate`: known date → `'2026-06-19'`.
- `groupItemsByDay`: feed with 3 days → 3-entry map with right keys.
- `buildMonthMatrix` for June 2026 (starts Monday): leadingBlanks=0, first cell is `2026-06-01`,
  `isInMonth=true`; cells outside June carry `isInMonth=false` + empty items; today's cell `isToday=true`;
  matrix has 5 weeks (June 2026 spans 5 weeks Monday-anchored). Pin the exact `date` of cell [0][0]
  and [4][6].
- `complianceColor`: each bucket → expected Tailwind class; `null`/`undefined` → empty dot.
- `sportColor`: Bike/Run/Swim/Strength/undefined → distinct classes; pin exact strings.

**Verify:** `pnpm test` green.

## Step 5 — Store (`ui/src/stores/calendar.ts`)

Mirror `stores/analytics.ts`'s setup-store style:
```ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ApiError } from '@/services/api'
import { getCalendarFeed } from '@/services/calendar'
import type { CalendarFeedResponse } from '@/types/calendar'

export const useCalendarStore = defineStore('calendar', () => {
  const feed = ref<CalendarFeedResponse | null>(null)
  const loading = ref(false)
  const error = ref<ApiError | Error | null>(null)

  async function loadFeed(from?: string, to?: string) {
    loading.value = true
    error.value = null
    try {
      feed.value = await getCalendarFeed(from, to)
    } catch (e) {
      error.value = e as ApiError | Error
    } finally {
      loading.value = false
    }
  }

  return { feed, loading, error, loadFeed }
})
```

## Step 6 — Components

Build in this order (each compiles before the next):

### `MonthWeekToggle.vue`
Props: `modelValue: 'month' | 'week'`. Emits `update:modelValue`. Two pill buttons; active =
`bg-primary-hi text-primary-foreground`. Match `ChartRangeToggle.vue`'s styling if it exists (grep).

### `CalendarItemChip.vue`
Props: `item: CalendarItemDto`. Pure presentational. Layout: sport-color square (use `sportColor`)
+ truncated title + load number (when present) + compliance dot (use `complianceColor`, right-aligned).
Events: dark-slate pill + priority badge (A/B/C from `EventPriority`), no dot. Unplanned: small
"unplanned" tag. Hover: subtle shadow + `cursor-grab` (the grab cursor is for 16-4; harmless here).
Use Tailwind tokens (`text-faint`, `text-subtle`, `bg-muted`, `border`) consistent with `WorkoutsView`'s chips.

### `CalendarDayCell.vue`
Props: `cell: CalendarDayCell` (the matrix entry), `today: string`. Date number top-left; today =
filled accent circle (`bg-primary-hi text-primary-foreground rounded-full size-6` etc.).
Out-of-month: `text-faint`, no items. In-month: render up to 3 `CalendarItemChip`s + a
"+N more" label when `items.length > 3` (static label for 16-3; 16-5 wires the popover).

### `CalendarGrid.vue`
Props: `days: CalendarDayDto[]`, `anchorMonth: { year: number; month: number }`.
- Computes `today = isoDate(new Date())`.
- Calls `buildMonthMatrix(props.days, props.anchorMonth, today)`.
- Renders a 7-column CSS grid (`grid grid-cols-7`); Mon–Sun header row (muted uppercase `text-faint`).
- One `CalendarDayCell` per matrix cell.

### `CalendarView.vue` (the route component)
- `onMounted`: read `route.query.from`/`to`, call `calendarStore.loadFeed(from, to)`.
- Header: "Calendar" h1 + `MonthWeekToggle` + chevrons (left/right buttons) + period label
  (`new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(anchorDate)`).
- Chevrons: advance/retreat `anchorMonth` by one month (state: `anchorMonth: { year; month }`).
  On change, recompute the feed window to cover the visible month (`from = isoDate(new Date(year, month-1, 1))`,
  `to = isoDate(new Date(year, month, 0))`) and call `loadFeed`. **Cap check:** if the month span
  exceeds 62 days (impossible for one month — max 31 — but the chevron could land on a 2-month window
  if you're not careful; pin to the single anchor month).
- Content: loading skeleton (6×7 muted rectangles), empty state (fresh athlete: "No training
  scheduled yet. Create a plan to get started." linking to `/plans`), error state (banner), or
  `CalendarGrid`.
- Week view: `MonthWeekToggle` wired; when `week`, render a placeholder div "Week view coming next
  (Task 16-5)." (16-5 replaces this with `WeekStrip`).

## Step 7 — Route

**Edit** `ui/src/router/index.ts` — add (alphabetical-ish with the other routes):
```ts
{
  path: '/calendar',
  name: 'calendar',
  component: () => import('@/views/CalendarView.vue'),
},
```
**Do not** touch `AppSidebar.vue` — that's 16-5.

## Step 8 — Final verification + commit

- `pnpm run build` (vue-tsc) green.
- `pnpm test` green (transform tests + existing).
- Manual: navigate to `/calendar` by URL (sidebar not wired yet) — renders the current month with
  seeded data; chevrons advance the month; toggle switches to the week placeholder; loading/empty
  states render. No console errors.
- `git diff --stat` — only `ui/src/types/calendar.ts`, `ui/src/services/calendar.ts`,
  `ui/src/lib/calendar.ts` + spec, `ui/src/stores/calendar.ts`, `ui/src/components/calendar/*`,
  `ui/src/views/CalendarView.vue`, `ui/src/router/index.ts`. No sidebar changes, no package.json
  changes.
- Commit with the message in `Tasks-16-3.md`.
