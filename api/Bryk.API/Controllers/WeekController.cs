using Asp.Versioning;
using Bryk.Application.DTOs.Week;
using Bryk.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class WeekController(IWeekService weekService) : ControllerBase
{
    /// <summary>Returns a single week by ID.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        WeekDto result = await weekService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a week with its full day breakdown.</summary>
    [HttpGet("{id}/with-days")]
    public async Task<IActionResult> GetWithDaysAsync(Guid id, CancellationToken cancellationToken)
    {
        WeekWithDaysDto result = await weekService.GetWithDaysAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Updates an existing week.</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateWeekDto dto, CancellationToken cancellationToken)
    {
        WeekDto result = await weekService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }
}
