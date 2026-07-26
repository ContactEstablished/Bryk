# Task 19-4 — upload / commit / discard endpoints, validation, match candidates

## Surface
Backend only. A new `Bryk.Application/ActivityFiles/` service slice (interface + primary-ctor service +
request/response DTOs + two validators + a limits constant), one new thin controller
(`ActivityFilesController`) with four actions, the per-route upload size cap, the DI appends in
`Program.cs`, and tests on both layers. **No migration** (19-1 shipped the only one), **no new package**
(19-3 shipped the only one), **no UI**.

## Why
This is where Phase 19 becomes reachable. The two-step flow exists because a device file is not
self-evidently a workout: the athlete has to see what was parsed, see the load it will produce, and pick
which planned session it satisfies before anything is written to their history. So `POST /activityfiles`
parses, stores the bytes and returns a preview with **match candidates**, and `POST
/activityfiles/{id}/commit` is the only call that creates a `Workout` — `DELETE` throws the preview away.
This task is also where ADR-0010 §3's load routing actually happens: commit writes **one synthetic
`WorkoutStepResult`**, which is what pushes the import down `LoadCalculator`'s StepResults branch and
finally reaches the power and pace IF branches with real numbers — with **zero** change to the
calculator. Getting that one object right is the difference between an import that reports a real TSS
and one that silently falls back to heart rate.

## Depends on
- **Task 19-1** — `ActivityFile`, `ActivityFileFormat`, `IActivityFileRepository` (its four methods are
  the complete surface; **do not extend that interface** — if something is missing, STOP and ask), and
  the `AddScoped<IActivityFileRepository, ActivityFileRepository>()` line already in `Program.cs`.
- **Task 19-2** — `IActivityFileParser`, `ParsedActivity`, `ActivitySample`, `ZoneHistogramEntry`,
  `ZoneHistogramCalculator`, and the `File:`-prefixed `ValidationException` contract on parse failure.
  **Sample sanity (HR 30–230, power ≤ 2000 W) is 19-2's, not this task's** — do not re-implement it.
- **Task 19-3** — `FitActivityParser`; this task registers it alongside the other two.
- **ADR-0010 §2** (25 MB cap, per-route), **§3** (synthetic `WorkoutStepResult`), **§4** (duplicate-commit
  guard keys on `ActivityFile.ParsedWorkoutId`), **§5** (this task writes the histogram JSON).
- **ADR-0005 §5** — `WorkoutStepResult.WorkoutStepId` is nullable, which is what makes §3 legal.
- **`Program.cs` is shared with Task 19-1.** 19-1 lands first with the repository line; **this task
  appends only.** Do not reorder or rewrite the existing block.

## Required reading
- `api/Bryk.Application/Training/Workouts/WorkoutService.cs` — **the template**. Specifically: the
  primary-ctor dependency list (L10–17), `await validator.ValidateOrThrowAsync(request, ct)` at the top
  of each write (L21), the plan-ownership check → `KeyNotFoundException` (L26–33), the
  `Workout` construction block (L35–49), `BuildStepResults` (L151–188) — the exact `WorkoutStepResult`
  field set and `Guid.NewGuid()` / `AthleteId` / `WorkoutId` / `OrderIndex` discipline the synthetic
  result copies — and `workout.ComputedLoad = await loadService.ComputeActualLoadAsync(workout, ct)`
  **before** `AddAsync` + one `SaveChangesAsync` (L57–60). **Read only — this file is not modified.**
- `api/Bryk.Application/Training/Load/LoadCalculator.cs:74–83` and `:91–126` — read `ComputeActualLoad`'s
  StepResults branch and `ActualCardioTss`'s IF precedence yourself, so you can see exactly why the
  synthetic step result is load-bearing. **Read only — frozen for Phase 19.**
- `api/Bryk.API/Controllers/WorkoutsController.cs` — thin-controller style: `[ApiController]`,
  `[ApiVersion("1.0")]`, `[Route("api/v{version:apiVersion}/[controller]")]`, `IActionResult`,
  `StatusCode(201, result)` for creates, `NoContent()` for deletes, XML `<summary>` on every action, no
  try/catch, athlete id never from route/query/body.
