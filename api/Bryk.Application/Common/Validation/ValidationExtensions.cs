using FluentValidation;

namespace Bryk.Application.Common.Validation;

/// <summary>
/// Extension methods that collapse the locked three-line FluentValidation
/// invocation pattern (ValidateAsync → check IsValid → throw) into a single
/// call site. Preserves the existing <see cref="Bryk.Application.Exceptions.ValidationException"/>
/// type and the JSON response shape produced by the global exception middleware
/// — does NOT use FluentValidation's built-in <c>ValidateAndThrowAsync</c>,
/// which throws <c>FluentValidation.ValidationException</c> and is not handled
/// by the middleware. See ROADMAP / CLAUDE.md tech-debt item 5.
/// </summary>
public static class ValidationExtensions
{
    public static async Task ValidateOrThrowAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (!result.IsValid)
        {
            throw new Bryk.Application.Exceptions.ValidationException(
                result.Errors.Select(e => e.ErrorMessage));
        }
    }
}
