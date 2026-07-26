using Bryk.Application.ActivityFiles;
using Bryk.Application.ActivityFiles.Validators;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.ActivityFiles;

// Validator-only, no host: this is where the 25 MB size boundary is pinned, because a 25 MB multipart
// POST is not worth the integration-test runtime.
public class ActivityFileUploadRequestValidatorTests
{
    private static readonly ActivityFileUploadRequestValidator Validator = new();

    private static ActivityFileUploadRequest Request(string fileName, byte[]? content = null) => new()
    {
        FileName = fileName,
        Content = content ?? new byte[] { 1, 2, 3, 4 }
    };

    [Fact]
    public void Rejects_UnsupportedExtension()
    {
        var result = Validator.Validate(Request("ride.csv"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("FileName:"));
    }

    [Fact]
    public void Accepts_UpperCaseExtension()
    {
        var result = Validator.Validate(Request("RIDE.TCX"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_EmptyContent()
    {
        var result = Validator.Validate(Request("ride.tcx", Array.Empty<byte>()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("Content:"));
    }

    [Fact]
    public void Rejects_ContentOneByteOverTheCap()
    {
        var result = Validator.Validate(Request("ride.tcx", new byte[ActivityFileLimits.MaxBytes + 1]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("25 MB limit"));
    }

    [Fact]
    public void Accepts_ContentExactlyAtTheCap()
    {
        // Inclusive bound: exactly MaxBytes is allowed, MaxBytes + 1 is not.
        var result = Validator.Validate(Request("ride.tcx", new byte[ActivityFileLimits.MaxBytes]));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_FileNameOver260Characters()
    {
        var result = Validator.Validate(Request(new string('a', 260) + ".tcx"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ActivityFileUploadRequest.FileName));
    }
}
