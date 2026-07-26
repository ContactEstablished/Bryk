using Bryk.Application.ActivityFiles;
using Bryk.Application.Exceptions;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.ActivityFiles;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.ActivityFiles;

// Fixture provenance (sample-ride.fit): a real device-written cycling file supplied 2026-07-26 as the
// stand-in until third-party API connectivity exists. Bike, starts 2026-02-17T22:05:18Z, 6175 s of timer
// time over 26 481 m, 6198 record messages carrying heart rate throughout and power on all but the first
// 24. An easy ride — average power is 40 W — which is why the pinned constants look low; they are the
// file's real figures, read once and promoted here rather than guessed.
//
// NOTE the gap between timer time and elapsed time: the last record sits at 7456 elapsed seconds while
// the session reports 6175 s of timer time, because FIT's total timer time excludes paused time. That is
// why the histogram test below bounds the buckets by the sample series' elapsed span rather than by
// DurationSeconds — see the comment there.
public class FitActivityParserTests
{
    // Read once from the fixture and pinned, so the suite guards real numbers rather than inequalities.
    private const int ExpectedAvgPower = 40;
    private const int ExpectedDurationSeconds = 6175;
    private const int ExpectedDistanceMeters = 26481;
    private const int ExpectedAvgHr = 82;
    private const int ExpectedMaxHr = 125;
    private const int ExpectedSampleCount = 6198;

    private static Stream Fixture(string name) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ActivityFiles", name));

    private static async Task<ParsedActivity> ParseRideAsync()
    {
        await using var stream = Fixture("sample-ride.fit");
        return await new FitActivityParser().ParseAsync(stream);
    }

    [Fact]
    public void Format_IsFit()
    {
        new FitActivityParser().Format.Should().Be(ActivityFileFormat.Fit);
    }

    [Fact]
    public async Task ParseAsync_RideFixture_ReturnsBikeSessionWithSamples()
    {
        var result = await ParseRideAsync();

        result.Sport.Should().Be(Sport.Bike);
        result.Samples.Should().NotBeEmpty();
        result.Samples.Should().HaveCount(ExpectedSampleCount);
        result.DurationSeconds.Should().BePositive();
        result.DurationSeconds.Should().Be(ExpectedDurationSeconds);
        result.DistanceMeters.Should().BePositive();
        result.DistanceMeters.Should().Be(ExpectedDistanceMeters);
        result.StartTimeUtc.Kind.Should().Be(DateTimeKind.Utc);
        result.AvgHr.Should().Be(ExpectedAvgHr);
        result.MaxHr.Should().Be(ExpectedMaxHr);
    }

    [Fact]
    public async Task ParseAsync_RideFixture_DerivesAveragePowerFromSamples()
    {
        var result = await ParseRideAsync();

        result.AvgPower.Should().BePositive();
        result.AvgPower.Should().Be(ExpectedAvgPower);
    }

    [Fact]
    public async Task ParseAsync_RideFixture_KeepsEveryHeartRateSampleInRange()
    {
        var result = await ParseRideAsync();

        // The ActivitySampleBounds contract, proven on real data: anything outside 30..230 was nulled at
        // the parse boundary, so every surviving value is in range.
        result.Samples.Where(s => s.Hr is not null)
            .Should().NotBeEmpty()
            .And.OnlyContain(s => s.Hr!.Value >= 30 && s.Hr.Value <= 230);
    }

    [Fact]
    public async Task ParseAsync_RideFixture_ElapsedSecondsAreMonotonicAndStartAtZero()
    {
        var result = await ParseRideAsync();

        result.Samples[0].ElapsedSeconds.Should().Be(0);
        result.Samples.Should().BeInAscendingOrder(s => s.ElapsedSeconds);
    }

    [Fact]
    public async Task ParseAsync_BikeSport_HasNullAvgPace()
    {
        var result = await ParseRideAsync();

        // Pace is Run/Swim only (ParsedActivity rule 4).
        result.AvgPace.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_RideFixture_HistogramIsComputableFromTheResult()
    {
        var result = await ParseRideAsync();

        var bands = new SportZonesResponse
        {
            Sport = Sport.Bike,
            Metric = ZoneMetric.Power,
            Zones = new List<ZoneDto>
            {
                new() { ZoneNumber = 1, LowerBound = 0m, UpperBound = 150m },
                new() { ZoneNumber = 2, LowerBound = 150m, UpperBound = 200m },
                new() { ZoneNumber = 3, LowerBound = 200m, UpperBound = 250m },
                new() { ZoneNumber = 4, LowerBound = 250m, UpperBound = 300m },
                new() { ZoneNumber = 5, LowerBound = 300m, UpperBound = null },
            }
        };

        var histogram = ZoneHistogramCalculator.Compute(result, bands, maxHr: null);

        histogram.Should().HaveCount(5);
        histogram.Sum(h => h.Seconds).Should().BePositive();

        // Bounded by the sample series' ELAPSED span, not by DurationSeconds. The histogram accumulates
        // per-sample gaps across wall-clock time, while DurationSeconds is the session's timer time,
        // which excludes paused time — on this real file the buckets legitimately sum to more than
        // DurationSeconds. Elapsed span is the true ceiling: every sample contributes at most its gap.
        histogram.Sum(h => h.Seconds).Should().BeLessThanOrEqualTo(result.Samples[^1].ElapsedSeconds);
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
