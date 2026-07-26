# ADR-0010 — Activity file import (storage, parsing boundary, load routing)

**Date:** 2026-07-26
**Status:** Accepted (2026-07-26) — FIT parsing uses the official `Garmin.FIT.Sdk` 21.205.0 in
`Bryk.Infrastructure` only; raw bytes live on the `ActivityFile` row as `varbinary(max)` behind a 25 MB
API cap; imported power/pace reaches the load math through one synthetic `WorkoutStepResult` rather than
any change to `LoadCalculator`; the migration creates `ActivityFile` and nothing else (no
`Workout.SourceFileId`, no `WorkoutZoneDuration`); the derived per-zone seconds histogram is a JSON
column on `ActivityFile` and reports method `samples`.

## Context

Phase 19 ("activity file import") turns a device file (`.fit`/`.tcx`/`.gpx`) into a `Workout` with real
actuals, matched to a planned workout. The ROADMAP's Phase 19 entry (`ROADMAP.md:527–551`) flags four
decisions under *Decisions needed*: the FIT parsing SDK, the migration scope, where the raw bytes live,
and whether per-sample data persists. All four — plus a fifth on histogram provenance — were resolved by
the Sr. Dev on 2026-07-26. **This ADR is the durable correction**, because the ROADMAP's own
Backend-scope prose has since drifted from what was actually decided: line 534 names a
`Workout.SourceFileId` column and a `WorkoutZoneDuration` table that are **not approved** (§4), and line
536's claim that "imported power finally exercises the top IF branch" of `LoadCalculator` is **false as
written** against the calculator that ships today (§3).

This ADR resolves:

1. **FIT parsing SDK + license** — whether the official Garmin SDK is used and where it may live.
2. **Raw-file storage** — filesystem, blob store, or database.
3. **Load routing** — how imported power/pace actually reaches `LoadCalculator`'s existing IF branches.
4. **Migration scope** — exactly which table(s) this phase is approved to create.
5. **Histogram persistence + provenance** — where the derived per-zone seconds histogram lives and how
   Phase 15's time-in-zone read consumes it.
6. **Sample persistence** — whether per-second sample series are ever stored.

### Conventions this ADR follows

- Athlete identity always via `ICurrentUserService`; missing/foreign resources are `KeyNotFoundException`
  → 404. Phase 12 auth stays deferred and approval-gated.
- Errors use the existing middleware contract (`ExceptionHandlingMiddleware`): `ValidationException` → 400
  with `{status, error, errors[], traceId}`, `KeyNotFoundException` → 404, `InvalidOperationException` →
  409. **No ProblemDetails rework** — Phase 21 owns that.
- Repository pattern; `IUnitOfWork` owns the commit; every write path commits **once**.
- Validation is `await validator.ValidateOrThrowAsync(request, ct)` (`Bryk.Application.Common.Validation`),
  never FluentValidation's `ValidateAndThrowAsync`.
- Zones are the existing `ZoneMetric` enum (`Power=1, Hr=2, Pace=3`) and the existing 5-bucket collapse
  (`Math.Min(z, 5)`) from ADR-0007 §4. Phase 19 introduces **no new zone enum**.

## Decision

### 1. FIT parsing uses the official Garmin FIT SDK

`Garmin.FIT.Sdk` **21.205.0**, added to **`Bryk.Infrastructure` only** (Task 19-3). Publisher-verified
Garmin International; ships `net46` / `netcoreapp2.0` / `netstandard2.0`, and `netstandard2.0` is
`net10.0`-compatible; the license is Garmin's proprietary royalty-free **FIT Protocol License Agreement**,
shipped as `LICENSE.txt` inside the package — **not** an OSI license. Approved by the Sr. Dev on
2026-07-26, so all three formats (`.fit`/`.tcx`/`.gpx`) ship in this phase and the ROADMAP's "degrade to
TCX/GPX-only" fallback is moot. `.tcx`/`.gpx` stay on `System.Xml.Linq` — **no package** (Task 19-2). Both
sit behind one Application abstraction, `IActivityFileParser`, so the FIT dependency never leaks past
`Bryk.Infrastructure`.

### 2. Raw bytes live in the database

`ActivityFile.Content` is `byte[]` → `varbinary(max)`. No filesystem path, no upload-root configuration, no
blob store. The app is pre-deployment, and a DB row is the only storage that is transactional with the rest
of the commit and needs zero new config or ops surface; a ~25 MB cap enforced at the API boundary bounds
the damage. Phase 21 may revisit when deployment topology is real. **The cap is per-route, not global** —
no Kestrel-wide or app-wide `FormOptions` change (Task 19-4).

### 3. Imported power/pace reaches the load math through a synthetic `WorkoutStepResult`

This is the ADR's most load-bearing section. `LoadCalculator.ComputeActualLoad` (lines 74–83) sums
`ActualCardioTss` per `WorkoutStepResult` when `workout.StepResults.Count > 0`, and otherwise (line 88)
calls the session path with `avgPower`/`avgPace` **hardcoded null**, so a session-level import could only
ever reach the HR branch. The ROADMAP's claim that "imported power finally exercises the top IF branch" is
therefore **false as written**. The fix is not to change the calculator: on commit the service creates
**one** `WorkoutStepResult` with `WorkoutStepId = null` (already nullable, ADR-0005 §5), `OrderIndex = 0`,
carrying the parsed `AvgPower` / `AvgPace` / `AvgHr` / `ActualDurationSeconds` / `ActualDistanceMeters`.
That routes the import into the existing StepResults branch and reaches the real power and pace IF
branches with **zero migration and zero edit to `LoadCalculator.cs`**. It also lights up the existing bike
session-power derivation in `AnalyticsService.cs:158–169` for free. Normatively: **`LoadCalculator.cs` is
frozen for Phase 19**; no `Workout.AvgPower`/`AvgPace` column; **no per-lap step results in v1**.

