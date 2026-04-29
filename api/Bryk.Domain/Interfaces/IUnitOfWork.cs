namespace Bryk.Domain.Interfaces;

/// <summary>
/// Single persistence boundary for a unit of work spanning one or more repositories.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Commits all staged changes across repositories sharing the same <see cref="DbContext"/> scope
    /// and returns the number of state entries written to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
