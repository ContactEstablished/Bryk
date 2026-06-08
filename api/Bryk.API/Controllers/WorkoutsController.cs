using Asp.Versioning;
using Bryk.Application.Training.Workouts;
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

    /// <summary>Returns the current athlete's most recent completed workouts (newest first); pass take to bound the count.</summary>
    [HttpGet]
    public async Task<IActionResult> GetRecentAsync([FromQuery] int take, CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkoutResponse> result = await workoutService.GetRecentAsync(take, cancellationToken);
        return Ok(result);
    }
}
