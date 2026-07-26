using System.Text;
using Bryk.Application.Exceptions;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.ActivityFiles;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.ActivityFiles;

public class TcxActivityParserTests
{
    private static Stream Fixture(string name) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ActivityFiles", name));

    private static Stream Inline(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));

    [Fact]
    public void Format_IsTcx()
    {
        new TcxActivityParser().Format.Should().Be(ActivityFileFormat.Tcx);
    }

    [Fact]
    public async Task ParseAsync_RunFixture_PinsSessionAggregates()
    {
        await using var stream = Fixture("sample-run.tcx");

        var result = await new TcxActivityParser().ParseAsync(stream);

        result.Sport.Should().Be(Sport.Run);
        result.DurationSeconds.Should().Be(600);
        result.DistanceMeters.Should().Be(2000);
        result.AvgHr.Should().Be(144);
        result.MaxHr.Should().Be(160);
        result.AvgPower.Should().BeNull();
        result.AvgPace.Should().Be(300);
        result.Samples.Should().HaveCount(5);
        result.StartTimeUtc.Should().Be(new DateTime(2026, 6, 1, 6, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ParseAsync_RideFixture_DetectsBikeAndAveragesPower()
    {
        await using var stream = Fixture("sample-ride.tcx");

        var result = await new TcxActivityParser().ParseAsync(stream);

        result.Sport.Should().Be(Sport.Bike);
        result.DurationSeconds.Should().Be(3600);
        result.DistanceMeters.Should().Be(30000);
        result.AvgHr.Should().Be(141);
        result.AvgPower.Should().Be(210);
        result.AvgPace.Should().BeNull();
        result.Samples.Should().HaveCount(4);
    }

    [Fact]
    public async Task ParseAsync_OutOfRangeHeartRate_IsDiscardedWithoutDroppingTheSample()
    {
        const string xml = """
            <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
              <Activities>
                <Activity Sport="Running">
                  <Lap StartTime="2026-06-01T06:00:00Z">
                    <Track>
                      <Trackpoint><Time>2026-06-01T06:00:00Z</Time><HeartRateBpm><Value>10</Value></HeartRateBpm></Trackpoint>
                      <Trackpoint><Time>2026-06-01T06:01:00Z</Time><HeartRateBpm><Value>150</Value></HeartRateBpm></Trackpoint>
                      <Trackpoint><Time>2026-06-01T06:02:00Z</Time><HeartRateBpm><Value>300</Value></HeartRateBpm></Trackpoint>
                    </Track>
                  </Lap>
                </Activity>
              </Activities>
            </TrainingCenterDatabase>
            """;
        await using var stream = Inline(xml);

        var result = await new TcxActivityParser().ParseAsync(stream);

        result.AvgHr.Should().Be(150);
        result.MaxHr.Should().Be(150);
        result.Samples.Should().HaveCount(3);
        result.Samples[0].Hr.Should().BeNull();
        result.Samples[^1].Hr.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_PowerAboveTwoThousandWatts_IsDiscarded()
    {
        const string xml = """
            <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
              <Activities>
                <Activity Sport="Biking">
                  <Lap StartTime="2026-06-01T06:00:00Z">
                    <Track>
                      <Trackpoint>
                        <Time>2026-06-01T06:00:00Z</Time>
                        <Extensions><TPX xmlns="http://www.garmin.com/xmlschemas/ActivityExtension/v2"><Watts>5000</Watts></TPX></Extensions>
                      </Trackpoint>
                      <Trackpoint>
                        <Time>2026-06-01T06:01:00Z</Time>
                        <Extensions><TPX xmlns="http://www.garmin.com/xmlschemas/ActivityExtension/v2"><Watts>200</Watts></TPX></Extensions>
                      </Trackpoint>
                    </Track>
                  </Lap>
                </Activity>
              </Activities>
            </TrainingCenterDatabase>
            """;
        await using var stream = Inline(xml);

        var result = await new TcxActivityParser().ParseAsync(stream);

        result.AvgPower.Should().Be(200);
    }

    [Fact]
    public async Task ParseAsync_MalformedXml_ThrowsValidationExceptionWithFilePrefix()
    {
        await using var stream = Inline("<not xml");

        var act = () => new TcxActivityParser().ParseAsync(stream);

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e => e.StartsWith("File:"));
    }

    [Fact]
    public async Task ParseAsync_WrongRootElement_ThrowsValidationException()
    {
        const string gpx = """
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg><trkpt lat="0" lon="0"><time>2026-01-01T00:00:00Z</time></trkpt></trkseg></trk>
            </gpx>
            """;
        await using var stream = Inline(gpx);

        var act = () => new TcxActivityParser().ParseAsync(stream);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ParseAsync_NoTrackpoints_ThrowsValidationException()
    {
        const string xml = """
            <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
              <Activities>
                <Activity Sport="Running">
                  <Lap StartTime="2026-06-01T06:00:00Z">
                    <Track></Track>
                  </Lap>
                </Activity>
              </Activities>
            </TrainingCenterDatabase>
            """;
        await using var stream = Inline(xml);

        var act = () => new TcxActivityParser().ParseAsync(stream);

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e => e.Contains("no track data"));
    }

    [Fact]
    public async Task ParseAsync_UnknownSportAttributeWithPowerSamples_FallsBackToBike()
    {
        const string xml = """
            <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
              <Activities>
                <Activity Sport="Other">
                  <Lap StartTime="2026-06-01T06:00:00Z">
                    <Track>
                      <Trackpoint>
                        <Time>2026-06-01T06:00:00Z</Time>
                        <Extensions><TPX xmlns="http://www.garmin.com/xmlschemas/ActivityExtension/v2"><Watts>150</Watts></TPX></Extensions>
                      </Trackpoint>
                    </Track>
                  </Lap>
                </Activity>
              </Activities>
            </TrainingCenterDatabase>
            """;
        await using var stream = Inline(xml);

        var result = await new TcxActivityParser().ParseAsync(stream);

        result.Sport.Should().Be(Sport.Bike);
    }

    [Fact]
    public async Task ParseAsync_UnknownSportAttributeWithoutPower_FallsBackToRun()
    {
        const string xml = """
            <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
              <Activities>
                <Activity Sport="Other">
                  <Lap StartTime="2026-06-01T06:00:00Z">
                    <Track>
                      <Trackpoint><Time>2026-06-01T06:00:00Z</Time></Trackpoint>
                    </Track>
                  </Lap>
                </Activity>
              </Activities>
            </TrainingCenterDatabase>
            """;
        await using var stream = Inline(xml);

        var result = await new TcxActivityParser().ParseAsync(stream);

        result.Sport.Should().Be(Sport.Run);
    }
}
