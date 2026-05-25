# Task 6-3 — CI pipeline (build + test gate on every push)

## Goal
Wire a single GitHub Actions workflow that runs `dotnet build`, `dotnet test`, the frontend build, and `pnpm test` on every push and PR. A red run blocks merge. Establishes the automated quality gate the project has been operating without since Phase 1.

## Current code/status
- No `.github/` directory exists. No CI of any kind today; "verify" has meant local build + manual smoke.
- After Task 6-1 lands: `api/Bryk.sln` will contain `Bryk.Application.Tests` and `Bryk.API.Tests`. `dotnet test api/Bryk.sln` will be the canonical invocation.
- After Task 6-2 lands: `ui/package.json` will have a `test` script (`vitest run`). `pnpm test` will be canonical from `ui/`.
- Repo currently builds against .NET 10 (preview SDK). Confirm the GitHub-hosted runner image carries it; if not, `actions/setup-dotnet` must pin the correct preview channel.
- Frontend uses pnpm 10.33.2 (declared in `ui/package.json` `packageManager`). `pnpm/action-setup` honors that field.
- `api/appsettings.development.json` currently contains a plaintext SQL connection string targeting a developer machine (`[REDACTED]`). Tests running on CI must not require this — Task 6-1's test-DB strategy (InMemory or Testcontainers) is what makes CI viable. Verify Task 6-1 landed before this task runs.

## Acceptance criteria
- Single workflow file at `.github/workflows/ci.yml`. Triggers: `push` to any branch and `pull_request` targeting `main`. Concurrency group keyed on the ref so duplicate pushes cancel in-flight runs.
- Two jobs (or one matrix job with two legs — pick whichever reads more clearly; one-per-stack is recommended for simpler log reading):
  - **`backend`** — `ubuntu-latest`. Steps: checkout → `actions/setup-dotnet` (pinned to the .NET 10 channel) → `dotnet restore api/Bryk.sln` → `dotnet build api/Bryk.sln --no-restore --configuration Release` → `dotnet test api/Bryk.sln --no-build --configuration Release --logger "trx;LogFileName=test_results.trx"` → upload TRX artifact on failure.
  - **`frontend`** — `ubuntu-latest`. Steps: checkout → `pnpm/action-setup` (reads `packageManager` from `ui/package.json`) → `actions/setup-node` with pnpm cache pointing at `ui/pnpm-lock.yaml` → `pnpm install --frozen-lockfile` (run from `ui/`) → `pnpm build` → `pnpm test`.
- If Task 6-1 selected Testcontainers, the `backend` job needs Docker available. `ubuntu-latest` runners include Docker out-of-the-box; verify by reading the GitHub docs at workflow time and call this out in the PR description. If Task 6-1 picked InMemory, no Docker dependency is required and this paragraph collapses to a single sentence in the workflow comment.
- Workflow surfaces failure logs clearly: do not swallow stderr, do not `continue-on-error: true`, do not gate on `if: success()` for downstream test-report steps unless the failure is still readable in the run summary.
- **Branch protection on `main`** updated to require both `backend` and `frontend` checks to pass before merge. Branch protection lives in GitHub settings, not in a file — call this out as a separate manual step in the PR description and confirm with Sr. Dev who applies it. Do not attempt to apply branch protection programmatically.
- First green run captured: open a no-op PR (or push a no-op commit) after the workflow lands and confirm both jobs go green. Capture the run URL in the commit message or PR description.
- Failure path verified at least once: temporarily break a test locally, push, watch the workflow go red, fix, re-push. Documented in the commit message or follow-up note.

## Files likely to change/add
- `.github/workflows/ci.yml` (new).
- Possibly `.github/dependabot.yml` (new, optional) — if Sr. Dev wants Dependabot wired in the same task. Recommendation: defer to Phase 14 dependency sweep; this task is about the test gate, not dependency automation.
- No source code changes expected. If a `Program.cs` shim or `appsettings.Testing.json` is required for CI-compatible test execution, that belongs to Task 6-1, not here.

## What NOT to modify
- Do not change application code, test code, or build configuration to make CI "work" — if CI surfaces a real problem, that's a separate task.
- Do not add a deployment workflow, release workflow, container build, or any non-CI automation. v1 cutover (Phase 15) decides production delivery.
- Do not enable Codecov, SonarCloud, or any third-party CI integration without explicit approval. Coverage reporting is optional Task 6-2 plumbing; CI surfacing of coverage is a separate decision.
- Do not gate CI on lint or formatting — neither is established in the project today. Phase 14 may add either; this task does not.
- Do not modify `api/appsettings.development.json` to placate CI. Real fix lives in Task 6-5; Task 6-1's test fixture should already insulate tests from the dev connection string.
- Do not commit workflow secrets or set up GitHub repository secrets without approval; this workflow should require none.

## Approval gates / open questions
- **Approval gate:** new CI service surface (GitHub Actions) — even though the platform is the obvious default, surfacing it falls under "cross-cutting concerns" per CLAUDE.md. Confirm with Sr. Dev before merge.
- **Approval gate:** branch protection rule additions on `main`. Owner of GitHub admin settings must apply these by hand.
- **Decision question:** pin .NET SDK version explicitly (e.g., `10.0.x` channel) vs pin to a single build number. Recommendation: pin to channel — preview-channel churn is too high to lock to a single build, and `global.json` (if it exists) already constrains local dev.
- **Decision question:** require Docker availability for tests (Testcontainers) — depends on Task 6-1's outcome. Re-read the Task 6-1 commit before writing this workflow.
- **Open question:** caching strategy. `actions/setup-dotnet` and `actions/setup-node` both support built-in package caching. Recommendation: enable both; first cold run will be slow, every subsequent run is fast.

## Test plan
1. Open a draft PR with the workflow file. Confirm both jobs trigger automatically.
2. Confirm `backend` job runs `dotnet build` + `dotnet test` and reports green.
3. Confirm `frontend` job runs `pnpm install` + `pnpm build` + `pnpm test` and reports green.
4. Push a deliberate failing assertion (separate commit on the branch) and confirm the workflow goes red. Revert before merge.
5. Confirm branch protection rules on `main` require both checks (manual verification in GitHub settings; ask Sr. Dev to apply if not the owner).
6. Confirm `git diff` for this task touches only `.github/workflows/ci.yml`.
