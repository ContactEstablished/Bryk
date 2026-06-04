# Task 9-2 — TrainingPlan / PlannedWorkout / Workout entities + `Sport.Strength` + EF migration

## Goal
Add the three domain entities defined in ADR-0003, add `Strength` to the `Sport` enum, define their repository contracts, wire `DbSet`s + entity configurations into `ApplicationDbContext`, and generate the code-first EF migration. This is the persistence-boundary task — no application services, no controllers, no endpoints.

Backend-only (Domain + Infrastructure). **Generates a migration → Sr. Dev approval required before apply (CLAUDE.md).**

## Depends on
- **Task 9-1 (ADR-0003).** The entity field lists, the strength-payload decision, the per-plan `Methodology` decision, and the `Sport.Strength` confirmation all come from ADR-0003. If 9-1 is not merged, stop and flag — do not invent the shapes.

## Required reading
- `md/decisions/0003-trainingplan-domain-shape.md` — the binding shape spec.
- `api/Bryk.Domain/Entities/Event.cs`, `Goal.cs`, `Athlete.cs` — entity conventions: `Guid Id`, `Guid AthleteId`, `IAuditable` (`CreatedAt`/`UpdatedAt` set globally by the interceptor — never manually), nav property back to `Athlete`.
- `api/Bryk.Domain/Entities/Enums/Sport.cs` — `Swim=1, Bike=2, Run=3, Triathlon=4`; add `Strength=5`.
- `api/Bryk.Domain/Interfaces/IEventRepository.cs` — the repository-contract style (XML docs, `GetByIdAsync`/`GetByAthleteIdAsync`/`AddAsync`/`Update`/`Delete`, "stages, does NOT call SaveChanges").
- `api/Bryk.Infrastructure/Repositories/EventRepository.cs` — primary-constructor repo, `AsNoTracking()` reads, `.Include()` for nav props.
- `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` — `DbSet` exposure + `OnModelCreating` config blocks (keys, `HasMaxLength`, `HasPrecision`, `HasMany/WithOne/HasForeignKey/OnDelete`, unique indexes).
- `api/Bryk.Infrastructure/Migrations/20260528010359_DropMesocycleSurface.cs` — the most recent migration, for the generated-migration format and the `IDesignTimeDbContextFactory` setup.

## Acceptance criteria

**Domain — entities (`api/Bryk.Domain/Entities/`):**
- `TrainingPlan.cs`, `PlannedWorkout.cs`, `Workout.cs` — exactly the field lists from ADR-0003. Each: `IAuditable`, `Guid Id`, `Guid AthleteId`, nav back to `Athlete`, audit fields. `PlannedWorkout` carries `Guid TrainingPlanId` + nav; `Workout` carries nullable `Guid? PlannedWorkoutId` + nav (unplanned executions are first-class per ADR-0001/0003).
- `Sport.cs` — add `Strength = 5`. Do not renumber existing values.
- If ADR-0003 chose a structured payload child collection (e.g. interval steps / strength sets), add that child entity too, exactly as specified.

**Domain — `Athlete` nav collections (`api/Bryk.Domain/Entities/Athlete.cs`):**
- Add `ICollection<TrainingPlan> TrainingPlans` (and `Workouts` if ADR-0003 puts a direct Athlete→Workout relationship). Match the existing collection-init style (`= new List<…>()`). Add ONLY the collections ADR-0003's relationships require — nothing speculative.

