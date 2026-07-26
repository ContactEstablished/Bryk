namespace Bryk.Application.ActivityFiles;

/// <summary>
/// The two-step activity-file import (ADR-0010): upload parses and previews, commit is the only call
/// that creates a <c>Workout</c>, discard throws the preview away.
/// </summary>
public interface IActivityFileService
{
    /// <summary>
    /// Validates, sniffs, parses and stores one uploaded activity file, returning the parsed preview with
    /// the load it will produce, the five-bucket zone histogram and the match candidates. Throws
    /// <see cref="Exceptions.ValidationException"/> → 400 for an unsupported extension, an empty or
    /// oversized body, contents that do not match the extension, a malformed file, or a start time in the
    /// future — in every one of those cases <b>nothing is persisted</b>.
    /// </summary>
    Task<ActivityFileUploadResponse> UploadAsync(ActivityFileUploadRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates the <c>Workout</c> for a previously uploaded file, optionally linked to a planned workout.
    /// Throws <see cref="KeyNotFoundException"/> → 404 when the file (or the supplied planned workout) is
    /// missing or belongs to another athlete, and <see cref="InvalidOperationException"/> → 409 when the
    /// file has already been committed (ADR-0010 §4).
    /// </summary>
    Task<ActivityFileCommitResponse> CommitAsync(Guid id, CommitActivityFileRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes an uncommitted preview. Throws <see cref="KeyNotFoundException"/> → 404 when missing or
    /// foreign, and <see cref="InvalidOperationException"/> → 409 when the file has already been
    /// committed (delete the workout instead).
    /// </summary>
    Task DiscardAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The "from file" badge lookup, via the reverse link <c>ActivityFile.ParsedWorkoutId</c>. Returns
    /// <b>null</b> — not a 404 — when the workout was logged by hand or belongs to another athlete:
    /// "this workout has no source file" is the common case and must not read as an error in the client.
    /// </summary>
    Task<ActivityFileSourceResponse?> GetSourceForWorkoutAsync(Guid workoutId, CancellationToken ct = default);
}
