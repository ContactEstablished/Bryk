# Impl 19-1 — Build order: ADR-0010 + `ActivityFile` entity, repository, DbContext config, migration

**Executor:** architect-implementer (per CLAUDE.md).
**Acceptance contract:** `md/Tasks-19-1.md`.
**Decision lock:** ADR-0010 (`md/decisions/0010-activity-file-import.md`, written in Step 1 of this build
order and **reviewed before any code is written**) + ADR-0003/ADR-0004 (denormalized-`AthleteId`-no-FK
convention this entity follows) + ADR-0005 §5 (`WorkoutStepResult.WorkoutStepId` already nullable — what
makes ADR-0010 §3's synthetic step result possible with zero migration, not built in this task) +
ADR-0007 §4 (the 5-bucket time-in-zone histogram whose provenance ADR-0010 §5 extends).
**Scope:** Backend only. One new ADR, one new entity + enum, one repository contract + implementation,
one `ApplicationDbContext` edit, **one reviewed migration (Sr. Dev gate)**, **one** `Program.cs` line. No
parser, no service, no controller, no DTO, no UI, nothing reachable over HTTP — 19-4 wires it up.

This is the step-by-step build order. Execute top-to-bottom; each step's verification is the gate to the
next. **Step 1 carries a hard stop** — do not write any `.cs` file until ADR-0010 has been read and
accepted. **Step 7 carries a second hard stop** — the migration must be reviewed and approved before
`dotnet ef database update` runs. One commit at the end with the message in `Tasks-19-1.md`.

## Step 0 — Pre-flight

- `git status` clean on `main`. `dotnet build api/Bryk.sln` green — confirm the stated baseline:
  **262 xUnit tests** (**173** `Bryk.Application.Tests` + **89** `Bryk.API.Tests`), **16 warnings** (9×
  design-time `System.Security.Cryptography.Xml` NU1903 + the two pre-existing
  `WorkoutsControllerTests.cs:121,150` nullable warnings — do not fix these, they predate this task).
  `dotnet test api/Bryk.sln` once to confirm **262 passed, 0 failed** before touching anything. `cd ui;
  pnpm run build` green; `pnpm exec vitest run --no-file-parallelism` at **252 passed / 56 files** — this
  task touches no frontend file, these numbers must be unchanged at the end.
- Confirm `api/Bryk.Domain/Entities/ActivityFile.cs`, `api/Bryk.Domain/Entities/Enums/ActivityFileFormat.cs`,
  `api/Bryk.Domain/Interfaces/IActivityFileRepository.cs`, `api/Bryk.Infrastructure/Repositories/ActivityFileRepository.cs`,
  and `api/Bryk.API.Tests/ActivityFiles/` do not yet exist (fresh surface — everything but
  `ApplicationDbContext.cs`, `Program.cs`, and the migrations folder is purely additive this task).
- Re-read `md/Tasks-19-1.md` in full. Open in editor:
  `ROADMAP.md:527–551` (the Phase 19 entry — note line 534's `Workout.SourceFileId`/`WorkoutZoneDuration`
  and line 536/537's IF-branch claim are **not** approved and are corrected by ADR-0010 §4/§3
  respectively),
  `md/decisions/0009-periodization-ramp-model.md` (the ADR format template — title line, `**Date:**`,
  `**Status:**`, `## Context` with a *Conventions this ADR follows* subsection, numbered `## Decision`
  sections, `## Consequences` with a per-task table, `## Alternatives considered`),
  `md/decisions/0007-progress-analytics.md` (second format reference, per-task consequences table),
  `api/Bryk.Domain/Entities/WorkoutStepResult.cs:5–8` (the entity header-comment style: names the ADR,
  states the rationale for the denormalized `AthleteId`, states the delete-behavior reasoning — mirror
  this for `ActivityFile.cs`),
  `api/Bryk.Domain/Entities/AthleteSportZone.cs:5–8` (the exact "avoids a SQL Server multiple-cascade-path
  diamond" wording behind the no-FK convention),
  `api/Bryk.Domain/Entities/Workout.cs` (confirm for yourself: 28 lines, no `SourceFileId`, no `AvgPower`,
  no `AvgPace` — this file must not appear in the diff),
  `api/Bryk.Domain/Interfaces/IWorkoutRepository.cs` (the repository-contract XML-doc style — `"No-tracking."`,
  `"Does NOT call SaveChanges."`) and `ITrainingPlanRepository.cs:40–46` (`GetPlannedWorkoutsByIdsWithStructureAsync`'s
  "empty ids → empty list with no query" wording),
  `api/Bryk.Infrastructure/Repositories/WorkoutRepository.cs` (primary-ctor repository, `AsNoTracking()`
  for display reads, tracked reads for mutation, `GetFirstWorkoutDateAsync` as the precedent for a
  projecting query inside a repository),
  `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` (233 lines — `DbSet` block L13–24, `AthleteSportZone`
  config L174–183, `WorkoutStepResult` config L215–230, the `TrainingPlan` config's L111–115 comment on why
  a DB-level `SET NULL` on `Event`→`TrainingPlan` would create a second cascade path — the same class of
  reasoning ADR-0010 §4 gives for skipping the FK entirely),
  `api/Bryk.Infrastructure/Migrations/20260608195550_AddWorkoutExecution.cs` (the most recent migration —
  the `CreateTable`/`CreateIndex`/no-`AddForeignKey`-unless-needed shape the new one must match),
  `api/Bryk.API/Program.cs:99–107` (the repositories `AddScoped` block — confirm line 106 is
  `IWorkoutRepository`, line 107 is `IUnitOfWork`) and `:35` (`AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>()`
  — no manual validator line is ever needed, and none is added this task since there is no validator yet),
  `api/Bryk.API.Tests/Fixtures/BrykWebApplicationFactory.cs:73–79` (the test factory replaces the
  `AddDbContext` registration with `UseInMemoryDatabase` and does **not** re-add
  `AuditableEntityInterceptor` — tests must never assert on `CreatedAt`/`UpdatedAt`),
  `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs:1–23,313–341` (the `JsonOptions`/`ApiError`
  scaffolding and the `factory.Services.CreateScope()` → `ApplicationDbContext` seeding pattern the new
  repository tests reuse).
- Confirm the exact insertion points before editing anything (verified this session, re-confirm by eye):
  `ApplicationDbContext.cs` line 24 is `public DbSet<WorkoutStepResult> WorkoutStepResults => Set<WorkoutStepResult>();`
  (the new `DbSet<ActivityFile>` line goes directly after it); line 230 is the `WorkoutStepResult` block's
  closing `});`, line 231 is the closing `}` of `OnModelCreating` (the new config block goes between them).
  `Program.cs` line 106 is `builder.Services.AddScoped<IWorkoutRepository, WorkoutRepository>();`, line 107
  is `builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();` (the new line goes between them).

## Step 1 — Write ADR-0010 first (`md/decisions/0010-activity-file-import.md`)

New file. Section-for-section skeleton matches ADR-0009: title line, `**Date:**`, `**Status:**`,
`## Context` (with a `### Conventions this ADR follows` subsection), `## Decision` (six numbered
sections), `## Consequences` (with a *For Tasks 19-1 … 19-6* table), `## Alternatives considered`.

```markdown
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
```

**Verify (docs-only step — no compiler gate):**
- File exists at `md/decisions/0010-activity-file-import.md`, matches ADR-0009's section skeleton
  including the *Conventions this ADR follows* subsection and the *For Tasks 19-1 … 19-6* table.
- §3 states in full why `LoadCalculator` is not edited and exactly what the synthetic `WorkoutStepResult`
  carries; §4 names `Workout.SourceFileId` and `WorkoutZoneDuration` as explicitly rejected.
- §5 records histogram normalization as a Phase 21 tech-debt candidate.
- Every numbered section cites the exact convention/file it rests on (ADR-0005 §5's nullable
  `WorkoutStepId`, ADR-0007 §4's 5-bucket collapse, ADR-0003/0004's no-FK convention) — no floating claim.

**STOP — Sr. Dev / reviewer gate.** Per CLAUDE.md and this task's own framing (the ADR "lands first and in
this task"), do not create, edit, or stage any `.cs` file until ADR-0010 has been read and accepted by the
reviewer. Do not proceed to Step 2 on your own authority.

## Step 2 — `ActivityFileFormat.cs`

**New file** `api/Bryk.Domain/Entities/Enums/ActivityFileFormat.cs`:

```csharp
namespace Bryk.Domain.Entities;

public enum ActivityFileFormat
{
    Fit = 1,
    Tcx = 2,
    Gpx = 3
}
```

File lives in `Entities/Enums/` with namespace `Bryk.Domain.Entities` — the convention every existing enum
follows (`Entities/Enums/Sport.cs` is the precedent: `namespace Bryk.Domain.Entities;` even though the file
sits in the `Enums/` subfolder — do not add a nested `Bryk.Domain.Entities.Enums` namespace). Explicit
values starting at 1, matching `Sport`/`ZoneMetric`.

**Verify:** `dotnet build api/Bryk.Domain/Bryk.Domain.csproj` green (new, unreferenced type — trivial; zero
`PackageReference`s in this project, confirm the build didn't add one).

## Step 3 — `ActivityFile.cs`

**New file** `api/Bryk.Domain/Entities/ActivityFile.cs`:

```csharp
using Bryk.Domain.Interfaces;

namespace Bryk.Domain.Entities;

// Raw upload for the two-step activity-file import flow (ADR-0010 §2/§4/§5): the parsed preview and the
// eventual Workout commit are two separate calls, and the uploaded bytes have to live somewhere between
// them. Content is the raw file bytes (varbinary(max) — no filesystem path, no blob store, ADR-0010 §2).
// AthleteId is denormalized + indexed with no FK to Athlete, matching Workout/WorkoutStepResult
// (ADR-0003/0004). ParsedWorkoutId is a plain indexed Guid? with NO FK to Workout (ADR-0010 §4) — the
// reverse link the "from file" badge and the duplicate-commit guard both read; a deleted Workout must
// not cascade the uploaded file away, and there is no delete-path to reason about. ZoneHistogramJson
// holds the derived 5-bucket per-zone seconds histogram (ADR-0010 §5), written once at commit and null
// before it. UploadedAt is the domain-facing timestamp, set once by the service at insert
// (DateTime.UtcNow); CreatedAt/UpdatedAt stay owned by AuditableEntityInterceptor and are NEVER set
// manually (CLAUDE.md) — the redundancy between UploadedAt and CreatedAt is deliberate, not a mistake.
public class ActivityFile : IAuditable
{
    public Guid Id { get; set; }
    public Guid AthleteId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ActivityFileFormat Format { get; set; }
    public int ByteSize { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public DateTime UploadedAt { get; set; }
    public Guid? ParsedWorkoutId { get; set; }
    public string? ZoneHistogramJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

No navigation properties — not to `Athlete`, not to `Workout`. No `ValueGeneratedNever()` on `Id`: unlike
`Athlete.Id` (externally supplied via `ICurrentUserService`), `ActivityFile.Id` is a service-generated
`Guid.NewGuid()` at upload time (Task 19-4), the same pattern `Workout`/`WorkoutStepResult` already use
with no special EF configuration.

**Verify:** `dotnet build api/Bryk.Domain/Bryk.Domain.csproj` green.

## Step 4 — `IActivityFileRepository.cs`

**New file** `api/Bryk.Domain/Interfaces/IActivityFileRepository.cs`. Exactly four members — the complete
surface 19-4 and 19-6 consume; **no sibling task may extend this file**, so it ships whole here:

```csharp
using Bryk.Domain.Entities;

namespace Bryk.Domain.Interfaces;

/// <summary>
/// Repository for the <see cref="ActivityFile"/> row (ADR-0010). Staging methods do NOT call SaveChanges.
/// </summary>
public interface IActivityFileRepository
{
    /// <summary>Stages a new <see cref="ActivityFile"/> for insertion. Does NOT call SaveChanges.</summary>
    Task AddAsync(ActivityFile file, CancellationToken ct = default);

    /// <summary>
    /// Loads an <see cref="ActivityFile"/> <b>tracked</b> (including <see cref="ActivityFile.Content"/>),
    /// for commit (set <see cref="ActivityFile.ParsedWorkoutId"/> + the histogram) and discard. Null if missing.
    /// </summary>
    Task<ActivityFile?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The athlete's <see cref="ActivityFile"/> rows whose <see cref="ActivityFile.ParsedWorkoutId"/> is in
    /// <paramref name="workoutIds"/> — the reverse "which workouts came from a file" lookup (ADR-0010 §4).
    /// <b>Never loads <see cref="ActivityFile.Content"/></b>; the returned instances carry an empty
    /// <c>Content</c>. No-tracking. An empty <paramref name="workoutIds"/> returns an empty list with no query.
    /// </summary>
    Task<IReadOnlyList<ActivityFile>> GetByParsedWorkoutIdsAsync(Guid athleteId, IEnumerable<Guid> workoutIds, CancellationToken ct = default);

    /// <summary>Stages an existing <see cref="ActivityFile"/> for deletion. Does NOT call SaveChanges.</summary>
    void Delete(ActivityFile file);
}
```

No `Update` method — commit mutates the tracked entity from `GetByIdTrackedAsync` and the service calls
`SaveChangesAsync` once, the same discipline `WorkoutService.UpdateAsync` uses (no `repo.Update` call on an
already-tracked entity). No `GetByIdAsync` (no-tracking) — nothing reads a single file for display in this
task; don't ship speculative code.

**Verify:** `dotnet build api/Bryk.Domain/Bryk.Domain.csproj` and
`dotnet build api/Bryk.Infrastructure/Bryk.Infrastructure.csproj` green (the interface alone compiles;
`Bryk.Infrastructure` still builds even before the implementation exists, since nothing references the
interface yet).

## Step 5 — `ActivityFileRepository.cs`

**New file** `api/Bryk.Infrastructure/Repositories/ActivityFileRepository.cs`:

```csharp
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using Bryk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bryk.Infrastructure.Repositories;

public class ActivityFileRepository(ApplicationDbContext db) : IActivityFileRepository
{
    public async Task AddAsync(ActivityFile file, CancellationToken ct = default) => await db.ActivityFiles.AddAsync(file, ct);

    public async Task<ActivityFile?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default)
    {
        return await db.ActivityFiles.FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<IReadOnlyList<ActivityFile>> GetByParsedWorkoutIdsAsync(Guid athleteId, IEnumerable<Guid> workoutIds, CancellationToken ct = default)
    {
        var ids = workoutIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return Array.Empty<ActivityFile>();
        }

        // Project scalar columns only — never Content. 19-6 calls this once per workout in a 90-day
        // analytics range; loading varbinary(max) for every matched row would be tens of megabytes per
        // request. Do NOT replace this with a plain entity query "for readability".
        var rows = await db.ActivityFiles
            .AsNoTracking()
            .Where(f => f.AthleteId == athleteId && f.ParsedWorkoutId != null && ids.Contains(f.ParsedWorkoutId.Value))
            .Select(f => new { f.Id, f.AthleteId, f.FileName, f.Format, f.ByteSize, f.UploadedAt, f.ParsedWorkoutId, f.ZoneHistogramJson })
            .ToListAsync(ct);

        return rows.Select(r => new ActivityFile
        {
            Id = r.Id,
            AthleteId = r.AthleteId,
            FileName = r.FileName,
            Format = r.Format,
            ByteSize = r.ByteSize,
            UploadedAt = r.UploadedAt,
            ParsedWorkoutId = r.ParsedWorkoutId,
            ZoneHistogramJson = r.ZoneHistogramJson
        }).ToList();
    }

    public void Delete(ActivityFile file) => db.ActivityFiles.Remove(file);
}
```

Primary-ctor, matching `WorkoutRepository`. `AddAsync`/`Delete` are one-liners like `WorkoutRepository`'s;
`GetByIdTrackedAsync` intentionally has **no** `AsNoTracking()` (tracked, for the commit-time mutation
19-4 performs). `GetByParsedWorkoutIdsAsync` is the one method with real logic — materialize ids, short
circuit on empty (mirrors `GetPlannedWorkoutsByIdsWithStructureAsync`'s "empty ids → empty list with no
query" contract), project scalars only, rebuild client-side.

**Verify:** `dotnet build api/Bryk.sln` green, 0 new warnings (still 16 total). At this point
`ActivityFile`/`ActivityFileFormat`/`IActivityFileRepository`/`ActivityFileRepository` all exist and
compile but are referenced by nothing yet — expected, not dead code to wire up early (`ApplicationDbContext`
doesn't know about the entity until Step 6).

## Step 6 — `ApplicationDbContext.cs` (edit — two additive blocks)

**File:** `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs`.

**6a. `DbSet`** — insert directly after line 24
(`public DbSet<WorkoutStepResult> WorkoutStepResults => Set<WorkoutStepResult>();`), before the blank line
preceding `OnModelCreating`:

```csharp
    public DbSet<ActivityFile> ActivityFiles => Set<ActivityFile>();
```

**6b. Configuration block** — insert directly after line 230 (the `WorkoutStepResult` block's closing
`});`) and before line 231 (`OnModelCreating`'s closing `}`):

```csharp

        // ActivityFile configuration (ADR-0010 §2/§4/§5)
        modelBuilder.Entity<ActivityFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(260);
            entity.Property(e => e.Content).IsRequired();

            // ParsedWorkoutId is a plain indexed Guid? with NO FK to Workout (ADR-0010 §4): the link is
            // one-directional (reverse lookup only) and deleting a workout must not cascade away the
            // uploaded file. Indexed because analytics reads it once per range query.
            entity.HasIndex(e => e.ParsedWorkoutId);
            // Denormalized AthleteId, no FK to Athlete (ADR-0003/0004).
            entity.HasIndex(e => e.AthleteId);
        });
