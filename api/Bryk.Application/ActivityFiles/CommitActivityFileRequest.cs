namespace Bryk.Application.ActivityFiles;

/// <summary>
/// The commit body. <see cref="PlannedWorkoutId"/> is the optional match the athlete picked from the
/// upload preview's candidates; ownership of that planned workout is a repository read and therefore
/// checked in the service, not the validator.
/// </summary>
public class CommitActivityFileRequest
{
    public Guid? PlannedWorkoutId { get; set; }
}
