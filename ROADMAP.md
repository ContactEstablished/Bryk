# ROADMAP — Bryk

**Status as of 2026-05-25.** Source of truth for phased Bryk development. Read alongside `CLAUDE.md` (workflow, conventions, pending decisions, tech debt) and `docs/product/feature-parity-trainingpeaks.md` (parity wishlist with status tags). Phase plans below win on scope; the parity doc is the candidate inventory.

**Phase 6 PR note.** Branch `phase-6-test-infra-tech-debt` contains Tasks 6-1 through 6-3 plus `Phase 6-Task4-handoff.md`. Task 6-4 implementation is intentionally paused: `MesocycleService` still accesses `ApplicationDbContext` directly and needs a repository-boundary plan before the layer move. Task 6-5 and Task 6-6 remain open.

This roadmap is intentionally verbose. Each phase entry exists to seed Cursor prompts — the success criteria, dependencies, and task groups should compose directly into Pattern A prompts without the architect re-deriving context.

---

## Working principles (carry into every phase)

Non-negotiable per phase. They constrain how prompts get written and how diffs get reviewed. Restated here so a single read of `ROADMAP.md` is enough to start work.

- **Simplicity first.** Minimum code that solves the named problem. No speculative abstractions, no "while we're here" cleanups bundled in.
- **Surgical changes.** Each prompt names exactly what to modify and explicitly states what NOT to modify. Adjacent code, comments, formatting are off-limits unless the prompt names them.
- **Goal-driven execution.** Every prompt carries a verifiable success criterion. Until Phase 6 ships test infrastructure, *done* means: build is green, manual smoke test for the affected endpoint passes, diff reads cleanly.
- **One logical change per commit.** Conventional prefixes (`feat:`, `refactor:`, `docs:`, `fix:`, `chore:`). Architect reads the diff, proposes the message; user commits and pastes the hash.
- **Cursor + DeepSeek is the default executor.** Pattern A. Architect writes prompts; Cursor writes code. Pattern B (architect edits directly) is reserved for trivial mechanical edits, file reads for validation, and one-off scratch scripts. Model selection per `CLAUDE.md` — Haiku for mechanical, DeepSeek for default coding (preferred through pricing review on 2026-05-05), Sonnet for second opinion, Opus for complex design.
- **Verify what you read.** Before a prompt is written, the relevant files are read, `git status` checked, build verified green. Repo-state claims that turn out wrong are expensive — they generate prompts that make wrong assumptions.
- **Sr. Dev approval gates** as listed in `CLAUDE.md`: migrations, new packages (first-party `Microsoft.Extensions.*` exempt), API breaking changes, cross-cutting concerns (auth, middleware, versioning, transactions), persistence boundary changes, Dapper switches, deviations from convention.

---

## Phase ledger at a glance

| #  | Phase                                                | Status         |
|----|------------------------------------------------------|----------------|
| 1  | Solution scaffold & .NET 10 Clean Architecture       | ✅ Complete    |
| 2  | Domain model & EF Core persistence                   | ✅ Complete    |
| 3  | Cross-cutting plumbing (UoW, validation, versioning) | ✅ Complete    |
| 4  | Onboarding API + DTOs                                | ✅ Complete    |
| 5  | Vue onboarding wizard (Required / Recommended / Goals) | 🟡 In progress |
| 6  | Test infrastructure + tech-debt sweep + model decisions | 🟡 In progress |
| 7  | TrainingPlan / PlannedWorkout / Workout domain & API | ⏳ Planned     |
| 8  | Zones, thresholds, structured workout builder        | ⏳ Planned     |
| 9  | TSS / IF / NP engine + workout execution capture     | ⏳ Planned     |
| 10 | Calendar view + scheduling UX                        | ⏳ Planned     |
| 11 | Performance Management Chart (CTL / ATL / TSB)       | ⏳ Planned     |
| 12 | Authentication & Identity (custom + OAuth)           | ⏳ Planned     |
| 13 | Annual Training Plan (ATP) with A/B/C events         | ⏳ Planned     |
| 14 | Documentation, configuration, security hardening     | ⏳ Planned     |
| 15 | v1 cutover: integration seams, observability, polish | ⏳ Planned     |

