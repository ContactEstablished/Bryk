using Bryk.Domain.Entities;

namespace Bryk.Application.Calendar;

/// <summary>
/// One row in a calendar day cell — a planned workout, a completed workout, or an event.
/// Matched planned+completed pairs render as <b>two</b> chips with the inverse-link ids set so the
/// popover can show planned-vs-actual; they are NOT merged (ADR-0008 §1).
/// </summary>
public class CalendarItemDto
{
    /// <summary>The underlying entity id (<see cref="PlannedWorkout.Id"/>, <see cref="Workout.Id"/>, or <see cref="Event.Id"/>).</summary>
    public Guid Id { get; set; }

    public CalendarItemKind Kind { get; set; }

    /// <summary>Events may carry a sport; planned/completed always do.</summary>
    public Sport? Sport { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Planned: its EffectiveLoad; completed: its EffectiveLoad; event: null.</summary>
    public decimal? Load { get; set; }

    /// <summary>
    /// Planned: its EffectiveLoad; completed: the linked planned's EffectiveLoad when linked, else null;
    /// event: null. Drives the chip's planned-vs-actual rendering.
    /// </summary>
    public decimal? PlannedLoad { get; set; }

    /// <summary>
    /// Planned: the classified bucket; completed: <see cref="ComplianceBucket.Green"/> if unplanned,
    /// else the linked planned's bucket; event: null (events aren't graded).
    /// </summary>
    public ComplianceBucket? Compliance { get; set; }

    /// <summary>True only for a completed <see cref="Workout"/> with null <see cref="Workout.PlannedWorkoutId"/>.</summary>
    public bool IsUnplanned { get; set; }

    /// <summary>Completed linked to planned; else null.</summary>
    public Guid? PlannedWorkoutId { get; set; }

    /// <summary>Planned matched to a completion; else null — the inverse link for the popover.</summary>
    public Guid? WorkoutId { get; set; }

    /// <summary>
    /// The owning <see cref="TrainingPlan"/> id for a planned item; null otherwise.
    /// Carried for the reschedule PATCH route (Impl-16-3/16-4).
    /// </summary>
    public Guid? TrainingPlanId { get; set; }

    /// <summary>Events only (<see cref="Event.Priority"/>); null otherwise — A/B/C styling.</summary>
    public EventPriority? Priority { get; set; }

    /// <summary>Event: <see cref="Event.Notes"/>; else null. Phase 17 renders event notes; surfaced now.</summary>
    public string? Notes { get; set; }
}
