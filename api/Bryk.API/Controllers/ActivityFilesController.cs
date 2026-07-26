using Asp.Versioning;
using Bryk.Application.ActivityFiles;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ActivityFilesController(IActivityFileService activityFileService) : ControllerBase
{
    /// <summary>
    /// Uploads one activity file (.fit/.tcx/.gpx) as multipart form data under the part name <c>file</c>,
    /// returning 201 with the parsed preview, the load it will produce, the five-bucket zone histogram and
    /// the match candidates. 400 when the extension is unsupported, the body is empty or over the 25 MB
    /// limit, the contents do not match the extension, the file is malformed, or the activity starts in
    /// the future — nothing is persisted in any of those cases.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(ActivityFileLimits.HardCapBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ActivityFileLimits.HardCapBytes)]
    public async Task<IActionResult> UploadAsync([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        ActivityFileUploadResponse result = await activityFileService.UploadAsync(
            await ToRequestAsync(file, cancellationToken), cancellationToken);
        return StatusCode(201, result);
    }

    /// <summary>
    /// Creates the workout for a previously uploaded file, optionally linked to a planned workout.
    /// 201 on success, 404 if the file or planned workout is missing or belongs to another athlete,
    /// 409 if the file has already been committed.
    /// </summary>
    [HttpPost("{id:guid}/commit")]
    public async Task<IActionResult> CommitAsync(Guid id, [FromBody] CommitActivityFileRequest request, CancellationToken cancellationToken)
    {
        ActivityFileCommitResponse result = await activityFileService.CommitAsync(id, request, cancellationToken);
        return StatusCode(201, result);
    }

    /// <summary>
    /// Discards an uncommitted upload. 204 on success, 404 if it does not exist or belongs to another
    /// athlete, 409 if it has already been committed (delete the workout instead).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DiscardAsync(Guid id, CancellationToken cancellationToken)
    {
        await activityFileService.DiscardAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Returns the activity file a workout was imported from, or 200 with a null body when it was logged
    /// by hand. Never 404 — "no source file" is the common case, not an error.
    /// </summary>
    [HttpGet("by-workout/{workoutId:guid}")]
    public async Task<IActionResult> GetSourceAsync(Guid workoutId, CancellationToken cancellationToken)
    {
        ActivityFileSourceResponse? result = await activityFileService.GetSourceForWorkoutAsync(workoutId, cancellationToken);

        // JsonResult, not Ok(): the framework's HttpNoContentOutputFormatter turns Ok(null) into a 204,
        // and the client needs a 200 with an explicit null body to tell "logged by hand" apart from
        // "endpoint returned nothing". Serializer options still come from the app's AddJsonOptions.
        return new JsonResult(result);
    }

    // IFormFile is Microsoft.AspNetCore.Http; Bryk.Application must not reference it, so the copy
    // to a transport-neutral request happens here and nowhere else. A missing form part yields an empty
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
