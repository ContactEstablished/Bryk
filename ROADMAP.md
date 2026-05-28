# ROADMAP — Bryk

**Status as of 2026-05-26.** Source of truth for phased Bryk development. Read alongside `CLAUDE.md` (workflow, conventions, pending decisions, tech debt), `md/decisions/` (architectural decision records), and `md/product/feature-parity-trainingpeaks.md` (parity wishlist with status tags). Phase plans below win on scope; the parity doc is the candidate inventory.

**Phase 7 reshape note.** This roadmap reflects a renumbering decided 2026-05-26 after ADR-0001 (supersede Mesocycle) and ADR-0002 (coaches are v2). Old Phase 7 (TrainingPlan domain) becomes new Phase 9. Two new phases — 7 (closeout) and 8 (profile + dashboard warmups) — are inserted. Downstream numbers shift by +2. Per-phase entries below reflect the new numbering; ADR documents capture the decisions that drove the reshape.

This roadmap is intentionally verbose. Each phase entry exists to seed Cursor prompts — the success criteria, dependencies, and task groups should compose directly into Pattern A prompts without the architect re-deriving context.

---

## Working principles (carry into every phase)

Non-negotiable per phase. They constrain how prompts get written and how diffs get reviewed. Restated here so a single read of `ROADMAP.md` is enough to start work.

- **Simplicity first.** Minimum code that solves the named problem. No speculative abstractions, no "while we're here" cleanups bundled in.
- **Surgical changes.** Each prompt names exactly what to modify and explicitly states what NOT to modify. Adjacent code, comments, formatting are off-limits unless the prompt names them.
- **Goal-driven execution.** Every prompt carries a verifiable success criterion. With Phase 6 test infrastructure landed, *done* means: build is green, tests pass, manual smoke test for the affected endpoint passes, diff reads cleanly.
- **One logical change per commit.** Conventional prefixes (`feat:`, `refactor:`, `docs:`, `fix:`, `chore:`). Architect reads the diff, proposes the message; user commits and pastes the hash.
- **Cursor + DeepSeek is the default executor.** Pattern A. Architect writes prompts; Cursor writes code. Pattern B (architect edits directly) is reserved for trivial mechanical edits, file reads for validation, and one-off scratch scripts. Model selection per `CLAUDE.md` — Haiku for mechanical, DeepSeek for default coding (preferred through pricing review on 2026-05-05), Sonnet for second opinion, Opus for complex design.
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
| 7  | Closeout: ADRs, tech-debt sweep, secrets hygiene, Phase 5 handoff                | 🟡 In progress (ADR-0001 and ADR-0002 landed) |
| 8  | Profile editing + dashboard warmup cards                                         | ⏳ Planned        |
| 9  | TrainingPlan / PlannedWorkout / Workout domain & API + This Week card            | ⏳ Planned        |
| 10 | Zones, thresholds, structured workout builder                                    | ⏳ Planned        |
| 11 | TSS / IF / NP engine + workout execution capture + Recent Activity / Weekly Load cards | ⏳ Planned  |
| 12 | Calendar view + scheduling UX                                                    | ⏳ Planned        |
| 13 | Performance Management Chart (CTL / ATL / TSB) + Form (TSB) card                 | ⏳ Planned        |
| 14 | Authentication & Identity (custom + OAuth)                                       | ⏳ Planned        |
| 15 | Annual Training Plan (ATP) with A/B/C events                                     | ⏳ Planned        |
| 16 | Documentation, configuration, security hardening                                 | ⏳ Planned        |
| 17 | v1 cutover: integration seams, observability, polish                             | ⏳ Planned        |

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

## Phase 7 — Closeout: ADRs, tech-debt sweep, secrets hygiene, Phase 5 handoff 🟡

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

## Phase 8 — Profile editing + dashboard warmup cards ⏳

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

## Phase 9 — TrainingPlan / PlannedWorkout / Workout domain & API + This Week card ⏳

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

## Phase 10 — Zones, thresholds, structured workout builder ⏳

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

## Phase 11 — TSS / IF / NP engine + workout execution capture + Recent Activity / Weekly Load cards ⏳

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

