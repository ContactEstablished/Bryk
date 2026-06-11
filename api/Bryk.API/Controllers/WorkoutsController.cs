using Asp.Versioning;
using Bryk.Application.Training.Workouts;
using Bryk.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class WorkoutsController(IWorkoutService workoutService) : ControllerBase
{
    /// <summary>Logs a completed workout (session + optional per-step actuals) for the current athlete.</summary>
    [HttpPost]
    public async Task<IActionResult> LogAsync([FromBody] LogWorkoutRequest request, CancellationToken cancellationToken)
    {
        WorkoutResponse result = await workoutService.LogAsync(request, cancellationToken);
        return StatusCode(201, result);
    }

    /// <summary>Returns a completed workout with its step results. 404 if it does not exist or belongs to another athlete.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        WorkoutResponse result = await workoutService.GetAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns the current athlete's completed workouts, newest first. All query parameters are optional:
    /// <c>from</c>/<c>to</c> bound <c>CompletedDate</c> (inclusive), <c>sport</c> narrows by discipline,
    /// and <c>skip</c>/<c>take</c> page the result (<c>take</c> clamped to 1..100, default 20; <c>skip</c>
    /// ≥ 0, default 0).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetWorkoutsAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Sport? sport,
        [FromQuery] int? skip,
        [FromQuery] int? take,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkoutResponse> result = await workoutService.GetWorkoutsAsync(from, to, sport, skip, take, cancellationToken);
        return Ok(result);
    }

    /// <summary>Replaces a completed workout's actuals and step results, recomputing load. 404 if it does not exist or belongs to another athlete.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateWorkoutRequest request, CancellationToken cancellationToken)
    {
        WorkoutResponse result = await workoutService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Hard-deletes a completed workout (step results cascade). 404 if it does not exist or belongs to another athlete.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await workoutService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
