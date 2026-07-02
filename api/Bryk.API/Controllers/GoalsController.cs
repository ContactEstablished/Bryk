using Asp.Versioning;
using Bryk.Application.Goals;
using Bryk.Application.Onboarding;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class GoalsController(IGoalService goalService) : ControllerBase
{
    /// <summary>
    /// Returns the current athlete's goals ordered by
    /// <see cref="Bryk.Domain.Entities.Goal.TargetDate"/> ascending (nulls last), each carrying computed
    /// <c>daysRemaining</c> and <c>status</c> (see <see cref="Bryk.Application.Goals.GoalProgress"/>).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<GoalListItemResponse> result = await goalService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a new goal for the current athlete.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] GoalDto request, CancellationToken cancellationToken)
    {
        GoalResponse result = await goalService.CreateAsync(request, cancellationToken);
        return StatusCode(201, result);
    }

    /// <summary>Updates a goal owned by the current athlete. 404 if it does not exist or belongs to another athlete.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] GoalDto request, CancellationToken cancellationToken)
    {
        GoalResponse result = await goalService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Deletes a goal owned by the current athlete. 404 if it does not exist or belongs to another athlete.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await goalService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
