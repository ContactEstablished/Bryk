using System.Text;
using Bryk.Application.Exceptions;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.ActivityFiles;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.ActivityFiles;

// PENDING FIXTURE: the six fixture-pinned facts from Tasks-19-3.md (bike session + samples, avg power
// derived from samples, HR samples in range, monotonic elapsed seconds, null pace for bike, and the
// ZoneHistogramCalculator integration) require a real device-written sample-ride.fit at
// Fixtures/ActivityFiles/. No such file was available when this task ran and Tasks-19-3.md explicitly
// forbids hand-crafting one or generating one with the SDK's encoder, so those facts are deferred rather
// than faked. The three facts below need no fixture and cover the failure contract that matters most:
// a corrupt upload must be a clean 400, never a 500.
public class FitActivityParserTests
{
    private static Stream Fixture(string name) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ActivityFiles", name));

    [Fact]
    public void Format_IsFit()
    {
        new FitActivityParser().Format.Should().Be(ActivityFileFormat.Fit);
    }

    [Fact]
    public async Task ParseAsync_GarbageBytes_ThrowsValidationExceptionWithFilePrefix()
    {
        await using var stream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02, 0x03 });

        var act = () => new FitActivityParser().ParseAsync(stream);

        // ThrowExactly, not Throw: a raw SDK exception escaping here is the difference between the
        // middleware returning a clean 400 and returning a 500.
        var thrown = await act.Should().ThrowExactlyAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e => e.StartsWith("File:"));
    }

    [Fact]
    public async Task ParseAsync_TcxFixtureBytes_ThrowsValidationException()
    {
        // The guard that 19-4's magic-byte sniffing backs up: XML fed to the FIT decoder must not escape
        // as a raw SDK exception either.
        await using var stream = Fixture("sample-run.tcx");

        var act = () => new FitActivityParser().ParseAsync(stream);

        var thrown = await act.Should().ThrowExactlyAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e => e.StartsWith("File:"));
    }
}
