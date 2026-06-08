namespace Bryk.Application.Training;

// Read-side, Id-bearing block with its ordered steps.
public class WorkoutBlockResponse
{
    public Guid Id { get; set; }
    public int OrderIndex { get; set; }
    public int Repeats { get; set; }
    public List<WorkoutStepResponse> Steps { get; set; } = new();
}