## Phase 12 — Calendar view + scheduling UX ⏳

**Goal.** Athletes see past/future workouts on a calendar, reschedule by date, and create unplanned workouts inline. This is the hub view per the parity doc and the eventual surface behind the dashboard sidebar's Calendar item.

**Success criteria.**
- Vue calendar component (week + month modes minimum). Library choice: prefer a headless calendar primitive over a heavyweight component lib; if none fits, scope a custom component. New library deps require Sr. Dev approval.
- API: `GET /api/v1/calendar?from=&to=` returns merged planned + executed workouts within the range. Paged or capped — decide explicitly.
- Reschedule action: PATCH-style endpoint that moves a `PlannedWorkout` to a new date.
- Inline create: from a date cell, create a `PlannedWorkout` or directly log an unplanned `Workout`.
- Drag-and-drop reschedule is **out of scope for v1**. Click-to-edit date is sufficient.
- Tests: integration test for the calendar query, store-level test for the reschedule action.

**Dependencies.** Phases 9, 10, 11 (so cells have something to render).

---

## Phase 13 — Performance Management Chart (CTL / ATL / TSB) + Form (TSB) card ⏳

**Goal.** Compute the canonical training-load chart so athletes can see fitness, fatigue, and form trends. Light up the final top-row dashboard card.

**Success criteria.**
- CTL (42-day exponentially-weighted average), ATL (7-day EWMA), TSB (CTL − ATL) computed from executed workouts' TSS. Decide explicitly whether to compute on-demand vs persist a daily rollup table (latter scales; former simpler). Document the decision in an ADR.
- API: `GET /api/v1/pmc?from=&to=&sports=` returns the time series. Filter by sport(s) — endurance athletes commonly want bike-only or run-only views.
- Vue chart view. Chart library: TBD in this phase; treat as a Sr. Dev approval gate (likely `vue-chartjs`, `apexcharts`, or `@unovis/vue` — whichever pairs cleanly with Vue 3 + TS).
- The dashboard's Form (TSB) placeholder card is replaced with real content: current TSB value plus a tiny sparkline of the last 7-14 days, with a colored productive/neutral/fresh status badge.
- Tests: golden-series tests for the PMC math (synthetic 90-day input → expected curves), API test for the endpoint.

**Dependencies.** Phase 11 (TSS data exists).

---

## Phase 14 — Authentication & Identity (custom + OAuth) ⏳

**Goal.** Replace the dev stub with real authentication. Direction: custom signup (email + password) plus OAuth via Google and Apple. Per ADR-0002, one human = one `Athlete` — no separate `User` entity at the domain level.

**Success criteria.**
- ADR captures the binding evaluation of ASP.NET Core Identity vs hand-rolled, plus the implementation choice on table layout: `ApplicationUser : IdentityUser<Guid>` linked 1:1 to `Athlete`, vs `Athlete : IdentityUser<Guid>`. `CLAUDE.md` recommendation is to evaluate Identity first; this phase makes that evaluation decisive. Sr. Dev approval before any code lands.
- If ASP.NET Core Identity: code lands under `Bryk.Infrastructure/Identity/`. Migration generated, **Sr. Dev approval before apply**.
- OAuth providers (Google, Apple) wired through ASP.NET external login flow if Identity is chosen, or equivalent OIDC plumbing otherwise. Apple Sign-In key-rotation noted in the deployment doc.
- Token strategy decided and committed (cookie vs JWT). Cookie auth is the default if the SPA is same-origin in dev/prod; JWT only if there's a clear cross-origin or mobile driver. Refresh-token rotation strategy documented and implemented (or deliberate deferral captured).
- `ICurrentUserService` production implementation reads from `ClaimsPrincipal`. All consumers from Phase 4 onward continue to work unchanged.
- `[Authorize]` attributes applied to controllers (everything except auth endpoints themselves). Anonymous request rejection covered by tests.
- Signup / login / OAuth-callback / logout Vue surfaces ship. Route guards in `src/router/`.
- Tests: integration tests for signup, login, OAuth callback (mocked external), authorized vs unauthorized request rejection.