```

**Do not** add `HasColumnType` for `Content` or `ZoneHistogramJson` — EF Core's SQL Server provider already
maps an unbounded `byte[]` to `varbinary(max)` and an unbounded `string?` to `nvarchar(max)` with no
explicit configuration (exactly as `WorkoutStepResult.Rpe`'s `decimal` needs `HasPrecision` but its `int?`
fields need nothing). **Do not** put `HasMaxLength` on `Content` — that would emit a bounded
`varbinary(n)`. Verify both mappings in the generated migration (Step 7), not in the model. **Do not
touch** the `Workout` (L147–171) or `WorkoutStepResult` (L215–230) blocks — this task's only edit to this
file is the two additive blocks above.

**Verify:** `dotnet build api/Bryk.sln` green, 0 new warnings. Confirm by reading the file back that
exactly two net-new lines/blocks were added and nothing existing moved or reformatted.

## Step 7 — The migration — **approval required before apply**

**This is the CLAUDE.md Sr. Dev migration gate.** Generate, read, get sign-off — do not run
`dotnet ef database update` until that sign-off exists.

**7a. Generate**, from the repo root:

```
dotnet ef migrations add AddActivityFile --project api/Bryk.Infrastructure --startup-project api/Bryk.API
```

This produces three files: `Migrations/<timestamp>_AddActivityFile.cs`,
`Migrations/<timestamp>_AddActivityFile.Designer.cs`, and a regenerated
`Migrations/ApplicationDbContextModelSnapshot.cs`.

**7b. Read the generated `Up`/`Down` in full** before doing anything else. Confirm, by eye, against the
most recent migration's shape (`20260608195550_AddWorkoutExecution.cs`, Step 0):

- Exactly **one** `migrationBuilder.CreateTable(name: "ActivityFiles", ...)` and **nothing else** — no
  `AddColumn`, no `AlterColumn`, no second `CreateTable`. If the generated migration touches **any other
  table** — including `Workouts` — the model has drifted: **STOP and ask**. Do not hand-edit the migration
  to remove the extra operations; that means the model, not the migration, is wrong.
- Columns and types, read literally off the generated `table: table => new { ... }` block: `Id
  uniqueidentifier not null`, `AthleteId uniqueidentifier not null`, `FileName nvarchar(260) not null`,
  `Format int not null` (the enum, stored as its underlying `int` — no `HasConversion<string>()` was
  configured, so this is expected), `ByteSize int not null`, `Content varbinary(max) not null`,
  `UploadedAt datetime2 not null`, `ParsedWorkoutId uniqueidentifier nullable`, `ZoneHistogramJson
  nvarchar(max) nullable`, `CreatedAt datetime2 not null`, `UpdatedAt datetime2 not null`.
- Exactly **two** `migrationBuilder.CreateIndex(...)` calls: `IX_ActivityFiles_AthleteId` (single-column,
  `AthleteId`) and `IX_ActivityFiles_ParsedWorkoutId` (single-column, `ParsedWorkoutId`).
- **Zero** `migrationBuilder.AddForeignKey(...)` calls anywhere in `Up`. This is the single most important
  line-by-line check — a stray FK to `Workouts` or `Athletes` would silently violate ADR-0010 §4.
  `table.PrimaryKey("PK_ActivityFiles", x => x.Id)` inside the `constraints:` lambda is expected and is not
  a foreign key.
- `Down` is exactly `migrationBuilder.DropTable(name: "ActivityFiles");` and nothing else.
- `ApplicationDbContextModelSnapshot.cs` — the tool regenerates this file; **commit it as generated, do
  not hand-edit it.**

**7c. STOP — Sr. Dev / reviewer gate.** Present the generated `Up`/`Down` (or the three new files) for
review. Do **not** run `dotnet ef database update` until sign-off is explicit. This mirrors CLAUDE.md's
"Migrations are code-first. Generate, review, get Sr. Dev approval before applying" and this task's own
framing ("one reviewed migration... do not apply blind").

**7d. Apply**, only after sign-off, from the repo root:

```
dotnet ef database update --project api/Bryk.Infrastructure --startup-project api/Bryk.API
```

**Verify:** `dotnet build api/Bryk.sln` green (the migration files compile as ordinary C#; this is a build
check, not a DB check). If a local dev SQL Server is reachable, confirm the `ActivityFiles` table exists
with the column/index shape above; if no local dev DB is configured for this session, the build-green
check plus the by-eye `Up`/`Down` review is the gate — do not block the rest of this task on DB
reachability. **If at any point a second migration seems necessary — STOP and ask; do not generate one.**

## Step 8 — `Program.cs` (edit — exactly one line)

**File:** `api/Bryk.API/Program.cs`. Insert directly after line 106
(`builder.Services.AddScoped<IWorkoutRepository, WorkoutRepository>();`) and before line 107
(`builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();`):

```csharp
builder.Services.AddScoped<IActivityFileRepository, ActivityFileRepository>();
```

No new `using` needed — `Bryk.Domain.Interfaces` (line 9) and `Bryk.Infrastructure.Repositories` (line 12)
are already imported. **Do not** add a validator registration (there is no validator in this task, and the
assembly scan at line 35 would pick one up automatically if there were). **Do not** pre-add
`IActivityFileService`, any `IActivityFileParser` registration, or any form/size options — this line is the
**only** edit to `Program.cs` in this task; Task 19-4 appends the rest on a fresh working tree.

**Verify:** `dotnet build api/Bryk.sln` green. `git diff api/Bryk.API/Program.cs` shows exactly one added
line and nothing else.

## Step 9 — Integration tests: `ActivityFileRepositoryTests.cs`

**New file** `api/Bryk.API.Tests/ActivityFiles/ActivityFileRepositoryTests.cs` (new folder). The repository
needs a real `DbContext`, and only `Bryk.API.Tests` has an EF provider wired (`Bryk.Application.Tests`
references `Bryk.Application` alone, no EF) — so these tests live here, constructing
`ActivityFileRepository` directly against a scoped `ApplicationDbContext`, the same
`factory.Services.CreateScope()` pattern `TrainingPlansControllerTests` uses to seed a foreign athlete.

```csharp
using Bryk.API.Tests.Fixtures;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using Bryk.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bryk.API.Tests.ActivityFiles;

