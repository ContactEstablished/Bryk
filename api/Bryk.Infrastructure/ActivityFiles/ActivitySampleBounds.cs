namespace Bryk.Infrastructure.ActivityFiles;

/// <summary>
/// Sample sanity at the parse boundary (ROADMAP's "sample sanity (HR 30–230 etc.)"), so a corrupt device
/// spike never reaches the service. Out-of-range values become null on that sample — the sample itself is
/// retained (its elapsed time still counts toward duration), it simply contributes nothing to the average,
/// the max, or the histogram bucket. Task 19-4 does not own sample sanity; Task 19-3's FIT parser reuses
/// this type read-only rather than redeclaring the constants.
/// </summary>
internal static class ActivitySampleBounds
{
    public const int MinHr = 30;
    public const int MaxHr = 230;
    public const int MaxPowerWatts = 2000;

    public static int? Hr(int? value) => value is { } v && v >= MinHr && v <= MaxHr ? v : null;
    public static int? Power(int? value) => value is { } v && v >= 0 && v <= MaxPowerWatts ? v : null;
}