Post-v1 expansion (coaches, device sync, marketplace, virtual training, etc.) is tracked in `docs/product/feature-parity-trainingpeaks.md` and folded back into this roadmap only when a coach decision lands or a candidate gets scoped.

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
- Domain entities under `Bryk.Domain/Entities/`: `Athlete`, `AthleteSportProfile`, `Event`, `Goal`, `Equipment`, `Mesocycle`, `Week`, `Day`, `DayExercise`, `Exercise`. Plus enums under `Entities/Enums/`: `Sport` (includes `Triathlon`), `EquipmentType`, `EventPriority`, `Gender`, `GoalType`, `MethodologyChoice`, `TriathlonDistance`.
- Repository contracts live in `Bryk.Domain` with implementations in `Bryk.Infrastructure/Repositories/`.
- `ApplicationDbContext` is the only EF Core entry point; no DbContext access outside repositories. Services consume repos; controllers consume services.
- Entity IDs are `Guid`. No hardcoded IDs or magic numbers.
- Code-first migrations generate and apply against SQL Server.
- `Event.CustomDistanceName` exists to support triathlon onboarding events.

**Dependencies.** Phase 1.

**Task groups (retrospective).**
1. Core entity model — Athlete, AthleteSportProfile, Event, Goal, Equipment with relationships and constraints.
2. Mesocycle hierarchy — Mesocycle / Week / Day / DayExercise / Exercise with `.Include()`-friendly navigation properties and cascade-delete rules.
3. Repository contracts + EF implementations, one repo per aggregate root, `.AsNoTracking()` defaults for display reads.
4. Initial migration committed and applied; review process documented.

**Known follow-up.** `MesocycleService` was placed under `Bryk.Infrastructure/Services/` — a layer violation tracked as tech debt and addressed in Phase 6.

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
- Global exception middleware in `Bryk.API/Middleware/` maps known types to status codes. No try/catch in controllers. `OperationCanceledException` → 499 (recent fix).
- Swashbuckle + Scalar render OpenAPI docs at a known route in Development.

**Dependencies.** Phase 2.

**Task groups (retrospective).**
1. `IUnitOfWork` + `UnitOfWork` + DI registration; repos refactored to stage-only.
2. `AuditableEntityInterceptor` + `IAuditable` plumbing.
3. FluentValidation DI registration + `ValidatorPlaceholder` anchor type (replace in Phase 6).
4. API versioning packages + middleware + retrofit of legacy controllers.
5. Global exception middleware + custom exception types.
6. `ICurrentUserService` dev stub.
7. Smoke matrix — strict-mode rejection, header reader, supported-versions header, validation 400 shape.

---

## Phase 4 — Onboarding API + DTOs ✅

**Goal.** Ship the server-side surface for athlete onboarding: required identity step, recommended thresholds step, goals/events step, and a status flags endpoint.

**Success criteria.**
- DTOs under `Bryk.Application/Onboarding/`: `OnboardingRequiredRequest`, `OnboardingRecommendedRequest`, `OnboardingGoalsRequest`, `OnboardingStatusResponse`, `SportThresholdsDto`, `EventDto`, `GoalDto`. DTO naming enforced (`*Request` / `*Response` / `*Dto`).
- Six FluentValidation validators under `Bryk.Application/Onboarding/Validators/`.
- `IOnboardingService` + `OnboardingService` expose `SubmitRequiredAsync`, `SubmitRecommendedAsync`, `SubmitGoalsAsync`, `GetStatusAsync`.
- `OnboardingController` at `Bryk.API/Controllers/`: `GET /api/v1/onboarding/status` returns 200 + flags; `POST /required`, `POST /recommended`, `POST /goals` return 204.
- State machine semantics (locked, see `docs/handoffs/2026-04-29-phase-4-complete.md`):
  - **Required** is upsert. First call creates `Athlete`; subsequent calls update the eight non-nullable fields. Identity from `ICurrentUserService`, never from request body.
  - **Recommended** is upsert by `(AthleteId, Sport)`. Profiles for sports omitted from the request are left untouched. HR fields on `Athlete` updated alongside. Throws `InvalidOperationException` if Athlete row doesn't exist (Recommended before Required) — middleware maps to 409.
  - **Goals** is append. No upsert, no replace. Goals/Events have no natural client-side key.
- Status flags: `RequiredComplete` = Athlete row exists; `RecommendedComplete` = at least one `AthleteSportProfile`; `GoalsComplete` = at least one `Event` OR at least one `Goal`. No echoed data.
- Triathlon support: `Sport.Triathlon` and `Event.CustomDistanceName` available.
- `SportThresholdsDto` mirrors the entity (generic `ThresholdValue`); per-sport semantics (FTP for bike, threshold pace for run, threshold pace / 100m for swim) live in the frontend.
- Recent regression fix: `SubmitRecommendedAsync` upserts sport profiles to prevent `DbUpdateConcurrencyException`.

**Dependencies.** Phase 3.

**Task groups (retrospective).**
1. DTOs and validators.
2. Service + controller with three POSTs + one GET.
3. State-machine semantics (upsert vs append) baked into service logic.
4. Smoke matrix per the Phase 4 handoff.