**Dependencies.** Phase 6 ✅. Should land before any production traffic. May land earlier than this numeric slot if a real-traffic milestone arrives — re-sequence if so.

**Architect notes.** Per `CLAUDE.md`, no `[Authorize]` attributes, claims-based logic, password hashing, or token issuance lands without explicit Sr. Dev approval. This phase is gated end-to-end.

---

## Phase 15 — Annual Training Plan (ATP) with A/B/C events ⏳

**Goal.** Athletes set season-long target events with priority, and Bryk computes a load progression that respects taper/peak rhythm. Builds on the periodization fields seeded onto `TrainingPlan` in Phase 9 (carried forward from the retired `Mesocycle` per ADR-0001).

**Success criteria.**
- `TrainingPlan` extended (or paired) with an annual view: list of `Event` rows tagged A/B/C with target dates. The existing `Event` entity from Phase 4 onboarding (with `EventPriority`) is the seed.
- Load engine: given a list of A/B/C events plus a fitness baseline (CTL), generate weekly TSS targets across the season honoring build/recovery ratios and taper requirements before A-events. The methodology fields on `TrainingPlan` (Polarized / Pyramidal / Periodization / Norwegian) parameterize the engine.
- API: `GET /api/v1/atp` returns the computed weekly target curve. POST/PUT to recompute when events change.
- Vue ATP view shows the curve alongside event markers, lets the athlete drag-adjust ratios or peak duration. Drag-adjust is acceptable here — this is a planning surface, not real-time data.
- Tests: golden-event-list tests (e.g., one A-event mid-season + two B-events) → expected weekly target series.

**Dependencies.** Phases 11 (TSS), 13 (CTL baseline). Phase 14 nice-to-have but not strictly required if the dev-stub identity is still acceptable for the ATP surface.

---

## Phase 16 — Documentation, configuration, security hardening ⏳

**Goal.** Close the gap between aspiration and reality in `README.md` and supporting docs. Clean up committed dev config. Document deployment.

