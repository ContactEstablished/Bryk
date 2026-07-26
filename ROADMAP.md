# ROADMAP — Bryk

**Status as of 2026-07-26.** Source of truth for phased Bryk development. Read alongside `CLAUDE.md` (workflow, conventions, pending decisions, tech debt), `md/decisions/` (architectural decision records), and `md/product/feature-parity-trainingpeaks.md` (parity wishlist with status tags). Phase plans below win on scope; the parity doc is the candidate inventory.

**Phase 7 reshape note.** This roadmap reflects a renumbering decided 2026-05-26 after ADR-0001 (supersede Mesocycle) and ADR-0002 (coaches are v2). Old Phase 7 (TrainingPlan domain) becomes new Phase 9. Two new phases — 7 (closeout) and 8 (profile + dashboard warmups) — are inserted. Downstream numbers shift by +2. Per-phase entries below reflect the new numbering; ADR documents capture the decisions that drove the reshape.

**2026-06-11 reshape note.** Phases 8–11 shipped (see `md/handoffs/`); the ledger is updated to match. Authentication moves 14 → **12**, matching CLAUDE.md's phase pointer (regenerated 2026-06-07). The old rows 12/13/15/16/17 are superseded and their scope absorbed into the new Phases 13–21: old 12 Calendar → new 16; old 13 PMC → new 14 (engine) + 15 (Progress page); old 15 ATP → new 18; old 16 docs/security + old 17 cutover → new 21; old 17's read-only file-import seam → new 19. New phases 13–21 below were scoped 2026-06-11 against the post-Phase-11 codebase and the UI redesign that landed the same week.

This roadmap is intentionally verbose. Each phase entry exists to seed Cursor prompts — the success criteria, dependencies, and task groups should compose directly into Pattern A prompts without the architect re-deriving context.

---

## Working principles (carry into every phase)

Non-negotiable per phase. They constrain how prompts get written and how diffs get reviewed. Restated here so a single read of `ROADMAP.md` is enough to start work.

- **Simplicity first.** Minimum code that solves the named problem. No speculative abstractions, no "while we're here" cleanups bundled in.
- **Surgical changes.** Each prompt names exactly what to modify and explicitly states what NOT to modify. Adjacent code, comments, formatting are off-limits unless the prompt names them.
- **Goal-driven execution.** Every prompt carries a verifiable success criterion. With Phase 6 test infrastructure landed, *done* means: build is green, tests pass, manual smoke test for the affected endpoint passes, diff reads cleanly.
- **One logical change per commit.** Conventional prefixes (`feat:`, `refactor:`, `docs:`, `fix:`, `chore:`). Architect reads the diff, proposes the message; user commits and pastes the hash.
- **Claude Code is the architect and implementer.** Per `CLAUDE.md` (regenerated 2026-06-07), the architect designs the work, writes the code, and validates it directly. Phase work is seeded by per-phase task specs at `md/Tasks-N-n.md`; external executor sessions (e.g., Opus-driven) follow the same specs and conventions.
- **Verify what you read.** Before a prompt is written, the relevant files are read, `git status` checked, build verified green. Repo-state claims that turn out wrong are expensive — they generate prompts that make wrong assumptions.
- **Sr. Dev approval gates** as listed in `CLAUDE.md`: migrations, new packages (first-party `Microsoft.Extensions.*` exempt), API breaking changes, cross-cutting concerns (auth, middleware, versioning, transactions), persistence boundary changes, Dapper switches, deviations from convention.

---

## Phase ledger at a glance

| #  | Phase                                                                            | Status            |
|----|----------------------------------------------------------------------------------|-------------------|
| 1  | Solution scaffold & .NET 10 Clean Architecture                                   | ✅ Complete       |
| 2  | Domain model & EF Core persistence                                               | ✅ Complete       |
| 3  | Cross-cutting plumbing (UoW, validation, versioning)                             | ✅ Complete       |
| 4  | Onboarding API + DTOs                                                            | ✅ Complete       |
| 5  | Vue onboarding wizard (Required / Recommended / Goals)                           | ✅ Complete       |
| 6  | Test infrastructure (xUnit + Vitest + CI)                                        | ✅ Complete       |
| 7  | Closeout: ADRs, tech-debt sweep, secrets hygiene, Phase 5 handoff                | ✅ Complete       |
| 8  | Profile editing + dashboard warmup cards                                         | ✅ Complete       |
| 9  | TrainingPlan / PlannedWorkout / Workout domain & API + This Week card            | ✅ Complete       |
| 10 | Zones, thresholds, structured workout builder                                    | ✅ Complete       |
| 11 | Training-load engine + executed-workout capture + Recent Activity / Weekly Load cards | ✅ Complete  |
| 12 | Authentication & Authorization (approval-gated)                                  | ⏳ Next           |
| 13 | Workout history & plan browser                                                   | ✅ Complete       |
| 14 | Daily-load history & PMC engine (CTL / ATL / TSB / ACWR)                         | ✅ Complete       |
| 15 | Progress page (PMC chart, weekly load, time-in-zone, peaks)                      | ✅ Complete       |
| 16 | Calendar & scheduling (reschedule, compliance coloring)                          | ✅ Complete       |
| 17 | Goals & events surface (Goals page, ProgressRing, plan↔event links)              | ✅ Complete       |
| 18 | ATP / periodization engine (weekly targets, ramp, taper)                         | ✅ Complete       |
| 19 | Activity file import (.fit / .tcx / .gpx)                                        | ✅ Complete       |
| 20 | Wellness metrics (sleep, RHR, weight, soreness, HRV)                             | ✅ Complete       |
| 21 | Production hardening & deployment                                                | ⏳ Planned        |

Post-v1 expansion (v2 coach features, device sync, marketplace, virtual training, etc.) is tracked in `md/product/feature-parity-trainingpeaks.md` and folded back into this roadmap only when a candidate gets scoped.

---

## Phase 1 — Solution scaffold & .NET 10 Clean Architecture ✅

**Goal.** Establish the four-project Clean Architecture solution with correct dependency direction and a .NET 10 toolchain. This phase predates the Cursor + Claude Code workflow.

**Success criteria.**
- `api/Bryk.sln` builds clean from a fresh checkout with `dotnet build`.
- Project references enforce: API → Application → Domain; Infrastructure → Domain. No reverse references.
- `Program.cs` boots, serves a default route, registers DI for each layer's composition root.
- All four projects target .NET 10 consistently.

**Dependencies.** None.

**Task groups (retrospective).**
1. Solution + project skeleton (`Bryk.Domain`, `Bryk.Application`, `Bryk.Infrastructure`, `Bryk.API`, `Bryk.sln`).
2. DI composition root in `Program.cs` registering Application + Infrastructure modules.
3. API host basics (Kestrel config, JSON options including string-enum converter, routing, controller discovery).
4. Initial smoke run — host boots end-to-end before any domain work lands.

---

## Phase 2 — Domain model & EF Core persistence ✅

**Goal.** Land the entity model, repository contracts, and EF Core 10 SQL Server persistence layer that the rest of the application builds on.

**Success criteria.**
- Domain entities under `Bryk.Domain/Entities/`: `Athlete`, `AthleteSportProfile`, `Event`, `Goal`, `Equipment`. Plus enums under `Entities/Enums/`: `Sport` (includes `Triathlon`; gains `Strength` in Phase 9 per ADR-0001), `EquipmentType`, `EventPriority`, `Gender`, `GoalType`, `MethodologyChoice`, `TriathlonDistance`.
- Repository contracts live in `Bryk.Domain` with implementations in `Bryk.Infrastructure/Repositories/`.
- `ApplicationDbContext` is the only EF Core entry point; no DbContext access outside repositories. Services consume repos; controllers consume services.
- Entity IDs are `Guid`. No hardcoded IDs or magic numbers.
- Code-first migrations generate and apply against SQL Server.
- `Event.CustomDistanceName` exists to support triathlon onboarding events.

**Dependencies.** Phase 1.

**Task groups (retrospective).**
1. Core entity model — Athlete, AthleteSportProfile, Event, Goal, Equipment with relationships and constraints.
2. Legacy Mesocycle hierarchy (Mesocycle / Week / Day / DayExercise / Exercise) — **superseded by ADR-0001**; entities slated for deletion as part of Phase 7 or Phase 9.
3. Repository contracts + EF implementations, one repo per aggregate root, `.AsNoTracking()` defaults for display reads.
4. Initial migration committed and applied; review process documented.

---

## Phase 3 — Cross-cutting plumbing (UoW, validation, versioning) ✅

**Goal.** Land the cross-cutting infrastructure every feature depends on: unit-of-work boundary, validation pattern, global exception handling, API versioning, doc UI, and the dev-time current-user seam.

**Success criteria.**
- `IUnitOfWork` (in `Bryk.Domain`) plus `UnitOfWork` (in `Bryk.Infrastructure`). Repos stage only (`AddAsync` / `Update` / `Delete`); services commit exactly once via `_unitOfWork.SaveChangesAsync(ct)`.
- `IAuditable` + `AuditableEntityInterceptor` populate `CreatedAt` / `UpdatedAt` globally. Audit fields never set manually.
- `ICurrentUserService` dev stub at `Bryk.Application/Common/`, implementation at `Bryk.Infrastructure/Services/`. Reads `DevAuth:CurrentAthleteId` from `appsettings.Development.json`. Throws outside Development.
- `Asp.Versioning.Mvc` 10.0.0 + `Asp.Versioning.Mvc.ApiExplorer` 10.0.0 wired with URL segment primary, `api-version` header secondary, strict mode (`AssumeDefaultVersionWhenUnspecified = false`), `ReportApiVersions = true`. Default 1.0.
- All controllers carry `[ApiController]`, `[ApiVersion("1.0")]`, `[Route("api/v{version:apiVersion}/[controller]")]`.
- FluentValidation pattern locked: `await validator.ValidateAsync(request, ct)`, throw `Bryk.Application.Exceptions.ValidationException` on failure. `ValidateAndThrowAsync` is forbidden — middleware does not handle FluentValidation's own exception type.
- Global exception middleware in `Bryk.API/Middleware/` maps known types to status codes. No try/catch in controllers. `OperationCanceledException` → 499 (already fixed).
- Swashbuckle + Scalar render OpenAPI docs at a known route in Development.

