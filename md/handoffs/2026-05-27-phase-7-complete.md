# HANDOFF — Bryk Project, Phase 7 complete

**Date:** 2026-05-27
**Phase:** 7 — Closeout: ADRs, tech-debt sweep, secrets hygiene, Phase 5 handoff
**Status flip:** 🟡 → ✅

## Context

Phase 7 was a deliberate "clear the runway" phase inserted into the ROADMAP after the 2026-05-26 reshape. It bundled five small-to-medium pieces of work — two architectural decisions, two tech-debt items, secrets hygiene, and the Phase 5 closeout — none of which were feature work, all of which were prerequisites for Phase 8+ to be built on clean foundations.

The phase took ~2 working sessions and produced no UI changes. Visible-on-screen progress resumes in Phase 8.

## What shipped in Phase 7

### Task 7-1: Phase 5 completion handoff ✅

- `md/handoffs/2026-05-26-phase-5-complete.md` — formal Phase 5 closeout document covering wizard final shape, summary-card band-aid, dashboard shell inter-phase work, smoke matrix, test status, Phase 8 follow-ups.
- `ROADMAP.md` Phase 5 row flipped from 🟡 to ✅.

### Task 7-2: ADR-0001 — Mesocycle vs TrainingPlan ✅

- `md/decisions/0001-mesocycle-vs-trainingplan.md` — supersede Mesocycle; TrainingPlan / PlannedWorkout / Workout become the unified training framework. Strength is a first-class v1 discipline (Sport enum gains Strength when the new entities land in Phase 9). Periodization concepts carry forward as fields on TrainingPlan.

### Task 7-3: ADR-0002 — Coaches as first-class user type ✅

- `md/decisions/0002-coaches-as-first-class.md` — coaches are v2. One human = one Athlete at the domain level. Auth-table layout decision deferred to Phase 14.
- `md/product/feature-parity-trainingpeaks.md` — coach-tagged features flipped from `candidate` to `v2`. Marketplace/concierge items stay `deferred` per their original tagging.

### Task 7-4: Tech-debt sweep (Mesocycle deletion + ValidateOrThrowAsync) ✅

- **28 files deleted** across `Bryk.Domain.Entities`, `Bryk.Infrastructure.Services`, `Bryk.API.Controllers`, `Bryk.Application.Interfaces`, `Bryk.Application.Validators`, and `Bryk.Application.DTOs`. The task spec originally listed `MesocycleService`, `MesocycleValidators`, and the four Mesocycle controllers — the deletion scope expanded during execution to include sibling Day/Week/Exercise services + their interfaces + DTOs + validators, all part of the same Mesocycle surface (revealed by `dotnet build` errors immediately after the first batch deletion).
- `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` — removed 5 DbSets and 5 entity configurations.
- `api/Bryk.API/Program.cs` — removed 4 DI registrations (`IMesocycleService`/`IWeekService`/`IDayService`/`IExerciseService`) and the `Bryk.Application.Interfaces` using directive.
- **Migration `DropMesocycleSurface`** (timestamp 20260528010359) — drops the 5 tables (`DayExercises`, `Days`, `Exercises`, `Weeks`, `Mesocycles`) in FK-dependency order. Down() recreates them for reversibility. Sr. Dev approval obtained before apply; applied 2026-05-28 (UTC) against the local Bryk DB.
- **ValidateOrThrowAsync extension** at `api/Bryk.Application/Common/Validation/ValidationExtensions.cs` — collapses the three-line FluentValidation pattern into a single call site. Uses fully-qualified `Bryk.Application.Exceptions.ValidationException` to disambiguate from `FluentValidation.ValidationException`. **Does NOT** switch to FluentValidation's built-in `ValidateAndThrowAsync` — middleware would not handle the different exception type.
- `api/Bryk.Application/Onboarding/OnboardingService.cs` — three call sites refactored (`SubmitRequiredAsync`, `SubmitRecommendedAsync`, `SubmitGoalsAsync`). Behavior preserved; existing tests pass unchanged.
- **CLAUDE.md tech-debt items closed:** 3 (MesocycleService layer violation — file deleted), 5 (verbose validation pattern — extension method shipped), 7 (CS8604 in MesocycleValidators — file deleted).

### Task 7-5: Secrets hygiene (dotnet user-secrets) ✅

- `api/Bryk.API/Bryk.API.csproj` — `<UserSecretsId>1be86356-65c6-46b5-88a8-64bce0c6fcc4</UserSecretsId>` added by `dotnet user-secrets init`.
- `api/Bryk.API/appsettings.Development.json` — trimmed to the `Logging` block only. `ConnectionStrings:DefaultConnection` and `DevAuth:CurrentAthleteId` now live in per-developer `dotnet user-secrets`.
- **Two orphan config files deleted:**
  - `api/appsettings.development.json` — the one named in the task spec.
  - `api/appsettings.json` — additional find during execution; contained a plaintext sa password (`Techno100!`) that nothing was actually using but anyone with repo access could see. Higher-severity find than the spec'd one.
- `README.md` — Backend Setup section rewritten with the user-secrets workflow.

## Commit history (Phase 7 work)

```
<pending>  refactor: add ValidateOrThrowAsync extension; adopt in OnboardingService
<pending>  chore: delete Mesocycle surface and drop tables per ADR-0001
<pending>  chore: migrate dev secrets to dotnet user-secrets
81d75ea    docs: Phase 5 complete — handoff and ledger flip
99d86f6    docs: reorganize all process docs into md/ + reshape Phase 7+
66812fb    Adding documentation (ADRs 0001 + 0002)
```

(Pending commits land alongside this handoff; expected order: secrets → Mesocycle schema → validation refactor → this handoff + Phase 7 ledger flip.)