**Domain — repository contracts (`api/Bryk.Domain/Interfaces/`):**
- `ITrainingPlanRepository.cs` — at minimum `GetByIdAsync` (with `PlannedWorkouts` included), `GetByAthleteIdAsync`, `AddAsync`, `Update`, `Delete`, plus the planned-workout staging methods the 9-3 service will need (e.g. `AddPlannedWorkout`/`UpdatePlannedWorkout`/`RemovePlannedWorkout`, or expose them via the aggregate — follow ADR-0003's aggregate-boundary guidance). XML docs in the `IEventRepository` style; every mutator documents "stages, does NOT call SaveChanges."
- A `IWorkoutRepository.cs` **only if** ADR-0003 says `Workout` is a separate aggregate root needing its own reads in Phase 9. If `Workout` is dormant until Phase 11, do NOT add its repository now — add the entity + DbSet only. Decide per ADR-0003; default to **not** adding it.

**Infrastructure — repositories (`api/Bryk.Infrastructure/Repositories/`):**
- `TrainingPlanRepository.cs` — primary-constructor, implements the contract. Reads `AsNoTracking()` for display, tracked reads where the service mutates and saves. `.Include(p => p.PlannedWorkouts)`; `.AsSplitQuery()` if multiple includes.

**Infrastructure — DbContext (`api/Bryk.Infrastructure/Data/ApplicationDbContext.cs`):**
- Expose `DbSet<TrainingPlan>`, `DbSet<PlannedWorkout>` (+ payload child + `Workout` per ADR-0003), using the `=> Set<T>()` style.
- `OnModelCreating` config blocks for each: keys, `HasMaxLength` on strings, `HasPrecision` on decimals (mirror `HeightCm`'s `(5,2)` etc.), FK relationships with explicit `OnDelete` (cascade plan→planned-workout; for `Workout.PlannedWorkoutId` nullable use `DeleteBehavior.SetNull` or `Restrict` per ADR-0003).

**Migration:**
- Generate via `dotnet ef migrations add AddTrainingPlanDomain` from the Infrastructure project (the repo's existing EF tooling / `IDesignTimeDbContextFactory`).
- **Review the generated `Up()`/`Down()` before anything else.** Confirm: new tables only, no unintended drops/alters of existing tables, FK + index shapes match the config, `Strength` enum addition does not alter existing data (it's int-backed).
- **Do NOT apply the migration as part of this task's automated work.** Present the generated migration for Sr. Dev review; apply (`dotnet ef database update`) only after approval, per CLAUDE.md.

**DI:**
- `api/Bryk.API/Program.cs` — register `ITrainingPlanRepository → TrainingPlanRepository` (scoped), alongside the existing repo registrations. (Service/controller registration is 9-3.)

**Build / test:**
- `dotnet build api/Bryk.sln` green.
- `dotnet test api/Bryk.sln` green — existing tests still pass (no new tests required in this task; entity/migration scaffolding is exercised by 9-3's service tests). If the harness wants a smoke test, a single "DbContext model builds / migration applies to a SQLite or in-memory provider" test is acceptable but optional.

## Files likely to change/add
- `api/Bryk.Domain/Entities/TrainingPlan.cs` (new), `PlannedWorkout.cs` (new), `Workout.cs` (new), payload child (new, if ADR-0003)
- `api/Bryk.Domain/Entities/Enums/Sport.cs` — add `Strength = 5`
- `api/Bryk.Domain/Entities/Athlete.cs` — add nav collection(s)
- `api/Bryk.Domain/Interfaces/ITrainingPlanRepository.cs` (new)
- `api/Bryk.Infrastructure/Repositories/TrainingPlanRepository.cs` (new)
- `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` — DbSets + config
- `api/Bryk.Infrastructure/Migrations/<timestamp>_AddTrainingPlanDomain.cs` (+ Designer + snapshot update) — generated
- `api/Bryk.API/Program.cs` — one repo DI line

## What NOT to modify
- Do not add services, validators, DTOs, or controllers — that's Task 9-3.
- Do not add execution-capture fields to `Workout` (HR-zone minutes, pace, weather, calories, etc.) — Phase 11 per ADR-0001/0003. `Workout` is minimal in Phase 9.
- Do not touch the Mesocycle migrations or the `DropMesocycleSurface` migration — they're history.
- Do not renumber existing `Sport` enum values.
- Do not expose `Strength` in the onboarding `SportThresholdsDto` flow — out of scope (known gap per ADR-0003).
- Do not apply the migration without Sr. Dev approval.
- Do not modify `Event`, `Goal`, `Equipment`, `AthleteSportProfile` shapes (you may only ADD a nav collection to `Athlete`).

## Test plan
1. `dotnet build api/Bryk.sln` green.
2. Inspect the generated migration `Up()`/`Down()` by eye — new tables only, FK/index/precision correct, `Down()` cleanly reverses.
3. `dotnet test api/Bryk.sln` green (existing suite unbroken).
4. Present migration for Sr. Dev approval; apply against the local Bryk DB after approval; confirm the new tables exist and existing tables are untouched.
5. `git diff --stat` — only Domain + Infrastructure + one Program.cs DI line + the generated migration files.

## Suggested commit
```
feat: add TrainingPlan / PlannedWorkout / Workout domain + Sport.Strength

New entities per ADR-0003 with their repository contract, DbContext
configuration, and the AddTrainingPlanDomain EF migration. Sport gains
Strength (=5). Workout is defined minimally; execution-capture metric
fields are deferred to Phase 11. No services or endpoints yet (Task 9-3).

Migration reviewed and applied with Sr. Dev approval.
```