**Dependencies.** Phase 2.

---

## Phase 4 — Onboarding API + DTOs ✅

**Goal.** Ship the server-side surface for athlete onboarding: required identity step, recommended thresholds step, goals/events step, and a status flags endpoint.

**Success criteria.**
- DTOs under `Bryk.Application/Onboarding/`: `OnboardingRequiredRequest`, `OnboardingRecommendedRequest`, `OnboardingGoalsRequest`, `OnboardingStatusResponse`, `SportThresholdsDto`, `EventDto`, `GoalDto`. DTO naming enforced (`*Request` / `*Response` / `*Dto`).
- Six FluentValidation validators under `Bryk.Application/Onboarding/Validators/`.
- `IOnboardingService` + `OnboardingService` expose `SubmitRequiredAsync`, `SubmitRecommendedAsync`, `SubmitGoalsAsync`, `GetStatusAsync`.
- `OnboardingController` at `Bryk.API/Controllers/`: `GET /api/v1/onboarding/status` returns 200 + flags; `POST /required`, `POST /recommended`, `POST /goals` return 204.
- State machine semantics (locked, see `md/handoffs/2026-04-29-phase-4-complete.md`):
  - **Required** is upsert. First call creates `Athlete`; subsequent calls update the eight non-nullable fields. Identity from `ICurrentUserService`, never from request body.
  - **Recommended** is upsert by `(AthleteId, Sport)`. Profiles for sports omitted from the request are left untouched. HR fields on `Athlete` updated alongside. Throws `InvalidOperationException` if Athlete row doesn't exist (Recommended before Required) — middleware maps to 409.
  - **Goals** is append. No upsert, no replace. Goals/Events have no natural client-side key.
- Status flags: `RequiredComplete` = Athlete row exists; `RecommendedComplete` = at least one `AthleteSportProfile`; `GoalsComplete` = at least one `Event` OR at least one `Goal`. No echoed data.
- Triathlon support: `Sport.Triathlon` and `Event.CustomDistanceName` available.
- `SportThresholdsDto` mirrors the entity (generic `ThresholdValue`); per-sport semantics (FTP for bike, threshold pace for run, threshold pace / 100m for swim) live in the frontend.

**Dependencies.** Phase 3.

---

## Phase 5 — Vue onboarding wizard ✅

**Goal.** Ship a three-step onboarding wizard in Vue 3 that drives the Phase 4 API end-to-end. Resume-friendly: on mount, call `GET /onboarding/status` and land on the first incomplete step.

**Status.** Complete. Wizard ships at `/onboarding` with Required / Recommended / Goals steps; vee-validate + zod client validation; vee-validate field error mapping from server validation responses (`src/services/apiErrors.ts`); resume-aware stepper navigation; read-only summary cards on completed steps (band-aid for missing edit-my-profile surface — see Phase 8); HomeView gates on status flags and renders a dashboard shell (`src/views/HomeView.vue` + `src/components/dashboard/`) when all three flags are true. Manual smoke walked end-to-end with a fresh GUID 2026-05-26. Closeout handoff: `md/handoffs/2026-05-26-phase-5-complete.md`.

**Inter-phase work that landed during Phase 5 closeout (outside original scope, kept for the record):**
- Read-only summary cards on completed onboarding steps (commit superseded by Phase 8 edit-my-profile surface).
- Dashboard shell with placeholder cards (`DashboardSidebar.vue`, `PlaceholderCard.vue`). Cards are inert; Phase 8 lights up the first two (Primary Goal, Resting HR).

**Dependencies.** Phase 4.

---

## Phase 6 — Test infrastructure (xUnit + Vitest + CI) ✅

**Goal.** Establish the safety net for everything that lands from Phase 7 onward. Backend xUnit + integration-test scaffolding, frontend Vitest, and a CI pipeline.

**Status.** All test infrastructure landed on branch `phase-6-test-infra-tech-debt` and merged to main:
- `66e1679` — backend test infrastructure (`Bryk.Application.Tests`, `Bryk.API.Tests` with `WebApplicationFactory<Program>`, `OnboardingServiceTests`, `OnboardingControllerTests`).
- `9d7aeda` — frontend Vitest infrastructure (store / service / step-component tests).
- `eb38c4d` — GitHub Actions CI workflow at `.github/workflows/ci.yml`.

**What changed from the original Phase 6 plan.** Original scope bundled test infrastructure with the tech-debt sweep (Task 6-4), secrets hygiene (Task 6-5), and the two model-decision ADRs (Task 6-6). The latter three are reframed and absorbed into Phase 7 to keep Phase 6 scoped tightly to "safety net landed."

**Outstanding from Phase 6's original scope, redistributed to Phase 7:**
- Tech-debt sweep — see Task 7-4 (simplified by ADR-0001; MesocycleService move replaced with deletion).
- Secrets hygiene — see Task 7-5.
- Two model-decision ADRs — done (`md/decisions/0001-mesocycle-vs-trainingplan.md`, `md/decisions/0002-coaches-as-first-class.md`).

**Dependencies.** Phase 5 functionally complete.

---

## Phase 7 — Closeout: ADRs, tech-debt sweep, secrets hygiene, Phase 5 handoff ✅

**Goal.** Clear the runway before opening the training-domain work in Phase 9. Boring but necessary — everything downstream is cheaper with these locked.

**Success criteria.**
- Phase 5 marked ✅ in this ledger after Task 7-1 lands the formal handoff document.
- Both architectural decisions captured as ADRs in `md/decisions/` and reflected in `CLAUDE.md` pending-decisions list.
- Tech-debt items 3 (MesocycleService layer violation), 5 (verbose validation pattern), and 7 (CS8604 in MesocycleValidators) addressed. Items 3 and 7 collapse to "delete the Mesocycle files" per ADR-0001.
- Plaintext dev SQL credentials removed from `api/Bryk.API/appsettings.Development.json` and replaced with `dotnet user-secrets` workflow. README and per-developer setup notes document the workflow.
- Mesocycle retirement migration may land in this phase or be deferred to Phase 9 — Sr. Dev approval required either way.

**Dependencies.** Phase 6 ✅.

**Task groups.**
1. **Task 7-1: Phase 5 completion handoff.** Write `md/handoffs/2026-05-26-phase-5-complete.md` capturing the wizard's final shape, the band-aid summary cards, the dashboard shell that landed alongside, the manual smoke verification, and what Phase 8 needs to do first. Flip the ledger above to ✅. Update the parking lot below if any v1 scope items got promoted.
2. **Task 7-2: ADR-0001 — Mesocycle vs TrainingPlan.** ✅ Landed 2026-05-26. See `md/decisions/0001-mesocycle-vs-trainingplan.md`.
3. **Task 7-3: ADR-0002 — Coaches as first-class user type.** ✅ Landed 2026-05-26. See `md/decisions/0002-coaches-as-first-class.md`.
4. **Task 7-4: Tech-debt sweep (revised).** Per ADR-0001, the original Phase 6 Task 6-4 simplifies: delete `MesocycleService`, the four Mesocycle controllers, `MesocycleValidators.cs`, and the five Mesocycle entity files instead of moving / fixing them. The migration that drops the five Mesocycle tables may land here or be deferred to Phase 9 — Sr. Dev approval required. Validation pattern (tech debt item 5) extracts to a `ValidateOrThrowAsync` extension method per the revised plan in `md/handoffs/Phase 6-Task4-handoff.md`. Update onboarding service call sites.
5. **Task 7-5: Secrets hygiene.** Migrate `ConnectionStrings:DefaultConnection` and `DevAuth:CurrentAthleteId` out of `appsettings.Development.json` to `dotnet user-secrets`. Replace the committed file with placeholder values (or remove `appsettings.Development.json` entirely if user-secrets covers all dev needs). Document the per-developer setup in README. Note: the orphan `api/appsettings.development.json` at the api/ root is dead — delete it as part of this task.

**Out of scope for this phase.** New feature work, profile editing surface (Phase 8), training-domain entities (Phase 9), strength-discipline support in `Sport` enum (lands in Phase 9 with the new entities).

**Phase exit checklist.**
- Phase 5 ledger row flipped to ✅.
- `md/decisions/0001-*.md` and `0002-*.md` exist and are referenced by `CLAUDE.md`.
- `MesocycleService`, four Mesocycle controllers, `MesocycleValidators.cs`, and the five Mesocycle entity files deleted (or, if Mesocycle deletion is deferred to Phase 9, this task is folded into Task 9-1 and noted here).
- `dotnet user-secrets` workflow documented and the plaintext SQL password removed from version control.
- `dotnet build api/Bryk.sln` green; `dotnet test api/Bryk.sln` green; UI `pnpm test` green.

---

## Phase 8 — Profile editing + dashboard warmup cards ✅

**Shipped.** `/profile` surface, Primary Goal + Resting HR cards live. Entry below kept as the historical plan; see `md/handoffs/2026-05-31-phase-8-queue.md` and subsequent handoffs.