## Build / test status

- **Backend.** `dotnet build api/Bryk.sln` green (21 warnings, all pre-existing NuGet vulnerability warnings unrelated to Phase 7; CS8604 in MesocycleValidators gone with the file). `dotnet test api/Bryk.sln` green — 3/3 passing (1 unit + 2 integration).
- **Frontend.** Phase 7 made no UI changes. `pnpm test` from `ui/` last green at the end of Phase 5 closeout (14 tests). No regression expected.
- **Migration applied.** 5 Mesocycle tables dropped from the local Bryk database via `dotnet ef database update`. Database currently contains Athletes, AthleteSportProfiles, Events, Goals, Equipment.
- **Build cached migrations:** the four prior migrations (`InitialCreate`, `AddAthleteOnboardingEntities`, `MakeLegacyEntitiesAuditable`, `AddTriathlonSportAndCustomDistanceName`) remain in `api/Bryk.Infrastructure/Migrations/` and reference the deleted entity types in their Designer files. This is normal — historical migration designers retain the model state at the time of generation; EF does not require entity types to exist at runtime for already-applied migrations.

## ROADMAP reshape recap

Phase 7 didn't just close out — it reshaped phases 7+ in the ROADMAP (commit `99d86f6`) to reflect post-ADR sequencing:

| Old | New | Note |
|---|---|---|
| Phase 6 (test infra + tech debt + decisions) | Phase 6 (test infra only) | Rescoped; ✅ Complete |
| — | Phase 7 (closeout) | New |
| — | Phase 8 (profile + dashboard warmups) | New |
| Phase 7 (TrainingPlan domain) | Phase 9 (TrainingPlan domain + This Week card) | Renumbered + dashboard wire-up appended |
| Phase 8 → Phase 10 | Zones + workout builder | +2 shift |
| Phase 9 → Phase 11 | TSS + execution + Recent Activity / Weekly Load | +2 shift + dashboard wire-up |
| Phase 10 → Phase 12 | Calendar | +2 shift |
| Phase 11 → Phase 13 | PMC + Form (TSB) card | +2 shift + dashboard wire-up |
| Phase 12 → Phase 14 | Auth | +2 shift |
| Phase 13 → Phase 15 | ATP | +2 shift |
| Phase 14 → Phase 16 | Docs / config | +2 shift |
| Phase 15 → Phase 17 | v1 cutover | +2 shift |

## Pending decisions (carried forward)

- **Authentication implementation choice.** Phase 14 ADR will pick `ApplicationUser : IdentityUser<Guid>` vs `Athlete : IdentityUser<Guid>`. Both satisfy the conceptual constraint from ADR-0002.
- **Strength load metric formula.** Deferred to Phase 11 per ADR-0001's open follow-ups.
- **Strength workout entity shape** within `PlannedWorkout` / `Workout`. Deferred to Phase 9 design per ADR-0001.

## Tech debt status snapshot

CLAUDE.md tech debt list as of Phase 7 close:

1. ✅ `OperationCanceledException` → 499 — fixed pre-Phase 7 (commit `2d3a74c`).
2. ✅ Test coverage — Phase 6 infrastructure landed; coverage grows phase by phase.
3. ✅ MesocycleService layer violation — file deleted (Task 7-4).
4. ✅ ValidatorPlaceholder rename — done pre-Phase 7 (`d4bff8c`).
5. ✅ Verbose validation pattern — ValidateOrThrowAsync extension shipped (Task 7-4).
6. ⏳ `NotImplementedException` → 501 — deferred to Phase 17 mop-up.
7. ✅ CS8604 in MesocycleValidators — file deleted (Task 7-4).
8. ⏳ `DbUpdateException` / unique-constraint → 409 — deferred to Phase 17.
9. ⏳ RFC 7807 ProblemDetails — deferred to Phase 17 unless external consumers arrive.
10. ⏳ Hardcoded `SwaggerDoc("v1")` — TODO in place; address when v2 ships.
11. ⏳ Pre-existing NuGet vulnerability warnings — audit during Phase 16 dependency sweep.

## What Phase 8 should do next

Phase 8 (Profile editing + dashboard warmup cards) is the next phase. Task files at `md/Tasks-8-1.md` through `md/Tasks-8-5.md` define the five task groups:

- **Task 8-1: Profile read endpoints.** `GET /api/v1/profile/required`, `/profile/recommended`, `/profile/goals`. Reads existing Athlete / SportProfile / Goal / Event data.
- **Task 8-2: Profile Vue surface.** New `/profile` route with three sections pre-filled from the read endpoints. Submits via existing onboarding POSTs (upsert / append semantics already match).
- **Task 8-3: Onboarding band-aid removal.** Delete the summary-card branches in the three onboarding step components; relax the completed-step stepper lock; add "Manage your profile" link from the onboarding "All set" view.
- **Task 8-4: Primary Goal dashboard card wire-up.**
- **Task 8-5: Resting HR + Athlete-derived stats card wire-up; sidebar Profile nav activation.**

Task 8-1 has no dependencies. Task 8-2 depends on 8-1. Tasks 8-3, 8-4, 8-5 depend on 8-2.

## What the next session should do first

1. Read this handoff plus `md/Tasks-8-1.md`.
2. `git status` clean, `git log --oneline -10` for context, `dotnet build api/Bryk.sln` + `pnpm test` from `ui/` both green.
3. Confirm `dotnet user-secrets list` shows your local `ConnectionStrings:DefaultConnection` and `DevAuth:CurrentAthleteId` — required for any backend run since Task 7-5.
4. Open Task 8-1 (profile read endpoints) — backend-only, ~1 session.
