namespace Bryk.Application.Events;

// Id + name only — the chip navigates to /plans/{id}; no plan body needed (Tasks-17-1).
public class LinkedPlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
