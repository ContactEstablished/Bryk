# Task 6-1 — .NET test infrastructure (xUnit + WebApplicationFactory)

## Goal
Stand up the first xUnit projects under `api/` so Phase 6 has a real safety net before any Phase 7 data-model work. Land a passing service-level unit test and a passing integration test against `OnboardingController`, plus a documented decision on test-DB strategy. Coverage breadth comes later in Tasks 6-2 and 6-3 — this task is about establishing the surface.

## Current code/status
- No tests exist anywhere in the repo. `api/Bryk.sln` contains only the four production projects (`Bryk.Domain`, `Bryk.Application`, `Bryk.Infrastructure`, `Bryk.API`).
- `OnboardingController` (Phase 4) exposes `GET /api/v1/onboarding/status`, `POST /required`, `POST /recommended`, `POST /goals`. State-machine semantics are locked in `md/handoffs/2026-04-29-phase-4-complete.md`.
- `OnboardingService` (`api/Bryk.Application/Onboarding/OnboardingService.cs`) uses the locked FluentValidation pattern (`await validator.ValidateAsync(request, ct)` then throw `Bryk.Application.Exceptions.ValidationException`). Identity comes from `ICurrentUserService` — dev stub reads `DevAuth:CurrentAthleteId` from `appsettings.Development.json` and throws outside Development.
- API versioning is strict (`AssumeDefaultVersionWhenUnspecified = false`); routes are `api/v1/...`.
- Global exception middleware maps `ValidationException` → 400 with the JSON shape documented in `md/Tasks-5-1.md`.

## Test-DB strategy — decision required in this task
Pick one and document the choice in an XML doc comment at the top of the base fixture (and reference it from the Task 6-6 ADR work). Options:
- **EF Core InMemory provider.** Fast, no infrastructure. Diverges from SQL Server semantics (no real constraints, no unique-index enforcement, no transactions). Adequate for service-layer unit tests; weak for integration tests that exercise persistence-boundary behavior.
- **SQL Server LocalDB.** Real provider, free on Windows dev boxes, painful on Linux/WSL. Matches production semantics; CI needs Windows runners or a different fallback.
- **Testcontainers (`Testcontainers.MsSql`).** Spins up a real SQL Server container per test run. Best fidelity. Adds Docker as a CI/dev dependency.
- **SQLite in-memory with EF provider switch.** Cheap and cross-platform; still diverges from SQL Server in collation, constraint, and migration behavior.

Recommendation to discuss with Sr. Dev before locking: **Testcontainers** for `Bryk.API.Tests` integration; **InMemory** for `Bryk.Application.Tests` service-level unit tests. Document the rationale and the trade-off in the base fixture file plus the Task 6-6 ADR.

## Acceptance criteria
- Two new test projects added to `api/Bryk.sln`:
  - `api/Bryk.Application.Tests/Bryk.Application.Tests.csproj` (xUnit) — service-layer unit tests.
  - `api/Bryk.API.Tests/Bryk.API.Tests.csproj` (xUnit + `Microsoft.AspNetCore.Mvc.Testing`) — integration tests using `WebApplicationFactory<Program>`.
- `Program.cs` made test-discoverable (e.g., `public partial class Program {}` at file end if needed). Do not change runtime behavior.
- Base test fixture in `Bryk.API.Tests/Fixtures/` constructs the `WebApplicationFactory`, swaps the test DB per the chosen strategy, and seeds the dev-stub `ICurrentUserService` so `DevAuth:CurrentAthleteId` resolves to a known Guid for tests.
- At least one passing service-level unit test: e.g., `OnboardingService.SubmitRequiredAsync` throws `ValidationException` on a missing required field.
- At least one passing integration test against `OnboardingController`: e.g., `GET /api/v1/onboarding/status` returns 200 and the three boolean flags for a fresh athlete (all false), plus a `POST /required` happy-path round-trip that flips `RequiredComplete` to true.
- `dotnet test` from repo root (or `api/`) succeeds locally. Test count and timing captured in the commit message.
- Sr. Dev approval obtained before committing the package additions (new NuGet refs are an approval gate per CLAUDE.md): `xunit`, `xunit.runner.visualstudio`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.InMemory` and/or `Testcontainers.MsSql`, plus `FluentAssertions` if we adopt it (recommend yes; flag in the approval request).

## Files likely to change/add
- `api/Bryk.sln` — add test project entries.
- `api/Bryk.Application.Tests/Bryk.Application.Tests.csproj` (new).
- `api/Bryk.Application.Tests/Onboarding/OnboardingServiceTests.cs` (new) — one unit test.
- `api/Bryk.API.Tests/Bryk.API.Tests.csproj` (new).
- `api/Bryk.API.Tests/Fixtures/BrykWebApplicationFactory.cs` (new).
- `api/Bryk.API.Tests/Onboarding/OnboardingControllerTests.cs` (new) — one integration test.
- `api/Bryk.API/Program.cs` — only if `partial class Program` shim is required for `WebApplicationFactory<Program>`. No behavior change.

## What NOT to modify
- Do not change production service or controller logic.
- Do not introduce a CI workflow file — that belongs to Task 6-3.
- Do not start the Vue test setup — that is Task 6-2.
- Do not touch tech-debt items (`MesocycleService` move, `ValidatorPlaceholder` rename, validation extension method, CS8604) — Task 6-4 owns those.
- Do not touch `appsettings.development.json` secrets — Task 6-5 owns that.
- Do not add tests beyond the two required to prove the infrastructure works; broader coverage is for Phase 7+ as new code lands.

## Test plan
1. `dotnet build api/Bryk.sln` succeeds.
2. `dotnet test api/Bryk.sln` runs and both new test projects report ≥1 passing test, 0 failures.
3. Manually break one assertion locally and confirm `dotnet test` fails red, to prove the fixture is actually executing the code.
4. Review the base fixture doc comment to confirm the test-DB strategy is recorded.
5. Confirm `git diff` touches only test infrastructure files plus the `Program.cs` shim (if needed) and the solution file.
