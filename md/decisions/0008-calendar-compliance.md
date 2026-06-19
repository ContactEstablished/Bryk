# ADR-0008 — Calendar compliance bands + reschedule policy

**Date:** 2026-06-19
**Status:** Accepted (2026-06-19) — 5-bucket compliance classifier with a single null-load fallback rule; out-of-window reschedule rejected with 400; new `Calendar` sidebar item.

## Context

Phase 16 ("Calendar & scheduling") turns the daily-use loop (plan → see week → do → log) into one
surface: a month/week grid merging planned workouts, completed workouts, and events, with drag (desktop)
/ tap-to-move (mobile) reschedule and compliance coloring on past days. The ROADMAP Phase 16 entry
flags three *Decisions needed* under the "When to slow down" rule, and two of them are cross-phase —
Phase 18 (ATP) reuses the compliance bands to drive its target-vs-actual feedback and inherits the
reschedule-window contract. Locking them in a mini-ADR before any task code is the same role ADR-0007
played for Phase 15.

This ADR resolves:

1. **Compliance classifier thresholds + null-load fallback** — the cross-phase one (Phase 18 reuses it).
2. **Reschedule policy** — reject vs warn when the requested `scheduledDate` falls outside the plan window.
3. **Sidebar IA** — new `Calendar` item vs folding into `Training`.

### Conventions this ADR follows

Grounded in `TrainingPlan`, `PlannedWorkout`, `Workout`, `Event`, `ThisWeekService`,
`TrainingPlanService`, `ITrainingPlanRepository`, `IWorkoutRepository`:

- **"Today"** is `DateOnly.FromDateTime(DateTime.UtcNow)`, the same source `ThisWeekService`,
  `EventDtoValidator`, and the analytics range validator use. No `IClock` abstraction.
- **ISO weeks are Monday-based**, exactly as `ThisWeekService.CurrentWeek` computes them
  (`((int)DayOfWeek + 6) % 7`).
- **EffectiveLoad** is unchanged from ADR-0005/0006/0007: `LoadOverride ?? ComputedLoad` for a completed
  `Workout`, `PlannedLoad ?? ComputedLoad` for a `PlannedWorkout` (computed via
  `LoadCalculator.ComputePlannedLoad`).
- **No migration, no new package** — every field the feed and the PATCH need already exists
  (`PlannedWorkout.ScheduledDate`, `Workout.CompletedDate`, `Event.EventDate` are all `DateOnly`;
  `PlannedWorkout.PlannedLoad`, `PlannedWorkout.PlannedDurationMinutes`,
  `Workout.ActualDurationSeconds` exist for the compliance ratio).
- Athlete identity always via `ICurrentUserService` — never from query/body (Phase 12 still deferred).
- **Honesty rule (normative).** A planned workout with no completion is `red` (missed) for a past day —
  never silently dropped. A completed workout with no matching planned is `unplanned` — never disguised
  as on-target. The compliance dot is absent only for future days (`grey`).

## Decision

### 1. Compliance classifier — 5 buckets, single null-load fallback (locked — Phase 18 reuses this)

The `GET /api/v1/calendar?from=&to=` feed classifies **each planned workout** into one of five
buckets. The classifier is a pure function living in `Bryk.Application/Calendar/ComplianceClassifier.cs`
(mirroring `PmcCalculator`/`WeeklyLoadCalculator`); the service does the I/O and delegates.

**Inputs** (per planned workout, computed by the service):
- `ScheduledDate : DateOnly` — the planned day.
- `PlannedLoad : decimal?` — the planned workout's `EffectiveLoad` (`PlannedLoad ?? ComputedLoad`).
- `PlannedDurationSeconds : int?` — `PlannedDurationMinutes × 60` when set, else null.
- `MatchingCompleted : (DateOnly CompletedDate, decimal? EffectiveLoad, int? ActualDurationSeconds)?`
  — the **single** completed `Workout` linked to this planned workout via `Workout.PlannedWorkoutId`,
  if any. Unlinked completions do **not** match a planned workout (see `unplanned` below).
- `Today : DateOnly` — passed in (calculators never call `DateTime.UtcNow`).

**Buckets:**

| Bucket | When | Color |
|---|---|---|
| `grey` | `ScheduledDate > Today` (future) — no completion possible yet | gray |
| `green` | past or today, completion exists, ratio ∈ `[0.8, 1.2]` | green |
| `yellow` | past or today, completion exists, ratio ∈ `[0.5, 0.8) ∪ (1.2, ∞)` | yellow |
| `red` | past (`ScheduledDate < Today`), no completion | red (missed) |
| `unplanned` | **tag**, not a bucket — applied to a completed `Workout` whose `PlannedWorkoutId` is null, rendered on its `CompletedDate` | (uses `green` when on a past/today day — done is a win) |

