# Task 6-4 — Tech-debt sweep (items 3, 4, 5, 7)

## Goal
Address four named tech-debt items from `CLAUDE.md` that have accumulated since Phase 3. Each lands as its own commit per the one-logical-change-per-commit rule. No drive-by cleanup — touch only what each named item requires.

Items in scope:

- **#3** — `MesocycleService` moved from `Bryk.Infrastructure/Services/` to `Bryk.Application/Services/` (layer violation fix).
- **#4** — `ValidatorPlaceholder` anchor type replaced with a named marker.
- **#5** — Verbose validation pattern simplified (extension method **or** migrate to `FluentValidation.ValidationException` + middleware handler — pick one).
- **#7** — `MesocycleValidators.cs` CS8604 nullability warning cleared.

## Current code/status
- `MesocycleService` lives at `api/Bryk.Infrastructure/Services/MesocycleService.cs`. Known layer violation called out in CLAUDE.md tech-debt item 3 and the Phase 2 retrospective in ROADMAP.md. Mesocycle entities themselves are unrelated to the upcoming TrainingPlan decision — moving the service is safe regardless of Task 6-6's outcome (the file will either be retired or kept; either way it should live in the correct layer first).
- `ValidatorPlaceholder` is the FluentValidation DI anchor introduced in Phase 3 task group 3 — a code smell flagged at the time. Find it under `Bryk.Application/` or wherever `AddValidatorsFromAssemblyContaining<...>` is registered.
- Validation pattern is currently three lines at each call site per the locked CLAUDE.md convention:
  ```csharp
  var validationResult = await validator.ValidateAsync(request, ct);
  if (!validationResult.IsValid)
      throw new Bryk.Application.Exceptions.ValidationException(validationResult.Errors);
  ```
  Used across `OnboardingService.cs` (four call sites) and `MesocycleService.cs` (multiple). `ValidateAndThrowAsync` is explicitly forbidden today because global middleware does not handle `FluentValidation.ValidationException`.
- `MesocycleValidators.cs` has a CS8604 (possible null reference argument) warning. Read the actual file at task time — the line and exact nullability gap drives the fix.
- After Task 6-1 lands, tests exist. Each commit in this sweep must keep `dotnet test` green; if behavior shifts unexpectedly, stop and re-think rather than papering over.

## Acceptance criteria
- **One commit per item.** Four commits total in this task. Conventional prefixes per CLAUDE.md:
  - `refactor: move MesocycleService to Bryk.Application` (item 3)
  - `refactor: replace ValidatorPlaceholder with named marker` (item 4)
  - `refactor: simplify FluentValidation call pattern` (item 5)
  - `fix: clear CS8604 in MesocycleValidators` (item 7)
