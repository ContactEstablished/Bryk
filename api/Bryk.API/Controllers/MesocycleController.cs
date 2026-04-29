using Asp.Versioning;
using Bryk.Application.DTOs.Mesocycle;
using Bryk.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class MesocycleController(IMesocycleService mesocycleService) : ControllerBase
{
    /// <summary>Returns all mesocycles.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        List<MesocycleDto> result = await mesocycleService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a single mesocycle by ID.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        MesocycleDto result = await mesocycleService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a mesocycle with its full week breakdown.</summary>
    [HttpGet("{id}/with-weeks")]
    public async Task<IActionResult> GetWithWeeksAsync(Guid id, CancellationToken cancellationToken)
    {
        MesocycleWithWeeksDto result = await mesocycleService.GetWithWeeksAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a new mesocycle.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateMesocycleDto dto, CancellationToken cancellationToken)
    {
        MesocycleDto result = await mesocycleService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Updates an existing mesocycle.</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateMesocycleDto dto, CancellationToken cancellationToken)
    {
        MesocycleDto result = await mesocycleService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>Deletes a mesocycle by ID.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await mesocycleService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