**Ratio computation (the null-load fallback — single rule, locked):**

```
ratio = PlannedLoad is not null
        ? EffectiveLoad(planned) == 0 ? 1.0            // planned 0 + completed anything → green (degenerate, don't div-by-zero)
                                       : EffectiveLoad(completed) / EffectiveLoad(planned)
        : PlannedDurationSeconds is not null
          ? ActualDurationSeconds is null ? 0.0         // planned duration, no actual duration → red
                                            : ActualDurationSeconds / PlannedDurationSeconds
          : 1.0                                          // neither planned load nor duration → completion is green
```

- `EffectiveLoad(planned)` = `PlannedLoad ?? ComputedLoad` (the service resolves `ComputedLoad` via
  `LoadCalculator.ComputePlannedLoad` exactly as `ThisWeekService` does).
- `EffectiveLoad(completed)` = `LoadOverride ?? ComputedLoad ?? 0`.
- The fallback is **one rule with two layers**: planned-load ratio first, then planned-duration ratio,
  then "completion = green." No third "completed=green" branch separate from the duration fallback —
  it's the tail of the same chain. This kills the roadmap's "duration ratio else completed=green"
  ambiguity by making it a single deterministic walk.
- Today's planned workout with no completion is **not** `red` — it's `grey` (the day isn't over). The
  `red` rule is strictly `ScheduledDate < Today` (i.e. before today, UTC midnight). This matches the
  "future = grey" framing: today is the last day that can still be `grey`-with-no-completion.
- `unplanned` completions render on their `CompletedDate` cell with the `unplanned` tag and a `green`
  dot (a done-but-unplanned session is a win, not a yellow). They never appear on a planned workout's row.

**Thresholds locked (Phase 18 reuses verbatim):** green `[0.8, 1.2]`, yellow `[0.5, 0.8) ∪ (1.2, ∞)`,
red `[0, 0.5)` or no completion (past), grey future, `unplanned` tag for completed-without-planned.

### 2. Reschedule policy — reject out-of-window (400)

`PATCH /api/v1/trainingplans/{planId}/plannedworkouts/{plannedWorkoutId}/schedule` takes a body
`{ scheduledDate: DateOnly }` only. The validator rejects (400 via `ValidateOrThrowAsync`) any
`scheduledDate` outside `[TrainingPlan.StartDate, TrainingPlan.EndDate]` inclusive.

- The plan window is authoritative — Phase 18's ramp targets are computed against it; drifting workouts
  past the plan end silently breaks the periodization model.
- Athletes who want to push a workout past the plan end must edit the plan itself (Phase 18's
  `PUT /trainingplans/{id}` owns plan-metadata edits including dates).
- 404 (not 400) when the plan or planned workout is missing or foreign — matches the existing
  `TrainingPlanService` ownership pattern (`KeyNotFoundException` → 404).
- The PATCH stages a fresh nav-free `PlannedWorkout` entity (only `Id`, `AthleteId`,
  `TrainingPlanId`, `ScheduledDate`, and `CreatedAt` carried over) via
  `planRepo.UpdatePlannedWorkout`, exactly mirroring the staging discipline in
  `TrainingPlanService.UpdatePlannedWorkoutAsync` — the loaded plan comes from a no-tracking `Include`
  and must not be re-attached.
- Response: `204 NoContent` on success (the calendar feed re-fetches; no body needed).

### 3. Sidebar IA — new `Calendar` item at `/calendar`

- New sidebar entry in `AppSidebar.vue`'s `trainItems`, between `Training` and `Workouts`:
  `{ icon: CalendarDays, label: 'Calendar', to: '/calendar', routeName: 'calendar' }`.
  (Uses `CalendarDays` from `lucide-vue-next`; the existing `CalendarRange` icon stays on `Training`.)
- `Training` keeps authoring (plan create/structure edit); `Calendar` is the daily-use see + move view.
- Mobile tab bar picks it up automatically (it filters `trainItems` to navigable items).
- Router: lazy-loaded `CalendarView.vue` at `/calendar`, name `calendar`.

## Consequences

**Closed by this decision:** the ROADMAP Phase 16 *Decisions needed* — compliance thresholds + null-load
fallback, reject-vs-warn on out-of-window reschedule, sidebar IA.

**Created by this decision (no migration, no new package):**

- `Bryk.Application/Calendar/`: pure `ComplianceClassifier` (the 5-bucket + ratio rule), the
  `CalendarFeedResponse`/`CalendarDayDto`/`CalendarItemDto` (+ a `CalendarItemKind` enum:
  `Planned`/`Completed`/`Event`, and a `ComplianceBucket` enum:
  `Grey`/`Green`/`Yellow`/`Red` + an `IsUnplanned` flag on completed items) shapes;
  `CalendarFeedRequest` (+ validator) and `ScheduleRequest` (+ validator).
- `ICalendarService`/`CalendarService` — owns the merged feed read + the reschedule PATCH; resolves
  athlete via `ICurrentUserService`; commits via `IUnitOfWork`.
- `CalendarController` (new, additive) at `api/v{version}/calendar`: `GET ?from=&to=`,
  and the schedule PATCH lives on `TrainingPlansController` (it's a planned-workout mutation —
  keeps the plan aggregate boundary clean: `PATCH /trainingplans/{id}/plannedworkouts/{pwId}/schedule`).
- New repo reads (additive, no migration): `IWorkoutRepository.GetByAthleteInRangeWithPlannedAsync`
  (the range read **with** `PlannedWorkoutId` resolved for matching) and
  `IEventRepository.GetByAthleteInRangeAsync` (range-filtered events). The existing
  `ITrainingPlanRepository.GetPlannedWorkoutsInRangeWithStructureAsync` covers the planned side
  (structure needed for `ComputedLoad`).
- UI: `types/calendar.ts`, `services/calendar.ts`, a calendar-store slice, `CalendarView.vue`,
  `components/calendar/` (`CalendarGrid`, `CalendarDayCell`, `CalendarItemChip`, `DayDetailPopover`,
  `ComplianceLegend`, `WeekStrip`), pointer-event drag (desktop) + tap-select/tap-target (mobile),
  the `/calendar` route, Calendar nav live. **No drag-and-drop library.**

**Phase 18 depends on this** — the compliance bands (`[0.8, 1.2]` green, `[0.5, 0.8) ∪ (1.2, ∞)`
yellow, `< 0.5` red) are the target-vs-actual feedback rule the ATP `ThisWeekCard` reuses, and the
plan-window-is-authoritative contract is what makes the ramp targets meaningful.

### For Tasks 16-1 … 16-5

| Task | Surface | Depends on | Pins from this ADR |
|---|---|---|---|
| **16-1** `ComplianceClassifier` + `CalendarService` + `GET /calendar` feed + xUnit | Backend | ADR-0008 | Decisions 1 (buckets + ratio rule). |
| **16-2** `ScheduleRequest` + validator + `PATCH .../schedule` on `TrainingPlansController` + `TrainingPlanService.RescheduleAsync` + xUnit | Backend | ADR-0008 | Decision 2 (reject out-of-window, 404 ownership, 204). |
| **16-3** `CalendarGrid` + `CalendarDayCell` + `CalendarItemChip` + month/week toggle + Vitest | Frontend | 16-1 | Decision 1 (chip + dot rendering). |
| **16-4** Pointer-event drag (desktop) + tap-select/tap-target (mobile) calling the PATCH + Vitest | Frontend | 16-2, 16-3 | Decision 2 (out-of-window → visible message). |
| **16-5** `DayDetailPopover` + `ComplianceLegend` + `/calendar` route + nav live + assembly | Frontend | 16-3, 16-4 | Decision 3 (sidebar IA). |

## Alternatives considered

- **6th `purple`/overcooked bucket above 120%.** Rejected (decision 1) — lumps 121% and 200% under
  `yellow` is deliberate for v1; a 6th bucket adds a legend entry, another rule Phase 18 must mirror,
  and more classifier tests. The `yellow` upper tail is honest enough; a finer split can return with
  Phase 18's target feedback if needed.
- **Warn-but-allow reschedule (200 + `warnings` field).** Rejected (decision 2) — requires a warnings
  channel on a 204 endpoint (response-shape change) and silently breaks Phase 18's ramp targets when
  workouts drift past the plan window. Reject keeps plan dates authoritative and is a plain validator rule.
- **Fold calendar under `Training` in the sidebar.** Rejected (decision 3) — buries the daily-use view
  behind an authoring surface; bad IA for a training app. Calendar is the spine of the daily loop and
  earns its own slot, mirroring how Workouts and Progress got theirs.
- **Two-step null-load fallback ("duration ratio else completed=green") as separate branches.** Rejected
  (decision 1) — the roadmap worded it as two steps but the outcome is identical to a single chain;
  pinning one deterministic walk is easier to test and reason about.
- **Today-no-completion = red.** Rejected (decision 1) — the day isn't over; today stays `grey` until
  UTC midnight rolls past. `red` is strictly `ScheduledDate < Today`.
- **Drag-and-drop library (vuedraggable / sortablejs).** Rejected — ROADMAP explicitly says
  "hand-rolled pointer-event drag (desktop) + tap-to-move (mobile). No drag-and-drop library." Pointer
  events are well-supported and the calendar's drag is constrained (same-grid, day-cell snap), so the
  library overhead isn't justified.
