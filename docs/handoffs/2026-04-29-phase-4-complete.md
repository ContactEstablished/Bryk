# HANDOFF — Bryk Project, Phase 4 → Phase 5

## Context

Phase 4 (Onboarding API + DTOs) is complete. The project is transitioning from the Claude.ai-as-architect / Cursor-as-executor workflow to a Claude Code-as-architect / Cursor-as-executor workflow. The new `CLAUDE.md` at the repo root captures the workflow and conventions.

## What shipped in Phase 4

End-to-end onboarding API:

- `IUnitOfWork` abstraction + `UnitOfWork` implementation. Single persistence boundary.
- Repositories no longer commit. They stage via `AddAsync` / `Update` / `Delete`; services commit once via `IUnitOfWork.SaveChangesAsync`.
- `ICurrentUserService` dev stub at `Bryk.Application/Common/ICurrentUserService.cs` and `Bryk.Infrastructure/Services/CurrentUserService.cs`. Reads `DevAuth:CurrentAthleteId` from `appsettings.Development.json`. Throws outside Development.
- Onboarding DTOs at `Bryk.Application/Onboarding/`: `OnboardingRequiredRequest`, `OnboardingRecommendedRequest`, `OnboardingGoalsRequest`, `OnboardingStatusResponse`, plus nested `SportThresholdsDto`, `EventDto`, `GoalDto`.
- FluentValidation validators at `Bryk.Application/Onboarding/Validators/`. Six validator classes covering all request DTOs.
- `IOnboardingService` + `OnboardingService` at `Bryk.Application/Onboarding/`. Four methods: `SubmitRequiredAsync`, `SubmitRecommendedAsync`, `SubmitGoalsAsync`, `GetStatusAsync`. Validation done inside the service via `ValidateAsync` + custom `Bryk.Application.Exceptions.ValidationException`.
- API versioning wired via `Asp.Versioning.Mvc` 10.0.0 + `Asp.Versioning.Mvc.ApiExplorer` 10.0.0. URL segment primary, header secondary (`api-version`), strict mode (`AssumeDefaultVersionWhenUnspecified = false`), `ReportApiVersions = true`. Default version 1.0.
- All five controllers (four legacy retrofitted, OnboardingController new) use `[ApiVersion("1.0")]` + `[Route("api/v{version:apiVersion}/[controller]")]`.
- `OnboardingController` at `Bryk.API/Controllers/OnboardingController.cs`. Four endpoints: `GET /status`, `POST /required`, `POST /recommended`, `POST /goals`. POSTs return 204; GET returns 200 with `OnboardingStatusResponse`.

## State machine semantics (locked)

- **Required step:** upsert. First call creates the Athlete row; subsequent calls update the eight fields. Athlete ID comes from `ICurrentUserService`, not the request.
- **Recommended step:** upsert by sport. For each submitted `SportThresholdsDto`, find existing row by `(AthleteId, Sport)` → update if found, create if not. Profiles for sports omitted from the request are left untouched. HR fields on Athlete updated alongside. Throws `InvalidOperationException` if Athlete row doesn't exist (recommended before required) — middleware maps to 409.
- **Goals step:** append. No upsert, no replace. Multiple submissions accumulate. Goals/Events have no natural client-side key.
- **Status:**
  - `RequiredComplete` = Athlete row exists for current ID.
  - `RecommendedComplete` = at least one `AthleteSportProfile` row exists. HR-only does not count.
  - `GoalsComplete` = at least one `Event` OR at least one `Goal` exists.

## DTO design (locked)

- `SportThresholdsDto` mirrors the `AthleteSportProfile` entity shape — generic `ThresholdValue` decimal, not sport-explicit. Frontend handles per-sport semantics (FTP for bike, threshold pace for run, threshold pace per 100m for swim).
- `OnboardingRequiredRequest` carries all 8 non-nullable Athlete fields. Required step matches entity nullability.
- `OnboardingStatusResponse` is flags only — no echoed data. Edit-my-profile surface gets its own dedicated GET later.
- Equipment is out of onboarding scope. Added later via separate surface.

## Phase 4 commit history

