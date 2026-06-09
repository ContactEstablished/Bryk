# HANDOFF — Phase 11 complete (training-load engine + executed-workout capture)

**Date:** 2026-06-08
**Phase:** 11 — load/TSS math + executed-`Workout` capture (✅ COMPLETE)
**ADR:** [ADR-0005](../decisions/0005-training-load-and-executed-workout.md) — **Accepted**; HR-load §1 = option a (IF = targetHr / Lt2), strength §2 = option c (scaled tonnage, sRPE for actuals).

Phase 11 turned the *prescribed* structure from Phase 10 into *numbers*: a deterministic
training-load (TSS) engine for planned sessions, a weekly load total on the dashboard, and
end-to-end executed-`Workout` capture (log actuals → computed actual load → Recent Activity),
with per-step planned-vs-actual. `Workout` / `WorkoutStepResult` — dormant since their schema
landed — are now live.

## What shipped

| Area | Scope | Commit |
|---|---|---|
| Load engine | `LoadCalculator` (pure planned-TSS math, `sec × IF² / 3600 × 100`; power/pace/HR intensity; strength tonnage) + `LoadService`; `ComputedLoad` / `EffectiveLoad` on planned reads | `592df1a` |
| Weekly load | `ThisWeekService` weekly-load total (Monday-based UTC week) + dashboard **Weekly Load** card | `14dc698` |
| Execution schema | `Workout` execution fields + `WorkoutStepResult` entity + `AddWorkoutExecution` migration | `b6f71b4` |
| Executed capture | `WorkoutService.LogAsync` — log endpoint, actual-load computation (`ComputeActualLoad`, ADR-0005 §6), step-result seeding from planned steps | `e1cd98d` |
| Log UI | log-workout UI + **Recent Activity** on the dashboard | `49cb6b6` |
| Dev seed | re-runnable `db/dev-seed.sql` — reset+reseed a rich, coherent dataset for the dev athlete (this session) | `d45831e` |

Planning artefacts earlier in the phase: `8b2deb0` (Phase 11 tasks + planning), `b50d073`
(merge planning docs), `09efac3` (normalize spec paths + accept ADR-0005).

## Verification state (code frozen at `49cb6b6`; seed `d45831e` is SQL-only)

- **Backend:** `dotnet test api/Bryk.sln` green — **84 tests**. `dotnet build` clean.
- **Frontend:** `pnpm run build` (vue-tsc) green; `pnpm test` green — **54 tests**.
- **DB:** `AddWorkoutExecution` migration applied to the dev DB. A fresh DB needs `dotnet ef database update`.
- This session added **only `db/dev-seed.sql`** (no app code), so the suites are unchanged from `49cb6b6`.
- Only outstanding warning remains the design-time `System.Security.Cryptography.Xml` High advisory (non-shipping — CLAUDE.md tech-debt #1).

## Key design decisions

- **Load math is a pure static `LoadCalculator`** (no I/O) — callers pass the sport profile (thresholds) + that sport's effective zones; `LoadService` wires in the repos. Deterministic and unit-tested directly.
- **Intensity precedence (planned cardio step):** power/FTP (Bike) → pace/threshold (Run/Swim, inverse) → HR/Lt2 fallback (ADR-0005 §1 a). Distance-only steps convert to seconds via target pace.
- **`EffectiveLoad = PlannedLoad ?? ComputedLoad`** for planned (explicit override wins, `IsLoadOverride` flags it); **`EffectiveLoad = LoadOverride ?? ComputedLoad`** for completed `Workout`s.
- **Actual load is computed once at log time and persisted** on `Workout.ComputedLoad`, so historical reads stay single-table (no recompute on GET).
- **Strength:** planned = scaled tonnage `Σ(sets×reps×load)×k` (RPE-volume fallback); actual = Foster sRPE `rpe × minutes × k` (ADR-0005 §2 c / §6).
- **Week boundary is Monday-based UTC** (`((int)DayOfWeek + 6) % 7`), matching how `DateOnly` "today" is treated elsewhere.

## Dev seed script (`db/dev-seed.sql`)

Re-runnable T-SQL (SQL Server / `Bryk`) so every Phase 11 screen reads end-to-end on the dev box.

- **Parameterized** by `DevAuth:CurrentAthleteId` (paste at top), **scoped to that one athlete**, **dates relative to today** (Monday-based week matches `ThisWeekService`). Wrapped in a transaction; `CreatedAt`/`UpdatedAt` set manually (the audit interceptor doesn't run for raw SQL).
- **Reset** deletes child→parent across all 12 tables — several carry a denormalized `AthleteId` with **no FK**, so deleting the `Athlete` row does not cascade to them.
- **Reseed:** fully-onboarded athlete → 4 sport profiles (Bike FTP 250 / Run 4:15 km / Swim 1:35 / Strength, with Lt1/Lt2) → 3 events (A 70.3 +8wk, B, C) + 2 goals + 3 equipment → 7 bike power-zone overrides (`/zones` "Customized") → 1 active 8-week plan → 17 planned workouts (4 structured in the current week with `PlannedLoad` NULL so the engine drives Weekly Load ≈ 366 TSS; the rest simple/past/future) → 9 completed workouts (one `LoadOverride` demo, strength via sRPE) → 8 per-step actuals on the two bike sessions.
- **Run:** edit `@AthleteId`, then `sqlcmd -S <server> -d Bryk -f 65001 -i db/dev-seed.sql` (or SSMS/ADS). Idempotent — re-run resets + reseeds, row counts stable.

## Known gaps / carry forward

- **Seed is SQL-only, not an EF seeder** — deliberate (full control, no app coupling). It hardcodes the load formula's *shape* in the data (sane targets), not the calculator; if `LoadCalculator` math changes, the seeded `ComputedLoad`/`PlannedLoad` numbers are illustrative, not recomputed.
- **No workout/plan browser** still (carried from Phase 10) — the structure builder opens from the just-created plan; the seed gives data but the UI to re-open an arbitrary planned workout's builder isn't built.
- **`CustomZonesJson` remains vestigial** (superseded by `AthleteSportZone`) — a later cleanup can drop the column.
- **CLAUDE.md tech-debt list** (DbUpdateException→409, NotImplemented→501, ProblemDetails, per-version SwaggerDoc) is untouched by Phase 11.

## Next — Phase 12

**Authentication & Authorization** (Open Decisions in CLAUDE.md) — currently deferred and
**approval-gated**: no `[Authorize]`, Identity, or `AddAuthentication` until the Phase 12 auth ADR
picks the table layout (Identity-in-own-table vs `Athlete : IdentityUser<Guid>`). The
`ICurrentUserService` dev stub swaps to read from `ClaimsPrincipal` when real auth lands;
consumers don't change. **Do not write production auth code without Sr. Dev approval.**

## Session-start checklist

1. Read this handoff + ADR-0005.
2. `git status` clean; `git log --oneline -12` for context.
3. Backend: `dotnet test api/Bryk.sln` (expect 84 green). Frontend (from `ui/`): `pnpm run build` + `pnpm test` (expect 54 green).
4. Confirm `dotnet user-secrets list` shows `ConnectionStrings:DefaultConnection` + `DevAuth:CurrentAthleteId` before any backend run.
5. To populate dev screens: paste `DevAuth:CurrentAthleteId` into `db/dev-seed.sql` and run it.
