# Impl 19-4 — Build order: activity-file upload / commit / discard endpoints, validation, match candidates

**Executor:** architect-implementer (per CLAUDE.md).
**Acceptance contract:** `md/Tasks-19-4.md`.
**Decision lock:** `md/decisions/0010-activity-file-import.md` (written by Task 19-1) §2 (25 MB cap,
per-route, no global `FormOptions`), §3 (the synthetic `WorkoutStepResult` — **the load-bearing detail
this build order gates on twice**), §4 (duplicate-commit guard keyed on `ActivityFile.ParsedWorkoutId`,
never on a `Workout` column), §5 (the zone histogram is written to `ActivityFile.ZoneHistogramJson` at
commit, never at upload) — plus ADR-0005 §5 (`WorkoutStepResult.WorkoutStepId` nullable, which is what
makes §3 legal with no migration).
**Scope:** Backend only. No migration (19-1 shipped the only one), no new package (19-3 shipped the only
one), no `ExceptionHandlingMiddleware` change, no UI. This is the largest and highest-risk task in Phase
19 — it is the only task that makes anything in the phase reachable over HTTP.

This is the step-by-step build order. Execute top-to-bottom; each step's verification is the gate to the
next. `ActivityFileService.cs` is written incrementally (skeleton → helpers → each method body) so that
`dotnet build` stays a meaningful gate at every step rather than one big edit at the end. **Step 9 writes
the load-bearing synthetic `WorkoutStepResult` but cannot prove it behaviorally yet** (no host exists);
**Step 16 is the actual proof** — a test that fails if Step 9's object is dropped. Do not consider this
task's headline risk retired until Step 16 passes. One commit at the end with the message in
`Tasks-19-4.md`.

## Step 0 — Pre-flight

- `git status` clean on `main`.
- **Confirm Tasks 19-1, 19-2 and 19-3 have actually landed** — this task's Program.cs edit registers
  three parsers together and its service depends on all three tasks' contracts. Check for the presence
  of, at minimum:
  - 19-1: `api/Bryk.Domain/Entities/ActivityFile.cs`, `api/Bryk.Domain/Entities/Enums/ActivityFileFormat.cs`,
    `api/Bryk.Domain/Interfaces/IActivityFileRepository.cs`,
    `api/Bryk.Infrastructure/Repositories/ActivityFileRepository.cs`,
    `md/decisions/0010-activity-file-import.md` (status `Accepted`), and that
    `api/Bryk.API/Program.cs` already contains
    `builder.Services.AddScoped<IActivityFileRepository, ActivityFileRepository>();` directly after the
    `IWorkoutRepository` line in the repositories block.
  - 19-2: `api/Bryk.Application/ActivityFiles/IActivityFileParser.cs`, `ParsedActivity.cs`,
    `ZoneHistogramEntry.cs`, `ZoneHistogramCalculator.cs`,
    `api/Bryk.Infrastructure/ActivityFiles/ActivitySampleBounds.cs`, `TcxActivityParser.cs`,
    `GpxActivityParser.cs`, and the three fixtures under `api/Bryk.API.Tests/Fixtures/ActivityFiles/`
    (`sample-run.tcx`, `sample-ride.tcx`, `sample-activity.gpx`).
  - 19-3: `api/Bryk.Infrastructure/ActivityFiles/FitActivityParser.cs`, the
    `<PackageReference Include="Garmin.FIT.Sdk" Version="21.205.0" />` line in
    `api/Bryk.Infrastructure/Bryk.Infrastructure.csproj`, and `sample-ride.fit` in the same fixtures
    folder.
  - If any of these is missing — **STOP**. This task cannot start (its Program.cs step and its service's
    `IEnumerable<IActivityFileParser>` dependency assume all three parsers exist and resolve).
- `dotnet build api/Bryk.sln` green. Confirm the warning count is **still ≤ 16** — 19-3 is the only prior
  task in this phase allowed to have moved that number (it adds a package), and its own gate required it
  not to grow past 16 either.