- `api/Bryk.API/Middleware/ExceptionHandlingMiddleware.cs` — the mapping this task relies on:
  `ValidationException` → **400** with `{status, error, errors[], traceId}`, `KeyNotFoundException` →
  **404**, `InvalidOperationException` → **409**, everything else → **500**. Note there is **no** case
  for `InvalidDataException` or `BadHttpRequestException` (see *Size cap* below).
- `api/Bryk.Domain/Interfaces/ITrainingPlanRepository.cs:25–31` — `GetPlannedWorkoutsInRangeAsync`, the
  match-candidate read (single-table, no-tracking, across all the athlete's plans).
- `api/Bryk.Domain/Interfaces/IWorkoutRepository.cs:20–24` — `GetByAthleteInRangeAsync`, used to find
  which planned workouts are already linked.
- `api/Bryk.Application/Zones/IZoneService.cs:13` — `GetZonesAsync`; and
  `api/Bryk.Application/Analytics/AnalyticsService.cs:139–142` for how zones + `athlete?.MaxHr` are
  resolved together before a histogram computation.
- `api/Bryk.Application/Training/Workouts/LogWorkoutRequestValidator.cs` — the validator style
  (`AbstractValidator<T>`, `IsInEnum()`, `.When(...)`, a `Must` with `WithMessage`).
- `api/Bryk.API/Program.cs:35` (validator assembly scan — **no manual validator registration**),
  `:99–121` (the `AddScoped` block this task appends to).
- `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs:15–18` — the `JsonOptions`
  (`JsonSerializerDefaults.Web` + `JsonStringEnumConverter`) every integration test uses.
- `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs` — the private `ApiError` record with
  `Errors[]`, and the foreign-athlete seeding block (L172–227) this task's 404 tests reuse.

## Acceptance criteria

### `api/Bryk.Application/ActivityFiles/ActivityFileLimits.cs` (new)

```csharp
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

### `api/Bryk.Application/ActivityFiles/ActivityFileUploadRequest.cs` (new)

```csharp
public class ActivityFileUploadRequest
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
```
Class comment: the transport-neutral upload body. `IFormFile` is `Microsoft.AspNetCore.Http` and
`Bryk.Application` must not reference it (Clean Architecture dependency direction), so the **controller**
is the only place allowed to touch `IFormFile`; it copies the stream into `Content` and hands this over.

### `api/Bryk.Application/ActivityFiles/Validators/ActivityFileUploadRequestValidator.cs` (new)

`AbstractValidator<ActivityFileUploadRequest>`, exactly these rules:
- `FileName` — `NotEmpty().MaximumLength(260)`.
- `FileName` — `Must(HasSupportedExtension).WithMessage("FileName: Only .fit, .tcx and .gpx files are supported.")`
  where `HasSupportedExtension` compares `Path.GetExtension(name)` **case-insensitively**
  (`StringComparison.OrdinalIgnoreCase`) against `.fit`/`.tcx`/`.gpx`.
- `Content` — `Must(c => c.Length > 0).WithMessage("Content: The uploaded file is empty.")`.
- `Content` — `Must(c => c.Length <= ActivityFileLimits.MaxBytes).WithMessage($"Content: The file exceeds the {ActivityFileLimits.MaxBytes / (1024 * 1024)} MB limit.")`.
- Registered automatically by the assembly scan; **do not** add a `Program.cs` line for validators.
- **No magic-byte rule here** — the sniff needs the resolved format, so it lives in the service (below).
- **No sample-sanity rule here** — Task 19-2 owns HR/power bounds at the parse boundary.

### `api/Bryk.Application/ActivityFiles/CommitActivityFileRequest.cs` + `Validators/CommitActivityFileRequestValidator.cs` (new)

```csharp
public class CommitActivityFileRequest
{
    public Guid? PlannedWorkoutId { get; set; }
}
```
Validator: `RuleFor(x => x.PlannedWorkoutId).NotEqual(Guid.Empty).When(x => x.PlannedWorkoutId.HasValue)`.
Ownership of the planned workout is a repository read and therefore lives in the service.

### `api/Bryk.Application/ActivityFiles/ActivityFileResponses.cs` (new — all read shapes in one file)

```csharp
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
- `DayOffset` is `ScheduledDate.DayNumber − CompletedDate.DayNumber`, so `-1 / 0 / +1`. It is what lets
  the UI put same-day matches first and label the others.
- `ActivityFileCommitResponse` is deliberately small: the client only needs `workoutId` to navigate to
  `/workouts/{id}`, which then loads the workout through the existing `GET /workouts/{id}`.
  **Do not** rebuild a `WorkoutResponse` here — `WorkoutService.Map` is private and
  `Bryk.Application/Training/Workouts/*` is not this task's to modify.

### `api/Bryk.Application/ActivityFiles/IActivityFileService.cs` + `ActivityFileService.cs` (new)

Interface, XML `<summary>` on every member stating the 400/404/409 conditions (the
`ITrainingPlanService` style):

```csharp
public interface IActivityFileService
{
    Task<ActivityFileUploadResponse> UploadAsync(ActivityFileUploadRequest request, CancellationToken ct = default);
    Task<ActivityFileCommitResponse> CommitAsync(Guid id, CommitActivityFileRequest request, CancellationToken ct = default);
    Task DiscardAsync(Guid id, CancellationToken ct = default);
    Task<ActivityFileSourceResponse?> GetSourceForWorkoutAsync(Guid workoutId, CancellationToken ct = default);
}
```

Primary-ctor service:
```csharp
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
```

**`UploadAsync`, in this exact order:**
1. `await uploadValidator.ValidateOrThrowAsync(request, ct);` — the
   `Bryk.Application.Common.Validation` extension. **Never** FluentValidation's `ValidateAndThrowAsync`.
2. Resolve the format from the extension (`.fit`/`.tcx`/`.gpx`, case-insensitive) — the validator has
   already guaranteed it is one of the three.
3. **Magic-byte sniff.** Private static `bool ContentMatchesFormat(byte[] content, ActivityFileFormat format)`:
   - `Fit` — `content.Length >= 12` and bytes `8..11` are ASCII `.FIT` (the FIT header's data-type
     signature at offset 8).
   - `Tcx` / `Gpx` — after skipping a UTF-8 BOM and any leading ASCII whitespace, the first byte is `<`.
     The **root-element** check belongs to 19-2's parsers; this is only the cheap "is it even XML" gate.
   - Mismatch → `throw new Exceptions.ValidationException(new[] { "File: The file's contents do not match its extension." })`
     → 400, **nothing persisted** (nothing has been staged yet).
4. `var parser = parsers.First(p => p.Format == format);` and
   `var parsed = await parser.ParseAsync(new MemoryStream(request.Content, writable: false), ct);`
   A malformed file throws 19-2's `File:` `ValidationException` here → 400 with nothing persisted.
   Do **not** wrap this in try/catch.
5. Reject a file from the future:
   `if (DateOnly.FromDateTime(parsed.StartTimeUtc) > DateOnly.FromDateTime(DateTime.UtcNow))` →
   `ValidationException(new[] { "File: The activity's start time is in the future." })`. (Mirrors
   `LogWorkoutRequestValidator`'s `CompletedDate` rule; a future timestamp means a corrupt or
   misconfigured device.)
6. Build the preview:
   - `var zones = await zoneService.GetZonesAsync(ct);`
     `var athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct);`
   - `var histogram = ZoneHistogramCalculator.Compute(parsed, zones.Sports.FirstOrDefault(s => s.Sport == parsed.Sport), athlete?.MaxHr);`
   - Build a **transient, unsaved** `Workout` via the shared private
     `BuildWorkout(parsed, athleteId, plannedWorkoutId: null)` (below) and
     `var load = await loadService.ComputeActualLoadAsync(transient, ct);` — the preview's TSS is the
     number commit will persist, computed the same way. The transient workout is never staged.
   - Match candidates (below).
7. Persist the row and commit **once**:
   ```csharp
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
       ZoneHistogramJson = null
   };
   await fileRepo.AddAsync(file, ct);
   await unitOfWork.SaveChangesAsync(ct);
   ```
   Never set `CreatedAt`/`UpdatedAt` — the `AuditableEntityInterceptor` owns them.
   The histogram is **not** stored at upload: an un-committed preview must leave no derived data behind
   (ADR-0010 §5 — the JSON is written at commit).
8. Return the `ActivityFileUploadResponse`.

**Match candidates** — private
`Task<List<MatchCandidateDto>> FindCandidatesAsync(Guid athleteId, Sport sport, DateOnly date, CancellationToken ct)`:
- `var planned = await planRepo.GetPlannedWorkoutsInRangeAsync(athleteId, date.AddDays(-1), date.AddDays(1), ct);`
  — **±1 day inclusive**, the ROADMAP's window.
- `var completed = await workoutRepo.GetByAthleteInRangeAsync(athleteId, date.AddDays(-1), date.AddDays(1), ct);`
  then `var linked = completed.Where(w => w.PlannedWorkoutId is not null).Select(w => w.PlannedWorkoutId!.Value).ToHashSet();`
- Keep planned workouts where `pw.Sport == sport && !linked.Contains(pw.Id)`.
- Order by `Math.Abs(DayOffset)`, then `ScheduledDate`, then `Title` — same-day matches first.
- No fuzzy duration/load scoring in v1. Sport + date + unlinked, nothing more.

**`CommitAsync`, in this exact order:**
1. `await commitValidator.ValidateOrThrowAsync(request, ct);`
2. `var file = await fileRepo.GetByIdTrackedAsync(id, ct);`
   `if (file is null || file.AthleteId != athleteId) throw new KeyNotFoundException();` → **404**.
3. **Duplicate-commit guard (ADR-0010 §4).**
   ```csharp
   if (file.ParsedWorkoutId is not null)
   {
       throw new InvalidOperationException("This activity file has already been committed to a workout.");
   }
   ```
   → **409** through the existing middleware. The guard keys on the **file row**, never on a `Workout`
   column — there is no `Workout.SourceFileId` and none is being added.
4. Re-parse `file.Content` with the parser for `file.Format`. (Commit re-parses rather than caching the
   preview: samples are never persisted, ADR-0010 §6, and re-parsing is deterministic.)
5. If `request.PlannedWorkoutId is { } pwId`:
   `var planned = await planRepo.GetPlannedWorkoutWithStructureAsync(pwId, ct);` and
   `if (planned is null || planned.AthleteId != athleteId) throw new KeyNotFoundException();` → **404**,
   mirroring `WorkoutService.LogAsync:26–33`. No further check: a planned workout that was linked by
   someone else between preview and commit is accepted (a single-athlete race not worth a lock in v1) —
   state that in a comment.
6. `var workout = BuildWorkout(parsed, athleteId, request.PlannedWorkoutId);` — the shared private
   builder, and **the heart of this task**:
   ```csharp
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

       // ADR-0010 §3: ONE synthetic step result carries the parsed power/pace/HR. Workout has no
       // session-level AvgPower/AvgPace, and LoadCalculator's session path (LoadCalculator.cs:88)
       // hardcodes power and pace to null — so without this object an imported ride can only ever
       // reach the HR branch. With it, ComputeActualLoad takes its StepResults branch (L74–83) and
       // hits the real power/pace IF branches. Do NOT "fix" this by editing LoadCalculator, and do
       // NOT emit one step result per lap (explicitly out of scope for v1).
       workout.StepResults.Add(new WorkoutStepResult
       {
           Id = Guid.NewGuid(),
           AthleteId = athleteId,
           WorkoutId = workout.Id,
           WorkoutStepId = null,   // nullable by ADR-0005 §5 — no planned step is being realised
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
7. `workout.ComputedLoad = await loadService.ComputeActualLoadAsync(workout, ct);` — before staging,
   exactly as `WorkoutService.LogAsync:57`.
8. `await workoutRepo.AddAsync(workout, ct);`
9. Mutate the tracked file row — `file.ParsedWorkoutId = workout.Id;` and
   `file.ZoneHistogramJson = JsonSerializer.Serialize(histogram, JsonOptions);` where the histogram is
   recomputed from the re-parsed activity and `JsonOptions` is a `private static readonly
   JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);`. The stored JSON is therefore
   camelCase: `[{"zoneNumber":1,"seconds":600}, …]`, five entries, always. **No `fileRepo.Update` call**
   — the entity is tracked.
10. `await unitOfWork.SaveChangesAsync(ct);` — **exactly one commit**, covering the workout, its step
    result and the file link atomically. Two `SaveChangesAsync` calls here would leave a window where a
    workout exists but the file is still marked un-committed, which the duplicate guard would then let
    through twice. Do not split it.
11. Return `new ActivityFileCommitResponse { WorkoutId = workout.Id, PlannedWorkoutId = workout.PlannedWorkoutId, ComputedLoad = workout.ComputedLoad }`.

**`DiscardAsync`:**
- `GetByIdTrackedAsync` → missing or foreign → `KeyNotFoundException` (404).
- `if (file.ParsedWorkoutId is not null) throw new InvalidOperationException("A committed activity file cannot be discarded; delete the workout instead.");` → **409**. Discard is for previews.
- `fileRepo.Delete(file); await unitOfWork.SaveChangesAsync(ct);` — one commit. 204 from the controller.

**`GetSourceForWorkoutAsync`:**
- `var files = await fileRepo.GetByParsedWorkoutIdsAsync(athleteId, new[] { workoutId }, ct);`
- Return the first mapped to `ActivityFileSourceResponse`, or **`null`** when there is none.
  A workout that was logged manually — and a workout belonging to another athlete — both return `null`.
  **Not a 404**: "this workout has no source file" is the common case and must not read as an error in
  the client. State that in the XML doc.

### `api/Bryk.API/Controllers/ActivityFilesController.cs` (new)

```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ActivityFilesController(IActivityFileService activityFileService) : ControllerBase
```
Four actions, XML `<summary>` on each naming the status codes, no try/catch, athlete id never from
route/query/body:

1. `[HttpPost]` + `[RequestSizeLimit(ActivityFileLimits.HardCapBytes)]` +
   `[RequestFormLimits(MultipartBodyLengthLimit = ActivityFileLimits.HardCapBytes)]`, signature
   `UploadAsync([FromForm] IFormFile? file, CancellationToken cancellationToken)` → `StatusCode(201, result)`.
   The action's only logic is the transport adapter:
   ```csharp
   // IFormFile is Microsoft.AspNetCore.Http; Bryk.Application must not reference it, so the copy
   // to a transport-neutral request happens here and nowhere else.
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
   ```
   A missing form part yields an empty request, which the validator rejects with 400 — **not** an NRE.
2. `[HttpPost("{id:guid}/commit")]` — `CommitAsync(Guid id, [FromBody] CommitActivityFileRequest request, CancellationToken ct)` → `StatusCode(201, result)`.
3. `[HttpDelete("{id:guid}")]` — `DiscardAsync(Guid id, CancellationToken ct)` → `NoContent()`.
4. `[HttpGet("by-workout/{workoutId:guid}")]` — `GetSourceAsync(Guid workoutId, CancellationToken ct)`
   → `Ok(result)`, where `result` may be `null` (200 with a JSON `null` body).

Routes resolve to `/api/v1/activityfiles`, `/api/v1/activityfiles/{id}/commit`,
`/api/v1/activityfiles/{id}`, `/api/v1/activityfiles/by-workout/{workoutId}`.

### Size cap — read this before implementing it

The real 25 MB limit is enforced by the **validator** (`Content.Length > MaxBytes` → 400 with a clear
message). The two action attributes sit at a deliberately **higher** 32 MB so the framework never trips
first for a merely-oversized file. Reason: when the request pipeline aborts an over-limit body it throws
`InvalidDataException` / `BadHttpRequestException`, and `ExceptionHandlingMiddleware`'s switch has **no
case for either** — they would fall through to a generic **500**, which is not the "fails clean" the
ROADMAP asks for. Above 32 MB the framework still wins and the status is whatever it produces; that is a
**known, accepted edge**, to be recorded in the phase handoff as a Phase-21 (error-contract) follow-up.
**Adding an `InvalidDataException` case to `ExceptionHandlingMiddleware` is a cross-cutting change
requiring Sr. Dev approval — STOP and ask; do not do it inline.**

### `api/Bryk.API/Program.cs` (edit — append only)

Append to the `AddScoped` block (which already ends with the Phase-18 `IPeriodizationService` line at
L121, plus 19-1's `IActivityFileRepository` line):
```csharp
builder.Services.AddScoped<Bryk.Application.ActivityFiles.IActivityFileParser, Bryk.Infrastructure.ActivityFiles.FitActivityParser>();
builder.Services.AddScoped<Bryk.Application.ActivityFiles.IActivityFileParser, Bryk.Infrastructure.ActivityFiles.TcxActivityParser>();
builder.Services.AddScoped<Bryk.Application.ActivityFiles.IActivityFileParser, Bryk.Infrastructure.ActivityFiles.GpxActivityParser>();
builder.Services.AddScoped<Bryk.Application.ActivityFiles.IActivityFileService, Bryk.Application.ActivityFiles.ActivityFileService>();
```
Three registrations of the same interface is intentional — the service takes `IEnumerable<IActivityFileParser>`
and selects by `Format`.
- **No validator lines** (assembly scan, `Program.cs:35`).
- **No global `FormOptions`/Kestrel configuration** — the cap is per-route (the ROADMAP's own wording),
  which leaves every other endpoint's limits exactly as they are today.
- **Do not** reorder, reformat or rewrite any existing line in this file, including 19-1's.

## Non-goals
- **No migration.** 19-1 shipped the only approved one. No column, no table, no `ApplicationDbContext`
  edit, no `dotnet ef`. If this task appears to need one — **STOP and ask** (Sr. Dev gate).
- **Do not add `Workout.SourceFileId`.** The duplicate-commit guard reads `ActivityFile.ParsedWorkoutId`
  and the badge reads `GetByParsedWorkoutIdsAsync` (ADR-0010 §4). If you reach for a column on `Workout`,
  or for a `WorkoutZoneDuration` table for the histogram, **STOP and ask**. `Workout.cs` must not appear
  in `git diff`.
- **Do not edit `api/Bryk.Application/Training/Load/LoadCalculator.cs`** — frozen. Imported power and
  pace reach the IF branches through the synthetic `WorkoutStepResult` and nothing else. If a test shows
  load coming out HR-based for a power file, the bug is in the step result, **not** in the calculator —
  **STOP and ask** before touching it.
- **Do not emit one step result per lap.** Exactly one synthetic result, `OrderIndex = 0`. Per-lap
  detail is explicitly out of scope for v1.
- **Do not modify** `WorkoutService.cs`, `IWorkoutService.cs`, `WorkoutResponse.cs`,
  `LogWorkoutRequest*`, `UpdateWorkoutRequest*`, or `WorkoutsController.cs`.
- **Do not extend `IActivityFileRepository`** (19-1's, complete as shipped) or any other repository
  contract. Adding a method to a repository contract is a persistence-boundary change: **STOP and ask**.
- **Do not modify `ExceptionHandlingMiddleware.cs`** — cross-cutting, Sr. Dev gate. Use the existing
  `ValidationException` → 400 / `KeyNotFoundException` → 404 / `InvalidOperationException` → 409 mapping.
  **No ProblemDetails rework** — Phase 21 owns it.
- **Do not re-implement sample sanity** (HR 30–230, power ≤ 2000 W) — Task 19-2 owns it at the parse
  boundary and its parsers have already applied it by the time this service sees a `ParsedActivity`.
- **Do not edit any Task 19-2 or 19-3 file**: the parsers, `IActivityFileParser`, `ParsedActivity`,
  `ZoneHistogramCalculator`, `ZoneHistogramEntry`, `ActivitySampleBounds`, `Bryk.Infrastructure.csproj`,
  or the fixtures/csproj glob in `Bryk.API.Tests`.
- **No new NuGet or npm package.** 19-3 shipped the only one.
- No UI (19-5 owns it), no analytics change (19-6 owns `TimeInZoneCalculator.cs`,
  `TimeInZoneResponse.cs`, `AnalyticsService.cs`), no list endpoint for activity files, no re-parse or
  re-commit endpoint, no bulk/multi-file upload, no vendor OAuth or device sync, no per-second sample
  persistence, no power curves/decoupling/lap deep-dives, no push-to-device.
- **No auth code** — Phase 12 stays deferred and approval-gated; ownership is `ICurrentUserService` +
  `KeyNotFoundException` → 404, nothing else.
- **Do not fix** the two pre-existing nullable warnings in `WorkoutsControllerTests.cs:121,150`.
- **Do not** revert, stash, or commit unrelated working-tree changes.

## Test expectations

**Unit — `api/Bryk.Application.Tests/ActivityFiles/ActivityFileUploadRequestValidatorTests.cs` (new).**
Validator-only, no host — this is where the size boundary is pinned, because a 25 MB multipart POST is
not worth the integration-test runtime.
- `Rejects_UnsupportedExtension` — `"ride.csv"` → invalid with a `FileName:` message.
- `Accepts_UpperCaseExtension` — `"RIDE.TCX"` → the extension rule passes.
- `Rejects_EmptyContent` — zero-length `Content` → `Content:` message.
- `Rejects_ContentOneByteOverTheCap` — `new byte[ActivityFileLimits.MaxBytes + 1]` → invalid.
- `Accepts_ContentExactlyAtTheCap` — `new byte[ActivityFileLimits.MaxBytes]` → the size rule passes
  (inclusive bound).
- `Rejects_FileNameOver260Characters`.

**Integration — `api/Bryk.API.Tests/ActivityFiles/ActivityFilesControllerTests.cs` (new).**
Post multipart with `MultipartFormDataContent` + `ByteArrayContent`, part name **`"file"`**, using
19-2's committed fixtures. Reuse the `JsonOptions` and private `ApiError` record patterns from
`WorkoutsControllerTests` / `TrainingPlansControllerTests`. Where a test needs an athlete profile or a
foreign athlete, seed through a `factory.Services.CreateScope()` `ApplicationDbContext`.

Upload:
- `Upload_TcxRunFixture_Returns201WithParsedPreview` — `parsed.sport == "Run"`,
  `durationSeconds == 600`, `distanceMeters == 2000`, `avgHr == 144`, `avgPace == 300`,
  `byteSize > 0`, `id != Guid.Empty`.
- `Upload_UnsupportedExtension_Returns400` — the TCX bytes sent as `ride.csv` → 400 with a `FileName:`
  error.
- `Upload_ExtensionAndContentMismatch_Returns400` — the TCX bytes sent as `ride.fit` → 400 with a
  `File:` error (the magic-byte sniff).
- `Upload_CorruptXml_Returns400AndPersistsNothing` — `"<not xml"` as `ride.tcx` → 400, **and** a
  fresh-scope `ApplicationDbContext` shows `ActivityFiles` empty. This is the ROADMAP's "nothing
  persisted on parse failure" made executable.
- `Upload_MissingFilePart_Returns400` — an empty `MultipartFormDataContent` → 400, not 500.
- `Upload_SameDaySameSportUnlinkedPlannedWorkout_IsOfferedAsACandidate` — seed a plan with a planned
  workout on the fixture's date with `Sport.Run` → `matchCandidates` has exactly 1 entry with
  `dayOffset == 0` and the planned workout's id/title.
- `Upload_PlannedWorkoutTwoDaysAway_IsNotOffered` — the ±1 boundary, both directions
  (seed `date − 2` and `date + 2`; assert both absent, while `date − 1` and `date + 1` are present).
- `Upload_PlannedWorkoutOfADifferentSport_IsNotOffered`.
- `Upload_PlannedWorkoutAlreadyLinkedToAWorkout_IsNotOffered` — log a workout against it first.
- `Upload_ZoneSeconds_AlwaysHasFiveBuckets` — `zoneSeconds` length 5, `zoneNumber` 1..5 in order.

Commit — including the phase's headline assertion:
- `Commit_BikeFileWithPower_ComputesLoadThroughThePowerIfBranch` — seed the test athlete with an
  `AthleteSportProfile` for `Sport.Bike` with `ThresholdValue = 200m`; upload `sample-ride.tcx`
  (avg power 210 W over 3600 s); commit → `computedLoad.Should().Be(110.25m)`.
  (`IF = 210/200 = 1.05`; `3600 × 1.05² / 3600 × 100 = 110.25`.) This proves ADR-0010 §3: without the
  synthetic step result the same file would fall to the HR branch and produce a different number.
  Assert the exact decimal — no tolerance.
- `Commit_CreatesExactlyOneSyntheticStepResult` — after commit, `GET /api/v1/workouts/{workoutId}`
  returns `stepResults` with **1** entry whose `workoutStepId` is `null`, `orderIndex == 0`,
  `avgPower == 210`, `avgHr == 141`.
- `Commit_WithoutPlannedWorkoutId_CreatesAnUnlinkedWorkout` — `plannedWorkoutId` null in both the
  commit response and the workout read.
- `Commit_WithOwnedPlannedWorkoutId_LinksIt` — the workout read echoes the planned workout id.
- `Commit_ForeignPlannedWorkoutId_Returns404` — reuse the foreign-athlete seeding block.
- `Commit_Twice_Returns409` — second call → 409 (ADR-0010 §4's duplicate guard), and a fresh-scope
  count of `Workouts` shows exactly **1** row.
- `Commit_UnknownFileId_Returns404` / `Commit_ForeignFileId_Returns404`.
- `Commit_PersistsTheZoneHistogramJson` — a fresh-scope read of the `ActivityFile` row shows
  `ZoneHistogramJson` non-null, deserializing to **5** `ZoneHistogramEntry` values whose `zoneNumber`s
  are `1..5` (proves the camelCase shape 19-6 will read).
- `Commit_SetsParsedWorkoutIdToTheNewWorkout` — fresh-scope assertion.

Discard + source:
- `Discard_UncommittedFile_Returns204AndRemovesTheRow`.
- `Discard_CommittedFile_Returns409`.
- `Discard_ForeignFile_Returns404`.
- `GetSource_ForACommittedWorkout_ReturnsTheFileSummary` — `fileName`, `format == "Tcx"`.
- `GetSource_ForAManuallyLoggedWorkout_Returns200WithNullBody` — log a workout through
  `POST /workouts`, then `GET /activityfiles/by-workout/{id}` → 200 and a null body (**not** 404).

## Verification commands
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```
xUnit must rise from the **262** baseline (173 `Bryk.Application.Tests` + 89 `Bryk.API.Tests`) plus what
19-1 … 19-3 added, with zero failures. Vitest stays at exactly **252 / 56 files** — this task touches no
UI. Warnings must not exceed **16**.