**Goal.** Replace the read-only summary-card band-aid from Phase 5 with a real edit-my-profile surface. Light up the two cheapest dashboard cards (Primary Goal, Resting HR) using data already collected during onboarding.

**Success criteria.**
- Three GET endpoints exposed under `/api/v1/profile/`:
  - `GET /profile/required` — returns the eight non-nullable Athlete fields.
  - `GET /profile/recommended` — returns the Athlete HR fields + the per-sport thresholds.
  - `GET /profile/goals` — returns the athlete's Events and Goals.
  - Reuse onboarding `*Request` types as response shapes where they match; introduce `*Response` DTOs if divergence makes that cleaner.
- A `/profile` Vue route mounts a three-section view that pre-fills its forms from the GET endpoints and submits via the existing onboarding POSTs (which are upsert / append per Phase 4 semantics — no new write path needed).
- The summary-card band-aid in the onboarding step components (`RequiredStep.vue`, `RecommendedStep.vue`, `GoalsStep.vue`) is replaced by a single "Manage your profile" link from the onboarding "All set" view. The completed-step lock on the stepper relaxes: revisiting a completed onboarding step shows the editable form again (now safe because edit-on-the-spot saves through the same upsert path that the profile editor uses).
- The dashboard's Primary Goal placeholder card is replaced with real content: the athlete's highest-priority Event (sorted by `EventPriority` then date) shown with name, sport, date, and weeks-to-go countdown. Backend reads existing Event data — no new entity work.
- The dashboard's Resting HR placeholder card is replaced with real content from the existing `Athlete.RestingHr` field. If the athlete hasn't set it, render a "Set in profile" affordance linking to `/profile`.
- The sidebar's Profile nav item navigates to `/profile` and renders as active.
- Vitest + xUnit coverage for the new endpoints and at least one frontend component per new surface.

**Dependencies.** Phase 7 ✅ (closeout cleared).

**Task groups.**
1. **Profile read endpoints.** Service method(s) + DTOs + controller. Same FluentValidation pattern even though GETs are read-only — keep convention.
2. **Profile Vue surface.** Service layer, store action (load + save per section), three-section view component, route entry. Tests.
3. **Onboarding band-aid removal.** Delete summary-card branches in the three onboarding step components. Update OnboardingView's stepper logic to relax the completed-step disable. Update the "All set" view to link to `/profile`.
4. **Primary Goal card.** Backend service exposes a "highest-priority event" query (could live on the goals read endpoint as a separate property, or as a dedicated method — decide during design). Frontend swaps the placeholder for the real card. Empty-state (no events) keeps the placeholder.
5. **Resting HR + secondary stats wire-up.** Frontend swaps placeholder for real value. Decide whether the other three top-row cards (Sleep Avg, Weekly Load, Form/TSB) keep their placeholders or get a small adjustment to acknowledge they're awaiting later phases. Recommendation: keep their placeholders, no copy change.

**Out of scope.** New domain entities, training-plan work, any card whose underlying data doesn't exist yet (Weekly Load, Form/TSB, This Week, Recent Activity — Phases 9-13 own those).

**Phase exit checklist.**
- `/profile` works end-to-end against a clean DB.
- Onboarding summary-card band-aid removed; revisiting a completed onboarding step shows an editable, pre-filled form.
- Primary Goal and Resting HR dashboard cards show real data for an onboarded athlete.
- Latest handoff written to `md/handoffs/`.

---

## Phase 9 — TrainingPlan / PlannedWorkout / Workout domain & API + This Week card ✅

**Shipped.** See ADR-0003 (`md/decisions/0003-trainingplan-domain-shape.md`) for the final entity shapes — the periodization fields landed as `BuildWeeks` / `RecoveryWeeks` / `RecoveryWeekPercentage` (not the names sketched below). Entry kept as the historical plan.

**Goal.** Introduce the v1 training data model — a plan owns planned workouts; planned workouts mature into executed workouts. This is the spine for everything from Phase 10 onward. Ship it all the way through to a populated "This Week" dashboard card so it's not domain-work-in-a-vacuum.

**Success criteria.**
- New domain entities under `Bryk.Domain/Entities/`: `TrainingPlan`, `PlannedWorkout`, `Workout` (executed), plus supporting enums (e.g., `WorkoutStatus`, `IntensityTarget` as needed). `Guid` IDs, `IAuditable`.
- `TrainingPlan` carries the periodization fields from the retired `Mesocycle` per ADR-0001 — `BuildRecoveryRatio`, `RecoveryWeekPercentage`, `WeeklyPatternType` (Polarized / Pyramidal / Periodization / Norwegian / Custom). These seed Phase 15 ATP work.
- `Sport` enum gains `Strength` per ADR-0001. Reconcile against existing `Methodology` enum on Athlete during design — methodology may belong on `TrainingPlan` (per-plan) rather than (or in addition to) `Athlete` (default).
- Strength workouts share the `PlannedWorkout` / `Workout` shape with cardio sessions. Sport-specific differences (sets/reps for strength vs interval steps for cardio) live as discipline-specific payload on the shared base, not as separate entity hierarchies.
- Relationships and cascade rules explicitly documented in the migration commit message. A `Workout` may reference its `PlannedWorkout` (nullable) — unplanned executions are first-class.
- Repository contracts + service contracts in `Bryk.Application/`. Services consume `IUnitOfWork`, never `DbContext`.
- API surface at `/api/v1/`: CRUD for `TrainingPlan` and `PlannedWorkout`; create + complete for `Workout`. Thin controllers, FluentValidation per the locked pattern, no try/catch.
- New endpoint `GET /api/v1/calendar/this-week` returning the planned workouts for the current week (Monday-Sunday by default).
- DTOs: `TrainingPlanRequest/Response`, `PlannedWorkoutRequest/Response`, `WorkoutRequest/Response`. No entity leakage across the API boundary.
- Migration generated, reviewed, **Sr. Dev approval obtained before apply**. If the Mesocycle retirement migration didn't land in Phase 7, it lands here as part of the same migration set.
- Tests cover the happy path for each new endpoint plus one validation-failure case per request DTO.
- Stub plan-creation UI at `/training-plan` (sidebar nav item lights up) — minimal form to create a `TrainingPlan` and add `PlannedWorkouts` so test data isn't manual SQL.
- The dashboard's This Week placeholder card is replaced with real content from the `/calendar/this-week` endpoint: list of upcoming planned workouts with date, name, type tag, target TSS.

**Dependencies.** Phase 7 ✅ (Mesocycle retirement decision locked, tech-debt sweep done) and Phase 8 ✅ (profile surface).

**Task groups.**
1. **Domain entities + migration.** `TrainingPlan`, `PlannedWorkout`, `Workout`, enums, EF configurations. Add `Strength` to `Sport`. Migration that drops Mesocycle tables (if not already done in Phase 7) and adds the new tables. Sr. Dev approval before apply.
2. **Repositories + UoW wiring.** `ITrainingPlanRepository`, `IPlannedWorkoutRepository`, `IWorkoutRepository` per locked pattern.
3. **Services + DTOs + validators.** `TrainingPlanService`, `PlannedWorkoutService`, `WorkoutService`, per-DTO validators using the new `ValidateOrThrowAsync` extension from Task 7-4.
4. **Controllers + Scalar verification.** Endpoints reachable, Scalar UI renders the new operations, strict API versioning honored.
5. **Test coverage.** xUnit + integration tests for each endpoint, one validation-failure case per DTO.
6. **Frontend service + Pinia store.** Typed service for new endpoints, plan store.
7. **Stub plan creation UI.** Minimal form at `/training-plan` that lights up the sidebar Training Plan nav item.
8. **Wire This Week card.** Populate the dashboard card from `/calendar/this-week`.

**Out of scope.** Zones engine, structured workout step list, TSS / IF / NP calculation, calendar view — those land in Phases 10–13. This phase is the bare model + API + a stub UI to prove it works + one dashboard card lit up.

---

## Phase 10 — Zones, thresholds, structured workout builder ✅

**Shipped.** See ADR-0004 and `md/handoffs/2026-06-08-phase-10-complete.md`. Entry kept as the historical plan.

**Goal.** Make `PlannedWorkout` *structured*: typed steps targeting power/HR/pace, derived from the per-sport thresholds the athlete supplied in onboarding.

**Success criteria.**
- Per-sport zone calculation lives in `Bryk.Application/Training/Zones/`. Inputs: athlete's `AthleteSportProfile` for the relevant sport. Outputs: zone bands for power (bike), HR (all sports), pace (run/swim). Methodology choice carried on `Athlete` and/or `TrainingPlan` (reconciliation locked in Phase 9).
- `PlannedWorkout` carries an ordered list of `WorkoutStep` (new entity or owned type — decide explicitly): warmup / interval / recovery / cooldown, with target zone or absolute target plus duration or distance.
- For strength discipline (per ADR-0001 first-class), `WorkoutStep` accommodates sets/reps/load instead of duration/intensity zone. Single entity shape with discipline-specific payload, not parallel hierarchies.
- API surface for editing a structured workout: PUT replaces the step list atomically; partial-step edits not supported in v1.
- Vue builder UI under `src/views/` and `src/components/workouts/`. Drag-to-reorder out of scope for v1; ordered add/remove sufficient.
- Tests: zone calculation unit tests across all four endurance sports (bike/run/swim/triathlon) + at least one builder integration test. Strength workout step persistence has its own test.

**Dependencies.** Phase 9 (`PlannedWorkout` exists).

