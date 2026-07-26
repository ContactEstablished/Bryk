using System.Text.Json;
using Bryk.Application.Common;
using Bryk.Application.Common.Validation;
using Bryk.Application.Training.Load;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentValidation;
using ValidationException = Bryk.Application.Exceptions.ValidationException;

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
    // The persisted histogram is camelCase — [{"zoneNumber":1,"seconds":600}, ...] — which is the shape
    // Task 19-6 deserializes. Changing it after Phase 19 ships is a data-format change (ADR-0010 §5).
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ActivityFileUploadResponse> UploadAsync(ActivityFileUploadRequest request, CancellationToken ct = default)
    {
        await uploadValidator.ValidateOrThrowAsync(request, ct);
        var athleteId = currentUser.GetCurrentAthleteId();

        // The validator has already guaranteed the extension is one of the three.
        var format = ResolveFormat(request.FileName);

        if (!ContentMatchesFormat(request.Content, format))
        {
            throw new ValidationException(new[] { "File: The file's contents do not match its extension." });
        }

        // A malformed file throws 19-2's "File:" ValidationException here → 400 with nothing persisted.
        // Deliberately not wrapped in try/catch.
        var parser = parsers.First(p => p.Format == format);
        var parsed = await parser.ParseAsync(new MemoryStream(request.Content, writable: false), ct);

        // A future timestamp means a corrupt or misconfigured device (mirrors LogWorkoutRequestValidator's
        // CompletedDate rule). Parsers never read the clock, so the rejection lives here.
        if (DateOnly.FromDateTime(parsed.StartTimeUtc) > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ValidationException(new[] { "File: The activity's start time is in the future." });
        }

        var completedDate = DateOnly.FromDateTime(parsed.StartTimeUtc);
        var histogram = await ComputeHistogramAsync(parsed, athleteId, ct);

        // The preview's TSS is the number commit will persist, computed the same way — through a
        // transient, never-staged Workout carrying the same synthetic step result.
        var transient = BuildWorkout(parsed, athleteId, plannedWorkoutId: null);
        var load = await loadService.ComputeActualLoadAsync(transient, ct);

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
            // Not stored at upload: an un-committed preview must leave no derived data behind — the JSON
            // is written at commit (ADR-0010 §5).
            ZoneHistogramJson = null
        };

        await fileRepo.AddAsync(file, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ActivityFileUploadResponse
        {
            Id = file.Id,
            FileName = file.FileName,
            Format = file.Format,
            ByteSize = file.ByteSize,
            Parsed = MapParsed(parsed, completedDate),
            ComputedLoad = load,
            ZoneSeconds = histogram,
            MatchCandidates = candidates
        };
    }

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

        // Commit re-parses rather than caching the preview: samples are never persisted (ADR-0010 §6) and
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
            // No further check: a planned workout that was linked by someone else between preview and
            // commit is accepted — a single-athlete race not worth a lock in v1.
        }

        var workout = BuildWorkout(parsed, athleteId, request.PlannedWorkoutId);
        workout.ComputedLoad = await loadService.ComputeActualLoadAsync(workout, ct);
        await workoutRepo.AddAsync(workout, ct);

        var histogram = await ComputeHistogramAsync(parsed, athleteId, ct);

        // The file row is tracked, so mutating it is enough — no fileRepo.Update call.
        file.ParsedWorkoutId = workout.Id;
        file.ZoneHistogramJson = JsonSerializer.Serialize(histogram, JsonOptions);

        // Exactly one commit, covering the workout, its step result and the file link atomically. Two
        // SaveChangesAsync calls here would leave a window where a workout exists but the file is still
        // marked un-committed, which the duplicate guard would then let through twice.
        await unitOfWork.SaveChangesAsync(ct);

        return new ActivityFileCommitResponse
        {
            WorkoutId = workout.Id,
            PlannedWorkoutId = workout.PlannedWorkoutId,
            ComputedLoad = workout.ComputedLoad
        };
    }

    public async Task DiscardAsync(Guid id, CancellationToken ct = default)
    {
        var file = await fileRepo.GetByIdTrackedAsync(id, ct);
        if (file is null || file.AthleteId != currentUser.GetCurrentAthleteId())
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
        var files = await fileRepo.GetByParsedWorkoutIdsAsync(currentUser.GetCurrentAthleteId(), new[] { workoutId }, ct);
        var file = files.FirstOrDefault();

        return file is null
            ? null
            : new ActivityFileSourceResponse
            {
                Id = file.Id,
                FileName = file.FileName,
                Format = file.Format,
                UploadedAt = file.UploadedAt
            };
    }

    private static ActivityFileFormat ResolveFormat(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".fit" => ActivityFileFormat.Fit,
            ".tcx" => ActivityFileFormat.Tcx,
            _ => ActivityFileFormat.Gpx
        };

    // Cheap "is it even the right kind of bytes" gate. The root-element check belongs to 19-2's parsers.
    private static bool ContentMatchesFormat(byte[] content, ActivityFileFormat format)
    {
        if (format == ActivityFileFormat.Fit)
        {
            // The FIT header carries the ASCII data-type signature ".FIT" at offset 8.
            return content.Length >= 12
                   && content[8] == (byte)'.' && content[9] == (byte)'F'
                   && content[10] == (byte)'I' && content[11] == (byte)'T';
        }

        var i = 0;
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
        {
            i = 3; // skip a UTF-8 BOM
        }

        while (i < content.Length && content[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            i++;
        }

        return i < content.Length && content[i] == (byte)'<';
    }

    private async Task<IReadOnlyList<ZoneHistogramEntry>> ComputeHistogramAsync(ParsedActivity parsed, Guid athleteId, CancellationToken ct)
    {
        var zones = await zoneService.GetZonesAsync(ct);
        var athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct);
        return ZoneHistogramCalculator.Compute(
            parsed,
            zones.Sports.FirstOrDefault(s => s.Sport == parsed.Sport),
            athlete?.MaxHr);
    }

    // Match candidates: the athlete's unlinked planned workouts within ±1 day (inclusive), same sport,
    // nearest first. No fuzzy duration/load scoring in v1 — sport + date + unlinked, nothing more.
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
            .OrderBy(c => Math.Abs(c.DayOffset))
            .ThenBy(c => c.ScheduledDate)
            .ThenBy(c => c.Title)
            .ToList();
    }

    private static ParsedActivityDto MapParsed(ParsedActivity parsed, DateOnly completedDate) => new()
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
    };

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
}
