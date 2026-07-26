# Task 19-1 — ADR-0010 + `ActivityFile` entity, repository, DbContext config, migration

## Surface
Backend only. One new ADR (`md/decisions/0010-activity-file-import.md`), one new domain entity + enum,
one repository contract + implementation, an `ApplicationDbContext` `DbSet` + configuration block, **one
reviewed migration (Sr. Dev gate — generate and read it, do not apply blind)**, and **one** `AddScoped`
line in `Program.cs`. No parser, no service, no controller, no DTO, no UI. Nothing in this task is
reachable over HTTP; 19-4 wires it up.

## Why
Phase 19's flow is deliberately two-step: `POST /activityfiles` parses and previews, `POST
/activityfiles/{id}/commit` creates the `Workout`. Between those two calls the uploaded bytes have to
live somewhere, and the athlete must be able to discard a preview without leaving a phantom workout
behind. That is the `ActivityFile` row. It also carries the two pieces of state the rest of the phase
keys on: `ParsedWorkoutId` (the reverse link that both the "from file" badge and the duplicate-commit
guard read) and the derived per-zone seconds histogram (which Phase 15's time-in-zone read unions in
19-6). The ADR lands **first and in this task** because five decisions were taken by the Sr. Dev on
2026-07-26 that contradict the ROADMAP's Phase 19 prose — most importantly the migration is
`ActivityFile` **only**, not the three-object set the ROADMAP names — and a durable record beats a
commit message, exactly as ADR-0009 §6 recorded the `RecoveryWeekPercentage` scale correction.

## Depends on
- **ROADMAP Phase 19** (lines 527–551) — goal, scope, out-of-scope, the six-task split.
- **ADR-0003 / ADR-0004** — the denormalized-`AthleteId`-with-no-FK convention this entity follows.
- **ADR-0005 §5** — `WorkoutStepResult.WorkoutStepId` is already nullable, which is what makes ADR-0010
  §3's synthetic step result possible with no migration.
- **ADR-0007 §4** — the 5-bucket time-in-zone histogram whose provenance ADR-0010 §5 extends.
- Nothing in this task depends on 19-2 … 19-6. It lands **first and alone**.

## Required reading
- `ROADMAP.md:527–551` — the Phase 19 entry. Read it, then read ADR-0010's §4/§5 corrections: line 534
  names `Workout.SourceFileId` and a `WorkoutZoneDuration` table that are **not approved**.
- `md/decisions/0009-periodization-ramp-model.md` — **the ADR format template**: title line, `**Date:**`,
  `**Status:** Accepted (date) — one-sentence summary`, `## Context` with a *Conventions this ADR follows*
  subsection, numbered `## Decision` sections, `## Consequences` with a per-task table,
  `## Alternatives considered`. Match it section-for-section.
- `md/decisions/0007-progress-analytics.md` — the second format reference (per-task consequences table).
- `api/Bryk.Domain/Entities/WorkoutStepResult.cs:5–8` — the entity header-comment style that names the
  ADR and explains the denormalized `AthleteId`. Mirror it.
- `api/Bryk.Domain/Entities/Workout.cs` — the entity shape this task **does not touch**; confirm for
  yourself that there is no `SourceFileId`, no `AvgPower`, no `AvgPace`.
- `api/Bryk.Domain/Interfaces/IWorkoutRepository.cs` — the repository-contract XML-doc style
  (`"No-tracking."`, `"Does NOT call SaveChanges."`), and `GetPlannedWorkoutsByIdsWithStructureAsync`'s
  doc in `ITrainingPlanRepository.cs:40–46` for the "empty ids → empty list with no query" wording.
- `api/Bryk.Infrastructure/Repositories/WorkoutRepository.cs` — primary-ctor repository, `AsNoTracking()`
  for display reads, tracked reads for mutation, `GetFirstWorkoutDateAsync` as the precedent for a
  projecting query inside a repository.
- `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` — the `DbSet` block (L13–24), the
  `AthleteSportZone` config (L174–183) and the `WorkoutStepResult` config (L215–230) for the
  "Denormalized AthleteId, no FK to Athlete (ADR-0003/0004)" comment convention.
- `api/Bryk.Infrastructure/Migrations/20260608195550_AddWorkoutExecution.cs` — the most recent migration;
  the shape and naming the new one must match.
