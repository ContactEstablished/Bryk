# HANDOFF — Phase 10 complete (structured workouts + training zones)

**Date:** 2026-06-08
**Phase:** 10 — structured workouts + training zones (✅ COMPLETE)
**ADR:** [ADR-0004](../decisions/0004-structured-workout-and-zones.md) — **Accepted 2026-06-08**; all four sub-decisions stood as written and are implemented.

Phase 10 delivered the sport-tailored training-zone model and the structured-workout
(blocks + steps) framework end-to-end, backend through UI, exactly as ADR-0004 pinned.

## What shipped

| Task | Scope | Commit |
|---|---|---|
| 10-1 | Zones backend — `ZoneService` auto-calc (Coggan 7-zone power / 5-zone pace), `AthleteSportZone` override table + migration, `GET/PUT/DELETE /api/v1/zones` | `6fdafbe` |
| 10-2 | Zones config UI — `/zones` route + sidebar entry, per-sport `ZoneSportCard` (edit overrides / reset to computed) | `4c11743` |
| 10-3 | `WorkoutBlock` / `WorkoutStep` entities + repo staging + additive `AddStructuredWorkoutPayload` migration | `c293ba6` |
| 10-4 | Structured-workout CRUD — `IStructuredWorkoutService`, sport-discriminated validation, `GET/PUT …/plannedworkouts/{pwId}/structure` | `5665c81` |
| 10-5 | Builder UI — `WorkoutStructureBuilder` → `WorkoutBlockCard` → `WorkoutStepRow`, launched from `/training`'s just-created plan | `e2543ee` |

Also this session: `b1e23c6` (CLAUDE.md regenerated against the codebase), `fda0616`
(dropped the unused `CodeGeneration.Design` scaffolding package — cleared the High-sev
NuGet warnings on `Bryk.API`/`Bryk.API.Tests`).

## Verification state (at `e2543ee`)

- **Backend:** `dotnet test api/Bryk.sln` green — **64 tests** (42 `Bryk.Application.Tests` + 22 `Bryk.API.Tests`). `dotnet build` clean.
- **Frontend:** `pnpm run build` (vue-tsc) green; `pnpm test` green — **50 tests / 18 files**.
- **DB:** `AddStructuredWorkoutPayload` + `AddAthleteSportZone` migrations applied to the dev DB. A fresh DB needs `dotnet ef database update`.
- Only outstanding warning is the design-time `System.Security.Cryptography.Xml` High advisory (non-shipping — see CLAUDE.md tech-debt #1).

## Key design decisions

- **Zone count is a single source of truth:** `ZoneScheme.Count(sport)` (7/5/0), used by both `ZoneService` and the structured-workout validation.
- **Structured-workout CRUD is a focused `IStructuredWorkoutService`** (not bolted onto `TrainingPlanService`); ownership via the denormalized `AthleteId` + route plan (ADR-0003 pattern).
- **`PlannedWorkoutResponse.blocks` is optional** on the shared shape — only the structure endpoint populates it; This-Week / plan reads omit it.
- **Client validation is a sport-aware zod schema factory** (`buildWorkoutStructureSchema(sport)`) mirroring 10-4's server rules.

## Known gaps / deliberate deviations (carry forward)

- **Builder launch is from the just-created plan only.** There's no plan/workout browser, so a workout's structure is editable right after the plan is created. A future task could add a plan browser to re-open any workout's builder (the builder is already props-driven: `planId` / `plannedWorkoutId` / `sport`).
- **Duration/distance is dual inputs + "set exactly one" + zod**, not a toggle (avoided fragile local-mode/field-sync; same guarantee).
- **Block reorder not implemented** — add/remove only. ADR-0004 scope is two-level blocks; reorder was unspecced by the tests.
- **Zone-pick is render-tested, not interaction-tested** — driving reka-ui `Select` in jsdom is unreliable; the pick→pre-fill logic is type-checked but not exercised end-to-end.
- **`CustomZonesJson` is now vestigial** (superseded by `AthleteSportZone`); a later cleanup task can drop the column (ADR-0004 §1).
- Test gotcha logged to memory: valid vee-validate submits over refined-array schemas need ~6 `flushPromises` in specs, not 2.

## Next — Phase 11

ADR-0004 §4 defers to **Phase 11**: **load/TSS math** (the computed training-load number for a structured session + the strength-load formula) and **executed-`Workout` step capture** (actual vs. planned). Phase 10 stores *prescribed* targets only and computes nothing. `Workout` stays dormant until then.

## Session-start checklist

1. Read this handoff + ADR-0004.
2. `git status` clean; `git log --oneline -12` for context.
3. Backend: `dotnet test api/Bryk.sln` (expect 64 green). Frontend (from `ui/`): `pnpm run build` + `pnpm test` (expect 50 green).
4. Confirm `dotnet user-secrets list` shows `ConnectionStrings:DefaultConnection` + `DevAuth:CurrentAthleteId` before any backend run.
