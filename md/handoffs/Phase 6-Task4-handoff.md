# Phase 6 — Task 4 Handoff

## Purpose

Task 6-4 was opened as a tech-debt sweep for CLAUDE.md items 3, 4, 5, and 7. During pre-implementation discovery, item 3 hit the approval gate explicitly called out in `md/Tasks-6-4.md`, and item 4 was found to already be complete. No Task 6-4 source changes were made after that discovery.

This document captures the remaining work, questions, concerns, and recommended path so the PR can be reviewed/merged before the next plan is written.

## Current branch context

Branch: `phase-6-test-infra-tech-debt`

Phase 6 work already on this branch:

- `498f4da` — Phase 6 task files created.
- `66e1679` — Task 6-1: backend test infrastructure.
- `9d7aeda` — Task 6-2: frontend test infrastructure.
- `eb38c4d` — Task 6-3: GitHub Actions CI workflow.

Task 6-4 implementation is intentionally not included yet. The branch is ready for review as a Phase 6 infrastructure PR plus this handoff.

## Task 6-4 discovery results

### Item 3 — Move `MesocycleService` to `Bryk.Application`

Status: **blocked pending design / Sr. Dev approval**

Observed code:

- Current file: `api/Bryk.Infrastructure/Services/MesocycleService.cs`
- Current namespace: `Bryk.Infrastructure.Services`
- Constructor dependency: `ApplicationDbContext context`
- Direct EF usage includes:
  - `context.Mesocycles.AsNoTracking().ToListAsync(...)`
  - `context.Mesocycles.AsNoTracking().FirstOrDefaultAsync(...)`
  - `Include(m => m.Weeks)` for the with-weeks read path
  - `context.Mesocycles.Add(entity)`
  - `context.SaveChangesAsync(...)`
  - tracked update/delete lookups and `context.Mesocycles.Remove(entity)`

Concern:

`CLAUDE.md` says services should not access `DbContext` directly. Moving the service from Infrastructure to Application without first adding a repository surface would either fail to compile or drag Infrastructure/EF concerns into Application. That would violate the locked architecture.

Task 6-4 already predicted this exact situation:

> If the service touches `DbContext` directly ... it must now route through a repository per the locked pattern. If the repository surface doesn't exist for what `MesocycleService` does, stop and write a separate prompt to add the repository methods first; do not bundle.

Current repository state:

- There is no `IMesocycleRepository` contract.
- `IUnitOfWork` only exposes `SaveChangesAsync`.
- Existing repository pattern examples live in:
  - `api/Bryk.Domain/Interfaces/IAthleteRepository.cs`
  - `api/Bryk.Domain/Interfaces/IEventRepository.cs`
  - `api/Bryk.Domain/Interfaces/IGoalRepository.cs`
  - `api/Bryk.Domain/Interfaces/IEquipmentRepository.cs`
  - implementations under `api/Bryk.Infrastructure/Repositories/`

Recommended path:

1. Add `IMesocycleRepository` in `Bryk.Domain/Interfaces` with only the methods needed by current `MesocycleService` behavior.
2. Add `MesocycleRepository` in `Bryk.Infrastructure/Repositories` using `ApplicationDbContext`.
3. Register `IMesocycleRepository` in `api/Bryk.API/Program.cs`.
4. Refactor `MesocycleService` to consume `IMesocycleRepository` + `IUnitOfWork` instead of `ApplicationDbContext`.
5. Move `MesocycleService` to `api/Bryk.Application/Services/MesocycleService.cs` and update namespace/usings/DI.
6. Verify `dotnet build api/Bryk.sln`, `dotnet test api/Bryk.sln`, and API startup.

Recommended commit split:

- `feat: add Mesocycle repository surface`
- `refactor: move MesocycleService to Bryk.Application`

Open questions for Sr. Dev:

- Should the repository return entities, DTO-ready read models, or both? Existing repos mostly expose entities and let services map.
- Should `GetWithWeeksAsync` use a dedicated repository method with `.Include(m => m.Weeks)` or should the service request a generic include-capable query? Recommendation: dedicated method; avoid generic query abstractions.
- Should `MesocycleService` keep direct create/update mapping logic, or should repository methods encapsulate mutation details? Recommendation: keep mapping/mutation in the service for now; repository owns persistence mechanics only.
- Does the upcoming Task 6-6 Mesocycle/TrainingPlan decision make this work wasteful? Recommendation: still resolve the layer violation if the legacy service remains active before Phase 7, but if Task 6-6 decides to supersede Mesocycle immediately, consider retiring the service in Phase 7 rather than investing heavily now.

## Item 4 — Replace `ValidatorPlaceholder`

Status: **already complete before Task 6-4**

Evidence:

- Commit found in history: `d4bff8c refactor: rename ValidatorPlaceholder marker type to ApplicationAssemblyMarker`
- Current marker file: `api/Bryk.Application/Validators/ApplicationAssemblyMarker.cs`
- Current registration: `builder.Services.AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>();`
- Search result: no `ValidatorPlaceholder` references remain.

Recommended path:

No code change needed. Treat item 4 as already complete and document that Task 6-4 should not attempt a second rename.

Open question:

- Task 6-4 recommended the name `ApplicationValidatorMarker`, but the repository already uses `ApplicationAssemblyMarker`. Should we keep the current name or rename again? Recommendation: keep `ApplicationAssemblyMarker`; it is clear, already committed, and avoids churn.

