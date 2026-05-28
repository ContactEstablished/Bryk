# Task 6-5 — Secrets hygiene (remove plaintext dev SQL credentials)

## Goal
Remove the plaintext SQL Server credentials currently committed to `api/appsettings.development.json` and replace them with a `dotnet user-secrets` workflow per-developer. Document the workflow in the API README (or a new short developer-setup doc) so a fresh clone can stand up the database without leaking secrets again.

## Current code/status
- `api/appsettings.development.json` currently contains a plaintext developer SQL Server connection string. The exact value is intentionally redacted here as `[REDACTED]` because task files should not preserve credentials or connection strings.
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "[REDACTED]"
  }
  ```
  This is a developer-machine connection string with SQL credentials — low blast radius but committed plaintext, flagged in CLAUDE.md cross-phase risks and ROADMAP.md tech-debt items.
- `Program.cs` reads the connection string via `IConfiguration.GetConnectionString("DefaultConnection")` per CLAUDE.md convention. The configuration system already layers user secrets above `appsettings.development.json` in Development; no code changes are required to enable secrets — only to remove the committed plaintext and document the override workflow.
- `api/Bryk.API.csproj` may or may not have `<UserSecretsId>` set. Inspect at task time. If missing, `dotnet user-secrets init --project api/Bryk.API` adds it — this is a project-file edit that is safe to commit (the GUID itself is not a secret).
- No `appsettings.json` in the repo currently contains secrets per the file shape, but verify before commit.
- `git log -- api/appsettings.development.json` will show this file's history — the password is present in git history regardless of what this task does. Surfacing the rotation question is in scope; rewriting history is **not**. See approval gates below.

## Acceptance criteria
- `<UserSecretsId>` present in `api/Bryk.API/Bryk.API.csproj` (initialize via `dotnet user-secrets init --project api/Bryk.API` if missing). The generated GUID may be committed — it's an identifier, not a secret.
- `api/appsettings.development.json` no longer contains the plaintext password. Pick one of:
  - **Option A — placeholder.** Replace the `DefaultConnection` value with a placeholder like `"REPLACE_VIA_USER_SECRETS"` and have the app fail loudly at startup if the placeholder leaks into a runtime read. Requires a null/placeholder guard in `Program.cs` (one line; matches the existing `IConfiguration["KeyName"]` null-guard convention from CLAUDE.md).
  - **Option B — remove the key entirely.** Delete the `ConnectionStrings` block from `appsettings.development.json`. Startup fails until the developer runs the `dotnet user-secrets set` command. Cleaner, no placeholder string risk.
  - **Option C — LocalDB default with optional override.** Keep a working `DefaultConnection` pointing at SQL Server LocalDB (`Server=(localdb)\\MSSQLLocalDB;...Trusted_Connection=True`). Painful on WSL/Linux; only suitable if the team is Windows-only.
  Recommendation to discuss with Sr. Dev before locking: **Option B**. It is the strictest, hardest to accidentally regress, and forces every developer through the documented workflow exactly once.
- Developer-setup documentation lands in one of:
  - `api/README.md` (extend the existing file with a "Local secrets" section), or
  - `md/local-dev-setup.md` (new short doc).
  Recommendation: **`api/README.md`** if the file is short, else `md/local-dev-setup.md`. Decide at task time after reading the existing README.
- The doc explicitly lists:
  1. `dotnet user-secrets init --project api/Bryk.API` (idempotent — only needed if the csproj doesn't already have `<UserSecretsId>`).
  2. `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your-connection-string>" --project api/Bryk.API`
  3. An example connection string that works against a local SQL Server (with a clear note: replace server/user/password to match your environment; the value is stored in `~/.microsoft/usersecrets/<id>/secrets.json` and is never committed).
  4. How to verify: `dotnet user-secrets list --project api/Bryk.API`.
  5. Pointer for CI: CI tests must not require this user-secrets file (Task 6-1's test-DB strategy and Task 6-3's CI workflow handle this — confirm both are landed before this task ships).