**Known follow-ups open after Phase 4.** RFC 7807 ProblemDetails (tech debt item 9) — defer unless the wizard reveals the current JSON error shape is unworkable.

---

## Phase 5 — Vue onboarding wizard 🟡 (in progress)

**Goal.** Ship a three-step onboarding wizard in Vue 3 that drives the Phase 4 API end-to-end. Resume-friendly: on mount, call `GET /onboarding/status` and land on the first incomplete step.

**Success criteria.**
- Vue project skeleton in place: Vite 8, Vue 3 + Composition API, TypeScript, Pinia, Vue Router 4, Tailwind 4 + shadcn-vue + reka-ui + lucide, vee-validate + zod, pnpm. (Already present — verify `ui/package.json` and `ui/vite.config.ts` rather than re-do.)
- `src/services/onboarding.ts` provides typed methods for the four endpoints, matching the DTO shapes exactly. All HTTP via `src/services/`; never `fetch`/`axios` from a component.
- `src/stores/onboarding.ts` (Pinia) holds wizard state and exposes the current step.
- Three step components under `src/components/onboarding/`: `RequiredStep.vue`, `RecommendedStep.vue`, `GoalsStep.vue`. Each uses `<script setup lang="ts">`. vee-validate + zod for client-side validation matching server FluentValidation rules.
- `OnboardingView.vue` route mounts the wizard, calls status on mount, routes to the correct step, lets the athlete advance forward or jump to any complete step.
- Submit handlers map server validation errors (current JSON shape from `ValidationException` middleware) into per-field form errors. If the mapping is painful enough to motivate moving RFC 7807 (tech debt item 9) up the queue, flag it before committing — do not silently add a new error shape.
- Manual smoke: full happy-path completion seeds Athlete + per-sport profiles + at least one Goal/Event; status flips all three flags to true on reload. Partially-complete state lands the user on the correct step.
- Recent landings on `main`: `GoalsStep` form with event/goal field arrays (7d409f6) and updated polish (4cb3c2a).

**Dependencies.** Phase 4. Vue styling library decision (resolved as Tailwind 4 + shadcn-vue + reka-ui).

**Task groups.**
1. **API service layer + types.** TypeScript interfaces in `src/types/` (or co-located with services) that mirror the four `Onboarding*` DTOs verbatim. `src/services/api.ts` is the HTTP wrapper. `src/services/onboarding.ts` exposes per-endpoint methods.
2. **Pinia store + router.** `useOnboardingStore` with status, current-step, and submit actions. Route entry under `src/router/` lazy-loaded.
3. **Required step.** All eight non-nullable Athlete fields. Triathlon-aware where applicable.
4. **Recommended step.** Per-sport thresholds field array. Per-sport semantics in the UI (FTP / threshold pace / threshold pace per 100m). HR fields on Athlete updated alongside.
5. **Goals step.** Event and Goal field arrays with append semantics. Custom distance name for triathlon events. Final submit + redirect to post-onboarding landing.

**Out of scope for this phase.** Edit-my-profile surface (separate GET later), equipment management (later surface), any auth (`ICurrentUserService` dev stub continues to drive identity).

**Phase exit checklist.**
- Wizard happy path verified end-to-end against a clean DB.
- Resume flow verified — partially-complete state lands on the correct step.
- Server-side validation surfaces sanely in the UI; if it doesn't, decide whether to address now or escalate as a Phase 6 prerequisite.
- Latest handoff written to `docs/handoffs/`.

---

## Phase 6 — Test infrastructure + tech-debt sweep + model decisions ⏳

**Goal.** Establish the safety net (automated tests) before the data model grows further, sweep accumulated tech debt, and resolve two decisions that gate Phase 7.

**Success criteria.**
- xUnit projects added under `api/`: at minimum a `Bryk.Application.Tests` for service-layer logic and a `Bryk.API.Tests` using `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`) for integration tests against a test DB. Decide explicitly: in-memory vs SQL Server LocalDB vs Testcontainers — and document the choice.
- Onboarding endpoints have happy-path + validation-failure coverage. At least one service-level unit test per onboarding submit.
- Vitest configured under `ui/` with smoke tests for `useOnboardingStore`, `src/services/onboarding.ts`, and one step component per step.
- A CI hook (GitHub Actions or equivalent — pick in this phase, Sr. Dev approval on the surface) runs `dotnet build`, `dotnet test`, and the frontend test command on every push. Red gates merges.
- Tech-debt items 3, 4, 5, 7 from `CLAUDE.md` addressed: `MesocycleService` moved to `Bryk.Application/Services/` (layer fix), `ValidatorPlaceholder` replaced with a named marker, validation pattern extracted to an extension method or migrated to a `FluentValidation.ValidationException` handler, `MesocycleValidators.cs` CS8604 cleared.
- Plaintext dev SQL credentials removed from `api/appsettings.development.json`. Replaced with `dotnet user-secrets` or a placeholder requiring per-developer override. README / deployment notes document the workflow.
- **Decision committed in writing — Mesocycle vs TrainingPlan.** Supersede / integrate / coexist. Recorded as an ADR in `docs/decisions/` (new folder) and `CLAUDE.md` pending-decisions list updated.
- **Decision committed in writing — Coaches as a first-class user type.** v1 / v2 / out-of-scope. Same ADR location. Parity doc tags updated for coach features.