**Task groups.**
1. **Zones engine.** Pure functions in `Bryk.Application/`. No DB access.
2. **WorkoutStep modeling + migration.** Sr. Dev approval before apply.
3. **API surface for steps.** PUT-replace semantics for the step list.
4. **Vue builder.** Component, store action, service method.
5. **Tests.** Zone math and step-list CRUD plus strength-discipline shape.

---

## Phase 11 — TSS / IF / NP engine + workout execution capture + Recent Activity / Weekly Load cards ✅

**Shipped.** See ADR-0005 and `md/handoffs/2026-06-08-phase-11-complete.md` — `LoadCalculator` (pure TSS math), weekly load, executed-`Workout` capture with per-step actuals, `db/dev-seed.sql`. Entry kept as the historical plan.

**Goal.** Compute load metrics for completed workouts so downstream analytics (Phase 13 PMC) have data to draw on. Light up two more dashboard cards along the way.

**Success criteria.**
- TSS / IF / NP formulas implemented per sport in `Bryk.Application/Training/Metrics/`. Pure functions, fully unit-tested against documented worked examples.
- For strength discipline, define a TSS-equivalent load metric (per ADR-0001 deferred to this phase). Decide and document the formula; reasonable starting point is volume × intensity-class mapping.
- `Workout.CompleteAsync` (or equivalent service entry) accepts execution data (duration, average power/HR/pace; per-sample series optional in v1), computes TSS/IF/NP, persists. The rich field design from the retired `DayExercise` entity (HR zone minutes, weather, performance comparison) carries forward to `Workout` per ADR-0001.
- Manual-override path: athlete can set TSS directly when sample data is missing or untrusted. Manual TSS is flagged so downstream surfaces can render it differently.
- DTOs for execution capture + corresponding Vue surface (workout completion form). Reuse Phase 10 zone displays for context.
- The dashboard's Recent Activity placeholder card is replaced with real content: list of executed `Workouts` over the last N days, sorted descending, with sport icon, name, key stats.
- The dashboard's Weekly Load placeholder card is replaced with real content: sum of TSS across the current week (Monday-Sunday), with delta-vs-last-week and a tiny sparkline if budget permits.
- Tests: golden-input TSS/IF/NP tests per sport with vectors sourced from established references; integration test for the completion endpoint.

**Dependencies.** Phase 9 (`Workout` entity) and Phase 10 (zones).

**Task groups.**
1. **Metrics engine.** Pure-function library + unit tests with worked examples per discipline including strength.
2. **Completion endpoint.** Service + controller + validators.
3. **Manual-override path.** Flag on `Workout`, surfaced through the DTO.
4. **Vue completion form.** Calls the completion endpoint, displays computed metrics on success.
5. **Recent Activity card wire-up.** Backend query + frontend swap.
6. **Weekly Load card wire-up.** Backend aggregation + frontend swap.
7. **Tests.** Full per-sport (including strength) golden-input matrix + integration tests.

**Architect notes.** This is the most numerically-sensitive phase to date. Hold the test golden-inputs to a higher bar than usual — the PMC in Phase 13 inherits their correctness.

---

## Phase 12 — Authentication & Authorization ⏳ (next, approval-gated)

**Goal.** Replace the dev stub with real authentication. Direction: custom signup (email + password) plus OAuth via Google and Apple. Per ADR-0002, one human = one `Athlete` — no separate `User` entity at the domain level. **Approval-gated end-to-end** per CLAUDE.md Open Decisions: no `[Authorize]`, Identity, or `AddAuthentication` lands without Sr. Dev approval.

**Success criteria.**
- The Phase 12 auth ADR captures the binding evaluation of ASP.NET Core Identity vs hand-rolled, plus the table-layout decision: Identity in its own table linked 1:1 to `Athlete`, vs `Athlete : IdentityUser<Guid>`. Sr. Dev approval before any code lands.
- Migration generated, reviewed, **approved before apply**.
- OAuth providers (Google, Apple) wired through the external-login flow; token strategy decided and committed (cookie vs JWT — cookie default if the SPA is same-origin).
- `ICurrentUserService` production implementation reads from `ClaimsPrincipal`; all consumers from Phase 4 onward continue unchanged.
- `[Authorize]` applied everywhere except auth endpoints; anonymous rejection covered by tests.
- Signup / login / OAuth-callback / logout Vue surfaces ship, with route guards in `src/router/`.