- `api/Bryk.API/Program.cs:99–107` — the repositories `AddScoped` block (`IWorkoutRepository` at L106);
  `:35` — `AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>()`.
- `api/Bryk.API.Tests/Fixtures/BrykWebApplicationFactory.cs:73–79` — note that the test factory replaces
  the `AddDbContext` registration with `UseInMemoryDatabase` and **does not** re-add
  `AuditableEntityInterceptor`. Tests must therefore never assert on `CreatedAt`/`UpdatedAt`.

## Acceptance criteria

### 1. `md/decisions/0010-activity-file-import.md` (new) — write this **first**

Header:

```
# ADR-0010 — Activity file import (storage, parsing boundary, load routing)

**Date:** 2026-07-26
**Status:** Accepted (2026-07-26) — FIT parsing uses the official `Garmin.FIT.Sdk` 21.205.0 in
`Bryk.Infrastructure` only; raw bytes live on the `ActivityFile` row as `varbinary(max)` behind a 25 MB
API cap; imported power/pace reaches the load math through one synthetic `WorkoutStepResult` rather than
any change to `LoadCalculator`; the migration creates `ActivityFile` and nothing else (no
`Workout.SourceFileId`, no `WorkoutZoneDuration`); the derived per-zone seconds histogram is a JSON
column on `ActivityFile` and reports method `samples`.
```

`## Context` explains: Phase 19 turns a device file into a `Workout` with real actuals; the ROADMAP
flags four decisions under *Decisions needed* (FIT SDK approval, migration approval, raw-file storage,
sample persistence) and all four were taken on 2026-07-26; **the ROADMAP's Backend-scope prose has since
drifted from those decisions and this ADR is the durable correction** (call out lines 534 and 536 by
what they claim, not by line number alone). Include a **### Conventions this ADR follows** subsection
stating, grounded in the files above:

- Athlete identity always via `ICurrentUserService`; missing/foreign resources are
  `KeyNotFoundException` → 404. Phase 12 auth stays deferred and approval-gated.
- Errors use the existing middleware contract (`ExceptionHandlingMiddleware`): `ValidationException` →
  400 with `{status, error, errors[], traceId}`, `KeyNotFoundException` → 404, `InvalidOperationException`
  → 409. **No ProblemDetails rework** — Phase 21 owns that.
- Repository pattern; `IUnitOfWork` owns the commit; every write path commits **once**.
- Validation is `await validator.ValidateOrThrowAsync(request, ct)` (`Bryk.Application.Common.Validation`),
  never FluentValidation's `ValidateAndThrowAsync`.
- Zones are the existing `ZoneMetric` enum (`Power=1, Hr=2, Pace=3`) and the existing 5-bucket
  collapse (`Math.Min(z, 5)`) from ADR-0007 §4. Phase 19 introduces **no new zone enum**.

`## Decision` carries six numbered sections:

**§1 — FIT parsing uses the official Garmin FIT SDK.** `Garmin.FIT.Sdk` **21.205.0**, added to
**`Bryk.Infrastructure` only** (Task 19-3). Record: publisher-verified Garmin International; ships
`net46 / netcoreapp2.0 / netstandard2.0`, and `netstandard2.0` is `net10.0`-compatible; the license is
Garmin's proprietary **royalty-free FIT Protocol License Agreement** shipped as `LICENSE.txt` in the
package — **not** an OSI license. Approved by the Sr. Dev on 2026-07-26, so all three formats
(`.fit`/`.tcx`/`.gpx`) ship in this phase and the ROADMAP's "degrade to TCX/GPX-only" fallback is moot.
`.tcx`/`.gpx` stay on `System.Xml.Linq` — **no package** (Task 19-2). Both sit behind one Application
abstraction, `IActivityFileParser`, so the FIT dependency never leaks past `Bryk.Infrastructure`.

**§2 — Raw bytes live in the database.** `ActivityFile.Content` is `byte[]` → `varbinary(max)`. No
filesystem path, no upload-root configuration, no blob store. Rationale: the app is pre-deployment, a
DB row is the only storage that is transactional with the rest of the commit and needs zero new config
or ops surface; a ~25 MB cap enforced at the API boundary bounds the damage. Phase 21 may revisit when
deployment topology is real. **The cap is per-route, not global** — no Kestrel-wide or app-wide
`FormOptions` change (Task 19-4).