public class ActivityFileRepositoryTests
{
    [Fact]
    public async Task AddAsync_ThenGetByIdTracked_RoundTripsContentFormatAndByteSize()
    {
        await using var factory = new BrykWebApplicationFactory();
        var fileId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new ActivityFileRepository(db);
            await repo.AddAsync(new ActivityFile
            {
                Id = fileId,
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                FileName = "ride.tcx",
                Format = ActivityFileFormat.Tcx,
                ByteSize = 4,
                Content = new byte[] { 1, 2, 3, 4 },
                UploadedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Fresh scope — proves the round trip survives a new DbContext instance, not just the change tracker.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new ActivityFileRepository(db);
            var loaded = await repo.GetByIdTrackedAsync(fileId);

            loaded.Should().NotBeNull();
            loaded!.Content.Should().Equal(1, 2, 3, 4);
            loaded.Format.Should().Be(ActivityFileFormat.Tcx);
            loaded.ByteSize.Should().Be(4);
        }
    }

    [Fact]
    public async Task GetByParsedWorkoutIds_EmptyIds_ReturnsEmpty()
    {
        await using var factory = new BrykWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = new ActivityFileRepository(db);

        var result = await repo.GetByParsedWorkoutIdsAsync(BrykWebApplicationFactory.TestAthleteId, Array.Empty<Guid>());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByParsedWorkoutIds_ReturnsOnlyMatchingRowsForThatAthlete()
    {
        await using var factory = new BrykWebApplicationFactory();
        var w1 = Guid.NewGuid();
        var otherAthleteId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ActivityFiles.AddRange(
                new ActivityFile { Id = Guid.NewGuid(), AthleteId = BrykWebApplicationFactory.TestAthleteId, FileName = "a.fit", Format = ActivityFileFormat.Fit, ByteSize = 1, Content = new byte[] { 1 }, UploadedAt = DateTime.UtcNow, ParsedWorkoutId = w1 },
                new ActivityFile { Id = Guid.NewGuid(), AthleteId = otherAthleteId, FileName = "b.fit", Format = ActivityFileFormat.Fit, ByteSize = 1, Content = new byte[] { 1 }, UploadedAt = DateTime.UtcNow, ParsedWorkoutId = w1 },
                new ActivityFile { Id = Guid.NewGuid(), AthleteId = BrykWebApplicationFactory.TestAthleteId, FileName = "c.fit", Format = ActivityFileFormat.Fit, ByteSize = 1, Content = new byte[] { 1 }, UploadedAt = DateTime.UtcNow, ParsedWorkoutId = null });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new ActivityFileRepository(db);

            var result = await repo.GetByParsedWorkoutIdsAsync(BrykWebApplicationFactory.TestAthleteId, new[] { w1 });

            result.Should().ContainSingle();
            result[0].ParsedWorkoutId.Should().Be(w1);
            result[0].AthleteId.Should().Be(BrykWebApplicationFactory.TestAthleteId);
        }
    }

    [Fact]
    public async Task GetByParsedWorkoutIds_DoesNotLoadContent()
    {
        await using var factory = new BrykWebApplicationFactory();
        var workoutId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ActivityFiles.Add(new ActivityFile
            {
                Id = Guid.NewGuid(),
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                FileName = "run.fit",
                Format = ActivityFileFormat.Fit,
                ByteSize = 4,
                Content = new byte[] { 9, 9, 9, 9 },
                UploadedAt = DateTime.UtcNow,
                ParsedWorkoutId = workoutId,
                ZoneHistogramJson = "[]"
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new ActivityFileRepository(db);

            var result = await repo.GetByParsedWorkoutIdsAsync(BrykWebApplicationFactory.TestAthleteId, new[] { workoutId });

            result.Should().ContainSingle();
            result[0].Content.Should().BeEmpty(); // proves the projection dropped the varbinary column
            result[0].ByteSize.Should().Be(4);     // ...while keeping the cheap scalar columns
            result[0].ZoneHistogramJson.Should().Be("[]");
        }
    }

    [Fact]
    public async Task Delete_RemovesTheRow()
    {
        await using var factory = new BrykWebApplicationFactory();
        var fileId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ActivityFiles.Add(new ActivityFile
            {
                Id = fileId,
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                FileName = "delete-me.gpx",
                Format = ActivityFileFormat.Gpx,
                ByteSize = 2,
                Content = new byte[] { 5, 6 },
                UploadedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new ActivityFileRepository(db);
            var tracked = await repo.GetByIdTrackedAsync(fileId);
            repo.Delete(tracked!);
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = new ActivityFileRepository(db);
            var result = await repo.GetByIdTrackedAsync(fileId);
            result.Should().BeNull();
        }
    }
}
```

Note `BrykWebApplicationFactory` gives each **factory instance** a fixed InMemory database name
(`_databaseName`, set once in the constructor) — every `CreateScope()` call against the *same* `factory`
shares that database, which is exactly what "re-read through a fresh scope" means here: fresh `DbContext`
instance and change tracker, same underlying store. Do **not** assert on `CreatedAt`/`UpdatedAt` anywhere
in this file — the InMemory registration in `BrykWebApplicationFactory.ConfigureWebHost` does not re-add
`AuditableEntityInterceptor`. No controller/service tests in this task — there is no endpoint and no
service yet (19-4 adds both); this is stated in the commit body, not worked around with a stub host.

**Verify:**
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln --filter FullyQualifiedName~ActivityFileRepositoryTests
```
Build green, 0 new warnings. All 5 facts pass by name: `AddAsync_ThenGetByIdTracked_RoundTripsContentFormatAndByteSize`,
`GetByParsedWorkoutIds_EmptyIds_ReturnsEmpty`, `GetByParsedWorkoutIds_ReturnsOnlyMatchingRowsForThatAthlete`,
`GetByParsedWorkoutIds_DoesNotLoadContent`, `Delete_RemovesTheRow`.

## Step 10 — Final verification, smoke check, and commit

Run the full command set from `Tasks-19-1.md`:
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```

- `dotnet build` — 0 errors, **16 warnings** (unchanged from the Step 0 baseline — the design-time
  `System.Security.Cryptography.Xml` NU1903 plus the two pre-existing `WorkoutsControllerTests.cs`
  nullable warnings; no new warning introduced by this task's files).
- `dotnet test api/Bryk.sln` — **267 tests** (262 baseline + the 5 new `ActivityFileRepositoryTests` facts),
  all green, no failures. `Bryk.Application.Tests` stays at **173** (untouched by this task);
  `Bryk.API.Tests` rises from **89** to **94**.
- `pnpm run build` — green (this task touches no UI file; sanity check only).
- `pnpm exec vitest run --no-file-parallelism` — **252 tests / 56 files**, byte-for-byte unchanged from
  baseline — if this number moved, something outside this task's scope changed; stop and investigate
  before committing.
- `git status` / `git add -A && git diff --cached --stat` — confirm **only** these files appear:
  - `md/decisions/0010-activity-file-import.md` (new)
  - `api/Bryk.Domain/Entities/Enums/ActivityFileFormat.cs` (new)
  - `api/Bryk.Domain/Entities/ActivityFile.cs` (new)
  - `api/Bryk.Domain/Interfaces/IActivityFileRepository.cs` (new)
  - `api/Bryk.Infrastructure/Repositories/ActivityFileRepository.cs` (new)
  - `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` (modified — two additive blocks)
  - `api/Bryk.Infrastructure/Migrations/<timestamp>_AddActivityFile.cs` (new)
  - `api/Bryk.Infrastructure/Migrations/<timestamp>_AddActivityFile.Designer.cs` (new)
  - `api/Bryk.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs` (regenerated)
  - `api/Bryk.API/Program.cs` (modified — one added line)
  - `api/Bryk.API.Tests/ActivityFiles/ActivityFileRepositoryTests.cs` (new)
  If the diff shows `Workout.cs`, `WorkoutStepResult.cs`, `LoadCalculator.cs`, any file under `ui/`,
  `Bryk.Infrastructure.csproj`, any `Bryk.Application/ActivityFiles/*` or `Bryk.API/Controllers/*` file, or
  a second migration — **STOP**, that is scope creep beyond `Tasks-19-1.md`'s "What NOT to modify" /
  Non-goals fence.
- Confirm the `Program.cs` diff is exactly the one line from Step 8 — no validator line, no service line, no
  form-options line.
- Confirm `GetByParsedWorkoutIdsAsync` never selects `Content` (re-read Step 5's implementation) and that
  `GetByParsedWorkoutIds_DoesNotLoadContent` actually exercises it.
- Commit with the message from `Tasks-19-1.md` (no AI co-author trailer — project convention):

```
feat: ADR-0010 + ActivityFile entity, repository and migration

Open Phase 19 with the decisions record, because the ROADMAP's Phase 19
prose has drifted from what was actually approved. ADR-0010 pins five
things: the official Garmin FIT SDK 21.205.0 goes into Bryk.Infrastructure
only (proprietary royalty-free FIT Protocol License, approved 2026-07-26);
raw bytes live on the ActivityFile row as varbinary(max) behind a 25 MB
per-route cap; imported power and pace reach the load math through one
synthetic WorkoutStepResult rather than any edit to LoadCalculator, whose
session path hardcodes power/pace to null and can only reach the HR branch;
the migration creates ActivityFile and nothing else - no Workout.SourceFileId
and no WorkoutZoneDuration, both of which the ROADMAP names and neither of
which is approved; and the derived per-zone histogram is a JSON column on the
same row, reported as method "samples" by Phase 15's time-in-zone read.

The entity follows the established denormalized-AthleteId-no-FK convention
and carries ParsedWorkoutId as a plain indexed Guid? with no FK to Workout:
that reverse lookup is what feeds the "from file" badge and the
duplicate-commit guard, so Workout stays untouched this phase. The repository
ships its complete four-method surface up front (no sibling task may extend
it) and the reverse lookup projects scalars only, never the varbinary column,
because analytics calls it once per 90-day range. One AddScoped line; the
service DI and the upload size cap are Task 19-4's append to the same file.

Migration AddActivityFile is generated and reviewed, not applied blind.
```