### 4. One migration: `ActivityFile` and nothing else

Approved: the `ActivityFile` entity + table. **Not approved, do not create:** a `Workout.SourceFileId`
column, a `WorkoutZoneDuration` child table. Both appear in ROADMAP line 534 and are superseded here.
Binding consequences: the **"from file" badge** derives from a reverse lookup on
`ActivityFile.ParsedWorkoutId == workoutId`, so `Workout.cs` is untouched by this phase;
**duplicate-commit rejection** keys on `ActivityFile.ParsedWorkoutId is not null`, not on a `Workout`
column; there is **no FK** from `ActivityFile` to `Workout` (a deleted workout must not cascade the
uploaded file away, and there is no delete-path to reason about) — just an index on `ParsedWorkoutId`. Any
second migration in Phase 19 → **STOP and ask**.

### 5. The zone histogram is a JSON column on `ActivityFile`

The derived per-zone seconds histogram is serialized to a `string?` column (`ZoneHistogramJson`) on the
same row as the bytes, written at commit. Phase 15's time-in-zone read unions it via
`ActivityFile.ParsedWorkoutId → Workout` and reports method **`samples`**, which **takes precedence over
structure and sessionAvg for covered workouts** (Task 19-6). `ZoneTimeMethodBreakdownDto` gains an
additive `SampleSeconds` field; the always-"estimated" badge becomes conditional. JSON is chosen over a
table because the histogram is read as a whole, never queried per-zone, and a table costs a second
migration this phase is not approved for. **Normalizing it into a real child table is a Phase 21
candidate — record it as tech debt in the phase handoff.**

### 6. No per-second sample persistence

Parsers materialize a sample series in memory; only derived aggregates (session actuals + the 5-bucket
histogram) are persisted. The file itself is kept, so richer analytics can re-parse later. This is the
ROADMAP's own recommendation, made binding.

## Consequences

**Closed by this decision:** all four ROADMAP *Decisions needed* bullets (FIT SDK, migration scope,
raw-file storage, sample persistence) plus the histogram-provenance question the ROADMAP's own §5 note
raised. **Created — one migration, one new package (`Garmin.FIT.Sdk`, 19-3 only):**

- `Bryk.Domain/Entities/ActivityFile.cs` + `Entities/Enums/ActivityFileFormat.cs`,
  `Bryk.Domain/Interfaces/IActivityFileRepository.cs`,
  `Bryk.Infrastructure/Repositories/ActivityFileRepository.cs`, an `ApplicationDbContext` `DbSet` +
  configuration block, and the `AddActivityFile` migration (19-1).
- `IActivityFileParser` + `ParsedActivity` + TCX/GPX parsers + zone-histogram math (19-2); `FitActivityParser`
  + the package (19-3).
- `ActivityFileService` + DTOs + validators + `ActivityFilesController` + the 25 MB per-route cap + the
  synthetic-`WorkoutStepResult` commit path (19-4).
- Upload + review UI + "from file" badge (19-5).
- `samples` time-in-zone: `SampleSeconds` + calculator/service union + UI provenance update (19-6).

### For Tasks 19-1 … 19-6

| Task | Surface | Depends on | Pins from this ADR |
|---|---|---|---|
| **19-1** ADR + `ActivityFile` entity/repo/migration | Backend | — | §2 §4 §5 (the row shape) |
| **19-2** `IActivityFileParser` + TCX/GPX + histogram math | Backend | 19-1 (contract only) | §1 (no package), §5 (bucket shape), §6 |
| **19-3** `FitActivityParser` + the package | Backend | 19-2 | §1 (SDK + license) |
| **19-4** service + DTOs + validators + controller | Backend | 19-1, 19-2 | §2 (cap), §3 (synthetic step result), §4 (duplicate guard), §5 (writes the JSON) |
| **19-5** upload + review UI + "from file" badge | Frontend | 19-4 | §4 (badge via reverse lookup) |
| **19-6** `samples` time-in-zone | Backend + Frontend | 19-2, 19-4 | §5 (precedence + `SampleSeconds`) |

## Alternatives considered

- **Filesystem or blob storage for the raw bytes.** Rejected (§2) — new config and ops surface, and
  non-transactional with the rest of the commit.
- **`Workout.SourceFileId`.** Rejected (§4) — a second column and a second write path for information the
  reverse index (`ActivityFile.ParsedWorkoutId`) already answers.
- **A `WorkoutZoneDuration` table.** Rejected (§5) — normalizes data that is only ever read whole, at the
  cost of an unapproved second migration.
- **Teaching `LoadCalculator` a session-level power/pace path.** Rejected (§3) — it would change the load
  of every existing session-level workout, an unannounced behavior change to persisted history.
- **Per-lap step results on import.** Rejected for v1 (§3) — the planned-vs-actual table has no lap concept
  yet.
- **Vendor OAuth / device sync.** Out of scope by ROADMAP lock, not re-litigated here.
