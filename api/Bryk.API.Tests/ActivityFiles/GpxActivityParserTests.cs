using System.Text;
using Bryk.Application.Exceptions;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.ActivityFiles;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.ActivityFiles;

public class GpxActivityParserTests
{
    private static Stream Fixture(string name) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ActivityFiles", name));

    private static Stream Inline(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));

    [Fact]
    public void Format_IsGpx()
    {
        new GpxActivityParser().Format.Should().Be(ActivityFileFormat.Gpx);
    }

    [Fact]
    public async Task ParseAsync_Fixture_DerivesDistanceFromHaversine()
    {
        // Great-circle arithmetic, not exact: two ~0.008993°-latitude steps at R = 6 371 000 m each
        // compute to ≈ 999.98 m, not precisely 1000 m — the only tolerance range in this suite.
        await using var stream = Fixture("sample-activity.gpx");

        var result = await new GpxActivityParser().ParseAsync(stream);

        result.DistanceMeters.Should().BeInRange(1995, 2005);
    }

    [Fact]
    public async Task ParseAsync_Fixture_ReadsTheHeartRateExtension()
    {
        await using var stream = Fixture("sample-activity.gpx");

        var result = await new GpxActivityParser().ParseAsync(stream);

        result.AvgHr.Should().Be(140);
        result.MaxHr.Should().Be(150);
    }

    [Fact]
    public async Task ParseAsync_Fixture_ResolvesRunFromTrackType()
    {
        await using var stream = Fixture("sample-activity.gpx");

        var result = await new GpxActivityParser().ParseAsync(stream);

        result.Sport.Should().Be(Sport.Run);
        result.DurationSeconds.Should().Be(600);
        result.AvgPace.Should().BeInRange(298, 302);
        result.AvgPower.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_MissingTrackType_FallsBackToRun()
    {
        const string xml = """
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <trkseg>
                  <trkpt lat="40.000000" lon="-105.000000"><time>2026-01-01T00:00:00Z</time></trkpt>
                  <trkpt lat="40.001000" lon="-105.000000"><time>2026-01-01T00:01:00Z</time></trkpt>
                </trkseg>
              </trk>
            </gpx>
            """;
        await using var stream = Inline(xml);

        var result = await new GpxActivityParser().ParseAsync(stream);

        result.Sport.Should().Be(Sport.Run);
    }

    [Fact]
    public async Task ParseAsync_MalformedXml_ThrowsValidationExceptionWithFilePrefix()
    {
        await using var stream = Inline("<not xml");

        var act = () => new GpxActivityParser().ParseAsync(stream);

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e => e.StartsWith("File:"));
    }

    [Fact]
    public async Task ParseAsync_NoTrackPoints_ThrowsValidationException()
    {
        const string xml = """
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg></trkseg></trk>
            </gpx>
            """;
        await using var stream = Inline(xml);

        var act = () => new GpxActivityParser().ParseAsync(stream);

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e => e.Contains("no track data"));
    }
}
