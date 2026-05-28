# Task 7-5 — Secrets hygiene (dotnet user-secrets migration)

## Goal
Remove plaintext credentials from `api/Bryk.API/appsettings.Development.json`. Adopt `dotnet user-secrets` as the per-developer mechanism for `ConnectionStrings:DefaultConnection` and `DevAuth:CurrentAthleteId`. Document the workflow so new contributors aren't blocked. Delete the orphan root-level `api/appsettings.development.json` while we're here.

## Current code/status
- `api/Bryk.API/appsettings.Development.json` currently contains:
  - `ConnectionStrings:DefaultConnection` (Trusted_Connection-style; less sensitive than the orphan file but still environment-specific and committed).
  - `DevAuth:CurrentAthleteId` (the dev-stub athlete GUID used by `CurrentUserService`).
- `api/appsettings.development.json` (root-level, orphan) contains a sa-account SQL connection string with the plaintext password `Techno100!`. **This file is not read at runtime** — ASP.NET reads the one next to `Program.cs`. Confirmed during the 2026-05-26 dashboard smoke session. It's dead code that confuses anyone looking for the active config file. Delete it.
- `CurrentUserService` (`api/Bryk.Infrastructure/Services/CurrentUserService.cs`) reads `IConfiguration["DevAuth:CurrentAthleteId"]` — transparently sees user-secrets in Development without code changes.
- `ApplicationDbContext` reads `IConfiguration.GetConnectionString("DefaultConnection")` — same transparent behavior.
- The test fixture `api/Bryk.API.Tests/Fixtures/BrykWebApplicationFactory.cs` overrides `DevAuth:CurrentAthleteId` with its own GUID via in-memory configuration — unaffected by this task.
- README does not currently document a per-developer setup flow for secrets.

## Acceptance criteria

**Initialize user-secrets:**
- `dotnet user-secrets init --project api/Bryk.API/Bryk.API.csproj` run. This adds a `<UserSecretsId>` element to the csproj — committed change. The value is a fresh GUID; nothing sensitive about the ID itself.

**Move secrets out of source:**
- `ConnectionStrings:DefaultConnection` and `DevAuth:CurrentAthleteId` configured locally via `dotnet user-secrets set ...`. **Local-only**; do not commit values anywhere in the repo.
- `api/Bryk.API/appsettings.Development.json` updated to one of:
  - **(a)** trimmed to only the `Logging` block (no `ConnectionStrings`, no `DevAuth`).
  - **(b)** removed entirely if the inherited `Logging` defaults from `appsettings.json` are acceptable for Development.
  - **Recommendation: (a)** — Logging defaults for Development (Debug + EF Core Information) are genuinely different from Production and worth keeping in the file.

**Delete the orphan:**
- `api/appsettings.development.json` deleted. Confirm with `git status` it's gone.

**Verify ASP.NET wiring:**
- `api/Bryk.API/Program.cs` should require **no code change** — `WebApplicationBuilder` already merges user-secrets in Development when a `UserSecretsId` is set. Confirm this is true; do not add a manual `builder.Configuration.AddUserSecrets(...)` call.

**Documentation:**
- `README.md` gains a "Per-developer setup" section (or extends an existing setup section) covering:
  - That user-secrets is now the source of truth for Development secrets.
  - The exact commands to populate the two required keys:
    ```
    cd api/Bryk.API
    dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your-connection-string>"
    dotnet user-secrets set "DevAuth:CurrentAthleteId" "<any-GUID>"
    ```
  - That the API throws on startup outside Development if `DevAuth:CurrentAthleteId` is missing — this is by design (see `CurrentUserService`).
  - A quick-reference command to view what's set: `dotnet user-secrets list`.

**Build / smoke:**
- `dotnet build api/Bryk.sln` green.
- `dotnet test api/Bryk.sln` green — fixture override path unaffected.
- API boots locally after the change; existing wizard / dashboard flow works.

## Files likely to change/add
- `api/Bryk.API/Bryk.API.csproj` — `<UserSecretsId>` added by init.
- `api/Bryk.API/appsettings.Development.json` — trimmed (or deleted).
- `api/appsettings.development.json` — deleted.
- `README.md` — per-developer setup section.

## What NOT to modify
- Do not add a new NuGet package — `Microsoft.Extensions.Configuration.UserSecrets` is implicit in ASP.NET 10's Web SDK; no `<PackageReference>` change required.
- Do not change `CurrentUserService` — `IConfiguration["DevAuth:CurrentAthleteId"]` already transparently sees user-secrets.
- Do not touch `appsettings.json` (the Production-facing one).
- Do not touch `api/Bryk.API.Tests/Fixtures/BrykWebApplicationFactory.cs` — test fixture's in-memory config override is intentional isolation.
- Do not pre-emptively configure production secrets — that's Phase 14 / Phase 16.
- Do not commit local user-secrets values anywhere in the repo (including the README — give the pattern, not your value).

## Test plan
1. After `dotnet user-secrets init` + both `dotnet user-secrets set` calls, run the API locally. The dashboard "Welcome back" / wizard flow should work identically to before.
2. Temporarily remove `DevAuth:CurrentAthleteId` from user-secrets (`dotnet user-secrets remove`) and confirm `CurrentUserService` throws on the first request that hits it — proves the error path still works.
3. Re-set the key; confirm API works again.
4. `dotnet test api/Bryk.sln` green — fixture override path unaffected.
5. Walk a fresh contributor (or simulate via a clean clone in a sibling directory) through the README's per-developer setup section verbatim. They should boot the API and run the wizard without help from another doc.
6. `git diff` and `git status` confirm only the named files are touched / deleted, and that user-secrets values are NOT in any tracked file.

## Sequencing notes
- Independent of Task 7-4 (Mesocycle deletion). Either order works.
- If Task 7-4's migration is being applied around the same time, make sure user-secrets `DefaultConnection` is populated first — `dotnet ef database update` reads the same `IConfiguration`.
- Task 7-1 (Phase 5 handoff) is fully independent.
