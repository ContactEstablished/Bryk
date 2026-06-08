namespace Bryk.Application.Training;

// Write-side block (ADR-0004 §2): an ordered, optionally-repeated group of steps. Repeats expresses
// interval sets (4× [work / recovery] = one block, Repeats = 4, two steps).
public class WorkoutBlockDto
{
    public int OrderIndex { get; set; }
    public int Repeats { get; set; }
    public List<WorkoutStepDto> Steps { get; set; } = new();
}