**Dependencies.** None hard; must precede production traffic. Phases 13–20 *can* execute on the dev stub if sequencing demands it (all athlete resolution flows through `ICurrentUserService`, so the later swap doesn't touch feature code), but auth remains the declared next phase.

---

## Math conventions (single source of truth for Phases 14–18)

- **Daily load** = Σ `EffectiveLoad` (= `LoadOverride ?? ComputedLoad`) across workouts sharing a `CompletedDate`. Empty days contribute **0** — zeros are load-bearing for EWMA decay; never skip them.
- **CTL** ("fitness") = 42-day EWMA: `CTL_today = CTL_yesterday + (load_today − CTL_yesterday)/42`.
- **ATL** ("fatigue") = 7-day EWMA: `ATL_today = ATL_yesterday + (load_today − ATL_yesterday)/7`.
- **TSB** ("form") = `CTL_yesterday − ATL_yesterday` (yesterday's values, by convention).
- **ACWR** = 7-day acute ÷ 28-day chronic (same units); sweet spot ~0.8–1.3; >1.5 elevated risk; undefined (render "—") with <28 days of history.
- **Time-in-zone honesty:** with manual entry, time-in-zone derives from planned structure (per-step duration × zone target) for linked workouts, else coarse session-level AvgHr classification, else "unclassified". Label it "estimated" in the UI until Phase 19 file import supplies real samples.

---

## Phase 13 — Workout history & plan browser ✅

**Shipped.** Workouts nav live with a filtered/paged history list (`WorkoutsView`), workout detail with step-level planned-vs-actual + edit/delete (`WorkoutDetailView`), and a plan browser reopening the Phase-10 structure builder (`PlansView`/`PlanDetailView`). `PUT`/`DELETE /workouts/{id}` (replace-style + load recompute, hard delete) and the filtered `GET /workouts` (skip/take pagination convention) landed with no migration. See `md/handoffs/2026-06-11-phase-13-complete.md` and `md/Tasks-13-1.md`…`13-5.md`. Entry kept as the historical plan.

**Goal.** Make logged training browsable: the Workouts nav item goes live with a filterable history list, a workout detail page with step-level planned-vs-actual, full edit/delete on workouts, and a plan browser that reopens the structure builder on existing planned workouts (the carried Phase-10 gap).

**Why now / depends on.** First feature phase after auth: everything downstream (PMC, Progress, Calendar, import) needs trustworthy history and a place to land. Closes the API write-gap before analytics bake on top.

**Backend scope.**
- **No migration expected** (all fields exist). If a task discovers a needed column — stop, Sr. Dev approval first.
- `PUT /api/v1/workouts/{id}` — replace-style update (session actuals + step results, like the structure endpoint). **Recompute `ComputedLoad` via `LoadCalculator` on every update**; `LoadOverride` survives unless explicitly cleared. 404 for missing/foreign.
- `DELETE /api/v1/workouts/{id}` — hard delete, cascades step results, 204.
- Extend `GET /api/v1/workouts`: `from`/`to` (DateOnly on `CompletedDate`), `sport`, `skip`/`take` (capped, newest-first). Non-breaking; becomes the date-range workhorse Phase 14 reuses.
- Planned-vs-actual: keep `WorkoutResponse` lean — the client composes with the existing `GET .../structure` endpoint (confirm in task 1; any response change must be additive).
- `UpdateWorkoutRequest` validator mirrors `LogWorkoutRequest` rules. `WorkoutService.UpdateAsync/DeleteAsync` + repo date-range/paged query; UoW commit-once.

**Frontend scope.**
- `WorkoutsView.vue` at `/workouts`; flip the inert sidebar item (and mobile tab bar) live in `ui/src/components/layout/AppSidebar.vue`. Rows reuse the Recent Activity pattern: `TypePill`, EffectiveLoad, duration, `DeltaChip` vs planned where linked. Sport + date-range filter bar, "load more" pagination.
- `WorkoutDetailView.vue` at `/workouts/:id` — `MetricTile` strip (load/duration/distance/avg-max HR, `useCountUp`); per-step planned-vs-actual table **finally displaying `AvgPower`/`AvgPace` and `Workout.Notes`**; edit via `LogWorkoutForm` in a new edit mode; delete with confirm.
- Plan browser: plan list → detail → planned-workout rows with "Edit structure" reopening `WorkoutStructureBuilder` against existing GET/PUT structure endpoints. Browse + structure-edit only (plan metadata editing is Phase 18).

**Decisions needed.** Hard vs soft delete (recommend hard for v1; soft = migration + approval). Pagination convention (recommend skip/take + capped take; record as a convention entry — every later list endpoint follows it).

**Out of scope.** Calendar rendering (16), aggregates/charts (14–15), plan-metadata editing (18), "save as template", file upload (19).

**Success criteria.** Workouts nav live; list filters and paginates 20+ seeded workouts; detail shows step planned-vs-actual incl. AvgPower/AvgPace/Notes; editing duration/RPE changes ComputedLoad on save; delete removes from list + dashboard feed; plan browser reopens, edits, saves an existing structure that survives reload.

**Estimated size.** **L** — 5 task docs (13-1 PUT/DELETE + recompute; 13-2 list filters/pagination; 13-3 WorkoutsView + nav; 13-4 detail + edit/delete; 13-5 plan browser).

---

## Phase 14 — Daily-load history & PMC engine (CTL / ATL / TSB / ACWR) ✅

**Shipped.** Compute-on-read analytics per [ADR-0006](md/decisions/0006-pmc-computation.md): pure `PmcCalculator`/`AcwrCalculator` (`Bryk.Application/Analytics/`), `AnalyticsService` (zero-filled daily series over a bounded 180-day seeded lookback), and `AnalyticsController` — `GET /api/v1/analytics/daily-load` and `/pmc` (series + a `current` summary, null for a fresh athlete). The dashboard "Form (TSB)" placeholder is live (signed TSB, delta vs 7 days ago, Fresh/Neutral/Fatigued band) and `WeeklyLoadCard` gained an ACWR chip (in/out of 0.8–1.3, "—" under 28 days of history). No migration, no new packages. See `md/handoffs/2026-06-12-phase-14-complete.md` and `md/Tasks-14-1.md`…`14-4.md`. Entry kept as the historical plan.

**Goal.** Deterministic server-side analytics: daily load series, CTL/ATL/TSB, ACWR — lighting up the dashboard "Form (TSB)" placeholder tile.

**Why now / depends on.** Builds on 13's date-range surface and Phase 11's `EffectiveLoad`. Must precede 15 (Progress consumes it) and 18 (ATP targets are expressed against weekly load/CTL).

**Backend scope.**
- **No migration** — compute-on-read (bounded lookback, e.g. 180 days, seeded at first workout or 0). A `DailyLoadSnapshot` table is explicitly out (future approved migration if profiling demands).
- New `Bryk.Application/Analytics/`: `PmcCalculator` (pure, like `LoadCalculator`: ordered zero-filled daily series → per-day `{date, load, ctl, atl, tsb}` per the math conventions above), `AcwrCalculator` (pure; null under 28 days — don't fake confidence), `IAnalyticsService`/`AnalyticsService` (groups workouts by `CompletedDate`, sums EffectiveLoad, zero-fills, delegates).
- New `AnalyticsController`: `GET /api/v1/analytics/daily-load?from=&to=`; `GET /api/v1/analytics/pmc?from=&to=` returning the series **plus a `current` summary** (today's CTL/ATL/TSB/ACWR) so the dashboard needs one call.
- Validation: range required, ≤ 400 days, `from <= to`, no future `to`. xUnit: EWMA seeding, zero-day decay, TSB yesterday-offset, ACWR insufficiency, LoadOverride respected; worked example (constant 100 TSS/day → CTL converges to 100).

**Frontend scope.**
- Form (TSB) tile goes live: `MetricTile` + `useCountUp`, signed TSB, `DeltaChip` vs 7 days ago, interpretation label (lock bands in the task doc: e.g. >+10 fresh / −10..+10 neutral / <−10 fatigued).
- ACWR chip on `WeeklyLoadCard` (in/out of 0.8–1.3 styling). New analytics service module + Pinia slice. Big charts wait for 15.

**Decisions needed.** **ADR: PMC computation strategy** (compute-on-read vs snapshots; lookback/seeding rule — 15 and 18 silently depend on it). Controller naming nod (`AnalyticsController`). TSB interpretation band values.

**Out of scope.** Charts (15), per-sport PMC split, snapshot/caching table, wellness inputs into form (20 stays separate).

**Success criteria.** PMC endpoint matches hand-verifiable EWMA examples in tests; Form tile shows a real TSB that changes after logging/deleting a workout (via Phase 13 endpoints) and matches `current`; ACWR renders "—" under 28 days of history.

**Estimated size.** **M** — 4 task docs (14-1 calculators + tests; 14-2 service + endpoints; 14-3 Form tile + ACWR chip; 14-4 seeded verification + wiring polish).

---

## Phase 15 — Progress page (PMC chart, weekly load, time-in-zone, peaks) ✅

**Shipped.** Compute-on-read analytics per [ADR-0007](md/decisions/0007-progress-analytics.md): pure `WeeklyLoadCalculator` / `PeaksCalculator` / `TimeInZoneCalculator` (`Bryk.Application/Analytics/`) + three additive `AnalyticsService` methods + `AnalyticsController` actions (`GET /analytics/weekly-load`, `/peaks`, `/time-in-zone`); and a `/progress` `ProgressView` composing hand-rolled-SVG `PMCChart` + `LoadChart` ports (no chart lib), a time-in-zone stacked bar (honestly "estimated"), and a session-level peaks `MetricTile` grid — Progress nav lit live. No migration, no new packages. See `md/handoffs/2026-06-14-phase-15-complete.md` and `md/Tasks-15-1.md`…`15-5.md`. Entry kept as the historical plan.

**Goal.** The Progress nav item goes live as the analytics home: ported PMC chart, weekly load bars with planned hatch + optimal band, time-in-zone (honestly labeled), personal records/peaks.

**Why now / depends on.** Strictly after 14 (consumes its endpoints) and 13 (history + list conventions). The deliberately deferred design-export charts finally earn their data.

**Backend scope (no migration; compute-on-read).**
- `GET /api/v1/analytics/weekly-load?weeks=8` — per ISO week `{weekStart, plannedLoad, actualLoad}` + 4-week rolling average + optimal band (decision below). Planned = Σ scheduled `PlannedWorkout` effective loads; actual = Σ completed `EffectiveLoad`.
- `GET /api/v1/analytics/time-in-zone?from=&to=&sport=` — zone histogram in seconds with per-method breakdown (`structure`/`sessionAvg`/`unclassified`) per the math conventions. **Stays coarse until 19 — do not pretend otherwise.**
- `GET /api/v1/analytics/peaks?sport=` — session-level records only: highest single-workout load, longest duration/distance, best session avg pace (run/swim), highest session AvgPower (bike). *Not* duration-curve peaks (need samples, 19+).
- Validation: weeks 1–26; range bounds as 14.

**Frontend scope.**
- `ProgressView.vue` at `/progress`; nav live. **Port `PMCChart`** (CTL/ATL lines + daily load bars; hand-rolled SVG, no chart lib) with 6w/3m/6m range toggle. **Port `LoadChart`** (8-week bars, planned hatch, optimal band, 4-week trend). The design-export reference is `charts.jsx` inside the Claude Design export (`Bryk UI.zip`); `ui/src/components/common/Sparkline.vue` demonstrates the established porting pattern.
- Time-in-zone stacked bars in `ZonesView` zone colors + "estimated" badge driven by the method breakdown.
- Peaks as `MetricTile` grid with `TypePill` + `DeltaChip` for in-range records.
- Reuse `Sparkline`, eyebrow/card-surface utilities, `useCountUp`.

**Decisions needed.** **Optimal-band definition** (recommend ACWR-safe range: 0.8–1.3 × trailing 4-week average; must agree with 18's ramp model — lock once). Peaks persistence (recommend compute-on-read; persisting = migration, pairs better with 19). Range-picker URL/query convention.

**Out of scope.** Sample-based analytics (power curves, decoupling, lap splits — 19+), per-sport PMC tabs, chart export/share, customizable dashboards.

**Success criteria.** `/progress` renders all four sections from seed data, zero console errors, no chart lib in package.json; planned hatch vs actual fill distinguishable and band/trend move when workouts change; time-in-zone badge logic correct and per-method seconds sum to total; Vitest covers chart data-transform composables.

**Estimated size.** **L** — 5 task docs (15-1 weekly-load + peaks endpoints; 15-2 time-in-zone + classification tests; 15-3 PMCChart port; 15-4 LoadChart port; 15-5 zone/peaks UI + assembly/nav).

---

## Phase 16 — Calendar & scheduling (reschedule, compliance coloring) ⏳

**Goal.** Month/week training calendar merging planned + completed workouts + events, with reschedule (drag on desktop, tap-to-move on mobile) and compliance coloring.

**Why now / depends on.** Needs 13; benefits from 14's load context; floats vs 15/17 but placed here to complete the daily-use loop (plan → see week → do → log) before ATP densifies scheduling.

**Backend scope (no migration).**
- `GET /api/v1/calendar?from=&to=` — day-keyed merged feed: planned items, completed items, events (race days render with A/B/C priority). Range ≤ ~62 days.
- `PATCH /api/v1/trainingplans/{id}/plannedworkouts/{pwId}/schedule` — body `{scheduledDate}` only (dedicated lightweight endpoint; avoids full-DTO PUT misuse). Validate date within plan window.
- Compliance classified server-side in the feed (one home for the rule): past planned — `green` completed within 80–120% of PlannedLoad; `yellow` 50–80% or >120%; `red` missed; `grey` future; `unplanned` tag for unmatched completions; null-PlannedLoad falls back to duration ratio else completed=green. Thresholds locked as a product decision (reused by 18).

**Frontend scope.**
- `CalendarView.vue` at `/calendar` — recommend a new sidebar item (Training keeps authoring); confirm in task 1. Month grid + week strip (mobile defaults to week strip). Day cells: compact chips (`TypePill` + load + compliance dot). Day detail popover linking planned→structure (13's browser) and completed→`/workouts/:id`.
- Reschedule: hand-rolled pointer-event drag (desktop) + tap-to-select/tap-target (mobile). **No drag-and-drop library.**

**Decisions needed.** Compliance thresholds + null-load fallback (mini-ADR; 18 reuses). Reject vs warn on rescheduling outside the plan window (recommend reject — plan dates stay meaningful for 18). Sidebar IA nod.

**Out of scope.** Drag-to-copy/bulk week ops (v2), weather/availability tags, creating planned workouts from the calendar (revisit post-18), iCal export.

**Success criteria.** Seeded planned/completed/event items render in correct cells across a month boundary; drag (desktop) and tap-move (mobile) persist and survive reload; past days color correctly against locked thresholds incl. a seeded missed + overcooked workout; out-of-window reschedule blocked with a visible message.

**Estimated size.** **L** — 5 task docs (16-1 feed + compliance classifier; 16-2 schedule PATCH; 16-3 grid rendering; 16-4 reschedule interactions; 16-5 day detail + legend + nav).

---

## Phase 17 — Goals & events surface (Goals page, ProgressRing, plan↔event links) ✅

**Goal.** The Goals nav item goes live: goal list with date-based progress, event cards with countdown using the ported ProgressRing, and the dormant plan↔event link surfaced.

**Why now / depends on.** Floats (could swap with 16); placed before 18 so ATP has a live event surface to anchor ramps. Closes a verified API gap: events/goals have **no GET endpoints at all** (the dashboard composes from profile reads).

**Backend scope (no migration expected).**
- `GET /api/v1/events` (ordered by date, `upcoming=true` filter; includes `Notes` + linked plan ids via reverse `TrainingPlan.EventId` lookup); `GET /api/v1/events/{id}`; `GET /api/v1/goals` (computed days-remaining + status).
- Surface event name in plan summaries (additive only).

**Frontend scope.**
- `GoalsView.vue` at `/goals`; nav live. Goals section (cards: `GoalType` `TypePill`, description, target-date countdown) + Events section (date-ordered, A/B/C styling, **`Event.Notes` finally rendered**, linked-plan chip → plan browser).
- **Port `ProgressRing`** from the design export (ticks + gradient + draw-in): fill = elapsed fraction of creation→target window (plan start when linked); center = days-to-go via `useCountUp`. Refactor dashboard `PrimaryGoalCard` to share the internals (one implementation, two surfaces).
- Goal/event CRUD forms on-page wrapping existing POST/PUT/DELETE (vee-validate + zod, onboarding patterns).

**Decisions needed.** Quantitative goal progress (`TargetValue/Unit/CurrentValue`) = migration + product decision — **recommend defer**; ship date-based honestly, record candidate in parity doc. Plan↔event write surface waits for 18's plan PUT (display-only here).

**Out of scope.** Goal target-value tracking, auto-prioritization, event reminders/notifications, goal↔workout attribution.

**Success criteria.** `/goals` lists seeded data from the new GETs; CRUD round-trips without touching onboarding; ProgressRing animates with correct elapsed fraction and the dashboard card renders identically via the shared component; linked events navigate to plan detail; Notes visible.

**Estimated size.** **M** — 4 task docs (17-1 GET endpoints + linked-plan lookup; 17-2 ProgressRing port + PrimaryGoalCard refactor; 17-3 GoalsView + nav; 17-4 CRUD forms).

---

## Phase 18 — ATP / periodization engine (weekly targets, ramp, taper) ✅

**Goal.** Bring `BuildWeeks`/`RecoveryWeeks`/`RecoveryWeekPercentage` (dormant since ADR-0003) alive: auto-generated weekly load targets ramping toward the linked event, recovery-week scaling, taper, and weekly target-vs-actual on the dashboard.

**Why now / depends on.** Needs 14 (load math + weekly conventions), 17 (live event surface), and 16's locked compliance bands. Last "training intelligence" phase before integrations.

**Backend scope.**
- **No migration** — columns exist; targets compute on read (persisted `WeeklyTarget` overrides = future migration; recommend against for v1).
- `PUT /api/v1/trainingplans/{id}` — **new endpoint** (verified gap: no plan-metadata update exists). Name, dates, methodology, `EventId`, the three periodization fields. Validation: `BuildWeeks` 1–8, `RecoveryWeeks` ≥ 1, `RecoveryWeekPercentage` 30–90, dates coherent with event. (**Corrected 2026-07-26:** this entry previously said `0.3–0.9`. The field is **percent-scale** — `decimal(5,2)`, ADR-0003 records "e.g. `60.0`", and the shipped POST validator accepts 0–100. The code wins; see ADR-0009 §6.)
- New `Bryk.Application/Training/Periodization/`: `WeeklyTargetCalculator` (pure: plan window + baseline + ramp + build/recovery cadence → `[{weekStart, targetLoad, isRecoveryWeek}]`, taper into a linked event), `IPeriodizationService` (baseline from trailing 4-week actuals via 14's series).
- `GET /api/v1/trainingplans/{id}/weekly-targets` — targets merged with actuals.
- xUnit: cadence (3 build + 1 recovery), ramp bounds, taper, no-event plans, degenerate short plans — exact values pinned.

**Frontend scope.**
- Plan detail (13's browser) gains a Periodization panel: edit fields + event link via the PUT; render the target ramp by **reusing 15's LoadChart** (targets in place of planned hatch).
- `ThisWeekCard` gains target-vs-actual progress bar + `DeltaChip` (reusing 16's compliance bands). Calendar week headers optionally show the weekly target.

**Decisions needed.** ✅ Closed by **ADR-0009** (`md/decisions/0009-periodization-ramp-model.md`): baseline = trailing 4-week mean actual load (ADR-0007's `A`); ramp = **+7 %/build week** (`1.07⁴ = 1.31`, derived from the locked ACWR 1.3 ceiling); `BuildWeeks : RecoveryWeeks` cadence with recovery weeks at `RecoveryWeekPercentage` % of the build target they interrupt (recovery does not advance the ramp); two-week **75 % / 50 %** taper into a linked in-window event, overriding recovery scaling; compute-on-read confirmed (no `WeeklyTarget` table, no migration); a plan-window shrink that would strand planned workouts is **rejected 400** (`PlanWindow:`), extending ADR-0008 §2 to the PUT.

**Out of scope.** Auto-generating planned *workouts* from targets (targets are numbers; authoring stays manual), multi-event season ATP, per-sport target split, coach overrides (v2).

**Success criteria.** 3-build/1-recovery/60% on a 12-week linked plan yields a visible ramp with every 4th week dipped and a race-week taper, reproducible via pinned unit tests; This Week shows target vs actual flipping state on log; plan PUT round-trips from the UI; foreign plan 404s.

**Estimated size.** **M/L** — 5 task docs (18-1 ramp ADR + calculator + tests; 18-2 plan PUT; 18-3 weekly-targets endpoint; 18-4 periodization panel; 18-5 dashboard tie-in).

---

## Phase 19 — Activity file import (.fit / .tcx / .gpx) ✅

**Goal.** Upload a device file → parsed `Workout` with real actuals + zone data, matched to a planned workout — upgrading time-in-zone from "estimated" to sample-based for imports.

**Why now / depends on.** Needs 13 (detail/edit surface + match UX); pays off 15's honesty caveat. File import only — **no vendor OAuth in this roadmap window** (locked decision).

**Backend scope.**
- **Migration: `ActivityFile` only** (approved 2026-07-26, one reviewed set): Id, AthleteId, FileName, Format, ByteSize, `Content` as **`varbinary(max)`** (ADR-0010 §2 — DB over filesystem path), UploadedAt, `ParsedWorkoutId?`, `ZoneHistogramJson?`. **`Workout.SourceFileId?` and the `WorkoutZoneDuration` child table were NOT approved** — this entry previously assumed both. The "from file" badge and the duplicate-commit guard read the reverse link `ActivityFile.ParsedWorkoutId`, and the zone histogram is JSON on the same row (ADR-0010 §5). `Workout` is untouched by this phase. Normalizing the JSON into a table is a Phase-21 candidate.
- **Package: `Garmin.FIT.Sdk` 21.205.0 approved** 2026-07-26, `Bryk.Infrastructure` only (ADR-0010 §1). Verified publisher-verified Garmin International, ships netstandard2.0 (net10.0-compatible); license is Garmin's proprietary royalty-free FIT Protocol License, not OSI. `.tcx`/`.gpx` parse with `System.Xml.Linq` (no package). All three formats ship in this phase.
- Parsers in `Bryk.Infrastructure` behind an Application `IActivityFileParser` abstraction. Extract sport, start, duration/distance, avg/max HR, avg power/pace + samples → session actuals + zone histogram (vs `AthleteSportZone`, bucketed by a pure Application-layer `ZoneHistogramCalculator` — the parsers can't see the athlete's zones) + `ComputedLoad`.
- **Corrected 2026-07-26:** this entry previously claimed imported power "finally exercises the top IF branch" of `LoadCalculator`. It does not, as written — `Workout` has no session-level `AvgPower`/`AvgPace`, and `ComputeActualLoad`'s session path (`LoadCalculator.cs:88`) hardcodes both to null, so an import could only ever reach the HR branch. ADR-0010 §3 resolves it: commit writes **one synthetic `WorkoutStepResult`** (`WorkoutStepId` is already nullable) carrying the parsed power/pace, which routes the import down the existing StepResults branch (`LoadCalculator.cs:74–83`) and reaches the real power/pace IF branches with **zero** change to the calculator. `LoadCalculator.cs` is frozen for this phase.
- Two-step flow: `POST /api/v1/activityfiles` (multipart → 201 parsed preview + load + zone histogram + **match candidates**: planned workouts ±1 day, same sport, unlinked); `POST /api/v1/activityfiles/{id}/commit` (`{plannedWorkoutId?}` → creates the Workout + its synthetic step result, writes `ZoneHistogramJson`, sets `ParsedWorkoutId`); `DELETE /api/v1/activityfiles/{id}` (discard); `GET /api/v1/activityfiles/by-workout/{id}` (the badge lookup — 200 with a null body when hand-logged, not 404).
- Validation: extension + magic-byte sniffing, size cap ~25 MB enforced by the validator behind a 32 MB per-route attribute (**not** global Kestrel config — the pipeline's own over-limit exceptions have no case in `ExceptionHandlingMiddleware` and would surface as 500; that middleware is Phase 21's to change), duplicate-commit rejection via `ParsedWorkoutId is not null`, sample sanity (HR 30–230 etc.), corrupt file → 400, nothing persisted on parse failure.
- 15's time-in-zone updated to prefer the imported histogram (method = `samples`), read via `ActivityFile.ParsedWorkoutId → Workout` and unioned with the existing structure/sessionAvg chain. `ZoneTimeMethodBreakdownDto` gains an additive `SampleSeconds`; sample-covered workouts attribute unmeasured seconds to `unclassified` so an import can never shrink total training time.

**Frontend scope.**
- Upload entry on `WorkoutsView` (button + drop zone) → import review flow: parsed `MetricTile` strip, zone histogram preview (reuse 15's bars), match-candidate radio list, confirm → `/workouts/:id`. Detail gains a "from file" source badge; Progress "estimated" badge disappears for sample-covered ranges.

**Decisions needed.** ✅ All resolved by the Sr. Dev 2026-07-26; **ADR-0010** (`md/decisions/0010-activity-file-import.md`) is Task 19-1's first deliverable and records them with rationale. §1 Garmin FIT SDK **approved** (21.205.0, Infrastructure only); §2 raw bytes in **DB `varbinary(max)`**, ~25 MB cap, no filesystem path; §3 imported power/pace reach the load math via **one synthetic `WorkoutStepResult`**, not a `LoadCalculator` edit and not new `Workout` columns; §4 migration limited to **`ActivityFile` alone** — no `Workout.SourceFileId`, no `WorkoutZoneDuration`; §5 zone histogram persisted as **JSON on `ActivityFile`**, normalization deferred to 21. Raw per-sample persistence stays **no** for v1 — keep the file, persist derived aggregates, re-parse later if richer analytics land.

**Out of scope.** Vendor OAuth/auto-sync, per-second sample persistence, power curves/decoupling/lap deep-dives, push-to-device, bulk/multi-file backfill.

**Success criteria.** Committed test fixtures (.fit ride, .tcx run, .gpx activity) upload→preview→commit→appear in history with the correct IF branch driving load — pinned by a test asserting a powered bike import yields a TSS **different from** the HR-only fallback (the regression guard on ADR-0010 §3); import against a seeded same-day planned workout offers + links the match and the calendar shows real compliance; Progress shows `samples` method for imports; corrupt/oversized files fail clean with nothing persisted.

**Estimated size.** **L** — 6 task docs (19-1 ADR-0010 + `ActivityFile` + migration; 19-2 parser abstraction + TCX/GPX + zone bucketing; 19-3 FIT parser; 19-4 endpoints + validation + commit; 19-5 review UI + match flow; 19-6 `samples` time-in-zone). Kicked off 2026-07-26 — see `md/Tasks-19-1.md` … `Tasks-19-6.md` and `md/Impl-19-1.md` … `Impl-19-6.md`.

**Delivered 2026-07-26.** All six tasks landed, one commit each; see `md/handoffs/2026-07-26-phase-19-complete.md`. Exactly **one migration** (`AddActivityFile`) and exactly **one new package** (`Garmin.FIT.Sdk` 21.205.0, `Bryk.Infrastructure` only). `LoadCalculator.cs`, `Workout.cs` and `ExceptionHandlingMiddleware.cs` were not touched. **All success criteria met**, including all three formats end to end: a real device-written `sample-ride.fit` (6198 records, HR + power) was supplied the same day, closing the two carry-forwards the phase originally shipped with — the six fixture-pinned FIT parser tests now run, and the `samples` badge state is observed live (a range containing only that import reports `sampleSeconds == totalSeconds`).

---

## Phase 20 — Wellness metrics (sleep, RHR, weight, soreness, HRV) ✅

**Goal.** Manual daily wellness entry — sleep, resting HR, weight, soreness, HRV — turning on the Sleep placeholder tile and giving Resting HR a real history trend.

**Why now / depends on.** Floats (needs only 12); scheduled late as additive context. Manual entry is the honest v1 answer to the "needs device integration" placeholder.

**Backend scope.**
- **Migration required (approval):** `DailyWellness` — Id, AthleteId, Date (DateOnly; **unique composite index AthleteId+Date**), SleepHours?, SleepQuality(1–5)?, RestingHr?, WeightKg?, Soreness(1–10)?, HrvMs?, Notes?, IAuditable. All metrics nullable — partial entries are the norm.
- `PUT /api/v1/wellness/{date}` — idempotent per-day upsert; 400 future dates; ≥1 metric present. `GET /api/v1/wellness?from=&to=` (sparse OK). `GET /api/v1/wellness/summary` — 7-day averages + deltas vs prior 7, one call for tiles.
- Validation ranges: SleepHours 0–16, RestingHr 25–120, WeightKg 30–250, Soreness 1–10, HrvMs 10–250.

**Frontend scope.**
- Dashboard "Today" wellness quick-entry card (collapsed → form). Sleep tile live: 7-day avg + `Sparkline` of nightly hours + `DeltaChip` vs prior week. `RestingHrCard` upgraded from the static onboarding value to entered history + sparkline. Weight/HRV as `MetricTile`+`Sparkline` pairs. Soreness input parameterizes `RpeSelector` into a shared scale selector (default to prop-parameterize over duplicate).

**Decisions needed.** ✅ All resolved by the Sr. Dev 2026-07-26; **ADR-0011** (`md/decisions/0011-wellness-metrics.md`) is Task 20-1's first deliverable and records them with rationale. §1 `DailyWellness` is **independent of `Athlete`** — a wellness save never writes back to `Athlete.WeightKg`/`RestingHr` (verified: neither feeds load, zone or PMC math), with a **read-only fallback** so the Resting HR tile never regresses to `—`; §2 one wide mostly-nullable row per athlete per day, uniqueness enforced by a composite index **and** a service-side read-then-update upsert (the index is unenforceable by the InMemory test provider, so no test may assert a duplicate insert throws); §3 HRV does **not** blend into TSB/PMC or any readiness score — ADR-0006's calculator stays pure, and this makes the ROADMAP's prior "recommend no" binding; §4 the soreness/sleep-quality input **generalizes** `RpeSelector` into a shared `ScaleSelector` (soreness 1–10, sleep quality 1–5) rather than duplicating it, leaving `LogWorkoutForm.vue` and its three specs untouched; §5 `DeltaChip` is **not** recoloured — the standing `ui/src/lib/weeklyTarget.ts:21–23` convention holds, and inverted metrics (RHR, weight, soreness) report their change in `MetricTile`'s footer slot so good news never renders red; §6 **one migration, `DailyWellness` alone** — no `Athlete` change, no FK, no second table, no new package. The `AddDailyWellness` migration remains the phase's single **Sr. Dev approval gate**: generate, read `Up`/`Down`, approve, then apply.

**Out of scope.** Device/health sync (Whoop/Oura/Apple Health), readiness scores/recommendations, hydration/menstruation/nutrition (additive later — schema is one row per day), logging reminders.

**Success criteria.** Today's entry persists, survives reload, re-submit updates not duplicates (upsert proven); Sleep tile shows real 7-day avg + sparkline; RestingHr sparkline reflects entries, not the onboarding constant; out-of-range and future dates rejected with field messages.

**Estimated size.** **M** — 4 task docs (20-1 ADR-0011 + entity + repo + migration; 20-2 endpoints + validators + summary math; 20-3 types/store/`ScaleSelector` + entry form; 20-4 tiles + dashboard wiring). Kicked off 2026-07-26 — see `md/Tasks-20-1.md` … `Tasks-20-4.md` and `md/Impl-20-1.md` … `Impl-20-4.md`.

**Two verified hazards the specs encode** (both confirmed against the code at kickoff, neither obvious from this entry): `Program.cs:32–33` sets `SuppressModelStateInvalidFilter = true`, so a `{date}` route segment that fails to bind does **not** 400 — it arrives as `default(DateOnly)` and the action still runs; the PUT therefore carries **both** a `{date:datetime}` route constraint (malformed → 404) and a validator `default` guard (→ 400), and neither alone is sufficient. And `BrykWebApplicationFactory` runs on EF InMemory, whose own doc comment records that it enforces **no unique index** — so the `{AthleteId, Date}` constraint is verified by reading the generated migration, while the *behaviour* is proven by a service-side "PUT twice, count rows" test.

**Delivered 2026-07-26.** All four tasks landed, one commit each; see `md/handoffs/2026-07-26-phase-20-complete.md`. Exactly **one migration** (`AddDailyWellness`) and **zero** new packages. `Athlete.cs`, `DeltaChip.vue`, `LogWorkoutForm.vue` and every PMC/load calculator were not touched. **All success criteria met and observed at runtime**, against real SQL Server with the unique index live: a duplicate `{AthleteId, Date}` insert is rejected with error 2601, ten repeated PUTs to one date leave exactly **one** row, the `Athletes` row is byte-identical before and after (`48|74.50`), `PUT /wellness/not-a-date` → **404** and `/0001-01-01` → **400**, and the dashboard's Sleep tile shows a real 7-day average with a sparkline while Resting HR reads **46 bpm** from logged entries rather than the onboarding constant 48. Both hazards above held exactly as specified. One spec expectation was wrong rather than the code: a date-with-time segment (`…/2026-07-25T10:00:00`) returns **200**, not 400/404, because ASP.NET Core's `DateOnly` binder accepts an ISO datetime and truncates it — the request canonicalises to a valid non-future date, so no bad date reaches the database (recorded as a carry-forward, not a defect).

---

## Phase 21 — Production hardening & deployment ⏳

**Goal.** The single hardening phase: correct error contracts, observability, security posture, containerization, a deployment target, and tech-debt burn-down — dev-mode app → deployable product. Absorbs old Phase 16 (docs/security) and old Phase 17 (cutover/observability) scope.

**Why now / depends on.** Last by design; hardens the full 12–20 surface and removes Development conveniences.

**Backend scope.**
- **Error contract:** middleware emits **ProblemDetails (RFC 9457)** on every path; `DbUpdateException` → 409 (safe detail), `NotImplementedException` → 501, validation → 400 with field `errors`, keep 499. Controller audit: no try/catch, consistent 404-for-foreign.
- **API docs:** per-version `SwaggerDoc` via ApiExplorer group names (kills the Program.cs TODO); Scalar dev-only or auth-gated in prod.
- **Observability:** logging-stack decision (below), correlation IDs, `/health` (SQL check — hand-rolled DbContext ping to stay package-free, or approve `AspNetCore.HealthChecks.SqlServer`).
- **Rate limiting:** `Microsoft.AspNetCore.RateLimiting` (in-framework, no package): default fixed-window + tighter policies on auth and the Phase 19 upload route.
- **Security pass:** prod CORS allowlist, HSTS, secrets via env vars (document the prod source), test proving the DevAuth stub is unreachable outside Development, upload body-size limits re-verified.
- **Schema cleanup migration (approval):** drop vestigial `AthleteSportProfile.CustomZonesJson` (superseded by `AthleteSportZone` since Phase 10) + audit findings.
- **Tech-debt burn-down:** CLAUDE.md ledger sweep — pagination defaults, `.AsNoTracking()` audit, CancellationToken propagation, dead code, README rewritten to match reality (absorbs old Phase 16 scope).

**Frontend scope.** Env-based API base URL; shared ProblemDetails parser for error toasts; 401 → login redirect hardening (Phase 12 follow-through); bundle audit; empty/loading/error-state polish across all 13–20 views.

**Decisions needed (decision-dense — up-front ADR batch).** Logging stack (Serilog package vs built-in JSON console — decide with the deployment target). Containerization (Dockerfile vs SDK `PublishContainer`; how the SPA ships: API-served static vs separate host). Deployment target (App Service / container host / VPS — owns SQL hosting, secrets, TLS, CI deploy stage). `CustomZonesJson` drop approval. Rate-limit numbers. Swashbuckle vs built-in OpenAPI (explicitly *not now* unless appetite exists).

**Out of scope.** Any product features (hard line), IaC/blue-green/autoscaling, load testing beyond smoke, account tiers/billing.

**Success criteria.** Integration tests assert `application/problem+json` on 400/404/409/499/500/501; forced unique-violation yields 409; clean-machine boot via the chosen container path with healthy `/health` and a fresh DB lacking `CustomZonesJson`; CI builds the container and deploys to the target on main; structured correlated logs in production mode; scripted burst yields 429 on limited routes; prod config serves the SPA with the stub disabled and CORS locked.

**Estimated size.** **L** — 6 task docs (21-1 ADR batch; 21-2 ProblemDetails + mapping + tests; 21-3 versioned docs + rate limiting + health; 21-4 security/secrets + cleanup migration; 21-5 containerization + CI deploy; 21-6 debt burn-down + UI states audit).

---

## Dependency graph (Phases 12–21)

```
12 (Auth)
 └─→ 13 (History & Plan Browser)            [fixed first]
      ├─→ 14 (PMC Engine) ──→ 15 (Progress) ─┐
      ├─→ 16 (Calendar)   ←─ needs 13 only ──┼─→ 18 (ATP)   [needs 14 + 17; reuses 15's LoadChart + 16's bands]
      ├─→ 17 (Goals/Events) ←─ floats ───────┘
      ├─→ 19 (File Import)  ←─ needs 13; pays off 15's caveat
      └─→ 20 (Wellness)     ←─ needs only 12; fully floating
21 (Hardening) — fixed last.
```

Strictly ordered: 12→13; 13→14→15; {14,17}→18; 13→19; all→21. Floating: 16/17 may swap or slide before 15; 19 any time after 13; 20 anywhere from 14 on. If the FIT SDK approval stalls, pull 20 forward and let 19 slip. Cross-phase contracts to lock early: pagination convention (13), PMC ADR (14), compliance bands (16), optimal band (15 — must agree with 18's ramp model).

## Deferred beyond this roadmap

- **Vendor OAuth sync** (Garmin/Wahoo/Strava/Apple Health) — file import (19) covers the data need without partner onboarding, token storage, or webhooks.
- **Coaches (v2)** — per ADR-0002; an epic atop the role model Phase 12 establishes.
- **Marketplace / Coach Match** — requires coach critical mass.
- **Virtual indoor training** — separate product effort.
- **Notifications / email digests** — needs a mail provider + scheduling infra nothing else requires; revisit post-deployment.
- **Account tiers (Free/Premium)** — premature before external users; Phase 21 deploys single-tier.

---

## Cross-phase risks & pending decisions

These are durable concerns that span multiple phases. Each is owned by the phase noted; raising any earlier is welcome if the situation warrants.

- **Real authentication is deferred (owner: Phase 12).** The dev stub `ICurrentUserService` works because nothing currently distinguishes one athlete from another at the network boundary. The moment two real users exist, this is a critical incident waiting to happen. If feature phases (13+) execute before 12 lands, all athlete resolution must keep flowing through `ICurrentUserService` so the swap stays a non-event.
- **Test coverage is bootstrapped but still shallow (owner: every phase from 8 onward).** Phase 6 landed the safety net; coverage breadth grows phase by phase. Resist landing a feature without a test pinning it.
- **README drift (owner: Phase 21).** README currently implies direct DbContext usage, an Electron shell, SQLite/MySQL providers, and AI providers — none match current code. Treat README as historical, not authoritative, until the Phase 21 rewrite.
- ~~**Plaintext dev SQL credentials.**~~ Resolved in Phase 7 — `dotnet user-secrets` workflow shipped; Phase 21's security pass re-verifies nothing regressed.
- **Bleeding-edge frontend tooling (ongoing).** Vite 8, Tailwind 4, pre-release codegen dependencies. Pin and audit during Phase 21's dependency sweep; expect occasional churn from upstream releases.
- **Hardcoded `SwaggerDoc("v1")` (owner: Phase 21, or sooner if v2 ships).** TODO already in place in `Program.cs`.

**Recently resolved (kept here for the historical trail; details in ADRs):**
- ~~**Mesocycle vs TrainingPlan.**~~ Resolved 2026-05-26 — see ADR-0001. Mesocycle superseded; TrainingPlan / PlannedWorkout / Workout is the unified framework. Strength is a first-class v1 discipline.
- ~~**Coaches as first-class user role.**~~ Resolved 2026-05-26 — see ADR-0002. Coaches are v2. One human = one Athlete at the domain level.
- ~~**MesocycleService layer violation.**~~ Resolved by ADR-0001 — file slated for deletion in Task 7-4 or Phase 9.

---

## After v1 (not phases — parking lot)

Tracked in `md/product/feature-parity-trainingpeaks.md`. When a candidate gets scoped, fold it back into this roadmap as a new phase entry and update the parity doc's status tag. Current high-likelihood post-v1 candidates:

- **Coach surfaces** (v2 per ADR-0002) — dashboard, athlete roster, workout/plan libraries, group calendars, post-workout comments, in-app chat, notification digests, coach account tiers. A coach in v2 is an `Athlete` granted a coach role.
- Device sync (Garmin / Wahoo / Apple Health / Coros / Suunto / Polar) two-way. (Read-only *file* import graduated into Phase 19.)
- ~~Compliance color coding on the calendar.~~ Graduated into Phase 16.
- ~~Peak Performances (auto-medal personal bests).~~ Graduated into Phase 15 (session-level; duration-curve peaks remain post-19).
- StackUp-style benchmarking.
- Health and recovery integrations (Whoop, Oura). (Manual wellness entry graduated into Phase 20.)
- Account tiers (Free / Premium).
- Indoor virtual training platform (separate product effort; `deferred`).
- Marketplace / Coach Match revenue features (`deferred`, dependent on coach critical mass even after v2 coach surfaces ship).
- Electron desktop shell + SQLite/MySQL provider alternates (build-vs-drop decision).
- AI-provider integration for plan recommendations / workout analysis (build-vs-drop decision).

---

## How to use this roadmap

- Read it at the start of each session alongside `CLAUDE.md`, the latest handoff in `md/handoffs/`, and any ADRs in `md/decisions/`.
- When opening a phase: re-read the phase entry, confirm dependencies are satisfied, sanity-check the success criteria against current repo state with `git log`, file reads, and `dotnet build`.
- Each task group seeds one or more Cursor prompts. When writing the prompt, copy the relevant success criteria into the prompt's "verify" section verbatim — don't re-derive.
- When a phase ships: update the ledger table at the top, mark the phase ✅, and write `md/handoffs/YYYY-MM-DD-phase-N-complete.md` capturing what shipped, what changed in the decisions list, and what the next phase should do first.
- When scope shifts (a candidate gets promoted, a phase gets resized): edit this file. The roadmap is a living document; it loses value the moment it drifts from intent.
- Roadmap edits are commits like any other: `docs: roadmap — mark Phase 5 complete`. Never bundled with feature work.
