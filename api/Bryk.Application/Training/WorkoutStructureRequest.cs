namespace Bryk.Application.Training;

// The full structured payload for one planned workout — set/replace semantics (ADR-0004 §4).
public class WorkoutStructureRequest
{
    public List<WorkoutBlockDto> Blocks { get; set; } = new();
}