**Success criteria.**
- `README.md` reflects actual implemented state. Specifically:
  - Remove or qualify Electron/SQLite/MySQL/AI-provider claims that are not implemented (or, if anything has shipped through Phase 17's evaluation gate, narrow the wording to what shipped).
  - Correct any architectural claims that don't match reality. The correct shape: services consume repositories; repositories own DbContext access; `IUnitOfWork` owns the persistence boundary.
  - Update the data-model snippet to reflect the post-ADR-0001 model (TrainingPlan / PlannedWorkout / Workout; Mesocycle retired).
  - Update the API overview table to match current routes.
- Secrets hygiene from Phase 7 verified — no plaintext secrets reintroduced. `dotnet user-secrets` workflow stable.
- `appsettings.json` audited for embedded secrets or environment-specific URLs; all moved to user secrets or environment variables.
- CORS configuration distinguishes Development from Production with a documented allowlist strategy for prod origins.
- Pre-existing NuGet vulnerability warnings in `Bryk.API` triaged: each warning either patched or explicitly accepted in a `md/security-notes.md` with rationale.
- Deployment doc at `md/deployment.md`: how to run the API + frontend, environment-variable matrix, migration apply procedure, OAuth-provider setup.
- `md/decisions/` ADR folder formalized; inline decisions (PMC storage, chart-library, calendar-library, etc.) materialized as ADRs if not already.

**Dependencies.** None hard — can shift earlier if config drift becomes painful. Lower priority than feature phases but must land before any production-facing launch.

---

## Phase 17 — v1 cutover: integration seams, observability, polish ⏳

**Goal.** Cross the v1 finish line. Anything that has to ship for the product to be "complete enough to invite real athletes" lives here.

**Success criteria.**
- **Observability.** Structured logging (Serilog or `Microsoft.Extensions.Logging` with a JSON sink — decide explicitly), correlation IDs through requests, at minimum INFO coverage of service-layer happy paths and ERROR coverage of caught exceptions in middleware. Sr. Dev approval before adding a logging dependency.
- **Tech-debt mop-up.** Items 6, 8, 9, 10, 11 from `CLAUDE.md` revisited: `NotImplementedException` → 501, `DbUpdateException` / unique-constraint → 409 with diagnostics, RFC 7807 ProblemDetails *if and only if* external consumers are imminent, multi-version Swagger doc generation if v2 has shipped, remaining NuGet warnings.
- **Integration seams (read-only first).** Per the parity doc, Garmin/Wahoo/Apple Health two-way sync is post-v1, but a read-only file import (`.fit`, `.tcx`, `.gpx`) into the Phase 11 completion flow is achievable here. Treat as a Sr. Dev approval gate (new parser dependency, payload size limits, validation).
- **Performance pass.** Spot-check the heaviest endpoints under realistic data volume (calendar range, PMC range, ATP recompute). Add `.AsNoTracking()` / `.AsSplitQuery()` where missing. Decide whether any query crosses into Dapper territory — Sr. Dev approval if it does.
- **Final smoke matrix.** End-to-end onboarding → plan creation → workout execution → calendar → PMC. Documented in the v1 cutover handoff.

**Dependencies.** Everything prior.

---

## Cross-phase risks & pending decisions

These are durable concerns that span multiple phases. Each is owned by the phase noted; raising any earlier is welcome if the situation warrants.

- **Real authentication is deferred (owner: Phase 14).** The dev stub `ICurrentUserService` works because nothing currently distinguishes one athlete from another at the network boundary. The moment two real users exist, this is a critical incident waiting to happen.
- **Test coverage is bootstrapped but still shallow (owner: every phase from 8 onward).** Phase 6 landed the safety net; coverage breadth grows phase by phase. Resist landing a feature without a test pinning it.
- **README drift (owner: Phase 16).** README currently implies direct DbContext usage, an Electron shell, SQLite/MySQL providers, and AI providers — none match current code. Treat README as historical, not authoritative, until Phase 16.
- **Plaintext dev SQL credentials (owner: Phase 7; Phase 16 verifies).** `api/Bryk.API/appsettings.Development.json` contains plaintext credentials. Dev-only label limits blast radius but file is committed.
- **Aspirational README claims — Electron / SQLite / MySQL / AI providers (owner: Phase 16, with optional Phase 17 build-vs-drop decision).** None implemented. Either build (post-v1) or strip the claims; don't leave the gap open.
- **Bleeding-edge frontend tooling (ongoing).** Vite 8, Tailwind 4, pre-release codegen dependencies. Pin and audit during Phase 16's dependency sweep; expect occasional churn from upstream releases.
- **Hardcoded `SwaggerDoc("v1")` (owner: Phase 16, or sooner if v2 ships).** Tech debt item 10. TODO already in place.

**Recently resolved (kept here for the historical trail; details in ADRs):**
- ~~**Mesocycle vs TrainingPlan.**~~ Resolved 2026-05-26 — see ADR-0001. Mesocycle superseded; TrainingPlan / PlannedWorkout / Workout is the unified framework. Strength is a first-class v1 discipline.
- ~~**Coaches as first-class user role.**~~ Resolved 2026-05-26 — see ADR-0002. Coaches are v2. One human = one Athlete at the domain level.
- ~~**MesocycleService layer violation.**~~ Resolved by ADR-0001 — file slated for deletion in Task 7-4 or Phase 9.

---

## After v1 (not phases — parking lot)

Tracked in `md/product/feature-parity-trainingpeaks.md`. When a candidate gets scoped, fold it back into this roadmap as a new phase entry and update the parity doc's status tag. Current high-likelihood post-v1 candidates:

- **Coach surfaces** (v2 per ADR-0002) — dashboard, athlete roster, workout/plan libraries, group calendars, post-workout comments, in-app chat, notification digests, coach account tiers. A coach in v2 is an `Athlete` granted a coach role.
- Device sync (Garmin / Wahoo / Apple Health / Coros / Suunto / Polar) two-way.
- Compliance color coding on the calendar.
- Peak Performances (auto-medal personal bests).
- StackUp-style benchmarking.
- Health and recovery integrations (Whoop, Oura).
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