**§3 — Imported power/pace reaches the load math through a synthetic `WorkoutStepResult`.** This is the
ADR's most load-bearing section; write it in full. `LoadCalculator.ComputeActualLoad` (lines 74–83) sums
`ActualCardioTss` per `WorkoutStepResult` when `workout.StepResults.Count > 0`, and otherwise (line 88)
calls the session path with `avgPower`/`avgPace` **hardcoded null**, so a session-level import could only
ever reach the HR branch. The ROADMAP's claim that "imported power finally exercises the top IF branch"
is therefore **false as written**. The fix is not to change the calculator: on commit the service creates
**one** `WorkoutStepResult` with `WorkoutStepId = null` (already nullable, ADR-0005 §5), `OrderIndex = 0`,
carrying the parsed `AvgPower` / `AvgPace` / `AvgHr` / `ActualDurationSeconds` / `ActualDistanceMeters`.
That routes the import into the existing StepResults branch and reaches the real power and pace IF
branches with **zero migration and zero edit to `LoadCalculator.cs`**. It also lights up the existing
bike session-power derivation in `AnalyticsService.cs:158–169` for free. State normatively:
**`LoadCalculator.cs` is frozen for Phase 19**; no `Workout.AvgPower`/`AvgPace` column; **no per-lap step
results in v1**.

**§4 — One migration: `ActivityFile` and nothing else.** Approved: the `ActivityFile` entity + table.
**Not approved, do not create:** a `Workout.SourceFileId` column, a `WorkoutZoneDuration` child table.
Both appear in ROADMAP line 534 and are superseded here. Binding consequences: the **"from file" badge**
derives from a reverse lookup on `ActivityFile.ParsedWorkoutId == workoutId`, so `Workout.cs` is untouched
by this phase; **duplicate-commit rejection** keys on `ActivityFile.ParsedWorkoutId is not null`, not on a
`Workout` column; there is **no FK** from `ActivityFile` to `Workout` (a deleted workout must not cascade
the uploaded file away, and there is no delete-path to reason about) — just an index on `ParsedWorkoutId`.
Any second migration in Phase 19 → **STOP and ask**.

**§5 — The zone histogram is a JSON column on `ActivityFile`.** The derived per-zone seconds histogram is
serialized to a `string?` column (`ZoneHistogramJson`) on the same row as the bytes, written at commit.
Phase 15's time-in-zone read unions it via `ActivityFile.ParsedWorkoutId → Workout` and reports method
**`samples`**, which **takes precedence over structure and sessionAvg for covered workouts** (Task 19-6).
`ZoneTimeMethodBreakdownDto` gains an additive `SampleSeconds` field; the always-"estimated" badge becomes
conditional. Rationale for JSON over the ROADMAP's table: the histogram is read as a whole, never queried
per-zone, and a table costs a second migration this phase is not approved for. **Normalizing it into a
real child table is a Phase 21 candidate — record it as tech debt in the phase handoff.**

**§6 — No per-second sample persistence.** Parsers materialize a sample series in memory; only derived
aggregates (session actuals + the 5-bucket histogram) are persisted. The file itself is kept, so richer
analytics can re-parse later. This is the ROADMAP's own recommendation, made binding.

`## Consequences` lists what is closed (all four ROADMAP *Decisions needed* bullets) and what is created,
with an explicit **"one migration, one new package (`Garmin.FIT.Sdk`, 19-3 only)"** line and this table:

| Task | Surface | Depends on | Pins from this ADR |
|---|---|---|---|
| **19-1** ADR + `ActivityFile` entity/repo/migration | Backend | — | §2 §4 §5 (the row shape) |
| **19-2** `IActivityFileParser` + TCX/GPX + histogram math | Backend | 19-1 (contract only) | §1 (no package), §5 (bucket shape), §6 |
| **19-3** `FitActivityParser` + the package | Backend | 19-2 | §1 (SDK + license) |
| **19-4** service + DTOs + validators + controller | Backend | 19-1, 19-2 | §2 (cap), §3 (synthetic step result), §4 (duplicate guard), §5 (writes the JSON) |
| **19-5** upload + review UI + "from file" badge | Frontend | 19-4 | §4 (badge via reverse lookup) |
| **19-6** `samples` time-in-zone | Backend + Frontend | 19-2, 19-4 | §5 (precedence + `SampleSeconds`) |