- `appsettings.json` re-read at task time to confirm no secrets snuck into the base file.
- `dotnet build` and `dotnet run --project api/Bryk.API` from a fresh local secrets store: the app boots if secrets are set; fails with a clear error if not. Capture both outcomes in the commit message.

## Files likely to change/add
- `api/appsettings.development.json` — connection string removed or replaced per the chosen option.
- `api/Bryk.API/Bryk.API.csproj` — `<UserSecretsId>` added if missing.
- `api/Bryk.API/Program.cs` — only if Option A requires a placeholder-detection guard (one line, matches existing null-guard idiom).
- `api/README.md` (extended) **or** `md/local-dev-setup.md` (new). Pick one.

## What NOT to modify
- Do not rewrite git history to scrub the password (`git filter-repo`, `git filter-branch`, BFG). The password is a low-blast-radius dev credential on a developer-named server; the rotation question belongs to Sr. Dev, not to this task. If Sr. Dev wants history scrubbed, that's a separate operation with explicit approval, performed on a coordinated window.
- Do not touch `appsettings.json` unless it contains a secret. If it does, surface it for separate handling — do not silently relocate.
- Do not introduce a `.env` file or env-variable-based loader. CLAUDE.md explicitly forbids `Environment.GetEnvironmentVariable` for connection strings.
- Do not change the `IConfiguration` access pattern — `GetConnectionString("DefaultConnection")` with the existing null guard is the locked convention.
- Do not add Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, or any production secret-management plumbing. That belongs to Phase 14 or Phase 15 deployment hardening.
- Do not modify CI workflow or test infrastructure — Tasks 6-1, 6-2, 6-3 own those. CI should not depend on this file.
- Do not bundle README rewrites (Phase 14 task) into this task. The doc addition here is the minimal developer-setup section, not a broader README refresh.

## Approval gates / open questions
- **Approval gate:** the change is a cross-cutting concern (secrets handling) and touches startup behavior — Sr. Dev sign-off required before the prompt is written.
- **Approval gate / decision:** Option A, B, or C above. Default recommendation is B; final call belongs to Sr. Dev.
- **Open question:** rotate the `sa` password on the developer's SQL Server? Recommendation: **yes, separately, by the developer, after this commit lands.** Outside the scope of a code-only task, but worth surfacing in the PR description.
- **Open question:** the password lives in git history regardless of this commit. Sr. Dev decides whether to (a) accept history exposure (low blast radius, dev-only credentials, dev-named server), (b) rotate the password and accept history exposure, (c) rotate AND scrub history. Surface the choice; do not act on (c) without explicit instruction.
- **Open question:** does Phase 14's secrets/config audit need its scope reduced after this task lands? Recommendation: yes — `ROADMAP.md` Phase 14 task group 2 already says "or verification" if Phase 6 handled this. Leave the ROADMAP edit to the Task 6-6 handoff.

## Test plan
1. Apply the chosen option. Delete `~/.microsoft/usersecrets/<id>/secrets.json` locally (or skip the `dotnet user-secrets set` step) and run `dotnet run --project api/Bryk.API`. The app should fail with a clear, actionable error message — capture the message verbatim in the commit body.
2. Run `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your-real-connection-string>" --project api/Bryk.API`. Re-run the app. It should boot and serve `/api/v1/onboarding/status` against the developer's local SQL Server, as before.
3. Run `dotnet test api/Bryk.sln` (Task 6-1's tests). All tests must still pass — the test fixture must not require the developer's user-secrets file.
4. Trigger the Task 6-3 CI workflow on a draft PR. It must still go green — the workflow runs without any user secrets on the runner.
5. Confirm `git diff` touches only the files listed above. Confirm `git log -p -- api/appsettings.development.json` no longer shows the plaintext password on `HEAD`.
6. Re-read `appsettings.json` to confirm no secrets remain.
