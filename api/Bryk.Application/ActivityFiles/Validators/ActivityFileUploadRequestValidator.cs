using FluentValidation;

namespace Bryk.Application.ActivityFiles.Validators;

/// <summary>
/// Upload-body rules (ADR-0010 §2). Deliberately excludes two things: the magic-byte sniff needs the
/// resolved format and lives in <see cref="ActivityFileService"/>, and sample sanity (HR 30–230, power
/// ≤ 2000 W) belongs to the parse boundary in <c>Bryk.Infrastructure</c>, not here.
/// </summary>
public class ActivityFileUploadRequestValidator : AbstractValidator<ActivityFileUploadRequest>
{
    private static readonly string[] SupportedExtensions = [".fit", ".tcx", ".gpx"];

    public ActivityFileUploadRequestValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(260);

        RuleFor(x => x.FileName)
            .Must(HasSupportedExtension)
            .WithMessage("FileName: Only .fit, .tcx and .gpx files are supported.");

        RuleFor(x => x.Content)
            .Must(c => c.Length > 0)
            .WithMessage("Content: The uploaded file is empty.");

        RuleFor(x => x.Content)
            .Must(c => c.Length <= ActivityFileLimits.MaxBytes)
            .WithMessage($"Content: The file exceeds the {ActivityFileLimits.MaxBytes / (1024 * 1024)} MB limit.");
    }

    private static bool HasSupportedExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        return SupportedExtensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase));
    }
}
