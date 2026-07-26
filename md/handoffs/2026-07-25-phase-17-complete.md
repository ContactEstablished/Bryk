# HANDOFF — Phase 17 complete (Goals & events surface)

**Date:** 2026-07-25
**Phase:** 17 — Goals & events surface (Goals page, ProgressRing, plan↔event links) (✅ COMPLETE)
**Decision:** **no new ADR.** Phase 17's two decisions were pre-recorded in the ROADMAP entry and held:
quantitative goal progress (`TargetValue`/`Unit`/`CurrentValue`) **deferred** (date-based only), and the
plan↔event link **display-only** until Phase 18's plan PUT.
**Specs:** `md/Tasks-17-1.md` … `md/Tasks-17-4.md` plus `md/Impl-17-1.md` … `md/Impl-17-4.md`.
**Executed on the DevAuth stub** — Phase 12 (auth) is still deferred/approval-gated. All athlete
resolution flows through `ICurrentUserService`; no athlete id is read from a request/query.

Phase 17 promotes events and goals to first-class read endpoints, ports the design export's
`ProgressRing`, and ships `/goals` as a self-service page: read-display cards with countdown rings,
inline `Notes`, A/B/C priority styling, linked-plan chips, and on-page CRUD over the existing Phase-8
writes. **No migration, no new packages, no backend change after 17-1.**

## What shipped

| Task | Area | Scope | Commit |
|---|---|---|---|
| specs | Docs | `Tasks-17-1..4` + `Impl-17-1..4` | `df47fe5` |
| 17-1 | Backend | `LinkedPlanDto`, `EventListItemResponse`, `GoalListItemResponse`, pure `GoalProgress.Compute` (+ `GoalStatus` enum), `GET /api/v1/events` (date-asc, `upcoming=true`, `Notes`, linked plans), `GET /api/v1/events/{id}`, `GET /api/v1/goals`; additive read-only `ITrainingPlanRepository.GetByEventIdsAsync` reverse `EventId` lookup; xUnit over ordering, the filter, linked/unlinked plans, 404s, and the DueSoon/Upcoming/Overdue boundaries | `c970b9b` |
| 17-2 | Frontend | `ProgressRing.vue` (hand-rolled SVG: 60 ticks, gradient, keyframe draw-in, `#center` slot) + pure `buildRingGeometry` in `lib/progressRing.ts` (clamps fraction, guards NaN); `PrimaryGoalCard` refactored to render its countdown through the ring; Vitest on both | `aa3ce12` |
| 17-3 | Frontend | `types/goals.ts`, `services/goals-events.ts` (GET-only read layer), `stores/goals.ts` (`loadAll` via `Promise.all` + `upcomingEvents`), `lib/dateFormat.ts`, `GoalsEventCard.vue` / `GoalsGoalCard.vue`, `GoalsView.vue` at `/goals`, lazy route, sidebar `Goals` live; 4 spec files | `4732120` |
| 17-4 | Frontend | `GoalsEventForm.vue` / `GoalsGoalForm.vue` (vee-validate + zod over the **unforked** `eventItemSchema`/`goalItemSchema`), six store CRUD actions re-fetching `loadAll()`, `GoalsView` draft-array + per-row Edit/Close wiring; 2 new spec files + 2 extended | `06548ea` |

## Verification state

- **Frontend:** `pnpm run build` (vue-tsc) green; `pnpm test` green — **53 test files, 229 tests**
  (was 45/169 at Phase 16 close). Run `pnpm exec vitest run --no-file-parallelism` for a clean exit
  (the known transient worker-fork quirk).
- **Backend:** `dotnet build api/Bryk.sln` green (only the known design-time
  `System.Security.Cryptography.Xml` advisory). `dotnet test api/Bryk.sln` green — **201 tests**
  (128 `Bryk.Application.Tests` + 73 `Bryk.API.Tests`; was 148 at Phase 16 close).
- **No migration.** No `package.json` change across all four tasks.

## Success criteria (ROADMAP Phase 17) — checked

Verified live in the browser against the dev seed (API on SQL Server + `db/dev-seed.sql`), not just by unit test:

- **`/goals` lists seeded data from the new GETs** — ✅ 3 events + 2 goals render; upcoming group
  (Indian Wells 70.3, priority A) above a muted "Past events" group (Gran Fondo, Parkrun).