## Review checklist
- [ ] `POST /api/v1/activityfiles` returns 201 with parsed actuals, the computed load, five zone buckets
      and the match candidates; a parse failure returns 400 with **zero** rows in `ActivityFiles`.
- [ ] Commit creates **exactly one** `WorkoutStepResult`, with `WorkoutStepId == null` and
      `OrderIndex == 0`, carrying the parsed power/pace/HR/duration/distance.
- [ ] `LoadCalculator.cs`, `Workout.cs`, `WorkoutService.cs` and `WorkoutsController.cs` are absent from
      `git diff`; no migration file; no `Workout.SourceFileId`; no `WorkoutZoneDuration`.
- [ ] The bike-power commit test asserts exactly `110.25m` — the IF branch is proven, not assumed.
- [ ] **Exactly one** `SaveChangesAsync` per write path (upload, commit, discard); zero on every
      rejection path.
- [ ] `ValidateOrThrowAsync` (not `ValidateAndThrowAsync`) at the top of both write methods.
- [ ] Duplicate commit → 409 keyed on `ActivityFile.ParsedWorkoutId`; discard of a committed file → 409;
      missing/foreign file or planned workout → 404; every validation failure → 400.
- [ ] `GET /activityfiles/by-workout/{id}` returns **200 with null** for a manually-logged workout.
- [ ] `IFormFile` appears only in `ActivityFilesController.cs`; `Bryk.Application` has no
      `Microsoft.AspNetCore.Http` reference.
- [ ] `Program.cs` diff is **append-only** (4 lines) with no validator registration and no global
      form/Kestrel configuration; `ExceptionHandlingMiddleware.cs` is untouched.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

## Suggested commit
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