- **Item 3 (MesocycleService move).** File moves to `api/Bryk.Application/Services/MesocycleService.cs`. Namespace updated. DI registration updated in `Program.cs` if the type lookup was namespace-bound. Any `using` statements in callers updated. No behavioral change. `dotnet build` and `dotnet test` green. If the service touches `DbContext` directly (likely — that's why it ended up in Infrastructure), it must now route through a repository per the locked pattern. **This is the risky part of the sweep.** If the repository surface doesn't exist for what `MesocycleService` does, stop and write a separate prompt to add the repository methods first; do not bundle.
- **Item 4 (ValidatorPlaceholder).** Replace with a named marker like `ApplicationValidatorMarker` (or whatever idiom Sr. Dev prefers — confirm naming before writing the prompt). Update the `AddValidatorsFromAssemblyContaining<...>` call. Delete the old `ValidatorPlaceholder` type.
- **Item 5 (validation pattern).** Pick one of:
  - **Extension method** (`ValidateOrThrowAsync` or similar) under `Bryk.Application/Common/Validation/`. Each call site collapses to one line. The custom `Bryk.Application.Exceptions.ValidationException` is still the thrown type — middleware mapping does not change.
  - **Middleware handler addition** that maps `FluentValidation.ValidationException` to the same JSON shape the custom exception currently produces, then call sites switch to `validator.ValidateAndThrowAsync(...)`. This deletes the custom exception type entirely.
  Recommendation to discuss with Sr. Dev: **extension method**. Keeps the locked custom-exception convention from Phase 3 intact, doesn't touch middleware, and is a smaller surgical change. The middleware-handler option is cleaner long-term but expands blast radius beyond a tech-debt sweep.
- **Item 7 (CS8604).** Read `MesocycleValidators.cs`, fix the nullability gap at the warned line. The fix is whatever the compiler is telling you — usually an explicit null guard or a `!` operator with justification. Do not silence the warning with a `#pragma`.
- Validation behavior unchanged after item 5: same exception type reaches middleware, same JSON 400 response shape goes back to the client (the shape documented in `md/Tasks-5-1.md`). If the chosen approach would change the response shape, stop — that is a Phase 5/UI breaking change and out of scope.
- `dotnet build api/Bryk.sln` clean (zero new warnings introduced; CS8604 in `MesocycleValidators.cs` cleared).
- `dotnet test api/Bryk.sln` green after each commit. Use the test infrastructure from Task 6-1.
- Manual smoke of one onboarding POST after the sweep (e.g., `POST /api/v1/onboarding/required` with a deliberately invalid payload) returns the same 400 JSON shape it did before. Capture the before/after response bodies in the commit message for item 5.

## Files likely to change/add
- `api/Bryk.Application/Services/MesocycleService.cs` (moved; new path).
- `api/Bryk.Infrastructure/Services/MesocycleService.cs` (deleted).
- `api/Bryk.API/Program.cs` (DI updates if namespace lookup is involved).
- `api/Bryk.Application/Common/Validation/ApplicationValidatorMarker.cs` (new, item 4 — rename TBD).
- Deletion of the file/type previously known as `ValidatorPlaceholder`.
- `api/Bryk.Application/Common/Validation/ValidationExtensions.cs` (new, item 5 — only if extension-method route is chosen).
- Call sites in `api/Bryk.Application/Onboarding/OnboardingService.cs` and `api/Bryk.Application/Services/MesocycleService.cs` (item 5 — updated to the new pattern).
- `api/Bryk.Application/Mesocycles/Validators/MesocycleValidators.cs` (or wherever `MesocycleValidators.cs` actually lives — verify path at task time).
- Possibly `api/Bryk.Application/Bryk.Application.csproj` and `api/Bryk.Infrastructure/Bryk.Infrastructure.csproj` if `MesocycleService` had any project-level dependencies that no longer apply post-move.

## What NOT to modify
- Do not touch other CLAUDE.md tech-debt items (1, 2, 6, 8, 9, 10, 11). Items 1 (`OperationCanceledException`) and 2 (test coverage) are already addressed elsewhere; items 6/8/9/10/11 are Phase 15 mop-up.
- Do not modify the locked `Bryk.Application.Exceptions.ValidationException` JSON response shape, unless item 5 explicitly chooses the middleware-handler route — and in that case, the response shape must remain identical, byte-for-byte, with the Phase 5 UI's expectations (see `md/Tasks-5-1.md`).
- Do not refactor `OnboardingService` beyond switching to the new validation idiom.
- Do not change repository contracts or `IUnitOfWork`. If `MesocycleService` post-move requires new repository methods to escape `DbContext` access, stop and design those separately — not in this sweep.
- Do not bundle the Mesocycle decision (Task 6-6) into this sweep. The layer fix is independent of supersede/integrate/coexist.
- Do not touch the dev secrets in `appsettings.development.json` — Task 6-5 owns that.
- Do not touch CI workflow or test infrastructure files — Tasks 6-1, 6-2, 6-3 own those.

## Approval gates / open questions
- **Approval gate (item 3):** if `MesocycleService` accesses `DbContext` directly today, the move surfaces a persistence-boundary question — Sr. Dev approval required before adding repository methods to support the move. Identify this before writing the prompt; if confirmed, split into two prompts: "add repository surface for X" then "move service".
- **Approval gate (item 5):** the extension-method-vs-middleware-handler choice is a cross-cutting decision. Sr. Dev signs off before the prompt is written.
- **Decision question (item 4):** name of the new marker type. Recommendation: `ApplicationValidatorMarker` (matches the `Bryk.Application` namespace), but Sr. Dev may have a preferred convention.
- **Open question:** does the move in item 3 force any new `using Bryk.Domain;` imports in `Bryk.Application` that weren't there before? Likely not, but verify — `Bryk.Application` already depends on `Bryk.Domain`, so this should be a no-op.

## Test plan
1. After each commit: `dotnet build api/Bryk.sln` (warnings count unchanged or lower; CS8604 in `MesocycleValidators.cs` gone after item 7), `dotnet test api/Bryk.sln` (green).
2. After item 3: confirm `MesocycleService` resolves via DI on app startup (`dotnet run --project api/Bryk.API` succeeds and the host serves `/api/v1/...` routes).
3. After item 5: send one invalid request to `POST /api/v1/onboarding/required`, capture the response body, diff against the same request's response on `main` before this task. Bodies must match exactly.
4. After item 7: `dotnet build` shows zero CS8604 warnings in `MesocycleValidators.cs`.
5. Confirm `git log` shows four separate commits with the conventional prefixes above. No squash-merge of the four into one.