- **CRUD round-trips without touching onboarding** — ✅ create → edit → delete round-tripped for both
  an event and a goal, entirely on `/goals`. The seed was left byte-identical afterward. `/profile`'s
  own Goals section and the onboarding flow are untouched by the diff.
- **Server-computed fields flow back after a write** — ✅ editing a goal to add a target date flipped
  its pill `NO DATE` → `DUE SOON` with "in 6 days", proving `loadAll()` re-hydrates
  `status`/`daysRemaining` rather than the client guessing.
- **Dashboard card renders identically via the shared component** — ✅ `PrimaryGoalCard` renders its
  countdown through `ProgressRing`; its spec asserts the ring, the week count, and the Today/Tomorrow
  centre swap.
- **ProgressRing animates with the correct elapsed fraction** — ⚠️ **partial, see carry-forward.**
  The ring draws correctly and clamps, but **both** surfaces use the rolling-horizon approximation
  (`1 − days/168`), not the true `[plan start, event date]` window the ROADMAP describes.
- **Linked events navigate to plan detail** — ✅ the chip on Indian Wells 70.3 navigates to
  `/plans/62b74190-…` and lands on "Indian Wells 70.3 Build".
- **Notes visible** — ✅ rendered inline on every event card.
- **Zero console errors** — ✅ across load, navigation, and all six CRUD operations.

## Decisions held (no ADR needed)

- **Quantitative goal progress deferred.** `GoalDto` has no `TargetValue`/`Unit`/`CurrentValue` and
  none were added — not even client-only. Goals are date-based; `GoalProgress.Compute` is pure and
  takes `today` as a parameter (the calculators-take-today convention).
- **Plan↔event link is display-only.** `LinkedPlanDto` is `{ Id, Name }`; no form, card, or service
  exposes a write path. Phase 18's plan PUT owns that.
- **`GoalStatus` thresholds:** `< 0` → Overdue, `0..14` → DueSoon, `> 14` → Upcoming, null target →
  NoDate. Pinned by `GoalProgressTests`.

## Known gaps / carry-forward

1. **The ring's true window is not wired.** `LinkedPlanDto` carries `Id` + `Name` only, so
   `GoalsEventCard` has no plan start date and falls back to the same rolling 168-day horizon as the
   dashboard. Closing this is small and additive: add `StartDate` to `LinkedPlanDto`, map it in
   `EventService`, then branch on it in the card. **Deliberately not done in 17-3** (it is a backend
   change, and 17-3 was scoped frontend-only). No dead-but-ready client field was left behind either
   — the type matches the shipped DTO exactly.
2. **Notes is a single-line `Input`, not a textarea,** on both the Goals and Profile forms — there is
   no Textarea primitive in `components/ui/`. The field accepts 2000 characters. Adding the primitive
   and using it on both surfaces is a clean standalone task.
3. **Past events cannot be saved through the form.** `eventItemSchema` requires "today or later", so
   opening a past event's editor and pressing Save is blocked on its own unchanged date. **Delete
   works.** This is pre-existing behaviour shared with `ProfileEventCard`; the schema was reused
   unforked per the task fence. Arguably a bug on both surfaces — needs a product call.
4. **No `GoalType` selector.** Every goal created from the UI is `General`, matching
   `ProfileGoalCard`. `EventDriven` goals exist in the seed and render correctly but cannot be
   authored.
5. **Mobile tab bar is now 8 items** (7 train + profile) — Goals joined Calendar. Flagged, not pruned;
   IA is the user's call.
6. **Dead template branch in `AppSidebar.vue`.** With Goals live there are no inert nav items left, so
   the `v-else` "soon" badge branch is now unreachable. Left in place (it is the generic affordance for
   a future inert item).
7. **Mild store duplication.** `stores/profile.ts` still composes its Goals section from
   `/profile/goals` while `stores/goals.ts` reads the first-class endpoints. Intentional and flagged in
   the 17-3 commit; the Profile page was explicitly out of scope. Worth revisiting if Profile's Goals
   section is ever reworked.
8. **ROADMAP doc drift (pre-existing):** the Phase 16 *heading* still reads `⏳` although its ledger row
   reads `✅`. Not corrected here — outside this phase's scope. One-character fix when convenient.
9. **Tap-to-move (mobile calendar) remains deferred** from Phase 16.

