# Impl 19-3 — Build order: `FitActivityParser` + the approved `Garmin.FIT.Sdk` package reference

**Executor:** architect-implementer (per CLAUDE.md).
**Acceptance contract:** `md/Tasks-19-3.md`.
**Decision lock:** ADR-0010 §1 (`md/decisions/0010-activity-file-import.md`, written by Task 19-1 — the
SDK decision, version `21.205.0`, and license; **already approved by the Sr. Dev on 2026-07-26, do not
re-litigate it**) + Task 19-2's cross-format resolution rules (`md/Tasks-19-2.md` §*Cross-format
resolution rules* and its `ActivitySampleBounds.cs` — reused read-only, never redeclared).
**Scope:** Backend only. New parser (`api/Bryk.Infrastructure/ActivityFiles/FitActivityParser.cs`),
exactly one `PackageReference` in `api/Bryk.Infrastructure/Bryk.Infrastructure.csproj`, one committed
`.fit` fixture, one new xUnit test file. No migration, no `Bryk.Application`/`Bryk.Domain`/`Bryk.API`
change, no `Program.cs` line, no UI, no DI registration (Task 19-4 registers all three parsers together —
this parser stays unresolvable from the container until then, which is expected, not dead code).

This is the step-by-step build order. Execute top-to-bottom; each step's verification is the gate to the
next. Commit once at the end with the message in `Tasks-19-3.md`.

## Step 0 — Pre-flight

- `git status` clean on `main`.
- Confirm Task 19-2 has actually landed before starting — this task cannot compile without it. Check
  that every one of these files exists (do not proceed if any is missing; that is a sequencing violation,
  not something to work around by re-declaring the contract here):
  - `api/Bryk.Domain/Entities/Enums/ActivityFileFormat.cs` (19-1)
  - `api/Bryk.Application/ActivityFiles/IActivityFileParser.cs` (19-2)
  - `api/Bryk.Application/ActivityFiles/ParsedActivity.cs` (19-2)
  - `api/Bryk.Application/ActivityFiles/ZoneHistogramEntry.cs` (19-2)
  - `api/Bryk.Application/ActivityFiles/ZoneHistogramCalculator.cs` (19-2)
  - `api/Bryk.Infrastructure/ActivityFiles/ActivitySampleBounds.cs` (19-2)
  - `api/Bryk.Infrastructure/ActivityFiles/TcxActivityParser.cs` (19-2)
  - `api/Bryk.Infrastructure/ActivityFiles/GpxActivityParser.cs` (19-2)
  - `api/Bryk.API.Tests/Fixtures/ActivityFiles/sample-run.tcx` and the csproj's
    `<None Update="Fixtures\ActivityFiles\**">` glob (19-2) — needed by this task's
    `ParseAsync_TcxFixtureBytes_ThrowsValidationException` test and by the "no second glob" constraint.
- `dotnet build api/Bryk.sln` green. Confirm warnings are still **16** (the Phase-18 baseline — the 9×
  design-time `NU1903` `System.Security.Cryptography.Xml` warnings plus the two pre-existing
  `WorkoutsControllerTests.cs:121,150` nullable warnings; 19-1 and 19-2 add no new warnings per their own
  docs). If the count differs, stop and reconcile before touching anything in this task.
- `dotnet test api/Bryk.sln` once. Record the current total as **N** — this is the 262-test Phase-18
  baseline plus whatever 19-1 (5 `ActivityFileRepositoryTests` facts) and 19-2 (8
  `ZoneHistogramCalculatorTests` + 10 `TcxActivityParserTests` + 7 `GpxActivityParserTests` facts) actually
  added once executed (**292** if both landed exactly as their task docs specify — confirm the live number
  is authoritative, not this arithmetic, per `Tasks-19-3.md`'s own "plus what 19-1 and 19-2 added"
  wording). Zero failures.
- `cd ui; pnpm run build` green; `pnpm exec vitest run --no-file-parallelism` at **252 / 56 files** — this
  task touches no frontend file; these numbers must be unchanged at the end.