## Item 5 — Simplify FluentValidation call pattern

Status: **not started; recommended approach identified**

Current pattern appears in:

- `api/Bryk.Application/Onboarding/OnboardingService.cs`
- `api/Bryk.Infrastructure/Services/MesocycleService.cs`
- other legacy Infrastructure services also use the verbose pattern, but Task 6-4 scoped only Onboarding + Mesocycle call sites.

Recommended path:

Use the extension-method approach from `md/Tasks-6-4.md`:

- Add `api/Bryk.Application/Common/Validation/ValidationExtensions.cs`.
- Define an extension such as `ValidateOrThrowAsync<T>(this IValidator<T> validator, T instance, CancellationToken ct = default)`.
- Internally call `ValidateAsync` and throw the existing custom `Bryk.Application.Exceptions.ValidationException` with the same error-message collection currently used by call sites.
- Replace scoped call sites with one-line calls.
- Do not use FluentValidation's `ValidateAndThrowAsync` unless middleware is deliberately expanded in a separate cross-cutting change.

Why this is recommended:

- Preserves the existing middleware response shape.
- Keeps the custom `Bryk.Application.Exceptions.ValidationException` convention intact.
- Avoids a cross-cutting middleware change during a tech-debt sweep.
- Should be behavior-preserving and easy to verify with existing API tests plus one invalid onboarding request smoke.

Suggested commit:

- `refactor: simplify FluentValidation call pattern`

Questions / concerns:

- Should the extension live under `Bryk.Application/Common/Validation/` or `Bryk.Application/Validation/`? Recommendation: `Common/Validation` because current common seams live under `Bryk.Application/Common/`.
- Should the extension accept `CancellationToken ct` or `cancellationToken`? Recommendation: use `cancellationToken` in the extension signature; call sites may pass their local variable names.
- Should item 5 wait until after item 3 moves `MesocycleService`? Recommendation: yes if the Mesocycle service will be touched anyway; otherwise do Onboarding first and handle Mesocycle during the move.

## Item 7 — Clear CS8604 in `MesocycleValidators.cs`

Status: **not started; root cause identified**

Build warning observed:

```text
Bryk.Application/Validators/MesocycleValidators.cs(91,53): warning CS8604: Possible null reference argument for parameter 'item' in 'bool HashSet<string>.Contains(string item)'.
```

Current code:

```csharp
When(x => x.WeeklyPatternType is not null, () =>
{
    RuleFor(x => x.WeeklyPatternType)
        .Must(x => AllowedPatterns.Contains(x))
        .WithMessage("Weekly pattern type must be one of: Polarized, Pyramidal, Sweet Spot.");
});
```

Root cause:

The outer `When` guard proves the property is non-null at runtime, but the compiler cannot prove that the lambda parameter passed into `.Must(...)` is non-null.

Recommended path:

Use an explicit null-safe predicate rather than a null-forgiving operator or pragma:

```csharp
.Must(x => x is not null && AllowedPatterns.Contains(x))
```

Alternative:

```csharp
.Must(x => x is null || AllowedPatterns.Contains(x))
```

The first option is better inside the existing `When` block because the rule only runs when non-null and keeps the predicate strict if the rule is ever moved later.

Suggested commit:

- `fix: clear CS8604 in MesocycleValidators`

Verification:

- `dotnet build api/Bryk.sln` should no longer show CS8604 for `MesocycleValidators.cs`.
- `dotnet test api/Bryk.sln` should stay green.

## Suggested revised Task 6-4 execution plan

Because item 4 is already complete and item 3 needs a repository prep step, the original "four commits total" acceptance criterion should be revised before implementation resumes.

Recommended revised split:

1. **Repository prep for item 3**
   - Add `IMesocycleRepository` + `MesocycleRepository` + DI.
   - Keep behavior unchanged.
   - Verify build/tests.
2. **Move `MesocycleService`**
   - Move service to Application.
   - Consume repository + unit of work.
   - Verify build/tests and API startup.
3. **Validation extension**
   - Add `ValidateOrThrowAsync` extension.
   - Update scoped call sites.
   - Verify validation response shape unchanged.
4. **Nullability fix**
   - Clear CS8604 in `MesocycleValidators.cs`.
   - Verify warning disappears.
5. **Document item 4 as already complete**
   - Either in the Task 6-4 final commit body or the eventual Phase 6 handoff.

## PR review focus for this branch

This PR should be reviewed as infrastructure groundwork and a handoff, not as a completed Task 6-4 implementation.

Review focus:

- Task 6-1 backend test infrastructure.
- Task 6-2 frontend test infrastructure.
- Task 6-3 CI workflow.
- `ROADMAP.md` Phase 6 status update.
- This handoff's plan/questions for Task 6-4.

Do not treat Task 6-4 as done after merging this PR. It remains open pending Sr. Dev decisions on the repository/layer-boundary work.

## Remaining Phase 6 work after this PR

- Task 6-4: tech-debt sweep implementation, revised per this handoff.
- Task 6-5: secrets hygiene.
- Task 6-6: decisions ADRs and Phase 6 completion handoff.

Phase 7 should not start until Task 6-6 locks the Mesocycle/TrainingPlan decision.
