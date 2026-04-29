using Asp.Versioning;
using Bryk.Application.DTOs.Day;
using Bryk.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class DayController(IDayService dayService) : ControllerBase
{
    /// <summary>Returns a single day by ID.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        DayDto result = await dayService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a day with its full exercise breakdown.</summary>
    [HttpGet("{id}/with-exercises")]
    public async Task<IActionResult> GetWithExercisesAsync(Guid id, CancellationToken cancellationToken)
    {
        DayWithExercisesDto result = await dayService.GetWithExercisesAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Updates an existing day.</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateDayDto dto, CancellationToken cancellationToken)
    {
        DayDto result = await dayService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>Adds an exercise to a day.</summary>
    [HttpPost("{id}/exercises")]
    public async Task<IActionResult> AddExerciseAsync(Guid id, [FromBody] AddExerciseToDayDto dto, CancellationToken cancellationToken)
    {
        DayExerciseDto result = await dayService.AddExerciseAsync(id, dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, result);
    }

    /// <summary>Updates an existing exercise on a day.</summary>
    [HttpPut("{id}/exercises/{exerciseId}")]
    public async Task<IActionResult> UpdateExerciseAsync(Guid id, Guid exerciseId, [FromBody] UpdateDayExerciseDto dto, CancellationToken cancellationToken)
    {
        DayExerciseDto result = await dayService.UpdateExerciseAsync(exerciseId, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>Removes an exercise from a day.</summary>
    [HttpDelete("{id}/exercises/{exerciseId}")]
    public async Task<IActionResult> RemoveExerciseAsync(Guid id, Guid exerciseId, CancellationToken cancellationToken)
    {
        await dayService.RemoveExerciseAsync(exerciseId, cancellationToken);
        return NoContent();
    }

    /// <summary>Reorders exercises within a day.</summary>
    [HttpPut("{id}/exercises/reorder")]
    public async Task<IActionResult> ReorderExercisesAsync(Guid id, [FromBody] List<Guid> orderedExerciseIds, CancellationToken cancellationToken)
    {
        await dayService.ReorderExercisesAsync(id, orderedExerciseIds, cancellationToken);
        return NoContent();
    }
}
