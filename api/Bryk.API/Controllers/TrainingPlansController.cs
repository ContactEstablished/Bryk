using Asp.Versioning;
using Bryk.Application.Calendar;
using Bryk.Application.Training;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class TrainingPlansController(
    ITrainingPlanService trainingPlanService,
    IStructuredWorkoutService structuredWorkoutService) : ControllerBase
{
    /// <summary>Creates a new training plan (with any planned workouts supplied) for the current athlete.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] TrainingPlanRequest request, CancellationToken cancellationToken)
    {
        TrainingPlanResponse result = await trainingPlanService.CreateAsync(request, cancellationToken);
        return StatusCode(201, result);
    }

    /// <summary>Returns all training plans owned by the current athlete (summaries).</summary>
    [HttpGet]
    public async Task<IActionResult> GetByAthleteAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TrainingPlanResponse> result = await trainingPlanService.GetByAthleteAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a single training plan with its planned workouts. 404 if it does not exist or belongs to another athlete.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        TrainingPlanResponse result = await trainingPlanService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Replaces a training plan's metadata (name, methodology, dates, target event, periodization
    /// fields) for the current athlete. Planned workouts are untouched. 404 if the plan is missing or
    /// foreign; 400 if the body is invalid, the new window would strand planned workouts, or the target
    /// event is not the athlete's.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] TrainingPlanUpdateRequest request, CancellationToken cancellationToken)
    {
        TrainingPlanResponse result = await trainingPlanService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Adds a planned workout to a plan owned by the current athlete. 404 if the plan is missing or foreign.</summary>
    [HttpPost("{id:guid}/plannedworkouts")]
    public async Task<IActionResult> AddPlannedWorkoutAsync(Guid id, [FromBody] PlannedWorkoutDto request, CancellationToken cancellationToken)
    {
        PlannedWorkoutResponse result = await trainingPlanService.AddPlannedWorkoutAsync(id, request, cancellationToken);
        return StatusCode(201, result);
    }

    /// <summary>Updates a planned workout within a plan owned by the current athlete. 404 if the plan or planned workout is missing or foreign.</summary>
    [HttpPut("{id:guid}/plannedworkouts/{plannedWorkoutId:guid}")]
    public async Task<IActionResult> UpdatePlannedWorkoutAsync(Guid id, Guid plannedWorkoutId, [FromBody] PlannedWorkoutDto request, CancellationToken cancellationToken)
    {
        PlannedWorkoutResponse result = await trainingPlanService.UpdatePlannedWorkoutAsync(id, plannedWorkoutId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Moves a planned workout to a new scheduled date within the owning plan's window
    /// [StartDate, EndDate]. 400 if the date is outside the window; 404 if the plan or planned workout
    /// is missing or foreign. Returns 204 NoContent (ADR-0008 §2).
    /// </summary>
    [HttpPatch("{id:guid}/plannedworkouts/{plannedWorkoutId:guid}/schedule")]
    public async Task<IActionResult> RescheduleAsync(Guid id, Guid plannedWorkoutId, [FromBody] ScheduleRequest request, CancellationToken cancellationToken)
    {
        await trainingPlanService.RescheduleAsync(id, plannedWorkoutId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Removes a planned workout from a plan owned by the current athlete. 404 if the plan or planned workout is missing or foreign.</summary>
    [HttpDelete("{id:guid}/plannedworkouts/{plannedWorkoutId:guid}")]
    public async Task<IActionResult> RemovePlannedWorkoutAsync(Guid id, Guid plannedWorkoutId, CancellationToken cancellationToken)
    {
        await trainingPlanService.RemovePlannedWorkoutAsync(id, plannedWorkoutId, cancellationToken);
        return NoContent();
    }

    /// <summary>Returns a planned workout's structured payload (blocks + steps) within an owned plan. 404 if the plan or planned workout is missing or foreign.</summary>
    [HttpGet("{id:guid}/plannedworkouts/{plannedWorkoutId:guid}/structure")]
    public async Task<IActionResult> GetStructureAsync(Guid id, Guid plannedWorkoutId, CancellationToken cancellationToken)
    {
        PlannedWorkoutResponse result = await structuredWorkoutService.GetStructureAsync(id, plannedWorkoutId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Replaces a planned workout's structured payload (blocks + steps) within an owned plan. 404 if the plan or planned workout is missing or foreign; 400 if the payload is invalid.</summary>
    [HttpPut("{id:guid}/plannedworkouts/{plannedWorkoutId:guid}/structure")]
    public async Task<IActionResult> SetStructureAsync(Guid id, Guid plannedWorkoutId, [FromBody] WorkoutStructureRequest request, CancellationToken cancellationToken)
    {
        PlannedWorkoutResponse result = await structuredWorkoutService.SetStructureAsync(id, plannedWorkoutId, request, cancellationToken);
        return Ok(result);
    }
}