`## Alternatives considered` — at minimum: filesystem/blob storage for the raw bytes (rejected, §2 — new
config and ops surface, non-transactional); `Workout.SourceFileId` (rejected, §4 — a second column and a
second write path for information the reverse index already answers); a `WorkoutZoneDuration` table
(rejected, §5 — normalizes data that is only ever read whole, at the cost of an unapproved migration);
teaching `LoadCalculator` a session-level power/pace path (rejected, §3 — it would change the load of
every existing session-level workout, an unannounced behavior change to persisted history); per-lap step
results on import (rejected for v1, §3 — the planned-vs-actual table has no lap concept yet); vendor
OAuth / device sync (out of scope by ROADMAP lock, not re-litigated).

### 2. `api/Bryk.Domain/Entities/Enums/ActivityFileFormat.cs` (new)

```csharp
namespace Bryk.Domain.Entities;

public enum ActivityFileFormat
{
    Fit = 1,
    Tcx = 2,
    Gpx = 3
}
```
File lives in `Entities/Enums/` with namespace `Bryk.Domain.Entities` — the convention every existing
enum follows (see `Entities/Enums/Sport.cs`). Explicit values starting at 1, like `Sport`/`ZoneMetric`.

### 3. `api/Bryk.Domain/Entities/ActivityFile.cs` (new)

```csharp
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
- Header comment in the `WorkoutStepResult.cs:5–8` style, naming ADR-0010 §2/§4/§5 and stating: the raw
  bytes live here (`varbinary(max)`); `AthleteId` is denormalized and indexed with no FK;
  `ParsedWorkoutId` is a **plain indexed `Guid?` with no FK to `Workout`** — it is the reverse link the
  "from file" badge and the duplicate-commit guard read; `ZoneHistogramJson` holds the derived 5-bucket
  histogram, written at commit and null before it.
- **No navigation properties.** Not to `Athlete`, not to `Workout`.
- `UploadedAt` is the domain-facing timestamp, set once by the service at insert
  (`DateTime.UtcNow`); `CreatedAt`/`UpdatedAt` stay owned by `AuditableEntityInterceptor` and are
  **never set manually** (CLAUDE.md). Say so in the comment so the redundancy reads as deliberate.

### 4. `api/Bryk.Domain/Interfaces/IActivityFileRepository.cs` (new)

Exactly four members — this is the complete surface 19-4 and 19-6 consume, and **no sibling task may
extend this file**, so it ships whole here:

```csharp
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
- **No `Update` method.** Commit mutates the tracked entity from `GetByIdTrackedAsync` and the service
  calls `SaveChangesAsync` once — the same discipline `WorkoutService.UpdateAsync` uses (no `repo.Update`
  call on a tracked entity).
- **No `GetByIdAsync` (no-tracking).** Nothing reads a single file for display; don't ship speculative code.

### 5. `api/Bryk.Infrastructure/Repositories/ActivityFileRepository.cs` (new)

`public class ActivityFileRepository(ApplicationDbContext db) : IActivityFileRepository` — primary ctor,
matching `WorkoutRepository`.

- `AddAsync` → `await db.ActivityFiles.AddAsync(file, ct)`.
- `GetByIdTrackedAsync` → `await db.ActivityFiles.FirstOrDefaultAsync(f => f.Id == id, ct)` (tracked, no
  `AsNoTracking`).
- `Delete` → `db.ActivityFiles.Remove(file)`.
- `GetByParsedWorkoutIdsAsync` — the one method with real logic. Materialize the ids first, short-circuit
  on empty, project the **scalar columns only** into an anonymous type, then build the `ActivityFile`
  instances client-side. `Content` must never appear in the projection:
  ```csharp
  var ids = workoutIds.Distinct().ToList();
  if (ids.Count == 0)
  {
      return Array.Empty<ActivityFile>();
  }

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
  ```
  Add a comment stating **why** the anonymous projection exists: 19-6 calls this for every workout in a
  90-day analytics range, and loading `varbinary(max)` for each would be tens of megabytes per request.
  Do **not** replace it with a plain entity query "for readability".