- `dotnet test api/Bryk.sln` green. Expected count, computed from each task's own *Test expectations*
  section (not yet verified against the live repo — confirm the actual number now and treat a divergence
  as a reason to go re-read what landed, not as an error to silently absorb):
  - 262 (Phase 18 close)
  - **+ 5** (19-1's `ActivityFileRepositoryTests`)
  - **+ 25** (19-2's `ZoneHistogramCalculatorTests` 8 + `TcxActivityParserTests` 10 + `GpxActivityParserTests` 7)
  - **+ 9** (19-3's `FitActivityParserTests`)
  - **= 301** expected baseline entering this task.
- `cd ui; pnpm run build` green; `pnpm exec vitest run --no-file-parallelism` at **252 / 56 files**
  (unchanged — no prior Phase 19 task touches `ui/`, and neither does this one until 19-5).
- Re-read `md/Tasks-19-4.md` in full. Open in editor:
  `api/Bryk.Application/Training/Workouts/WorkoutService.cs` (the template — ctor shape, ownership →
  `KeyNotFoundException`, `BuildStepResults`, `ComputedLoad` before `AddAsync`, one `SaveChangesAsync`),
  `api/Bryk.Application/Training/Load/LoadCalculator.cs:74–83,91–126` (read only — **frozen**),
  `api/Bryk.Application/Training/Load/ILoadService.cs` + `LoadService.cs` (confirm
  `ComputeActualLoadAsync` resolves the sport profile itself via
  `athleteRepo.GetSportProfileAsync(workout.AthleteId, workout.Sport, ct)` — a direct table query with
  **no** dependency on a parent `Athlete` row existing; this matters for Step 16's seeding),
  `api/Bryk.API/Controllers/WorkoutsController.cs` (thin-controller style),
  `api/Bryk.API/Middleware/ExceptionHandlingMiddleware.cs` (the mapping this task relies on — read only),
  `api/Bryk.Domain/Interfaces/ITrainingPlanRepository.cs` (`GetPlannedWorkoutsInRangeAsync`,
  `GetPlannedWorkoutWithStructureAsync`), `api/Bryk.Domain/Interfaces/IWorkoutRepository.cs`
  (`GetByAthleteInRangeAsync`, `AddAsync`), `api/Bryk.Domain/Interfaces/IAthleteRepository.cs`
  (`GetWithSportProfilesAsync`, `GetSportProfileAsync`),
  `api/Bryk.Application/Zones/IZoneService.cs` + `ZonesResponse.cs` + `SportZonesResponse.cs`,
  `api/Bryk.Application/Analytics/AnalyticsService.cs:139–142` (zones+athlete resolution pattern),
  `api/Bryk.Application/Training/Workouts/LogWorkoutRequestValidator.cs` (validator idiom),
  `api/Bryk.Application/Common/Validation/ValidationExtensions.cs` (`ValidateOrThrowAsync`),
  `api/Bryk.Application/Exceptions/ValidationException.cs`,
  `api/Bryk.API/Program.cs` (lines 35 and 99–121 — confirm the repositories block already ends with
  19-1's `IActivityFileRepository` line and the services block still ends with the Phase-18
  `IPeriodizationService` line),
  `api/Bryk.API.Tests/Fixtures/BrykWebApplicationFactory.cs` (`TestAthleteId`, InMemory — no real
  constraints, no FK enforcement), `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs:15–18`
  (`JsonOptions`), `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs:20–56,174–229` (`ApiError`
  record, foreign-athlete seeding block).
- Confirm `api/Bryk.Application/ActivityFiles/Validators/`, `api/Bryk.Application.Tests/ActivityFiles/`
  and `api/Bryk.API.Tests/ActivityFiles/` do not yet contain any file this task is about to write (the
  first two are fresh; the third already holds 19-2's/19-3's parser tests — this task adds
  `ActivityFilesControllerTests.cs` alongside them, touching none of them).

## Step 1 — `ActivityFileLimits.cs`

**New file** `api/Bryk.Application/ActivityFiles/ActivityFileLimits.cs`:

```csharp
namespace Bryk.Application.ActivityFiles;

public static class ActivityFileLimits
{
    /// <summary>Largest accepted activity file (ADR-0010 §2). Enforced by the upload validator → 400.</summary>
    public const int MaxBytes = 25 * 1024 * 1024;

    /// <summary>
    /// The framework-level ceiling on the upload action, deliberately above <see cref="MaxBytes"/> so a
    /// slightly-oversized file is rejected by our validator with a clean 400 instead of being killed by
    /// the request pipeline (whose exceptions the global middleware maps to 500).
    /// </summary>
    public const long HardCapBytes = 32L * 1024 * 1024;
}
```

**Verify:** `dotnet build api/Bryk.sln` green (new, unreferenced type — trivial).

## Step 2 — `ActivityFileUploadRequest.cs`

**New file** `api/Bryk.Application/ActivityFiles/ActivityFileUploadRequest.cs`:

```csharp
namespace Bryk.Application.ActivityFiles;

// The transport-neutral upload body. IFormFile is Microsoft.AspNetCore.Http, and Bryk.Application must
// not reference it (Clean Architecture dependency direction) — so the controller is the only place
// allowed to touch IFormFile; it copies the stream into Content and hands this over.
public class ActivityFileUploadRequest
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
```

**Verify:** `dotnet build api/Bryk.sln` green.

## Step 3 — `Validators/ActivityFileUploadRequestValidator.cs`

**New file** `api/Bryk.Application/ActivityFiles/Validators/ActivityFileUploadRequestValidator.cs`:

```csharp
using FluentValidation;

namespace Bryk.Application.ActivityFiles.Validators;

public class ActivityFileUploadRequestValidator : AbstractValidator<ActivityFileUploadRequest>
{
    private static readonly string[] SupportedExtensions = { ".fit", ".tcx", ".gpx" };

    public ActivityFileUploadRequestValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(260);

        RuleFor(x => x.FileName)
            .Must(HasSupportedExtension)
            .WithMessage("FileName: Only .fit, .tcx and .gpx files are supported.");

        RuleFor(x => x.Content)
            .Must(c => c.Length > 0)
            .WithMessage("Content: The uploaded file is empty.");

        RuleFor(x => x.Content)
            .Must(c => c.Length <= ActivityFileLimits.MaxBytes)
            .WithMessage($"Content: The file exceeds the {ActivityFileLimits.MaxBytes / (1024 * 1024)} MB limit.");
    }

    private static bool HasSupportedExtension(string fileName) =>
        SupportedExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
}
```

Notes:
- `Path.GetExtension` returns the extension with its leading dot (e.g. `.TCX`); the `OrdinalIgnoreCase`
  comparer on `Contains` is what makes `Accepts_UpperCaseExtension` (Step 4) pass without a `.ToLower()`.
- **No magic-byte rule here** — the sniff needs the resolved `ActivityFileFormat`, which the validator
  does not compute; that lives in the service (Step 10).
- **No sample-sanity rule here** — Task 19-2's parsers already applied HR/power bounds before this
  service ever sees a `ParsedActivity`.
- **No `Program.cs` line.** The assembly scan (`AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>()`
  at `Program.cs:35`) picks this up automatically.

**Verify:** `dotnet build api/Bryk.sln` green.

## Step 4 — Unit tests: `ActivityFileUploadRequestValidatorTests.cs`

**New file** `api/Bryk.Application.Tests/ActivityFiles/ActivityFileUploadRequestValidatorTests.cs` (new
folder). Pure validator test, no host — this is where the 25 MB boundary is pinned, because a 25 MB
multipart POST is not worth the integration-test runtime.

```csharp
using Bryk.Application.ActivityFiles;
using Bryk.Application.ActivityFiles.Validators;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.ActivityFiles;

public class ActivityFileUploadRequestValidatorTests
{
    private static readonly ActivityFileUploadRequestValidator Validator = new();

    private static ActivityFileUploadRequest Valid() => new()
    {
        FileName = "ride.tcx",
        Content = new byte[] { 1, 2, 3, 4 }
    };

    [Fact]
    public void Rejects_UnsupportedExtension()
    {
        var request = Valid();
        request.FileName = "ride.csv";

        var result = Validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("FileName:"));
    }

    [Fact]
    public void Accepts_UpperCaseExtension()
    {
        var request = Valid();
        request.FileName = "RIDE.TCX";

        Validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_EmptyContent()
    {
        var request = Valid();
        request.Content = Array.Empty<byte>();

        var result = Validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("Content:"));
    }

    [Fact]
    public void Rejects_ContentOneByteOverTheCap()
    {
        var request = Valid();
        request.Content = new byte[ActivityFileLimits.MaxBytes + 1];

        Validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Accepts_ContentExactlyAtTheCap()
    {
        var request = Valid();
        request.Content = new byte[ActivityFileLimits.MaxBytes];

        Validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_FileNameOver260Characters()
    {
        var request = Valid();
        request.FileName = new string('a', 261) + ".tcx";

        Validator.Validate(request).IsValid.Should().BeFalse();
    }
}
```

**Verify:**
```
dotnet test api/Bryk.sln --filter FullyQualifiedName~ActivityFileUploadRequestValidatorTests
```
All 6 facts pass. `dotnet build api/Bryk.sln` still 0 errors, warnings unchanged.

## Step 5 — `CommitActivityFileRequest.cs` + `Validators/CommitActivityFileRequestValidator.cs`

**New file** `api/Bryk.Application/ActivityFiles/CommitActivityFileRequest.cs`:

```csharp
namespace Bryk.Application.ActivityFiles;

public class CommitActivityFileRequest
{
    public Guid? PlannedWorkoutId { get; set; }
}
```

**New file** `api/Bryk.Application/ActivityFiles/Validators/CommitActivityFileRequestValidator.cs`:

```csharp
using FluentValidation;

namespace Bryk.Application.ActivityFiles.Validators;

public class CommitActivityFileRequestValidator : AbstractValidator<CommitActivityFileRequest>
{
    public CommitActivityFileRequestValidator()
    {
        RuleFor(x => x.PlannedWorkoutId)
            .NotEqual(Guid.Empty)
            .When(x => x.PlannedWorkoutId.HasValue);
    }
}
```

Ownership of the planned workout needs a repository read, so it is **not** validated here — that lives in
`ActivityFileService.CommitAsync` (Step 11).

**Verify:** `dotnet build api/Bryk.sln` green.

## Step 6 — `ActivityFileResponses.cs` (all read shapes in one file)

**New file** `api/Bryk.Application/ActivityFiles/ActivityFileResponses.cs`:

```csharp
using Bryk.Domain.Entities;

namespace Bryk.Application.ActivityFiles;

public class ParsedActivityDto
{
    public Sport Sport { get; set; }
    public DateOnly CompletedDate { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public int? DistanceMeters { get; set; }
    public int? AvgHr { get; set; }
    public int? MaxHr { get; set; }
    public int? AvgPower { get; set; }
    public int? AvgPace { get; set; }
    public int SampleCount { get; set; }
}

public class MatchCandidateDto
{
    public Guid PlannedWorkoutId { get; set; }
    public Guid TrainingPlanId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Sport Sport { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public decimal? PlannedLoad { get; set; }
    public int DayOffset { get; set; }
}

public class ActivityFileUploadResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ActivityFileFormat Format { get; set; }
    public int ByteSize { get; set; }
    public ParsedActivityDto Parsed { get; set; } = new();
    public decimal? ComputedLoad { get; set; }
    public IReadOnlyList<ZoneHistogramEntry> ZoneSeconds { get; set; } = new List<ZoneHistogramEntry>();
    public IReadOnlyList<MatchCandidateDto> MatchCandidates { get; set; } = new List<MatchCandidateDto>();
}

public class ActivityFileCommitResponse
{
    public Guid WorkoutId { get; set; }
    public Guid? PlannedWorkoutId { get; set; }
    public decimal? ComputedLoad { get; set; }
}

public class ActivityFileSourceResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ActivityFileFormat Format { get; set; }
    public DateTime UploadedAt { get; set; }
}
```

- `ZoneHistogramEntry` resolves with no extra `using` — it lives in this same namespace
  (`Bryk.Application.ActivityFiles`), shipped by Task 19-2.
- `DayOffset = ScheduledDate.DayNumber − CompletedDate.DayNumber` (`-1`/`0`/`+1`) is computed by the
  service (Step 10), not here.
- `ActivityFileCommitResponse` is deliberately small — the client navigates to `/workouts/{id}` and reads
  through the existing `GET /workouts/{id}`. **Do not** reconstruct a `WorkoutResponse` here;
  `WorkoutService.Map` is private and `Bryk.Application/Training/Workouts/*` is untouched by this task.

**Verify:** `dotnet build api/Bryk.sln` green.

## Step 7 — `IActivityFileService.cs`

**New file** `api/Bryk.Application/ActivityFiles/IActivityFileService.cs`:

```csharp
using Bryk.Domain.Entities;

namespace Bryk.Application.ActivityFiles;

/// <summary>
/// Parses, previews, commits and discards uploaded activity files (ADR-0010). Athlete identity always
/// comes from <see cref="Common.ICurrentUserService"/>; a missing or foreign resource is
/// <see cref="KeyNotFoundException"/> (404).
/// </summary>
public interface IActivityFileService
{
    /// <summary>
    /// Validates, magic-byte-sniffs and parses an uploaded file, then returns the parsed session
    /// actuals, the load it would produce, the five-bucket zone histogram and match candidates (the
    /// athlete's unlinked planned workouts within one day, same sport). Stores the bytes; commits
    /// nothing to history. 400 on an unsupported/mismatched/malformed file or a future start time.
    /// </summary>
    Task<ActivityFileUploadResponse> UploadAsync(ActivityFileUploadRequest request, CancellationToken ct = default);

    /// <summary>
    /// Re-parses the stored file and creates the <see cref="Workout"/> it describes, optionally linking
    /// it to a planned workout. 404 if the file or the planned workout is missing or foreign; 409 if the
    /// file was already committed (<see cref="ActivityFile.ParsedWorkoutId"/> is not null).
    /// </summary>
    Task<ActivityFileCommitResponse> CommitAsync(Guid id, CommitActivityFileRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes an un-committed preview. 404 if missing or foreign; 409 if the file has already been
    /// committed to a workout (delete the workout instead).
    /// </summary>
    Task DiscardAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns the activity file a workout was imported from, or null when the workout was logged by
    /// hand (or belongs to another athlete). <b>Not a 404</b> — "this workout has no source file" is the
    /// common case and must not read as an error in the client.
    /// </summary>
    Task<ActivityFileSourceResponse?> GetSourceForWorkoutAsync(Guid workoutId, CancellationToken ct = default);
}
```

**Verify:** `dotnet build api/Bryk.sln` green.

## Step 8 — `ActivityFileService.cs`: skeleton (ctor + stubbed interface members)

**New file** `api/Bryk.Application/ActivityFiles/ActivityFileService.cs`. Write the whole class shape
now — ctor with all 11 dependencies, the interface declaration, and every member stubbed — so the file
compiles and satisfies `IActivityFileService` at every subsequent step; only method **bodies** change
from here on.

```csharp
using Bryk.Application.Common;
using Bryk.Application.Common.Validation;
using Bryk.Application.Training.Load;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentValidation;
using System.Text.Json;

namespace Bryk.Application.ActivityFiles;

public class ActivityFileService(
    ICurrentUserService currentUser,
    IValidator<ActivityFileUploadRequest> uploadValidator,
    IValidator<CommitActivityFileRequest> commitValidator,
    IEnumerable<IActivityFileParser> parsers,
    IActivityFileRepository fileRepo,
    IWorkoutRepository workoutRepo,
    ITrainingPlanRepository planRepo,
    IAthleteRepository athleteRepo,
    ILoadService loadService,
    IZoneService zoneService,
    IUnitOfWork unitOfWork) : IActivityFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<ActivityFileUploadResponse> UploadAsync(ActivityFileUploadRequest request, CancellationToken ct = default)
        => throw new NotImplementedException(); // filled in at Step 10

    public Task<ActivityFileCommitResponse> CommitAsync(Guid id, CommitActivityFileRequest request, CancellationToken ct = default)
        => throw new NotImplementedException(); // filled in at Step 11

    public Task DiscardAsync(Guid id, CancellationToken ct = default)
        => throw new NotImplementedException(); // filled in at Step 12

    public Task<ActivityFileSourceResponse?> GetSourceForWorkoutAsync(Guid workoutId, CancellationToken ct = default)
        => throw new NotImplementedException(); // filled in at Step 12
}
```

Three registrations of `IActivityFileParser` will resolve into the `parsers` constructor parameter as an
`IEnumerable` (Step 14) — the service selects one by `Format`.

**Verify:** `dotnet build api/Bryk.sln` green — the class satisfies `IActivityFileService` with stub
bodies; no test calls it yet.

## Step 9 — `ActivityFileService.cs`: `BuildWorkout` — THE LOAD-BEARING DETAIL

Add this **private static** method to the bottom of `ActivityFileService.cs`, below the four stubbed
members. This is ADR-0010 §3 made executable, and it is deliberately its own step: get this one object
wrong and every imported ride or run with power/pace silently falls back to an HR-only (or zero) TSS,
with no compiler error and no obviously wrong-looking test until Step 16.

```csharp
    // ADR-0010 §3 — the load-bearing detail. Workout carries no session-level AvgPower/AvgPace, and
    // LoadCalculator's session-only path (LoadCalculator.cs:88) hardcodes both to null — a session-level
    // import could therefore only ever reach the HR IF branch. This one synthetic WorkoutStepResult —
    // WorkoutStepId null (nullable per ADR-0005 §5: no planned step is being realised), OrderIndex 0 —
    // is what routes ComputeActualLoad into its StepResults branch (LoadCalculator.cs:74–83) and reaches
    // the real power/pace IF branches. Do NOT "fix" this by editing LoadCalculator (frozen for Phase 19),
    // and do NOT emit one step result per lap (out of scope for v1) — exactly one, always OrderIndex 0.
    private static Workout BuildWorkout(ParsedActivity parsed, Guid athleteId, Guid? plannedWorkoutId)
    {
        var workout = new Workout
        {
            Id = Guid.NewGuid(),
            AthleteId = athleteId,
            PlannedWorkoutId = plannedWorkoutId,
            Sport = parsed.Sport,
            CompletedDate = DateOnly.FromDateTime(parsed.StartTimeUtc),
            ActualDurationSeconds = parsed.DurationSeconds,
            ActualDistanceMeters = parsed.DistanceMeters,
            AvgHr = parsed.AvgHr,
            MaxHr = parsed.MaxHr
        };

        workout.StepResults.Add(new WorkoutStepResult
        {
            Id = Guid.NewGuid(),
            AthleteId = athleteId,
            WorkoutId = workout.Id,
            WorkoutStepId = null,
            OrderIndex = 0,
            ActualDurationSeconds = parsed.DurationSeconds,
            ActualDistanceMeters = parsed.DistanceMeters,
            AvgPower = parsed.AvgPower,
            AvgHr = parsed.AvgHr,
            AvgPace = parsed.AvgPace
        });

        return workout;
    }
```

**Verify (build-only — the real proof is Step 16):** `dotnet build api/Bryk.sln` green. `BuildWorkout` is
not yet called by anything, so this gate cannot and does not prove the routing behavior. By eye, confirm
field-for-field against ADR-0010 §3: `WorkoutStepId` null ✓, `OrderIndex` 0 ✓, `AthleteId`/`WorkoutId` set
✓, `AvgPower`/`AvgPace`/`AvgHr`/`ActualDurationSeconds`/`ActualDistanceMeters` all carried from `parsed`
✓, exactly one result added (no loop, no per-lap emission) ✓. **Do not mark this task's headline risk
retired on this step** — carry it forward to Step 16.

## Step 10 — `ActivityFileService.cs`: `UploadAsync`

Replace the `UploadAsync` stub and add three private helpers below `BuildWorkout`:
`ResolveFormat`, `ContentMatchesFormat` (the magic-byte sniff) and `FindCandidatesAsync` (the
match-candidate query).

```csharp
    public async Task<ActivityFileUploadResponse> UploadAsync(ActivityFileUploadRequest request, CancellationToken ct = default)
    {
        await uploadValidator.ValidateOrThrowAsync(request, ct);
        var athleteId = currentUser.GetCurrentAthleteId();

        // The validator already guaranteed FileName ends in .fit/.tcx/.gpx.
        var format = ResolveFormat(request.FileName);

        if (!ContentMatchesFormat(request.Content, format))
        {
            throw new Exceptions.ValidationException(new[] { "File: The file's contents do not match its extension." });
        }

        var parser = parsers.First(p => p.Format == format);
        var parsed = await parser.ParseAsync(new MemoryStream(request.Content, writable: false), ct);

        if (DateOnly.FromDateTime(parsed.StartTimeUtc) > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new Exceptions.ValidationException(new[] { "File: The activity's start time is in the future." });
        }

        var zones = await zoneService.GetZonesAsync(ct);
        var athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct);
        var sportZones = zones.Sports.FirstOrDefault(s => s.Sport == parsed.Sport);
        var histogram = ZoneHistogramCalculator.Compute(parsed, sportZones, athlete?.MaxHr);

        // Preview load = commit's load, computed the same way, on a transient workout that is never
        // staged (no AddAsync, no SaveChangesAsync for this instance).
        var transient = BuildWorkout(parsed, athleteId, plannedWorkoutId: null);
        var load = await loadService.ComputeActualLoadAsync(transient, ct);

        var completedDate = DateOnly.FromDateTime(parsed.StartTimeUtc);
        var candidates = await FindCandidatesAsync(athleteId, parsed.Sport, completedDate, ct);

        var file = new ActivityFile
        {
            Id = Guid.NewGuid(),
            AthleteId = athleteId,
            FileName = request.FileName,
            Format = format,
            ByteSize = request.Content.Length,
            Content = request.Content,
            UploadedAt = DateTime.UtcNow,
            ParsedWorkoutId = null,
            ZoneHistogramJson = null // ADR-0010 §5: an un-committed preview leaves no derived data behind.
        };
        await fileRepo.AddAsync(file, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ActivityFileUploadResponse
        {
            Id = file.Id,
            FileName = file.FileName,
            Format = file.Format,
            ByteSize = file.ByteSize,
            Parsed = new ParsedActivityDto
            {
                Sport = parsed.Sport,
                CompletedDate = completedDate,
                StartTimeUtc = parsed.StartTimeUtc,
                DurationSeconds = parsed.DurationSeconds,
                DistanceMeters = parsed.DistanceMeters,
                AvgHr = parsed.AvgHr,
                MaxHr = parsed.MaxHr,
                AvgPower = parsed.AvgPower,
                AvgPace = parsed.AvgPace,
                SampleCount = parsed.Samples.Count
            },
            ComputedLoad = load,
            ZoneSeconds = histogram,
            MatchCandidates = candidates
        };
    }

    private static ActivityFileFormat ResolveFormat(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".fit" => ActivityFileFormat.Fit,
        ".tcx" => ActivityFileFormat.Tcx,
        ".gpx" => ActivityFileFormat.Gpx,
        _ => throw new InvalidOperationException("Unreachable: the upload validator already rejected any other extension.")
    };

    private static bool ContentMatchesFormat(byte[] content, ActivityFileFormat format)
    {
        if (format == ActivityFileFormat.Fit)
        {
            // The FIT header's data-type signature: bytes 8..11 are ASCII ".FIT".
            return content.Length >= 12
                && content[8] == (byte)'.' && content[9] == (byte)'F'
                && content[10] == (byte)'I' && content[11] == (byte)'T';
        }

        // Tcx/Gpx: a cheap "is it even XML" gate. Skip a UTF-8 BOM and leading ASCII whitespace, then
        // require the first byte to be '<'. The root-element check (TrainingCenterDatabase vs gpx)
        // belongs to 19-2's parsers — this only rules out obviously-wrong content up front.
        var i = 0;
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
        {
            i = 3;
        }

        while (i < content.Length && (content[i] == ' ' || content[i] == '\t' || content[i] == '\r' || content[i] == '\n'))
        {
            i++;
        }

        return i < content.Length && content[i] == (byte)'<';
    }

    // Match candidates: the athlete's unlinked planned workouts within ±1 day, same sport, nearest first
    // (same-day matches before day-before/day-after). No fuzzy duration/load scoring in v1.
    private async Task<List<MatchCandidateDto>> FindCandidatesAsync(Guid athleteId, Sport sport, DateOnly date, CancellationToken ct)
    {
        var planned = await planRepo.GetPlannedWorkoutsInRangeAsync(athleteId, date.AddDays(-1), date.AddDays(1), ct);
        var completed = await workoutRepo.GetByAthleteInRangeAsync(athleteId, date.AddDays(-1), date.AddDays(1), ct);
        var linked = completed
            .Where(w => w.PlannedWorkoutId is not null)
            .Select(w => w.PlannedWorkoutId!.Value)
            .ToHashSet();

        return planned
            .Where(pw => pw.Sport == sport && !linked.Contains(pw.Id))
            .OrderBy(pw => Math.Abs(pw.ScheduledDate.DayNumber - date.DayNumber))
            .ThenBy(pw => pw.ScheduledDate)
            .ThenBy(pw => pw.Title)
            .Select(pw => new MatchCandidateDto
            {
                PlannedWorkoutId = pw.Id,
                TrainingPlanId = pw.TrainingPlanId,
                Title = pw.Title,
                Sport = pw.Sport,
                ScheduledDate = pw.ScheduledDate,
                PlannedLoad = pw.PlannedLoad,
                DayOffset = pw.ScheduledDate.DayNumber - date.DayNumber
            })
            .ToList();
    }
```

`Exceptions.ValidationException` resolves with no extra `using` — `Bryk.Application` encloses
`Bryk.Application.ActivityFiles`, the same unqualified pattern `TrainingPlanService.cs` already uses.

**Verify:** `dotnet build api/Bryk.sln` green, 0 new warnings. No test yet exercises `UploadAsync` (Steps
16–17 do); this is a compile-only gate.

## Step 11 — `ActivityFileService.cs`: `CommitAsync`

Replace the `CommitAsync` stub:

```csharp
    public async Task<ActivityFileCommitResponse> CommitAsync(Guid id, CommitActivityFileRequest request, CancellationToken ct = default)
    {
        await commitValidator.ValidateOrThrowAsync(request, ct);
        var athleteId = currentUser.GetCurrentAthleteId();

        var file = await fileRepo.GetByIdTrackedAsync(id, ct);
        if (file is null || file.AthleteId != athleteId)
        {
            throw new KeyNotFoundException();
        }

        // Duplicate-commit guard (ADR-0010 §4): keyed on the file row, never on a Workout column — there
        // is no Workout.SourceFileId and none is being added.
        if (file.ParsedWorkoutId is not null)
        {
            throw new InvalidOperationException("This activity file has already been committed to a workout.");
        }

        // Re-parse rather than trusting the preview: samples are never persisted (ADR-0010 §6), and
        // re-parsing is deterministic.
        var parser = parsers.First(p => p.Format == file.Format);
        var parsed = await parser.ParseAsync(new MemoryStream(file.Content, writable: false), ct);

        if (request.PlannedWorkoutId is { } pwId)
        {
            var planned = await planRepo.GetPlannedWorkoutWithStructureAsync(pwId, ct);
            if (planned is null || planned.AthleteId != athleteId)
            {
                throw new KeyNotFoundException();
            }
            // No further check: a planned workout linked by someone else between preview and commit is
            // accepted — a single-athlete race not worth a lock in v1.
        }

        var workout = BuildWorkout(parsed, athleteId, request.PlannedWorkoutId);
        workout.ComputedLoad = await loadService.ComputeActualLoadAsync(workout, ct);
        await workoutRepo.AddAsync(workout, ct);

        var zones = await zoneService.GetZonesAsync(ct);
        var athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct);
        var sportZones = zones.Sports.FirstOrDefault(s => s.Sport == parsed.Sport);
        var histogram = ZoneHistogramCalculator.Compute(parsed, sportZones, athlete?.MaxHr);

        // Mutate the tracked file row — no fileRepo.Update call, the entity is tracked.
        file.ParsedWorkoutId = workout.Id;
        file.ZoneHistogramJson = JsonSerializer.Serialize(histogram, JsonOptions);

        // Exactly one commit, covering the workout, its synthetic step result and the file link
        // atomically. Two SaveChangesAsync calls here would leave a window where a workout exists but
        // the file is still marked un-committed, which the duplicate guard would then let through twice.
        await unitOfWork.SaveChangesAsync(ct);

        return new ActivityFileCommitResponse
        {
            WorkoutId = workout.Id,
            PlannedWorkoutId = workout.PlannedWorkoutId,
            ComputedLoad = workout.ComputedLoad
        };
    }
```

**Verify:** `dotnet build api/Bryk.sln` green. Still no test exercises this path — Step 16 is next.

## Step 12 — `ActivityFileService.cs`: `DiscardAsync` + `GetSourceForWorkoutAsync`

Replace both remaining stubs:

```csharp
    public async Task DiscardAsync(Guid id, CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();
        var file = await fileRepo.GetByIdTrackedAsync(id, ct);
        if (file is null || file.AthleteId != athleteId)
        {
            throw new KeyNotFoundException();
        }

        if (file.ParsedWorkoutId is not null)
        {
            throw new InvalidOperationException("A committed activity file cannot be discarded; delete the workout instead.");
        }

        fileRepo.Delete(file);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<ActivityFileSourceResponse?> GetSourceForWorkoutAsync(Guid workoutId, CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();
        var files = await fileRepo.GetByParsedWorkoutIdsAsync(athleteId, new[] { workoutId }, ct);
        var file = files.FirstOrDefault();

        return file is null ? null : new ActivityFileSourceResponse
        {
            Id = file.Id,
            FileName = file.FileName,
            Format = file.Format,
            UploadedAt = file.UploadedAt
        };
    }
```

`ActivityFileService.cs` is now complete — no more stubs remain.

**Verify:** `dotnet build api/Bryk.sln` green, 0 new warnings. `grep -c NotImplementedException` on the
file returns 0.

## Step 13 — `ActivityFilesController.cs`

**New file** `api/Bryk.API/Controllers/ActivityFilesController.cs`:

```csharp
using Asp.Versioning;
using Bryk.Application.ActivityFiles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ActivityFilesController(IActivityFileService activityFileService) : ControllerBase
{
    /// <summary>
    /// Uploads an activity file (.fit/.tcx/.gpx, multipart form part named "file", 25 MB validated cap).
    /// Parses it and returns 201 with the parsed session actuals, the load it would produce, the
    /// five-bucket zone histogram and match candidates — nothing is committed to history yet. 400 if the
    /// extension is unsupported, the content doesn't match the extension, the file is empty/oversized,
    /// the start time is in the future, or the file cannot be parsed.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(ActivityFileLimits.HardCapBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ActivityFileLimits.HardCapBytes)]
    public async Task<IActionResult> UploadAsync([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        var request = await ToRequestAsync(file, cancellationToken);
        ActivityFileUploadResponse result = await activityFileService.UploadAsync(request, cancellationToken);
        return StatusCode(201, result);
    }

    /// <summary>
    /// Commits a previewed activity file to a new Workout, optionally linking it to a planned workout.
    /// 201 with the new workout id. 404 if the file or the planned workout is missing or foreign; 409 if
    /// the file was already committed.
    /// </summary>
    [HttpPost("{id:guid}/commit")]
    public async Task<IActionResult> CommitAsync(Guid id, [FromBody] CommitActivityFileRequest request, CancellationToken cancellationToken)
    {
        ActivityFileCommitResponse result = await activityFileService.CommitAsync(id, request, cancellationToken);
        return StatusCode(201, result);
    }

    /// <summary>Discards an uncommitted preview. 404 if missing or foreign; 409 if already committed.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DiscardAsync(Guid id, CancellationToken cancellationToken)
    {
        await activityFileService.DiscardAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Returns the activity file a workout was imported from, or 200 with a null body when the workout
    /// was logged by hand — this is the common case and must not read as an error in the client.
    /// </summary>
    [HttpGet("by-workout/{workoutId:guid}")]
    public async Task<IActionResult> GetSourceAsync(Guid workoutId, CancellationToken cancellationToken)
    {
        ActivityFileSourceResponse? result = await activityFileService.GetSourceForWorkoutAsync(workoutId, cancellationToken);
        return Ok(result);
    }

    // IFormFile is Microsoft.AspNetCore.Http; Bryk.Application must not reference it, so the copy to a
    // transport-neutral request happens here and nowhere else. A missing form part yields an empty
    // request, which the validator rejects with 400 — not an NRE.
    private static async Task<ActivityFileUploadRequest> ToRequestAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null)
        {
            return new ActivityFileUploadRequest();
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        return new ActivityFileUploadRequest { FileName = file.FileName, Content = buffer.ToArray() };
    }
}
```

Routes resolve to `/api/v1/activityfiles`, `/api/v1/activityfiles/{id}/commit`,
`/api/v1/activityfiles/{id}`, `/api/v1/activityfiles/by-workout/{workoutId}`. No try/catch; athlete id
never comes from route/query/body.

**Verify:** `dotnet build api/Bryk.sln` green.

## Step 14 — `Program.cs` — append the four DI lines

**Edit** `api/Bryk.API/Program.cs` — **append only**. The repositories block already ends with 19-1's
`builder.Services.AddScoped<IActivityFileRepository, ActivityFileRepository>();` line, and the services
block still ends with the Phase-18 `IPeriodizationService` line. Add these four lines directly after that
last services line — do not reorder, reformat or touch any line above them, including 19-1's:

```csharp
builder.Services.AddScoped<Bryk.Application.ActivityFiles.IActivityFileParser, Bryk.Infrastructure.ActivityFiles.FitActivityParser>();
builder.Services.AddScoped<Bryk.Application.ActivityFiles.IActivityFileParser, Bryk.Infrastructure.ActivityFiles.TcxActivityParser>();
builder.Services.AddScoped<Bryk.Application.ActivityFiles.IActivityFileParser, Bryk.Infrastructure.ActivityFiles.GpxActivityParser>();
builder.Services.AddScoped<Bryk.Application.ActivityFiles.IActivityFileService, Bryk.Application.ActivityFiles.ActivityFileService>();
```

Three registrations of the same interface is intentional — `ActivityFileService`'s ctor takes
`IEnumerable<IActivityFileParser>` and selects by `Format`. Fully-qualified names match the style already
used for the Phase-16-onward lines (117–121) — no new `using` directive needed.

- **No validator line** — the assembly scan at `Program.cs:35` already covers
  `ActivityFileUploadRequestValidator` and `CommitActivityFileRequestValidator`.
- **No global `FormOptions`/Kestrel configuration** — the cap is per-route (the two attributes on
  `UploadAsync`), leaving every other endpoint's limits untouched.

**Verify:** `dotnet build api/Bryk.sln` green. `git diff api/Bryk.API/Program.cs` shows **exactly four
added lines**, nothing else changed (confirm 19-1's line above them is byte-for-byte unchanged).

## Step 15 — Build gate: production code complete

Run the full backend build and test suite once before writing any of this task's own tests, to prove
nothing above regressed anything already in the tree:

```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
```

- 0 errors, warnings unchanged from the Step 0 baseline (≤ 16).
- Test count unchanged from the Step 0 baseline (this task has added zero tests so far — production code
  only). If the count moved, something outside this task's scope changed; stop and investigate.

## Step 16 — THE LOAD-BEARING PROOF: pin the bike-power TSS and the synthetic step result

**New file** `api/Bryk.API.Tests/ActivityFiles/ActivityFilesControllerTests.cs`. Write the class skeleton
(usings, `JsonOptions`, `ApiError`, the two shared helpers below) plus **only** the two headline commit
tests in this step — everything else in the test matrix (Steps 17–19) is appended afterward, once this
gate passes. This is the step that actually proves ADR-0010 §3, not Step 9's build-only gate.

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.ActivityFiles;
using Bryk.Application.Training.Workouts;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bryk.API.Tests.ActivityFiles;

public class ActivityFilesControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed class ApiError
    {
        public int Status { get; set; }
        public string? Error { get; set; }
        public string[]? Errors { get; set; }
    }

    private static async Task<HttpResponseMessage> UploadFixtureAsync(HttpClient client, string fixtureFileName, string uploadFileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ActivityFiles", fixtureFileName);
        var bytes = await File.ReadAllBytesAsync(path);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), "file", uploadFileName);
        return await client.PostAsync("/api/v1/activityfiles", content);
    }

    private static async Task<ActivityFileUploadResponse> UploadAndReadAsync(HttpClient client, string fixtureFileName, string uploadFileName)
    {
        var response = await UploadFixtureAsync(client, fixtureFileName, uploadFileName);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions))!;
    }

    [Fact]
    public async Task Commit_BikeFileWithPower_ComputesLoadThroughThePowerIfBranch()
    {
        await using var factory = new BrykWebApplicationFactory();

        // LoadService.ComputeActualLoadAsync resolves the sport profile via
        // IAthleteRepository.GetSportProfileAsync, which queries AthleteSportProfiles directly by
        // (AthleteId, Sport) — it does NOT require a parent Athlete row to exist (confirmed at Step 0).
        // Seeding only the profile is enough to prove the IF branch; InMemory does not enforce the FK
        // either way.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AthleteSportProfiles.Add(new AthleteSportProfile
            {
                Id = Guid.NewGuid(),
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                Sport = Sport.Bike,
                ThresholdValue = 200m
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        // sample-ride.tcx (19-2's fixture): 3600 s, avg power 210 W.
        var uploaded = await UploadAndReadAsync(client, "sample-ride.tcx", "sample-ride.tcx");

        var commitResponse = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{uploaded.Id}/commit", new CommitActivityFileRequest());
        commitResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var committed = await commitResponse.Content.ReadFromJsonAsync<ActivityFileCommitResponse>(JsonOptions);

        // 210 W over a 200 W FTP: IF = 1.05; TSS = 3600 * 1.05^2 / 3600 * 100 = 110.25. Reachable ONLY
        // through the StepResults branch (LoadCalculator.cs:74-83). If BuildWorkout's synthetic
        // WorkoutStepResult (Step 9) were dropped, ComputeActualLoad would take the session-only path
        // (LoadCalculator.cs:88, power hardcoded null) and fall to the HR branch, producing a different
        // number — this assertion is exact, not a range, precisely so that regression is caught.
        committed!.ComputedLoad.Should().Be(110.25m);
    }

    [Fact]
    public async Task Commit_CreatesExactlyOneSyntheticStepResult()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var uploaded = await UploadAndReadAsync(client, "sample-ride.tcx", "sample-ride.tcx");
        var commitResponse = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{uploaded.Id}/commit", new CommitActivityFileRequest());
        var committed = await commitResponse.Content.ReadFromJsonAsync<ActivityFileCommitResponse>(JsonOptions);

        var workoutResponse = await client.GetAsync($"/api/v1/workouts/{committed!.WorkoutId}");
        var workout = await workoutResponse.Content.ReadFromJsonAsync<WorkoutResponse>(JsonOptions);

        workout!.StepResults.Should().ContainSingle();
        var step = workout.StepResults[0];
        step.WorkoutStepId.Should().BeNull();
        step.OrderIndex.Should().Be(0);
        step.AvgPower.Should().Be(210);
        step.AvgHr.Should().Be(141); // sample-ride.tcx's HR mean: (130+145+150+140)/4 = 141.25 → 141
    }
}
```

**Verify — this is the actual gate for this task's headline risk, not Step 9's build gate:**
```
dotnet test api/Bryk.sln --filter FullyQualifiedName~ActivityFilesControllerTests
```
Both facts pass, with the exact values above (`110.25m`, one step result, `workoutStepId` null,
`orderIndex` 0, `avgPower` 210, `avgHr` 141). **If either test fails, do not weaken the assertion** — the
bug is almost certainly a missing or malformed synthetic `WorkoutStepResult` in `BuildWorkout` (Step 9),
never `LoadCalculator.cs` (frozen — if it looks like the calculator is wrong, STOP and ask before touching
it, per `Tasks-19-4.md`'s explicit non-goal).

## Step 17 — Integration tests: upload preview + match candidates

Append to `ActivityFilesControllerTests.cs`, after the two Step 16 tests. Every value below is pinned by
`Tasks-19-4.md` — do not soften them.

```csharp
    [Fact]
    public async Task Upload_TcxRunFixture_Returns201WithParsedPreview()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await UploadFixtureAsync(client, "sample-run.tcx", "sample-run.tcx");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ActivityFileUploadResponse>(JsonOptions);
        body!.Id.Should().NotBe(Guid.Empty);
        body.ByteSize.Should().BePositive();
        body.Parsed.Sport.Should().Be(Sport.Run);
        body.Parsed.DurationSeconds.Should().Be(600);
        body.Parsed.DistanceMeters.Should().Be(2000);
        body.Parsed.AvgHr.Should().Be(144);
        body.Parsed.AvgPace.Should().Be(300);
    }

    [Fact]
    public async Task Upload_UnsupportedExtension_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await UploadFixtureAsync(client, "sample-run.tcx", "ride.csv");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        error!.Errors.Should().Contain(e => e.StartsWith("FileName:"));
    }

    [Fact]
    public async Task Upload_ExtensionAndContentMismatch_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        // The TCX fixture's bytes, sent with a .fit extension — the magic-byte sniff must catch this,
        // not the parser.
        var response = await UploadFixtureAsync(client, "sample-run.tcx", "ride.fit");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        error!.Errors.Should().Contain(e => e.StartsWith("File:"));
    }

    [Fact]
    public async Task Upload_CorruptXml_Returns400AndPersistsNothing()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        // "<not xml" passes the magic-byte gate (starts with '<') but fails XML parsing inside the parser.
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("<not xml")), "file", "ride.tcx");
        var response = await client.PostAsync("/api/v1/activityfiles", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.ActivityFiles.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Upload_MissingFilePart_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        using var content = new MultipartFormDataContent();
        var response = await client.PostAsync("/api/v1/activityfiles", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_SameDaySameSportUnlinkedPlannedWorkout_IsOfferedAsACandidate()
    {
        await using var factory = new BrykWebApplicationFactory();
        var fixtureDate = new DateOnly(2026, 6, 1); // sample-run.tcx starts 2026-06-01T06:00:00Z
        var plannedWorkoutId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var planId = Guid.NewGuid();
            db.TrainingPlans.Add(new TrainingPlan
            {
                Id = planId,
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                Name = "Match Test Plan",
                Methodology = MethodologyChoice.Polarized,
                StartDate = fixtureDate.AddDays(-5),
                EndDate = fixtureDate.AddDays(5),
                PlannedWorkouts = new List<PlannedWorkout>
                {
                    new()
                    {
                        Id = plannedWorkoutId,
                        AthleteId = BrykWebApplicationFactory.TestAthleteId,
                        TrainingPlanId = planId,
                        Sport = Sport.Run,
                        ScheduledDate = fixtureDate,
                        Title = "Target Run"
                    }
                }
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var body = await UploadAndReadAsync(client, "sample-run.tcx", "sample-run.tcx");

        body.MatchCandidates.Should().ContainSingle();
        var candidate = body.MatchCandidates[0];
        candidate.PlannedWorkoutId.Should().Be(plannedWorkoutId);
        candidate.DayOffset.Should().Be(0);
    }

    [Fact]
    public async Task Upload_PlannedWorkoutTwoDaysAway_IsNotOffered()
    {
        await using var factory = new BrykWebApplicationFactory();
        var fixtureDate = new DateOnly(2026, 6, 1);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var planId = Guid.NewGuid();
            db.TrainingPlans.Add(new TrainingPlan
            {
                Id = planId,
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                Name = "Boundary Test Plan",
                Methodology = MethodologyChoice.Polarized,
                StartDate = fixtureDate.AddDays(-10),
                EndDate = fixtureDate.AddDays(10),
                PlannedWorkouts = new List<PlannedWorkout>
                {
                    Seed(planId, fixtureDate.AddDays(-2), "Two Days Before"),
                    Seed(planId, fixtureDate.AddDays(-1), "One Day Before"),
                    Seed(planId, fixtureDate.AddDays(1), "One Day After"),
                    Seed(planId, fixtureDate.AddDays(2), "Two Days After")
                }
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var body = await UploadAndReadAsync(client, "sample-run.tcx", "sample-run.tcx");

        body.MatchCandidates.Select(c => c.Title).Should().BeEquivalentTo("One Day Before", "One Day After");

        static PlannedWorkout Seed(Guid planId, DateOnly date, string title) => new()
        {
            Id = Guid.NewGuid(),
            AthleteId = BrykWebApplicationFactory.TestAthleteId,
            TrainingPlanId = planId,
            Sport = Sport.Run,
            ScheduledDate = date,
            Title = title
        };
    }

    [Fact]
    public async Task Upload_PlannedWorkoutOfADifferentSport_IsNotOffered()
    {
        await using var factory = new BrykWebApplicationFactory();
        var fixtureDate = new DateOnly(2026, 6, 1);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var planId = Guid.NewGuid();
            db.TrainingPlans.Add(new TrainingPlan
            {
                Id = planId,
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                Name = "Sport Mismatch Plan",
                Methodology = MethodologyChoice.Polarized,
                StartDate = fixtureDate.AddDays(-5),
                EndDate = fixtureDate.AddDays(5),
                PlannedWorkouts = new List<PlannedWorkout>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        AthleteId = BrykWebApplicationFactory.TestAthleteId,
                        TrainingPlanId = planId,
                        Sport = Sport.Bike, // the fixture is Run
                        ScheduledDate = fixtureDate,
                        Title = "Wrong Sport"
                    }
                }
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var body = await UploadAndReadAsync(client, "sample-run.tcx", "sample-run.tcx");

        body.MatchCandidates.Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_PlannedWorkoutAlreadyLinkedToAWorkout_IsNotOffered()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();
        var fixtureDate = new DateOnly(2026, 6, 1);
        var planId = Guid.NewGuid();
        var plannedWorkoutId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.TrainingPlans.Add(new TrainingPlan
            {
                Id = planId,
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                Name = "Already Linked Plan",
                Methodology = MethodologyChoice.Polarized,
                StartDate = fixtureDate.AddDays(-5),
                EndDate = fixtureDate.AddDays(5),
                PlannedWorkouts = new List<PlannedWorkout>
                {
                    new()
                    {
                        Id = plannedWorkoutId,
                        AthleteId = BrykWebApplicationFactory.TestAthleteId,
                        TrainingPlanId = planId,
                        Sport = Sport.Run,
                        ScheduledDate = fixtureDate,
                        Title = "Already Linked"
                    }
                }
            });
            await db.SaveChangesAsync();
        }

        // Log a manual workout against it first, through the existing POST /workouts — untouched by this task.
        await client.PostAsJsonAsync("/api/v1/workouts", new LogWorkoutRequest
        {
            PlannedWorkoutId = plannedWorkoutId,
            Sport = Sport.Run,
            CompletedDate = fixtureDate,
            ActualDurationSeconds = 600
        });

        var body = await UploadAndReadAsync(client, "sample-run.tcx", "sample-run.tcx");

        body.MatchCandidates.Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_ZoneSeconds_AlwaysHasFiveBuckets()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var body = await UploadAndReadAsync(client, "sample-run.tcx", "sample-run.tcx");

        body.ZoneSeconds.Should().HaveCount(5);
        body.ZoneSeconds.Select(z => z.ZoneNumber).Should().Equal(1, 2, 3, 4, 5);
    }
```

**Verify:**
```
dotnet test api/Bryk.sln --filter FullyQualifiedName~ActivityFilesControllerTests
```
All 10 tests added in this step pass, plus the 2 from Step 16 (12 total in the file so far).

## Step 18 — Integration tests: remaining commit paths

Append to `ActivityFilesControllerTests.cs`, after the Step 17 tests.

```csharp
    [Fact]
    public async Task Commit_WithoutPlannedWorkoutId_CreatesAnUnlinkedWorkout()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var uploaded = await UploadAndReadAsync(client, "sample-run.tcx", "sample-run.tcx");
        var commitResponse = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{uploaded.Id}/commit", new CommitActivityFileRequest());
        var committed = await commitResponse.Content.ReadFromJsonAsync<ActivityFileCommitResponse>(JsonOptions);

        committed!.PlannedWorkoutId.Should().BeNull();

        var workoutResponse = await client.GetAsync($"/api/v1/workouts/{committed.WorkoutId}");
        var workout = await workoutResponse.Content.ReadFromJsonAsync<WorkoutResponse>(JsonOptions);
        workout!.PlannedWorkoutId.Should().BeNull();
    }

    [Fact]
    public async Task Commit_WithOwnedPlannedWorkoutId_LinksIt()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();
        var fixtureDate = new DateOnly(2026, 6, 1);
        var planId = Guid.NewGuid();
        var plannedWorkoutId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.TrainingPlans.Add(new TrainingPlan
            {
                Id = planId,
                AthleteId = BrykWebApplicationFactory.TestAthleteId,
                Name = "Link Plan",
                Methodology = MethodologyChoice.Polarized,
                StartDate = fixtureDate.AddDays(-5),
                EndDate = fixtureDate.AddDays(5),
                PlannedWorkouts = new List<PlannedWorkout>
                {
                    new()
                    {
                        Id = plannedWorkoutId,
                        AthleteId = BrykWebApplicationFactory.TestAthleteId,
                        TrainingPlanId = planId,
                        Sport = Sport.Run,
                        ScheduledDate = fixtureDate,
                        Title = "Target Run"
                    }
                }
            });
            await db.SaveChangesAsync();
        }

        var uploaded = await UploadAndReadAsync(client, "sample-run.tcx", "sample-run.tcx");
        var commitResponse = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{uploaded.Id}/commit",
            new CommitActivityFileRequest { PlannedWorkoutId = plannedWorkoutId });
        var committed = await commitResponse.Content.ReadFromJsonAsync<ActivityFileCommitResponse>(JsonOptions);

        committed!.PlannedWorkoutId.Should().Be(plannedWorkoutId);

        var workoutResponse = await client.GetAsync($"/api/v1/workouts/{committed.WorkoutId}");
        var workout = await workoutResponse.Content.ReadFromJsonAsync<WorkoutResponse>(JsonOptions);
        workout!.PlannedWorkoutId.Should().Be(plannedWorkoutId);
    }

    [Fact]
    public async Task Commit_ForeignPlannedWorkoutId_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var foreignAthleteId = Guid.NewGuid();
        var foreignPlanId = Guid.NewGuid();
        var foreignPwId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.TrainingPlans.Add(new TrainingPlan
            {
                Id = foreignPlanId,
                AthleteId = foreignAthleteId,
                Name = "Foreign Plan",
                Methodology = MethodologyChoice.Polarized,
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2026, 6, 30),
                PlannedWorkouts = new List<PlannedWorkout>
                {
                    new()
                    {
                        Id = foreignPwId,
                        AthleteId = foreignAthleteId,
                        TrainingPlanId = foreignPlanId,
                        Sport = Sport.Run,
                        ScheduledDate = new DateOnly(2026, 6, 15),
                        Title = "Foreign Workout"
                    }
                }
            });
            db.Athletes.Add(new Athlete
            {
                Id = foreignAthleteId,
                Name = "Foreign Athlete",
                Gender = Gender.Male,
                DateOfBirth = new DateOnly(1990, 1, 1),
                HeightCm = 180,
                WeightKg = 75,
                TypicalWeeklyHours = 10,
                Methodology = MethodologyChoice.Polarized
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var uploaded = await UploadAndReadAsync(client, "sample-run.tcx", "sample-run.tcx");
        var response = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{uploaded.Id}/commit",
            new CommitActivityFileRequest { PlannedWorkoutId = foreignPwId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Commit_Twice_Returns409()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var uploaded = await UploadAndReadAsync(client, "sample-run.tcx", "sample-run.tcx");
        var first = await client.PostAsJsonAsync($"/api/v1/activityfiles/{uploaded.Id}/commit", new CommitActivityFileRequest());
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync($"/api/v1/activityfiles/{uploaded.Id}/commit", new CommitActivityFileRequest());
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Workouts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Commit_UnknownFileId_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{Guid.NewGuid()}/commit", new CommitActivityFileRequest());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Commit_ForeignFileId_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var foreignFileId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ActivityFiles.Add(new ActivityFile
            {
                Id = foreignFileId,
                AthleteId = Guid.NewGuid(),
                FileName = "foreign.tcx",
                Format = ActivityFileFormat.Tcx,
                ByteSize = 4,
                Content = new byte[] { 1, 2, 3, 4 },
                UploadedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{foreignFileId}/commit", new CommitActivityFileRequest());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Commit_PersistsTheZoneHistogramJson()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var uploaded = await UploadAndReadAsync(client, "sample-run.tcx", "sample-run.tcx");
        var commitResponse = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{uploaded.Id}/commit", new CommitActivityFileRequest());
        commitResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var file = await db.ActivityFiles.AsNoTracking().SingleAsync(f => f.Id == uploaded.Id);

        file.ZoneHistogramJson.Should().NotBeNull();
        var entries = JsonSerializer.Deserialize<List<ZoneHistogramEntry>>(
            file.ZoneHistogramJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        entries.Should().HaveCount(5);
        entries!.Select(e => e.ZoneNumber).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task Commit_SetsParsedWorkoutIdToTheNewWorkout()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var uploaded = await UploadAndReadAsync(client, "sample-run.tcx", "sample-run.tcx");
        var commitResponse = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{uploaded.Id}/commit", new CommitActivityFileRequest());
        var committed = await commitResponse.Content.ReadFromJsonAsync<ActivityFileCommitResponse>(JsonOptions);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var file = await db.ActivityFiles.AsNoTracking().SingleAsync(f => f.Id == uploaded.Id);

        file.ParsedWorkoutId.Should().Be(committed!.WorkoutId);
    }
```

**Verify:**
```
dotnet test api/Bryk.sln --filter FullyQualifiedName~ActivityFilesControllerTests
```
All 8 tests added in this step pass, plus the 12 from Steps 16–17 (20 total in the file so far).

## Step 19 — Integration tests: discard + source lookup

Append to `ActivityFilesControllerTests.cs`, after the Step 18 tests — this closes out the file.

```csharp
    [Fact]
    public async Task Discard_UncommittedFile_Returns204AndRemovesTheRow()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var uploaded = await UploadAndReadAsync(client, "sample-run.tcx", "sample-run.tcx");
        var response = await client.DeleteAsync($"/api/v1/activityfiles/{uploaded.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.ActivityFiles.AnyAsync(f => f.Id == uploaded.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Discard_CommittedFile_Returns409()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var uploaded = await UploadAndReadAsync(client, "sample-run.tcx", "sample-run.tcx");
        await client.PostAsJsonAsync($"/api/v1/activityfiles/{uploaded.Id}/commit", new CommitActivityFileRequest());

        var response = await client.DeleteAsync($"/api/v1/activityfiles/{uploaded.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Discard_ForeignFile_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var foreignFileId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ActivityFiles.Add(new ActivityFile
            {
                Id = foreignFileId,
                AthleteId = Guid.NewGuid(),
                FileName = "foreign.tcx",
                Format = ActivityFileFormat.Tcx,
                ByteSize = 4,
                Content = new byte[] { 1, 2, 3, 4 },
                UploadedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var response = await client.DeleteAsync($"/api/v1/activityfiles/{foreignFileId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSource_ForACommittedWorkout_ReturnsTheFileSummary()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var uploaded = await UploadAndReadAsync(client, "sample-run.tcx", "sample-run.tcx");
        var commitResponse = await client.PostAsJsonAsync(
            $"/api/v1/activityfiles/{uploaded.Id}/commit", new CommitActivityFileRequest());
        var committed = await commitResponse.Content.ReadFromJsonAsync<ActivityFileCommitResponse>(JsonOptions);

        var response = await client.GetAsync($"/api/v1/activityfiles/by-workout/{committed!.WorkoutId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var source = await response.Content.ReadFromJsonAsync<ActivityFileSourceResponse>(JsonOptions);

        source!.FileName.Should().Be("sample-run.tcx");
        source.Format.Should().Be(ActivityFileFormat.Tcx);
    }

    [Fact]
    public async Task GetSource_ForAManuallyLoggedWorkout_Returns200WithNullBody()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var logResponse = await client.PostAsJsonAsync("/api/v1/workouts", new LogWorkoutRequest
        {
            Sport = Sport.Bike,
            CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ActualDurationSeconds = 1800,
            AvgHr = 140
        });
        var logged = await logResponse.Content.ReadFromJsonAsync<WorkoutResponse>(JsonOptions);

        var response = await client.GetAsync($"/api/v1/activityfiles/by-workout/{logged!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK); // not 404 — "no source file" is the common case
        var body = await response.Content.ReadFromJsonAsync<ActivityFileSourceResponse?>(JsonOptions);
        body.Should().BeNull();
    }
```

**Verify:**
```
dotnet test api/Bryk.sln --filter FullyQualifiedName~ActivityFilesControllerTests
```
All 5 tests added in this step pass, plus the 20 from Steps 16–18 — **25 tests total** in
`ActivityFilesControllerTests.cs`.

## Step 20 — Final verification, diff-stat sanity, and commit

Run the full command set from `Tasks-19-4.md`:
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```

- `dotnet build` — 0 errors, warnings unchanged from the Step 0 baseline (**≤ 16**).
- `dotnet test api/Bryk.sln` — every existing test still green, plus this task's **31 new tests** (6
  `ActivityFileUploadRequestValidatorTests` + 25 `ActivityFilesControllerTests`) — expected total **332**
  if the Step 0 baseline of 301 was confirmed correct; if the Step 0 baseline differed, the expected total
  is *(confirmed baseline) + 31*. Zero failures either way.
- `pnpm run build` and `pnpm exec vitest run --no-file-parallelism` — green, **252 / 56 files**, byte-for-
  byte unchanged (this task touches no `ui/` file — if this number moved, something outside scope
  changed; stop and investigate before committing).
- `git status` / `git add -A && git diff --cached --stat` — confirm **only** these files appear:
  - `api/Bryk.Application/ActivityFiles/ActivityFileLimits.cs` (new)
  - `api/Bryk.Application/ActivityFiles/ActivityFileUploadRequest.cs` (new)
  - `api/Bryk.Application/ActivityFiles/Validators/ActivityFileUploadRequestValidator.cs` (new)
  - `api/Bryk.Application/ActivityFiles/CommitActivityFileRequest.cs` (new)
  - `api/Bryk.Application/ActivityFiles/Validators/CommitActivityFileRequestValidator.cs` (new)
  - `api/Bryk.Application/ActivityFiles/ActivityFileResponses.cs` (new)
  - `api/Bryk.Application/ActivityFiles/IActivityFileService.cs` (new)
  - `api/Bryk.Application/ActivityFiles/ActivityFileService.cs` (new)
  - `api/Bryk.API/Controllers/ActivityFilesController.cs` (new)
  - `api/Bryk.API/Program.cs` (extended — exactly 4 added lines)
  - `api/Bryk.Application.Tests/ActivityFiles/ActivityFileUploadRequestValidatorTests.cs` (new)
  - `api/Bryk.API.Tests/ActivityFiles/ActivityFilesControllerTests.cs` (new)
  - If the diff shows `LoadCalculator.cs`, `Workout.cs`, `WorkoutService.cs`, `WorkoutsController.cs`,
    `ExceptionHandlingMiddleware.cs`, `IActivityFileRepository.cs`, any migration, or any `*.csproj` —
    **STOP**, that is scope creep beyond `Tasks-19-4.md`'s "What NOT to modify" / Non-goals fence.
- Re-confirm the review checklist from `Tasks-19-4.md` by eye: exactly one synthetic `WorkoutStepResult`
  per commit with `WorkoutStepId == null` and `OrderIndex == 0`; the bike-power test asserts exactly
  `110.25m`; exactly one `SaveChangesAsync` per write path and zero on every rejection path;
  `ValidateOrThrowAsync` (never `ValidateAndThrowAsync`) at the top of `UploadAsync`/`CommitAsync`;
  duplicate commit → 409 keyed on `ActivityFile.ParsedWorkoutId`; `GET .../by-workout/{id}` returns 200
  with a null body for a manually-logged workout; `IFormFile` appears only in
  `ActivityFilesController.cs`.
- Commit with the message from `Tasks-19-4.md` (no AI co-author trailer — project convention):

```
feat: activity-file upload, preview and commit endpoints

Make Phase 19 reachable. POST /api/v1/activityfiles takes a multipart file,
validates extension, size and magic bytes, parses it through the format's
IActivityFileParser and returns 201 with the parsed session actuals, the load
it will produce, the five-bucket zone histogram and the match candidates -
the athlete's unlinked planned workouts within one day, same sport, nearest
first. The bytes are stored; no Workout exists yet. POST
/activityfiles/{id}/commit creates it, DELETE throws the preview away, and
GET /activityfiles/by-workout/{id} answers the "from file" badge from the
reverse lookup (200 with null when a workout was logged by hand).

The load routing is the point. Workout has no session-level power or pace and
LoadCalculator's session path hardcodes both to null, so commit writes ONE
synthetic WorkoutStepResult - WorkoutStepId null, OrderIndex 0 - carrying the
parsed power, pace, HR, duration and distance. That routes the import into the
existing StepResults branch and reaches the real IF branches with no migration
and no edit to the calculator (ADR-0010 3). The test pins it: a 210 W hour
against a 200 W FTP commits at exactly 110.25 TSS.

Duplicate commits are rejected 409 on ActivityFile.ParsedWorkoutId rather than
a column on Workout, which is why Workout is untouched this phase. Corrupt and
mistyped files fail 400 before anything is staged, and each write path commits
once - the workout, its step result and the file link land in a single
SaveChangesAsync so a half-committed file can never slip past the guard. The
25 MB cap is enforced by the validator with the framework limit set higher on
purpose, so an oversized upload is a clean 400 rather than a 500 through a
middleware that has no case for the pipeline's own exceptions.
```