- Re-read `md/Tasks-19-3.md` in full. Open in editor:
  `api/Bryk.Infrastructure/ActivityFiles/TcxActivityParser.cs` (the class shape to mirror: `Format`
  property, `ParseAsync(Stream, CancellationToken)`, the `ValidationException` failure path, the
  `ActivitySampleBounds` calls — **read only**),
  `api/Bryk.Application/ActivityFiles/IActivityFileParser.cs` + `ParsedActivity.cs` (the contract —
  **read only**),
  `api/Bryk.Infrastructure/ActivityFiles/ActivitySampleBounds.cs` (**read only** — reuse the constants,
  never redeclare them),
  `api/Bryk.Application/ActivityFiles/ZoneHistogramCalculator.cs` (the `Compute(ParsedActivity,
  SportZonesResponse?, int?)` signature used by the last test),
  `api/Bryk.Application/Zones/SportZonesResponse.cs` + `ZoneDto.cs` (already in the codebase —
  `{ Sport, Metric, Zones: IReadOnlyList<ZoneDto> }` / `{ ZoneNumber, LowerBound, UpperBound?,
  IsOverride }` — the shape the last test's inline power bands use),
  `api/Bryk.Application/Exceptions/ValidationException.cs` (`ValidationException(IEnumerable<string>
  errors)`, `Errors` property — confirmed: `public class ValidationException(IEnumerable<string> errors)
  : Exception(...)`),
  `api/Bryk.Infrastructure/Bryk.Infrastructure.csproj` (the `PackageReference` `ItemGroup` the new line
  joins — currently EF Core 10 ×3, `Microsoft.Extensions.Configuration.Json` 10.0.0,
  `Microsoft.Extensions.Hosting.Abstractions` 10.0.0),
  `api/Bryk.API.Tests/Bryk.API.Tests.csproj` (confirm the `<None Update="Fixtures\ActivityFiles\**">`
  glob from 19-2 is present — **do not add a second entry**).
- Confirm `api/Bryk.Infrastructure/ActivityFiles/FitActivityParser.cs` and
  `api/Bryk.API.Tests/ActivityFiles/FitActivityParserTests.cs` do not yet exist (fresh files — this task
  only adds files plus the one csproj line, it modifies nothing else).

## Step 1 — Add the package reference

**File:** `api/Bryk.Infrastructure/Bryk.Infrastructure.csproj` — add one line to the existing
`PackageReference` `ItemGroup`:

```xml
<PackageReference Include="Garmin.FIT.Sdk" Version="21.205.0" />
```

**Approval record — already settled, do not re-litigate.** Approved by the Sr. Dev on **2026-07-26**
(ADR-0010 §1). Publisher-verified Garmin International; ships `net46 / netcoreapp2.0 / netstandard2.0`
(`netstandard2.0` is `net10.0`-compatible); license is Garmin's proprietary royalty-free **FIT Protocol
License Agreement** (`LICENSE.txt` in the package), **not** OSI — expected and accepted. This goes in the
commit body (Step 6) so the next reviewer doesn't reopen it.

Then:
```
dotnet restore api/Bryk.sln
dotnet build api/Bryk.sln
```

**STOP condition — read before proceeding.** Inspect the restore/build output for:
- The resolved version is exactly `21.205.0` (not a floated or substituted version).
- No unexpected transitive package was pulled in beyond what NuGet reports for `Garmin.FIT.Sdk` itself.
- The warning count is still **16**. If any new `NU1901`–`NU1904`-class audit warning appears, or the
  warning count grows for any other reason — **STOP and ask** before writing a single line of parser code.
  Do not suppress the warning and do not add a countervailing direct reference to "fix" it.

Confirm with `git diff --stat` that only `Bryk.Infrastructure.csproj` changed, exactly one added line.

**Verify:** `dotnet build api/Bryk.sln` — 0 errors, warnings still **16**. `git grep "Garmin.FIT.Sdk"`
returns exactly one hit in a `.csproj` (this file) plus whatever documentation lines already reference it
(`Tasks-19-3.md`, ADR-0010) — no hit in `Bryk.Domain`, `Bryk.Application`, `Bryk.API`, or either test
project's `.csproj`.

## Step 2 — `FitActivityParser.cs`

**New file:** `api/Bryk.Infrastructure/ActivityFiles/FitActivityParser.cs`. Namespace
`Bryk.Infrastructure.ActivityFiles`.

**Name-verified against the shipped assembly (coordinator, 2026-07-26 — do NOT re-derive these).**
Confirmed present in `lib/netstandard2.0/FitSDK.dll`, namespace `Dynastream.Fit`:
`Decode`, `MesgBroadcaster`, `MesgEventArgs`, `SessionMesg`, `RecordMesg`, `LapMesg`, `ActivityMesg`,
`FitException`; accessors `GetTimestamp`, `GetHeartRate`, `GetPower`, `GetSpeed`, `GetDistance`,
`GetSport`, `GetTotalTimerTime`, `GetTotalDistance`, `GetAvgHeartRate`, `GetMaxHeartRate`,
`GetAvgPower`, `GetDateTime`; and the FIT `Sport` enum members `Cycling`, `Running`, `Swimming`.
**The `DomainSport` alias below is genuinely required** — `Dynastream.Fit` really does declare its own
`Sport`, which collides with `Bryk.Domain.Entities.Sport`.

**Still unverified — these are what the `// SDK: confirm` markers are for:** the exact **event names**
on `MesgBroadcaster`, the wiring between `Decode` and `MesgBroadcaster`, and each accessor's **return
type / overload signature** (every one returns a nullable — `byte?`, `ushort?`, `float?` … — so
null-guard every read). Confirm those against IntelliSense once the package restores (Step 1). Because
the member **names** above are checked, a compile error on one of them means a wrong `using` or a wrong
receiver type, **not** a wrong name — do not start renaming. If a signature diverges, follow the package
and note it in the commit body (Step 6).

```csharp
using Bryk.Application.ActivityFiles;
using Bryk.Application.Exceptions;
using Bryk.Domain.Entities;
using Dynastream.Fit;
using DomainSport = Bryk.Domain.Entities.Sport; // Dynastream.Fit also declares a `Sport` enum — alias to
                                                 // avoid ambiguity between the two types of the same name.

namespace Bryk.Infrastructure.ActivityFiles;

/// <summary>
/// FIT parser behind <see cref="IActivityFileParser"/> (ADR-0010 §1, Task 19-2's contract). The only
/// place <c>Dynastream.Fit</c> (Garmin.FIT.Sdk 21.205.0) is referenced in the solution.
/// LIMITATION (documented, not implemented): a multisport/triathlon FIT file decodes to whichever single
/// <see cref="SessionMesg"/> the device wrote (or none at all) — this parser does not split a multisport
/// file into per-leg sessions. That is a future item.
/// </summary>
public class FitActivityParser : IActivityFileParser
{
    public ActivityFileFormat Format => ActivityFileFormat.Fit;

    /// <summary>
    /// See <see cref="IActivityFileParser.ParseAsync"/>. Pure function of <paramref name="content"/>: no
    /// file I/O, no clock read, no configuration. <c>Decode.Read</c> is synchronous — there is no true
    /// async work here, so the result is wrapped in <see cref="Task.FromResult{TResult}"/>.
    /// </summary>
    public Task<ParsedActivity> ParseAsync(Stream content, CancellationToken ct = default)
    {
        var records = new List<RecordMesg>();
        SessionMesg? session = null;

        var decode = new Decode();
        var broadcaster = new MesgBroadcaster();

        // SDK: confirm — the standard Decode -> MesgBroadcaster wiring; event names/signatures per
        // IntelliSense once restored.
        decode.MesgEvent += broadcaster.OnMesg;
        decode.MesgDefinitionEvent += broadcaster.OnMesgDefinition;
        broadcaster.RecordMesgEvent += (_, e) => records.Add((RecordMesg)e.mesg);
        broadcaster.SessionMesgEvent += (_, e) => session = (SessionMesg)e.mesg;

        try
        {
            // Read the stream once via the broadcaster's subscriptions above — no second buffer.
            if (!decode.Read(content)) // SDK: confirm return type/overload
            {
                throw DecodeFailure();
            }
        }
        catch (FitException)
        {
            throw DecodeFailure();
        }
        catch (EndOfStreamException)
        {
            throw DecodeFailure();
        }

        var withTimestamp = records.Where(r => r.GetTimestamp() is not null).ToList();
        if (withTimestamp.Count == 0)
        {
            throw new ValidationException(new[] { "File: The file contains no track data." });
        }

        var startTimeUtc = ToUtc(withTimestamp[0].GetTimestamp()!);

        // Pass 1 — elapsed seconds, sanity-bounded Hr/Power, cumulative distance. No pace yet: pace needs
        // the sport, and the sport's power-fallback (below) needs to see every sample's Power first.
        var elapsed = new int[withTimestamp.Count];
        var hr = new int?[withTimestamp.Count];
        var power = new int?[withTimestamp.Count];
        var distance = new float?[withTimestamp.Count]; // cumulative metres

        for (var i = 0; i < withTimestamp.Count; i++)
        {
            var record = withTimestamp[i];
            elapsed[i] = (int)(ToUtc(record.GetTimestamp()!) - startTimeUtc).TotalSeconds;
            hr[i] = ActivitySampleBounds.Hr((int?)record.GetHeartRate());
            power[i] = ActivitySampleBounds.Power((int?)record.GetPower());
            distance[i] = record.GetDistance(); // SDK: confirm — cumulative per-record distance in
                                                 // metres; not on Tasks-19-3's checked-accessor list, but
                                                 // is the standard Record-message field. If IntelliSense
                                                 // shows a different member for this, use it and note the
                                                 // divergence in the commit body. GetSpeed() (verified
                                                 // present) is the fallback: integrate speed * elapsed
                                                 // delta if no cumulative-distance accessor exists.
        }

        var sport = ResolveSport(session, power);

        // Pass 2 — per-sample pace (Run/Swim only), from the cumulative-distance delta to the next
        // sample, exactly as the TCX parser derives it (seconds per km run, per 100 m swim).
        var paceUnit = sport switch { DomainSport.Run => 1000d, DomainSport.Swim => 100d, _ => 0d };
        var samples = new List<ActivitySample>(withTimestamp.Count);

        for (var i = 0; i < withTimestamp.Count; i++)
        {
            int? pace = null;
            if (paceUnit > 0 && i > 0 && distance[i] is { } d && distance[i - 1] is { } prev
                && d > prev && elapsed[i] > elapsed[i - 1])
            {
                var deltaMeters = d - prev;
                var deltaSeconds = elapsed[i] - elapsed[i - 1];
                pace = (int)Math.Round(deltaSeconds / (deltaMeters / paceUnit));
            }

            samples.Add(new ActivitySample(elapsed[i], hr[i], power[i], pace));
        }

        // Session aggregates come from the retained SAMPLES, never from SessionMesg's own
        // GetAvgHeartRate/GetMaxHeartRate/GetAvgPower — the one rule Task 19-2 fixed across all three
        // formats. Those SessionMesg accessors exist (confirmed) but are deliberately unused here.
        var avgHr = Average(samples.Select(s => s.Hr));
        var avgPower = Average(samples.Select(s => s.Power));
        var hrValues = samples.Where(s => s.Hr is not null).Select(s => s.Hr!.Value).ToList();
        var maxHr = hrValues.Count > 0 ? hrValues.Max() : (int?)null;

        var durationSeconds = session?.GetTotalTimerTime() is { } timer
            ? (int)Math.Round(timer)
            : elapsed[^1];
        var distanceMeters = session?.GetTotalDistance() is { } dist
            ? (int)Math.Round(dist)
            : (distance[^1] is { } lastDistance ? (int)Math.Round(lastDistance) : (int?)null);

        int? avgPace = null;
        if (paceUnit > 0 && durationSeconds is > 0 && distanceMeters is > 0)
        {
            avgPace = (int)Math.Round(durationSeconds.Value / (distanceMeters.Value / paceUnit));
        }

        return Task.FromResult(new ParsedActivity(
            sport, startTimeUtc, durationSeconds, distanceMeters, avgHr, maxHr, avgPower, avgPace, samples));
    }

    // Session message's sport maps Cycling/Running/Swimming to Bike/Run/Swim; anything else, or no
    // session message at all, falls through to Task 19-2's shared chain: power present -> Bike, else
    // Run. Deliberately no case for a multisport/triathlon file - see the class-level LIMITATION comment.
    private static DomainSport ResolveSport(SessionMesg? session, int?[] power)
    {
        if (session?.GetSport() is { } fitSport) // SDK: confirm - GetSport() return type
        {
            var mapped = fitSport switch
            {
                Dynastream.Fit.Sport.Cycling => (DomainSport?)DomainSport.Bike,
                Dynastream.Fit.Sport.Running => (DomainSport?)DomainSport.Run,
                Dynastream.Fit.Sport.Swimming => (DomainSport?)DomainSport.Swim,
                _ => null
            };
            if (mapped is { } m)
            {
                return m;
            }
        }

        return power.Any(p => p is not null) ? DomainSport.Bike : DomainSport.Run;
    }

    // FIT timestamps are seconds since the FIT epoch (1989-12-31T00:00:00Z); the SDK's
    // Dynastream.Fit.DateTime wrapper exposes the converted value via GetDateTime().
    private static DateTime ToUtc(Dynastream.Fit.DateTime fitTimestamp) =>
        DateTime.SpecifyKind(fitTimestamp.GetDateTime(), DateTimeKind.Utc); // SDK: confirm GetDateTime()

    private static int? Average(IEnumerable<int?> values)
    {
        var present = values.Where(v => v is not null).Select(v => v!.Value).ToList();
        return present.Count > 0 ? (int)Math.Round(present.Average()) : null;
    }

    private static ValidationException DecodeFailure() =>
        new(new[] { "File: The .fit file could not be decoded." });
}
```

Notes for the transcription:
- `ActivitySampleBounds.Hr`/`.Power` are called exactly as `TcxActivityParser` calls them — no local
  redeclaration of `30`/`230`/`2000`.
- The `(int?)record.GetHeartRate()` / `(int?)record.GetPower()` casts compile regardless of the exact
  underlying nullable numeric type the SDK returns (`byte?`, `ushort?`, …) — that is deliberate, since
  those exact return types are on the "not verified" list.
- No `catch (Exception)` — only `FitException` and `EndOfStreamException`, per `Tasks-19-3.md`'s own list
  of named failure modes. If the restored package throws something else on the garbage-bytes test in
  Step 4, add that specific type here and note it in the commit body; do not widen to a bare `catch`.

**Verify:** `dotnet build api/Bryk.sln` green. Every `// SDK: confirm` line resolves with no compile
error (a compile error on one of the *named types/accessors* from `Tasks-19-3.md`'s checked list would
mean a wrong `using`, not a wrong name — fix the `using` before assuming the SDK differs). Warnings still
**16**.

## Step 3 — Source and commit the `.fit` fixture

**File:** `api/Bryk.API.Tests/Fixtures/ActivityFiles/sample-ride.fit` (new, binary).

- Obtain a **real device-written FIT file** — a short indoor or outdoor ride carrying HR and power (e.g.
  export one from a Garmin/Wahoo device, Garmin Connect, or a personal Zwift/TrainerRoad ride). Confirm it
  is `≤ 200 KB`. Do **not** hand-craft bytes and do **not** generate one with the SDK's own encoder — the
  fixture's value is that it carries the message mix and quirks of a genuine device file.
- Place it at the path above. 19-2's existing csproj glob
  (`<None Update="Fixtures\ActivityFiles\**">`) already copies it to the test output directory —
  **do not add a second `<None>` entry to `Bryk.API.Tests.csproj`**.
- `git add` it as a binary (verify `git status`/`git diff --stat` shows it as a new binary file, not text).
- Note its provenance (device/source, duration, whether it carries power) — this gets written as a
  comment at the top of the test file in Step 4, since every pinned assertion is derived from it.

**Verify:** the file exists at the exact path, is `≤ 200 KB` (`(Get-Item
api/Bryk.API.Tests/Fixtures/ActivityFiles/sample-ride.fit).Length` in PowerShell), and
`api/Bryk.API.Tests/Bryk.API.Tests.csproj` shows **no diff** (the glob already covers it).

## Step 4 — Structural test file: `FitActivityParserTests.cs`

**New file:** `api/Bryk.API.Tests/ActivityFiles/FitActivityParserTests.cs`. The FIT parser lives in
`Bryk.Infrastructure`, so its tests belong in `Bryk.API.Tests` (the only test project with an EF/
`Bryk.Infrastructure`-reachable reference) — do **not** add a `Bryk.Infrastructure` reference to
`Bryk.Application.Tests`.

Because the fixture is a real device file rather than a hand-authored one, every assertion below is
**structural** except `AvgPower`, which the task pins as a literal. Write `ExpectedAvgPower` as an
obviously-wrong placeholder for now (`0`) — Step 5 promotes it to the real observed value. Do **not**
guess a plausible-looking number here.

```csharp
using Bryk.Application.ActivityFiles;
using Bryk.Application.Exceptions;
using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.ActivityFiles;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.ActivityFiles;

// Fixture provenance (sample-ride.fit): fill in device/source, duration, and whether it carries power
// once the fixture from Step 3 is chosen. Every pinned assertion below is derived from this file.
public class FitActivityParserTests
{
    // Placeholder — Step 5 promotes this to the AvgPower value actually observed from the fixture.
    private const int ExpectedAvgPower = 0;

    private static Stream Fixture(string name) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ActivityFiles", name));

    [Fact]
    public void Format_IsFit()
    {
        new FitActivityParser().Format.Should().Be(ActivityFileFormat.Fit);
    }

    [Fact]
    public async Task ParseAsync_RideFixture_ReturnsBikeSessionWithSamples()
    {
        using var stream = Fixture("sample-ride.fit");
        var result = await new FitActivityParser().ParseAsync(stream);

        result.Sport.Should().Be(Sport.Bike);
        result.Samples.Should().NotBeEmpty();
        result.DurationSeconds.Should().BePositive();
        result.DistanceMeters.Should().BePositive();
        result.StartTimeUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task ParseAsync_RideFixture_DerivesAveragePowerFromSamples()
    {
        using var stream = Fixture("sample-ride.fit");
        var result = await new FitActivityParser().ParseAsync(stream);

        result.AvgPower.Should().BePositive();
        result.AvgPower.Should().Be(ExpectedAvgPower);
    }

    [Fact]
    public async Task ParseAsync_RideFixture_KeepsEveryHeartRateSampleInRange()
    {
        using var stream = Fixture("sample-ride.fit");
        var result = await new FitActivityParser().ParseAsync(stream);

        result.Samples.Where(s => s.Hr is not null)
            .Should().OnlyContain(s => s.Hr!.Value >= 30 && s.Hr.Value <= 230);
    }

    [Fact]
    public async Task ParseAsync_RideFixture_ElapsedSecondsAreMonotonicAndStartAtZero()
    {
        using var stream = Fixture("sample-ride.fit");
        var result = await new FitActivityParser().ParseAsync(stream);

        result.Samples[0].ElapsedSeconds.Should().Be(0);
        result.Samples.Select(s => s.ElapsedSeconds).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ParseAsync_BikeSport_HasNullAvgPace()
    {
        using var stream = Fixture("sample-ride.fit");
        var result = await new FitActivityParser().ParseAsync(stream);

        result.AvgPace.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_GarbageBytes_ThrowsValidationExceptionWithFilePrefix()
    {
        using var stream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02, 0x03 });

        Func<Task> act = async () => await new FitActivityParser().ParseAsync(stream);

        var thrown = await act.Should().ThrowExactlyAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e => e.StartsWith("File:"));
    }

    [Fact]
    public async Task ParseAsync_TcxFixtureBytes_ThrowsValidationException()
    {
        using var stream = Fixture("sample-run.tcx"); // Task 19-2's TCX fixture, read-only reuse

        Func<Task> act = async () => await new FitActivityParser().ParseAsync(stream);

        await act.Should().ThrowExactlyAsync<ValidationException>();
    }

    [Fact]
    public async Task ParseAsync_RideFixture_HistogramIsComputableFromTheResult()
    {
        using var stream = Fixture("sample-ride.fit");
        var result = await new FitActivityParser().ParseAsync(stream);

        var powerBands = new SportZonesResponse
        {
            Sport = Sport.Bike,
            Metric = ZoneMetric.Power,
            Zones = new List<ZoneDto>
            {
                new() { ZoneNumber = 1, LowerBound = 0m, UpperBound = 150m },
                new() { ZoneNumber = 2, LowerBound = 150m, UpperBound = 200m },
                new() { ZoneNumber = 3, LowerBound = 200m, UpperBound = 250m },
                new() { ZoneNumber = 4, LowerBound = 250m, UpperBound = 300m },
                new() { ZoneNumber = 5, LowerBound = 300m, UpperBound = null }
            }
        };

        var histogram = ZoneHistogramCalculator.Compute(result, powerBands, maxHr: null);

        histogram.Should().HaveCount(5);
        var totalSeconds = histogram.Sum(h => h.Seconds);
        totalSeconds.Should().BePositive();
        totalSeconds.Should().BeLessThanOrEqualTo(result.DurationSeconds!.Value);
    }
}
```

**Verify:** `dotnet build api/Bryk.sln` green.
```
dotnet test api/Bryk.sln --filter FullyQualifiedName~FitActivityParserTests
```
Every fact **except** `ParseAsync_RideFixture_DerivesAveragePowerFromSamples` passes. That one fails —
expected: `ExpectedAvgPower` is still the `0` placeholder, and `AvgPower.Should().Be(0)` fails against the
real (positive) value. Read the FluentAssertions failure message; it prints the actual `AvgPower` the
parser derived from the fixture. That is the number Step 5 promotes. Do not proceed to Step 5 until every
other fact in this file is green — a failure anywhere else means the parser or the fixture needs fixing
first, not the constant.

## Step 5 — Promote the observed `AvgPower` to a pinned constant

- From the failing assertion's message in Step 4, copy the actual `AvgPower` integer.
- Edit `FitActivityParserTests.cs`: replace `private const int ExpectedAvgPower = 0;` with the observed
  value, e.g. `private const int ExpectedAvgPower = 243;` (illustrative — use the number the test printed,
  not this one). Update the comment above it to state it is now the pinned, promoted value.
- Re-run.

**Verify:**
```
dotnet test api/Bryk.sln --filter FullyQualifiedName~FitActivityParserTests
```
All 9 facts pass by name: `Format_IsFit`, `ParseAsync_RideFixture_ReturnsBikeSessionWithSamples`,
`ParseAsync_RideFixture_DerivesAveragePowerFromSamples`,
`ParseAsync_RideFixture_KeepsEveryHeartRateSampleInRange`,
`ParseAsync_RideFixture_ElapsedSecondsAreMonotonicAndStartAtZero`, `ParseAsync_BikeSport_HasNullAvgPace`,
`ParseAsync_GarbageBytes_ThrowsValidationExceptionWithFilePrefix`,
`ParseAsync_TcxFixtureBytes_ThrowsValidationException`,
`ParseAsync_RideFixture_HistogramIsComputableFromTheResult`. Build green, warnings still **16**.

## Step 6 — Final verification, smoke check, and commit

Run the full command set from `Tasks-19-3.md`:
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```

- `dotnet build` — 0 errors, warnings still **16** (unchanged from Step 0/Step 1 — no new `NU1901`–
  `NU1904`-class warning survived the restore). If this has grown at any point up to now, this step should
  already have stopped earlier; re-confirm here as the final gate.
- `dotnet test api/Bryk.sln` — total is **N + 9** (the Step 0 baseline **N** plus this task's 9 new
  `FitActivityParserTests` facts), all green, nothing else broken.
- `pnpm run build` — green (sanity check only, this task touches no UI file).
- `pnpm exec vitest run --no-file-parallelism` — **252 / 56 files**, byte-for-byte unchanged from Step 0.
  If this number moved, something outside this task's scope changed — stop and investigate before
  committing.
- `git status` / `git add -A && git diff --cached --stat` — confirm **only** these files appear:
  - `api/Bryk.Infrastructure/Bryk.Infrastructure.csproj` (one added line)
  - `api/Bryk.Infrastructure/ActivityFiles/FitActivityParser.cs` (new)
  - `api/Bryk.API.Tests/Fixtures/ActivityFiles/sample-ride.fit` (new, binary)
  - `api/Bryk.API.Tests/ActivityFiles/FitActivityParserTests.cs` (new)
  If the diff shows `Program.cs`, any migration, any file from Task 19-2's list (`IActivityFileParser.cs`,
  `ParsedActivity.cs`, `ZoneHistogramEntry.cs`, `ZoneHistogramCalculator.cs`, `ActivitySampleBounds.cs`,
  `TcxActivityParser.cs`, `GpxActivityParser.cs`, `Bryk.API.Tests.csproj`), `LoadCalculator.cs`, anything
  under `ui/`, or a second `PackageReference` — **STOP**, that is scope creep beyond `Tasks-19-3.md`.
- Confirm `git grep "Garmin.FIT.Sdk"` still shows exactly one `.csproj` hit (`Bryk.Infrastructure.csproj`)
  plus documentation references — nowhere in `Bryk.Domain`, `Bryk.Application`, `Bryk.API`, or either test
  project's `.csproj`.
- Confirm no SDK type (`Dynastream.Fit.*`, `Dynastream.Utility.*`) appears anywhere outside
  `FitActivityParser.cs` and `FitActivityParserTests.cs` — the parser's only public surface is Task
  19-2's `IActivityFileParser`.
- If any `// SDK: confirm` line in `FitActivityParser.cs` turned out to diverge from what IntelliSense/the
  compiler actually resolved, make sure that divergence is called out in the commit body below rather than
  silently changed with no record.
- Commit with the message from `Tasks-19-3.md` (no AI co-author trailer — project convention):

```
feat: FIT parser behind the activity-file abstraction

Add the third format. FitActivityParser implements Task 19-2's
IActivityFileParser and produces the same ParsedActivity contract the TCX and
GPX parsers do: sport from the session message with the shared power-then-run
fallback, session averages derived from the retained records rather than the
device's own summary, duration and distance from the session totals when
present, and pace only for run and swim. It reuses ActivitySampleBounds
unchanged, so the 30-230 bpm and 2000 W sanity rules live in exactly one place
across all three formats.

Garmin.FIT.Sdk 21.205.0 goes into Bryk.Infrastructure only - approved by the
Sr. Dev on 2026-07-26 (ADR-0010 1). It is publisher-verified Garmin
International, ships netstandard2.0 which is net10.0-compatible, and is
licensed under Garmin's proprietary royalty-free FIT Protocol License
Agreement rather than an OSI license. That is the accepted trade for reading
the format athletes actually export; recording it here so it is not reopened.

Decode failures, garbage bytes and files with no records all raise the
existing Application ValidationException with a "File:" message, so a corrupt
upload is a clean 400 rather than a 500 - the tests assert the exact exception
type for that reason. No migration, no Program.cs change, no edit to
LoadCalculator: imported power reaches the load math through Task 19-4's
synthetic WorkoutStepResult, not through the calculator.
```