## Files added by Phase 17

| File | Purpose |
|---|---|
| `api/Bryk.Application/Events/LinkedPlanDto.cs` | `{ Id, Name }` — the chip's navigation target. |
| `api/Bryk.Application/Events/EventListItemResponse.cs` | `EventResponse` fields + `LinkedPlans`. |
| `api/Bryk.Application/Goals/GoalListItemResponse.cs` | `GoalResponse` fields + `DaysRemaining`/`Status`. |
| `api/Bryk.Application/Goals/GoalProgress.cs`, `GoalStatus.cs` | Pure date-based status; caller passes `today`. |
| `ui/src/lib/progressRing.ts`, `components/common/ProgressRing.vue` | Pure geometry + the SVG dial. |
| `ui/src/lib/dateFormat.ts` | UTC-stable `daysUntil` / `formatEventDate` for DateOnly strings. |
| `ui/src/types/goals.ts` | `LinkedPlan`, `EventListItem`, `GoalStatus`, `GoalListItem`. |
| `ui/src/services/goals-events.ts` | GET-only read layer (`getEvents`, `getEvent`, `getGoalsList`). |
| `ui/src/stores/goals.ts` | `loadAll` + `upcomingEvents` + six CRUD actions. |
| `ui/src/components/goals/GoalsEventCard.vue`, `GoalsGoalCard.vue` | Read-display cards. |
| `ui/src/components/goals/GoalsEventForm.vue`, `GoalsGoalForm.vue` | vee-validate + zod editors. |
| `ui/src/views/GoalsView.vue` | The page: sections, states, drafts, edit toggles. |

## Phase 17 closeout checklist

- [x] `GET /events` (+ `upcoming`, `Notes`, linked plans), `GET /events/{id}`, `GET /goals` (17-1).
- [x] `ProgressRing` ported + shared with `PrimaryGoalCard` (17-2).
- [x] `/goals` read/display surface + nav live (17-3).
- [x] On-page CRUD forms over the existing writes (17-4).
- [x] Vitest: 53 files, 229 tests. xUnit: 201 tests.
- [x] `pnpm run build` green. `dotnet build api/Bryk.sln` green.
- [x] Live CRUD round-trip smoke; seed restored; zero console errors.
- [x] Handoff doc written (`md/handoffs/2026-07-25-phase-17-complete.md`).
- [x] ROADMAP.md updated (Phase 17 → ✅ Complete; ledger + heading; status date).
- [x] CLAUDE.md phase pointer refreshed (it had drifted to "Phase 15 complete") + ADR-0008 indexed.

## Next — Phase 18 (ATP / periodization) or Phase 12 (Auth)

Phase 18 is the declared next feature phase and its stated dependencies are now all met: Phase 14
(load math), Phase 16's locked compliance bands, and Phase 17's live event surface. It needs an
**ADR on the ramp model** (baseline source, ramp cap ~5–8 %/week consistent with projected ACWR ≤ 1.3,
taper rule) written *before* any code task, plus `PUT /api/v1/trainingplans/{id}` — a verified gap.
That PUT is also what unblocks carry-forward item 1 (the plan↔event write path) and makes the ring's
true `[start, target]` window worth wiring.

**Phase 12 (Auth)** remains eligible and **approval-gated**: it needs an ADR evaluating ASP.NET Core
Identity vs hand-rolled, a table-layout decision, migration approval, OAuth wiring, and a
cookie-or-JWT decision. **All auth code requires approval before it is written.**

## Session-start checklist

1. Read this handoff + the ROADMAP Phase 18 entry (or Phase 12 if auth is next) + ADR-0008.
2. `git status` clean; `git log --oneline -5`.
3. Frontend: `pnpm run build` + `pnpm test` (expect **229**); use
   `pnpm exec vitest run --no-file-parallelism` for a clean exit.
4. Backend: `dotnet build api/Bryk.sln` + `dotnet test api/Bryk.sln` (expect **201**).
5. `dotnet user-secrets list` (from `api/Bryk.API/`) shows `ConnectionStrings:DefaultConnection` +
   `DevAuth:CurrentAthleteId`. Seed: `db/dev-seed.sql`.
6. Dev stack: API (`dotnet run` from `api/Bryk.API`, https://localhost:60129); `pnpm dev` from `ui/`
   (vite proxies `/api` → 60129).