### 6. `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs` (edit — two additive blocks)

- `DbSet` alongside the others (L13–24):
  ```csharp
  public DbSet<ActivityFile> ActivityFiles => Set<ActivityFile>();
  ```
- Configuration block appended **after** the `WorkoutStepResult` block (which currently ends at L230),
  in the existing comment style:
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
- **Do not** add `HasColumnType` for `Content` or `ZoneHistogramJson`: EF Core's SQL Server provider
  already maps an unbounded `byte[]` to `varbinary(max)` and an unbounded `string?` to `nvarchar(max)`.
  **Do not** put `HasMaxLength` on `Content` (that would emit `varbinary(n)`). Verify both in the
  generated migration rather than in the model.
- **Do not touch** the `Workout` (L147–171) or `WorkoutStepResult` (L215–230) configuration blocks.

### 7. The migration — **approval required before apply**

```
dotnet ef migrations add AddActivityFile --project api/Bryk.Infrastructure --startup-project api/Bryk.API
```
- Generate it, **read the generated `Up`/`Down` in full, and get Sr. Dev sign-off before
  `dotnet ef database update`** (CLAUDE.md Sr. Dev gate).
- The `Up` must contain **exactly**: one `CreateTable("ActivityFiles")` and two `CreateIndex`
  (`IX_ActivityFiles_AthleteId`, `IX_ActivityFiles_ParsedWorkoutId`). Confirm the column types read
  `varbinary(max)` for `Content` and `nvarchar(max)` for `ZoneHistogramJson`, `nvarchar(260)` for
  `FileName`, and that there is **no** `AddForeignKey`.
- If the generated migration touches **any other table** — including `Workouts` — the model has drifted:
  **STOP and ask**. Do not hand-edit the migration to remove the extra operations.
- The snapshot file (`ApplicationDbContextModelSnapshot.cs`) is regenerated by the tool; commit it as-is,
  do not hand-edit.

### 8. `api/Bryk.API/Program.cs` (edit — exactly one line)

Append inside the repositories block, directly after `IWorkoutRepository` (L106):
```csharp
builder.Services.AddScoped<IActivityFileRepository, ActivityFileRepository>();
```
- **Do not** add a validator registration — the assembly scan at `Program.cs:35` picks validators up
  automatically.
- **Do not** pre-add `IActivityFileService`, any `IActivityFileParser` registration, or any form/size
  options. `Program.cs` is the one file shared with Task 19-4: **19-1 lands first and adds only this
  line; 19-4 appends the rest.**

## Non-goals
- **No second migration.** Only `AddActivityFile`. If anything in this task appears to need another —
  **STOP and ask** (Sr. Dev gate).
- **Do not add `Workout.SourceFileId`.** The badge and the duplicate-commit guard both read
  `ActivityFile.ParsedWorkoutId` (ADR-0010 §4). If you find yourself reaching for a column on `Workout` —
  **STOP and ask**. `api/Bryk.Domain/Entities/Workout.cs` must not appear in `git diff`.
- **Do not create a `WorkoutZoneDuration` table** or any other child table for the histogram. The
  histogram is `ActivityFile.ZoneHistogramJson` (ADR-0010 §5). If a table feels necessary —
  **STOP and ask**.
- **Do not edit `api/Bryk.Application/Training/Load/LoadCalculator.cs`** — frozen for all of Phase 19.
- **No new NuGet or npm package.** `Garmin.FIT.Sdk` belongs to Task 19-3 and to
  `Bryk.Infrastructure.csproj` only; **do not** pre-add it here.
- **No FK from `ActivityFile` to `Workout` or `Athlete`.**
- Do not write files owned by siblings: `Bryk.Application/ActivityFiles/*` (19-2 and 19-4),
  `Bryk.Infrastructure/ActivityFiles/*` (19-2, 19-3), `Bryk.Infrastructure.csproj` (19-3),
  `Bryk.API/Controllers/ActivityFilesController.cs` (19-4), anything under `ui/` (19-5),
  `Analytics/TimeInZoneCalculator.cs` / `TimeInZoneResponse.cs` / `AnalyticsService.cs` (19-6).
- **No auth code** — Phase 12 stays deferred and approval-gated. Ownership is `ICurrentUserService` +
  `KeyNotFoundException` → 404, nothing else, and neither appears in this task.
