# Task 7-4 — Tech-debt sweep (revised per ADR-0001)

## Goal
Execute the tech-debt items that were redistributed from the original Phase 6 Task 6-4. Major simplification from the original plan: ADR-0001 supersedes Mesocycle, so the `MesocycleService` move-to-Application becomes a delete-the-whole-Mesocycle-surface operation. Validation pattern simplification (CLAUDE.md tech debt item 5) is unchanged.

## Current code/status
- `md/decisions/0001-mesocycle-vs-trainingplan.md` is committed and decides: retire the five Mesocycle entities, `MesocycleService`, the four Mesocycle controllers, and `MesocycleValidators`.
- `CLAUDE.md` tech-debt items 3 (MesocycleService layer violation) and 7 (CS8604 in MesocycleValidators) are already struck through (files slated for deletion) pending this task.
- Original task brief: `md/Tasks-6-4.md`. Pre-implementation discovery: `md/handoffs/Phase 6-Task4-handoff.md` — both files remain in the repo as historical artifacts.
- Validation pattern (tech debt item 5) is still verbose at call sites:
  - `api/Bryk.Application/Onboarding/OnboardingService.cs` — multiple `await validator.ValidateAsync(...)` + manual throw blocks.
  - The handoff recommends a `ValidateOrThrowAsync` extension method living under `Bryk.Application/Common/Validation/`.
- The `Sport` enum in `Bryk.Domain/Entities/Enums/Sport.cs` currently has `Swim`, `Bike`, `Run`, `Triathlon`. Per ADR-0001 it gains `Strength` — but **not in this task**. That's Phase 9's responsibility (when the new entities that need it land).
- The duplicate `SportType` enum (defined inline in `Exercise.cs`) goes away with the file deletion below.

## Sequencing decision required in this task
The Mesocycle retirement migration may land:
- **(a)** As part of this task — delete the entities and generate the migration together. Coupled commit but no awkward intermediate state.
- **(b)** Deferred to Phase 9 Task 9-1 — delete the entity files / service / controllers / validators now, fold the table-drop into the new TrainingPlan migration. Leaves "deleted entity files but the table still exists" as an intermediate state, which complicates further EF migration generation.

**Recommendation: (a).** Generate and apply the drop migration here. The intermediate state in (b) is genuinely awkward — EF Core complains on next migration generation about removed entities. Sr. Dev approval gate on the migration apply per CLAUDE.md.

## Acceptance criteria

**Files deleted:**
- `api/Bryk.Domain/Entities/Mesocycle.cs`
- `api/Bryk.Domain/Entities/Week.cs`
- `api/Bryk.Domain/Entities/Day.cs`
- `api/Bryk.Domain/Entities/DayExercise.cs`
- `api/Bryk.Domain/Entities/Exercise.cs` (also removes the inline `SportType` enum)
- `api/Bryk.Infrastructure/Services/MesocycleService.cs`
- `api/Bryk.API/Controllers/MesocycleController.cs`
- `api/Bryk.API/Controllers/WeekController.cs`
- `api/Bryk.API/Controllers/DayController.cs`
- `api/Bryk.API/Controllers/ExerciseController.cs`
- `api/Bryk.Application/Validators/MesocycleValidators.cs`

**DI / DbContext cleanup:**
- `api/Bryk.API/Program.cs` — remove DI registrations referencing any deleted type.
- `api/Bryk.Infrastructure/ApplicationDbContext.cs` — remove `DbSet<Mesocycle>`, `DbSet<Week>`, `DbSet<Day>`, `DbSet<DayExercise>`, `DbSet<Exercise>` and any entity configuration registrations for those types.

**Migration:**
- New migration generated under `api/Bryk.Infrastructure/Migrations/` that drops the five Mesocycle tables. Reviewed; Sr. Dev approval obtained before `dotnet ef database update`. Migration commit message documents what's dropped and references ADR-0001.

**Validation extension (tech debt item 5):**
- New file: `api/Bryk.Application/Common/Validation/ValidationExtensions.cs` defining:
  ```csharp
  public static async Task ValidateOrThrowAsync<T>(
      this IValidator<T> validator,
      T instance,
      CancellationToken cancellationToken = default)
  ```
  Implementation calls `ValidateAsync(instance, cancellationToken)`, then throws `Bryk.Application.Exceptions.ValidationException` with the same error-message collection used by current call sites. **Does not** use FluentValidation's built-in `ValidateAndThrowAsync` — middleware doesn't handle FluentValidation's `ValidationException`.
- `api/Bryk.Application/Onboarding/OnboardingService.cs` — each `await validator.ValidateAsync(...)` + manual throw block collapses to a single `await validator.ValidateOrThrowAsync(request, ct);` line.

**Build / test:**
- `dotnet build api/Bryk.sln` green; CS8604 warning gone with `MesocycleValidators.cs` deletion.
- `dotnet test api/Bryk.sln` green — existing onboarding tests pass because validation behavior is preserved.
- `pnpm test` from `ui/` green (frontend unaffected — never called Mesocycle endpoints).

## Files likely to change/add
- (Deletions per the list above.)
- `api/Bryk.API/Program.cs`
- `api/Bryk.Infrastructure/ApplicationDbContext.cs`
- New migration file under `api/Bryk.Infrastructure/Migrations/`
- `api/Bryk.Application/Common/Validation/ValidationExtensions.cs` (new)
- `api/Bryk.Application/Onboarding/OnboardingService.cs` (call-site updates)

## What NOT to modify
- No new entities — `TrainingPlan`, `PlannedWorkout`, `Workout` are Phase 9.
- `Sport` enum unchanged in this task — adding `Strength` is Phase 9 (with the new entities that need it).
- Do not touch validators outside Onboarding even if they use the verbose pattern — scope is the Onboarding call sites named above.
- Do not pre-emptively fix unrelated tech-debt items (4, 6, 8-11 — later phases).
- Do not change the validation response shape — middleware behavior must be preserved.
- Do not touch `appsettings.Development.json` — that's Task 7-5.
- Do not delete `md/Tasks-6-4.md` or `md/handoffs/Phase 6-Task4-handoff.md` — keep as historical artifacts.

## Test plan
1. `dotnet build api/Bryk.sln` green after deletions + refactor.
2. `dotnet test api/Bryk.sln` green — existing onboarding service and controller tests must pass unchanged.
3. Manually smoke an onboarding endpoint with an invalid payload (e.g., missing `name` on Required) — confirm 400 + same JSON shape as before.
4. `dotnet ef migrations script --idempotent` reviewed before applying — confirm only DROP statements for the five Mesocycle tables and nothing else.
5. Apply migration in a fresh dev DB; confirm the tables are gone and the API still boots, the onboarding flow still works end-to-end.
6. `git diff` confirms only the named files were touched.

## Suggested commit split
Three commits keep each change traceable and reviewable separately:

1. `chore: delete Mesocycle surface per ADR-0001` — entity / service / controller / validator file deletions + DI + DbContext cleanup.
2. `refactor: add ValidateOrThrowAsync extension and adopt in OnboardingService` — validation extension + call site updates.
3. `feat: drop Mesocycle tables (migration)` — generated migration. **Sr. Dev approval required before apply.**

Order matters: 1 and 2 can be either order; 3 must come after 1 (the migration generation needs the entities already removed from the DbContext to produce the right delta).
