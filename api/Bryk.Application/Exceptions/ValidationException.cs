namespace Bryk.Application.Exceptions;

public class ValidationException(IEnumerable<string> errors) : Exception("One or more validation errors occurred.")
{
    public IReadOnlyList<string> Errors { get; } = errors.ToList();
}
