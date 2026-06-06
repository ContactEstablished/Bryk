using Asp.Versioning;
using Bryk.Application.Training;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class TrainingController(IThisWeekService thisWeekService) : ControllerBase
{
    /// <summary>
    /// Returns the current athlete's planned workouts for the current Mon–Sun (UTC) week, flattened
    /// across all plans and ordered by date, with the week range. Always 200; the list is empty when
    /// nothing is planned this week (never 404).
    /// </summary>
    [HttpGet("this-week")]
    public async Task<IActionResult> GetThisWeekAsync(CancellationToken cancellationToken)
    {
        ThisWeekResponse result = await thisWeekService.GetThisWeekAsync(cancellationToken);
        return Ok(result);
    }
}