```
<HEAD>   feat: add OnboardingController
<TBD>    feat: add IOnboardingService and OnboardingService with input validation
1d0d972  feat: wire API versioning and standardize controller routes
392d300  feat: add onboarding request validators
be3259f  feat: add onboarding DTOs
bc19671  feat: add ICurrentUserService dev stub for athlete identification
67e0930  docs: add TrainingPeaks feature parity tracker
0289316  refactor: remove SaveChangesAsync from repositories in favor of IUnitOfWork
f456940  feat: add IUnitOfWork abstraction and DI registration
```

Plus two upcoming commits:

- `chore: add CLAUDE.md for Claude Code workflow`
- `docs: add Phase 4 → Phase 5 handoff` (this file)

## Smoke test status

Pending. Endpoints to verify before declaring Phase 4 fully done:

1. `GET /api/v1/onboarding/status` returns 200 with all flags `false` (assuming `DevAuth:CurrentAthleteId` is the all-zeros placeholder).
2. `GET /api/onboarding/status` (no version) returns 400 with version-required error. Confirms strict mode.
3. `GET /api/onboarding/status` with header `api-version: 1.0` returns 200. Confirms header reader works.
4. Successful response includes `api-supported-versions: 1.0` header. Confirms `ReportApiVersions`.

If any of these fail, address before starting Phase 5.

Scalar URL: `https://localhost:<port>/scalar/v1` (port from `Bryk.API/Properties/launchSettings.json`).

## Pending decisions

Carried forward from Phase 4. None resolved this phase except auth approach (already locked at Phase 4 start).

- **Authentication:** deferred. Direction: custom signup + OAuth (Google, Apple). Strong recommendation: ASP.NET Core Identity. No production auth code without Sr. Dev approval.
- **Vue styling library:** TBD. **Decide first thing in Phase 5.**
- **Testing infrastructure:** scheduled for Phase 6. xUnit + `Microsoft.AspNetCore.Mvc.Testing` for the .NET layer; Vitest for Vue.
- **Mesocycle vs TrainingPlan:** decide at Phase 6 when the new model arrives.
- **Coaches as first-class user type:** decide before any coach-facing work.

## Tech debt (carried forward)

See `CLAUDE.md` "Tech debt" section for the full ordered list. Headlines:

1. `OperationCanceledException` returns 500 from global exception middleware. Most operationally annoying.
2. No test coverage anywhere. Phase 6 establishes infrastructure.
3. `MesocycleService` lives in `Bryk.Infrastructure/Services/` — layer violation. Phase 6 sweep.
4. `ValidatorPlaceholder` anchor type is a code smell.
5. Validation pattern is verbose (3 lines per call site).

Items 6–11 in `CLAUDE.md`. Address opportunistically.

## What Phase 5 should do first

1. **Smoke-test Phase 4** if not already done. Don't carry suspected breakage forward.
2. **Decide the Vue styling library.** Real options: Tailwind + headless UI, PrimeVue, Vuetify, Quasar. Open with a focused side-by-side comparison. Don't write Vue code first.
3. **Bootstrap the Vue project skeleton.** Vite, Pinia, Vue Router 4, the chosen styling library, the `services/` API layer with TypeScript interfaces matching the four `Onboarding*` DTOs.
4. **Build the onboarding wizard.** Three-step wizard with status check on mount to choose the landing step. Resume-friendly per the locked flow.
5. **Wire it to the API.** `services/onboarding.ts` with typed methods for the four endpoints. Validation error format from the API will reveal whether tech-debt-item-#9 (RFC 7807) needs to move up the priority list.

## What the next session should do first

1. Confirm `CLAUDE.md` is loaded by Claude Code.
2. Run `git status` and `git log --oneline -10` and confirm clean state.
3. Run `dotnet build` and confirm green.
4. Read this handoff.
5. Confirm Phase 4 smoke test is done (or do it).
6. Open the Vue styling library decision before writing any code.

## Notes on the Cursor + Claude Code workflow

- Claude Code reads code directly. No more paste-back checkpoints.
- Cursor still does most of the writing. Claude Code writes the prompts.
- Pattern A (architect-only) is the default. Pattern B (Claude Code writes mechanical code) is exception-only and named in `CLAUDE.md`.
- After each Cursor-executed prompt: Claude Code reads the diff, verifies the build, asks user to commit and paste hash.
- High-risk prompts: explicitly state what was read and what was verified.
- Verify-what-you-read discipline (CLAUDE.md Section 5): check `git status`, `git log`, actual file contents, `dotnet build` directly rather than relying on user recall or Cursor's reports.