**Dependencies.** Phase 5 complete (so the wizard isn't blocked by mid-flight infra changes).

**Task groups.**
1. **.NET test infrastructure.** ✅ Landed on `phase-6-test-infra-tech-debt` (`66e1679`). Test projects, base fixture, one passing integration test against `OnboardingController`, one service-level unit test. Test-DB strategy decision recorded as EF Core InMemory bootstrap coverage.
2. **Vue test infrastructure.** ✅ Landed on `phase-6-test-infra-tech-debt` (`9d7aeda`). Vitest config, store + service + component tests, pnpm script wired.
3. **CI pipeline.** ✅ Landed on `phase-6-test-infra-tech-debt` (`eb38c4d`). Definition committed; first actual green/red GitHub Actions verification still requires the PR/push workflow.
4. **Tech-debt sweep.** 🟡 Paused. See `Phase 6-Task4-handoff.md`: `ValidatorPlaceholder` was already renamed, `MesocycleService` needs an `IMesocycleRepository` design/approval before the layer move, and validation/nullability fixes remain open.
5. **Secrets hygiene.** ⏳ Open. User-secrets migration, README/deployment doc update.
6. **Decisions ADRs.** ⏳ Open. Two short ADRs (Mesocycle/TrainingPlan, Coaches). `CLAUDE.md` updated.

**Architect notes.** The Mesocycle decision drives the Phase 7 data model. If the answer is *supersede*, write a migration plan that retires `Mesocycle`, `Week`, `Day`, `DayExercise` cleanly. If *coexist*, draw a hard boundary so the legacy surface doesn't bleed into the new one. Either way, do not start Phase 7 prompts until this decision is locked.

---

## Phase 7 — TrainingPlan / PlannedWorkout / Workout domain & API ⏳

**Goal.** Introduce the v1 training data model — a plan owns planned workouts; planned workouts mature into executed workouts. This is the spine for everything from Phase 8 onward.

**Success criteria.**
- New domain entities under `Bryk.Domain/Entities/`: `TrainingPlan`, `PlannedWorkout`, `Workout` (executed), plus supporting enums (e.g., `WorkoutStatus`, `IntensityTarget` as needed). `Guid` IDs, `IAuditable`.
- Relationships and cascade rules explicitly documented in the migration commit message. A `Workout` may reference its `PlannedWorkout` (nullable) — unplanned executions are first-class.
- Repository contracts + service contracts in `Bryk.Application/`. Services consume `IUnitOfWork`, never `DbContext`.
- API surface at `/api/v1/`: CRUD for `TrainingPlan` and `PlannedWorkout`; create + complete for `Workout`. Thin controllers, FluentValidation per the locked pattern, no try/catch.
- DTOs: `TrainingPlanRequest/Response`, `PlannedWorkoutRequest/Response`, `WorkoutRequest/Response`. No entity leakage across the API boundary.
- Migration generated, reviewed, **Sr. Dev approval obtained before apply**.
- Whatever Phase 6 decided about Mesocycle is enacted here — retire the entities, or wall them off.
- Tests cover the happy path for each new endpoint plus one validation-failure case per request DTO.

**Dependencies.** Phase 6 (test infra, Mesocycle decision).

**Task groups.**
1. **Domain model + migration.** Entities, EF configurations, migration. Sr. Dev approval before apply.
2. **Repositories + service interfaces.** Stage-only repos; services commit once.
3. **DTOs + validators.** Per-DTO validators; mirror server enums in TypeScript later.
4. **Controllers + Scalar verification.** Endpoints reachable, Scalar UI renders the new operations.
5. **Test coverage.** xUnit + integration tests for each endpoint. Vitest stubs only if a UI consumer ships in the same phase (defer otherwise).

**Out of scope.** Calendar UI, zones engine, TSS calculation, structured intervals — those land in Phases 8–11. This phase is the bare model + API.

---

## Phase 8 — Zones, thresholds, structured workout builder ⏳

**Goal.** Make `PlannedWorkout` *structured*: typed steps targeting power/HR/pace, derived from the per-sport thresholds the athlete supplied in onboarding.

**Success criteria.**
- Per-sport zone calculation lives in `Bryk.Application/Training/Zones/`. Inputs: athlete's `AthleteSportProfile` for the relevant sport. Outputs: zone bands for power (bike), HR (all sports), pace (run/swim). Methodology choice carried on the athlete (`MethodologyChoice` exists; verify and extend if needed).
- `PlannedWorkout` carries an ordered list of `WorkoutStep` (new entity or owned type — decide explicitly): warmup / interval / recovery / cooldown, with target zone or absolute target plus duration or distance.
- API surface for editing a structured workout: PUT replaces the step list atomically; partial-step edits not supported in v1.
- Vue builder UI under `src/views/` and `src/components/workouts/`. Drag-to-reorder out of scope for v1; ordered add/remove sufficient.
- Tests: zone calculation unit tests across all four sports (bike/run/swim/triathlon) + at least one builder integration test.

**Dependencies.** Phase 7 (`PlannedWorkout` exists).

**Task groups.**
1. **Zones engine.** Pure functions in `Bryk.Application/`. No DB access.
2. **WorkoutStep modeling + migration.** Sr. Dev approval before apply.
3. **API surface for steps.** PUT-replace semantics for the step list.
4. **Vue builder.** Component, store action, service method.
5. **Tests.** Zone math and step-list CRUD.

---

## Phase 9 — TSS / IF / NP engine + workout execution capture ⏳

**Goal.** Compute load metrics for completed workouts so downstream analytics (Phase 11 PMC) have data to draw on.

**Success criteria.**
- TSS / IF / NP formulas implemented per sport in `Bryk.Application/Training/Metrics/`. Pure functions, fully unit-tested against documented worked examples.
- `Workout.CompleteAsync` (or equivalent service entry) accepts execution data (duration, average power/HR/pace; per-sample series optional in v1), computes TSS/IF/NP, persists.
- Manual-override path: athlete can set TSS directly when sample data is missing or untrusted. Manual TSS is flagged so downstream surfaces can render it differently.
- DTOs for execution capture + corresponding Vue surface (workout completion form). Reuse Phase 8 zone displays for context.
- Tests: golden-input TSS/IF/NP tests per sport with vectors sourced from established references; integration test for the completion endpoint.

**Dependencies.** Phase 7 (`Workout` entity) and Phase 8 (zones).

**Task groups.**
1. **Metrics engine.** Pure-function library + unit tests with worked examples.
2. **Completion endpoint.** Service + controller + validators.
3. **Manual-override path.** Flag on `Workout`, surfaced through the DTO.
4. **Vue completion form.** Calls the completion endpoint, displays computed metrics on success.
5. **Tests.** Full per-sport golden-input matrix + integration tests.

**Architect notes.** This is the most numerically-sensitive phase to date. Hold the test golden-inputs to a higher bar than usual — the PMC in Phase 11 inherits their correctness.

---

## Phase 10 — Calendar view + scheduling UX ⏳

**Goal.** Athletes see past/future workouts on a calendar, reschedule by date, and create unplanned workouts inline. This is the hub view per the parity doc.

**Success criteria.**
- Vue calendar component (week + month modes minimum). Library choice: prefer a headless calendar primitive over a heavyweight component lib; if none fits, scope a custom component. New library deps require Sr. Dev approval.
- API: `GET /api/v1/calendar?from=&to=` returns merged planned + executed workouts within the range. Paged or capped — decide explicitly.
- Reschedule action: PATCH-style endpoint that moves a `PlannedWorkout` to a new date.
- Inline create: from a date cell, create a `PlannedWorkout` or directly log an unplanned `Workout`.
- Drag-and-drop reschedule is **out of scope for v1**. Click-to-edit date is sufficient.
- Tests: integration test for the calendar query, store-level test for the reschedule action.

**Dependencies.** Phases 7, 8, 9 (so cells have something to render).

**Task groups.**
1. **Calendar API.** Service + controller + query DTO + pagination/cap policy.
2. **Vue calendar component.** Headless primitive or custom; consult on library choice before committing.
3. **Reschedule + inline create.** Endpoint + UI actions.
4. **Wiring with Phase 9 completion flow.** Calendar cell click → completion form for executed workouts.
5. **Tests.** API + UI.

---

## Phase 11 — Performance Management Chart (CTL / ATL / TSB) ⏳

**Goal.** Compute the canonical training-load chart so athletes can see fitness, fatigue, and form trends.

**Success criteria.**
- CTL (42-day exponentially-weighted average), ATL (7-day EWMA), TSB (CTL − ATL) computed from executed workouts' TSS. Decide explicitly whether to compute on-demand vs persist a daily rollup table (latter scales; former simpler). Document the decision in an ADR.
- API: `GET /api/v1/pmc?from=&to=&sports=` returns the time series. Filter by sport(s) — endurance athletes commonly want bike-only or run-only views.
- Vue chart view. Chart library: TBD in this phase; treat as a Sr. Dev approval gate (likely `vue-chartjs`, `apexcharts`, or `@unovis/vue` — whichever pairs cleanly with Vue 3 + TS).
- Tests: golden-series tests for the PMC math (synthetic 90-day input → expected curves), API test for the endpoint.

**Dependencies.** Phase 9 (TSS data exists).

**Task groups.**
1. **PMC math.** Pure-function implementation with golden-series tests.
2. **Storage decision + rollup job (if chosen).** Background job or scheduled task surface; Sr. Dev approval.
3. **API endpoint.** Filter parameters, sane defaults.
4. **Chart library decision + Vue chart view.**
5. **Tests.** Math + endpoint.

---

## Phase 12 — Authentication & Identity (custom + OAuth) ⏳

**Goal.** Replace the dev stub with real authentication. Direction: custom signup (email + password) plus OAuth via Google and Apple. Bryk owns the user store; OAuth providers are login methods that resolve to a Bryk `Athlete` record.

**Success criteria.**
- ADR captures the binding evaluation of ASP.NET Core Identity vs hand-rolled. `CLAUDE.md` recommendation is to evaluate Identity first; this phase makes that evaluation decisive. Sr. Dev approval before any code lands.
- If ASP.NET Core Identity: `Bryk.Infrastructure/Identity/` with `ApplicationUser : IdentityUser<Guid>` linked to the existing `Athlete` row. Migration generated, **Sr. Dev approval before apply**.
- OAuth providers (Google, Apple) wired through ASP.NET external login flow if Identity is chosen, or equivalent OIDC plumbing otherwise. Apple Sign-In key-rotation noted in the deployment doc.
- Token strategy decided and committed (cookie vs JWT). Cookie auth is the default if the SPA is same-origin in dev/prod; JWT only if there's a clear cross-origin or mobile driver. Refresh-token rotation strategy documented and implemented (or deliberate deferral captured).
- `ICurrentUserService` production implementation reads from `ClaimsPrincipal`. All consumers from Phase 4 onward continue to work unchanged.
- `[Authorize]` attributes applied to controllers (everything except auth endpoints themselves). Anonymous request rejection covered by tests.
- Signup / login / OAuth-callback / logout Vue surfaces ship. Route guards in `src/router/`.
- Tests: integration tests for signup, login, OAuth callback (mocked external), authorized vs unauthorized request rejection.

**Dependencies.** Phase 6 (test infra). Should land before any production traffic. May land earlier than this numeric slot if a real-traffic milestone arrives — re-sequence if so.

**Task groups.**
1. **ADR + ASP.NET Identity vs hand-rolled decision.** Sr. Dev approval.
2. **Identity model + migration.** Tied to `Athlete` cleanly.
3. **OAuth providers.** Google + Apple. Apple is the harder one (signing key rotation) — budget time.
4. **Production `ICurrentUserService`.**
5. **Vue auth surfaces + route guards.**
6. **Tests.**

**Architect notes.** Per `CLAUDE.md`, no `[Authorize]` attributes, claims-based logic, password hashing, or token issuance lands without explicit Sr. Dev approval. This phase is gated end-to-end.

---

## Phase 13 — Annual Training Plan (ATP) with A/B/C events ⏳

**Goal.** Athletes set season-long target events with priority, and Bryk computes a load progression that respects taper/peak rhythm.

**Success criteria.**
- `TrainingPlan` extended (or paired) with an annual view: list of `Event` rows tagged A/B/C with target dates. The existing `Event` entity from Phase 4 onboarding (with `EventPriority`) is the seed.
- Load engine: given a list of A/B/C events plus a fitness baseline (CTL), generate weekly TSS targets across the season honoring build/recovery ratios and taper requirements before A-events.
- API: `GET /api/v1/atp` returns the computed weekly target curve. POST/PUT to recompute when events change.
- Vue ATP view shows the curve alongside event markers, lets the athlete drag-adjust ratios or peak duration. Drag-adjust is acceptable here — this is a planning surface, not real-time data.
- Tests: golden-event-list tests (e.g., one A-event mid-season + two B-events) → expected weekly target series.

**Dependencies.** Phases 9 (TSS), 11 (CTL baseline). Phase 12 nice-to-have but not strictly required if the dev-stub identity is still acceptable for the ATP surface.

**Task groups.**
1. **Load engine.** Pure function: events + baseline → weekly target series. Unit tests.
2. **API surface.** GET / POST / recompute on event change.
3. **Vue ATP view.**
4. **Integration with calendar.** Phase 10 calendar shows weekly target overlay.
5. **Tests.**

---

## Phase 14 — Documentation, configuration, security hardening ⏳

**Goal.** Close the gap between aspiration and reality in `README.md` and supporting docs. Clean up committed dev config. Document deployment.

**Success criteria.**
- `README.md` reflects actual implemented state. Specifically:
  - Remove or qualify Electron/SQLite/MySQL/AI-provider claims that are not implemented (or, if anything has shipped through Phase 15's evaluation gate, narrow the wording to what shipped).
  - Correct "Services consume `ApplicationDbContext` directly — no repository wrapper" — Phase 3 inverted that. The correct shape is: services consume repositories; repositories own DbContext access; `IUnitOfWork` owns the persistence boundary.
  - Update the data-model snippet so it reflects the model in force after the Phase 6 Mesocycle/TrainingPlan decision.
  - Update the API overview table to match current routes.
- Committed dev SQL password in `api/appsettings.development.json` rotated out: either replaced with LocalDB / Trusted_Connection defaults, or replaced with a placeholder requiring per-developer override via `dotnet user-secrets` (preferred). README documents the workflow. (If Phase 6 already did this, this task collapses to verification.)
- `appsettings.json` audited for embedded secrets or environment-specific URLs; all moved to user secrets or environment variables.
- CORS configuration distinguishes Development from Production with a documented allowlist strategy for prod origins.
- Pre-existing NuGet vulnerability warnings in `Bryk.API` triaged: each warning either patched or explicitly accepted in a `docs/security-notes.md` with rationale.
- Deployment doc at `docs/deployment.md`: how to run the API + frontend, environment-variable matrix, migration apply procedure, OAuth-provider setup.
- `docs/decisions/` ADR folder formalized; inline decisions (Mesocycle, Coaches, Auth-stack, PMC storage, chart-library, calendar-library, etc.) materialized as ADRs if not already.

**Dependencies.** None hard — can shift earlier if config drift becomes painful. Lower priority than feature phases but must land before any production-facing launch.

**Task groups.**
1. **README rewrite.** Surgical — keep tone, fix facts.
2. **Secrets/config audit + user-secrets migration (or verification).**
3. **CORS + security headers + HTTPS redirection sanity.**
4. **NuGet vulnerability triage.**
5. **Deployment doc + ADR backfill.**

---

## Phase 15 — v1 cutover: integration seams, observability, polish ⏳

**Goal.** Cross the v1 finish line. Anything that has to ship for the product to be "complete enough to invite real athletes" lives here.

**Success criteria.**
- **Observability.** Structured logging (Serilog or `Microsoft.Extensions.Logging` with a JSON sink — decide explicitly), correlation IDs through requests, at minimum INFO coverage of service-layer happy paths and ERROR coverage of caught exceptions in middleware. Sr. Dev approval before adding a logging dependency.
- **Tech-debt mop-up.** Items 6, 8, 9, 10, 11 from `CLAUDE.md` revisited: `NotImplementedException` → 501, `DbUpdateException` / unique-constraint → 409 with diagnostics, RFC 7807 ProblemDetails *if and only if* external consumers are imminent, multi-version Swagger doc generation if v2 has shipped, remaining NuGet warnings.
- **Integration seams (read-only first).** Per the parity doc, Garmin/Wahoo/Apple Health two-way sync is post-v1, but a read-only file import (`.fit`, `.tcx`, `.gpx`) into the Phase 9 completion flow is achievable here. Treat as a Sr. Dev approval gate (new parser dependency, payload size limits, validation).
- **Performance pass.** Spot-check the heaviest endpoints under realistic data volume (calendar range, PMC range, ATP recompute). Add `.AsNoTracking()` / `.AsSplitQuery()` where missing. Decide whether any query crosses into Dapper territory — Sr. Dev approval if it does.
- **Final smoke matrix.** End-to-end onboarding → plan creation → workout execution → calendar → PMC. Documented in the v1 cutover handoff.
- **Coach decision enacted.** Whatever Phase 6 decided is honored — either a minimum coach surface ships now, or the parity doc's coach section is fully tagged `v2`.

**Dependencies.** Everything prior.

**Task groups.**
1. **Logging + correlation IDs.** Sr. Dev approval on logging stack.
2. **Tech-debt mop-up.** One commit per item.
3. **File-import seam.** `.fit`/`.tcx`/`.gpx` parser via a vetted library (Sr. Dev approval), wired into Phase 9 completion.
4. **Performance pass.** No-tracking audit, split-query audit, optional Dapper escapes.
5. **Coach decision enactment (or formal v2 deferral).**
6. **v1 cutover handoff.** Written, committed, links to ADRs and the final test/build report.

---

## Cross-phase risks & pending decisions

These are durable concerns that span multiple phases. Each is owned by the phase noted; raising any earlier is welcome if the situation warrants.

- **Real authentication is deferred (owner: Phase 12).** The dev stub `ICurrentUserService` works because nothing currently distinguishes one athlete from another at the network boundary. The moment two real users exist, this is a critical incident waiting to happen.
- **Test coverage is newly bootstrapped but still shallow (owner: Phase 6).** Branch `phase-6-test-infra-tech-debt` adds backend xUnit/API tests, frontend Vitest tests, and CI. Coverage is intentionally smoke-level; keep resisting Phases 7–11 until Phase 6 decisions and remaining hygiene tasks land.
- **Mesocycle vs TrainingPlan (owner: Phase 6 decision; Phase 7 enacts).** Until the decision is locked, do not extend the Mesocycle model with new features — additions compound the eventual migration cost.
- **Coaches as a first-class user role (owner: Phase 6 decision).** Several parity features depend on this. No coach-facing scope work until v1/v2/out-of-scope is decided.
- **README drift (owner: Phase 14).** README currently implies direct DbContext usage, an Electron shell, SQLite/MySQL providers, and AI providers — none match current code. Treat README as historical, not authoritative, until Phase 14.
- **Plaintext dev SQL credentials (owner: Phase 6; Phase 14 verifies).** `api/appsettings.development.json` contains plaintext credentials. Dev-only label limits blast radius but file is committed.
- **`MesocycleService` layer violation (owner: Phase 6).** In `Bryk.Infrastructure/Services/` instead of `Bryk.Application/Services/`. Task 6-4 discovery found direct `ApplicationDbContext` usage and no `IMesocycleRepository`, so the fix needs a repository-boundary plan before implementation. See `Phase 6-Task4-handoff.md`.
- **Aspirational README claims — Electron / SQLite / MySQL / AI providers (owner: Phase 14, with optional Phase 15 build-vs-drop decision).** None implemented. Either build (post-v1) or strip the claims; don't leave the gap open.
- **Bleeding-edge frontend tooling (ongoing).** Vite 8, Tailwind 4, pre-release codegen dependencies. Pin and audit during Phase 14's dependency sweep; expect occasional churn from upstream releases.
- **Hardcoded `SwaggerDoc("v1")` (owner: Phase 14, or sooner if v2 ships).** Tech debt item 10. TODO already in place.

---

## After v1 (not phases — parking lot)

Tracked in `docs/product/feature-parity-trainingpeaks.md`. When a candidate gets scoped, fold it back into this roadmap as a new phase entry and update the parity doc's status tag. Current high-likelihood post-v1 candidates:

- Device sync (Garmin / Wahoo / Apple Health / Coros / Suunto / Polar) two-way.
- Coach surfaces — dashboard, athlete roster, workout/plan libraries, group calendars (gated on the Phase 6 coach decision).
- Compliance color coding on the calendar.
- Peak Performances (auto-medal personal bests).
- StackUp-style benchmarking.
- Health and recovery integrations (Whoop, Oura).
- Account tiers (Free / Premium).
- Indoor virtual training platform (separate product effort; `deferred`).
- Marketplace / Coach Match revenue features (`deferred`).
- Electron desktop shell + SQLite/MySQL provider alternates (build-vs-drop decision).
- AI-provider integration for plan recommendations / workout analysis (build-vs-drop decision).

---

## How to use this roadmap

- Read it at the start of each session alongside `CLAUDE.md` and the latest handoff in `docs/handoffs/`.
- When opening a phase: re-read the phase entry, confirm dependencies are satisfied, sanity-check the success criteria against current repo state with `git log`, file reads, and `dotnet build`.
- Each task group seeds one or more Cursor prompts. When writing the prompt, copy the relevant success criteria into the prompt's "verify" section verbatim — don't re-derive.
- When a phase ships: update the ledger table at the top, mark the phase ✅, and write `docs/handoffs/YYYY-MM-DD-phase-N-complete.md` capturing what shipped, what changed in the decisions list, and what the next phase should do first.
- When scope shifts (a candidate gets promoted, a phase gets resized): edit this file. The roadmap is a living document; it loses value the moment it drifts from intent.
- Roadmap edits are commits like any other: `docs: roadmap — mark Phase 5 complete`. Never bundled with feature work.
