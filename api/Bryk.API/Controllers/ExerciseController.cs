using Asp.Versioning;
using Bryk.Application.DTOs.Exercise;
using Bryk.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ExerciseController(IExerciseService exerciseService) : ControllerBase
{
    /// <summary>Returns all exercises with optional filtering and sorting.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] string? sportType,
        [FromQuery] string? searchTerm,
        [FromQuery] string? sortBy,
        CancellationToken cancellationToken)
    {
        ExerciseListDto result = await exerciseService.GetAllAsync(sportType, searchTerm, sortBy, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a single exercise by ID.</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        ExerciseDto result = await exerciseService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a new exercise.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateExerciseDto dto, CancellationToken cancellationToken)
    {
        ExerciseDto result = await exerciseService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Updates an existing exercise.</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateExerciseDto dto, CancellationToken cancellationToken)
    {
        ExerciseDto result = await exerciseService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>Deletes an exercise by ID.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await exerciseService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Duplicates an existing exercise.</summary>
    [HttpPost("{id}/duplicate")]
    public async Task<IActionResult> DuplicateAsync(Guid id, CancellationToken cancellationToken)
    {
        ExerciseDto result = await exerciseService.DuplicateAsync(id, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