- **No ProblemDetails / error-contract rework** — Phase 21 owns it.
- **Do not fix** the two pre-existing nullable warnings in
  `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs:121,150`.
- **Do not** revert, stash, or commit unrelated working-tree changes.
- No vendor OAuth, no device sync, no bulk upload, no per-second sample persistence.

## Test expectations

`api/Bryk.API.Tests/ActivityFiles/ActivityFileRepositoryTests.cs` (new folder). The repository needs a
`DbContext`, and only `Bryk.API.Tests` has an EF provider (`Bryk.Application.Tests` references
`Bryk.Application` alone) — so these live here. Resolve `ApplicationDbContext` from
`factory.Services.CreateScope()`, the pattern `TrainingPlansControllerTests` already uses to seed a
foreign athlete.

- `AddAsync_ThenGetByIdTracked_RoundTripsContentFormatAndByteSize` — add a row with
  `Content = new byte[] { 1, 2, 3, 4 }`, `Format = ActivityFileFormat.Tcx`, `ByteSize = 4`; re-read
  through a **fresh scope** and assert `Content.Should().Equal(1, 2, 3, 4)`, `Format` and `ByteSize`.
  **Do not assert on `CreatedAt`/`UpdatedAt`** — the test factory's InMemory registration does not wire
  `AuditableEntityInterceptor` (`BrykWebApplicationFactory.cs:73–79`).
- `GetByParsedWorkoutIds_EmptyIds_ReturnsEmpty` — `Should().BeEmpty()`.
- `GetByParsedWorkoutIds_ReturnsOnlyMatchingRowsForThatAthlete` — seed three rows: one with
  `ParsedWorkoutId = w1` for `TestAthleteId`, one with `ParsedWorkoutId = w1` for a **different**
  athlete id, one with `ParsedWorkoutId = null`. Query `(TestAthleteId, [w1])` → exactly **1** result.
- `GetByParsedWorkoutIds_DoesNotLoadContent` — the stored row has a 4-byte `Content`; the returned
  instance's `Content.Should().BeEmpty()` while `ByteSize.Should().Be(4)` and
  `ZoneHistogramJson.Should().Be("[]")` (proves the projection kept the cheap columns and dropped the
  expensive one).
- `Delete_RemovesTheRow` — delete + `SaveChangesAsync`, then a fresh-scope `GetByIdTrackedAsync`
  returns null.

No controller/service tests in this task — there is no endpoint and no service yet (19-4 adds both).
State that in the commit body rather than inventing a stub host.

## Verification commands
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```
xUnit must rise from the **262** baseline (173 `Bryk.Application.Tests` + 89 `Bryk.API.Tests`) with zero
failures. Vitest stays at exactly **252 / 56 files** — this task touches no UI. The build's **16**
warnings (9× NU1903 `System.Security.Cryptography.Xml` design-time + the two pre-existing
`WorkoutsControllerTests.cs` nullable warnings + the rest) must not grow.

## Review checklist
- [ ] ADR-0010 exists, is numbered/dated/`Accepted`, matches ADR-0009's section skeleton (including the
      *Conventions this ADR follows* subsection and the per-task consequences table), and records all
      five resolved decisions plus the no-sample-persistence call.
- [ ] ADR §3 states in full why `LoadCalculator` is not edited and what the synthetic `WorkoutStepResult`
      carries; ADR §4 names `Workout.SourceFileId` and `WorkoutZoneDuration` as explicitly rejected.
- [ ] ADR §5 records histogram normalization as a Phase 21 tech-debt candidate.
- [ ] `ActivityFile` implements `IAuditable`, has **no** navigation properties and **no** FKs.
- [ ] The generated migration creates one table and two indexes, nothing else; `Content` is
      `varbinary(max)`; it has been **read and approved before apply**.
- [ ] `git diff --stat` shows no change to `Workout.cs`, `WorkoutStepResult.cs`, `LoadCalculator.cs`, or
      any file under `ui/`.
- [ ] `Program.cs` diff is exactly one `AddScoped` line — no validator line, no service line, no form options.
- [ ] `GetByParsedWorkoutIdsAsync` never selects `Content`, and there is a test proving it.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

## Suggested commit
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
