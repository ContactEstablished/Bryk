# Task 19-3 — `FitActivityParser` + the approved `Garmin.FIT.Sdk` package reference

## Surface
Backend only. One new parser (`api/Bryk.Infrastructure/ActivityFiles/FitActivityParser.cs`) implementing
Task 19-2's `IActivityFileParser`, **one** `PackageReference` added to `Bryk.Infrastructure.csproj`, one
committed `.fit` fixture, and one xUnit file. **No migration, no Application change, no
`Program.cs` line, no UI.** The parser is unresolvable from DI until 19-4 registers it — expected, not
dead code.

## Why
`.fit` is the format athletes actually have: it is what Garmin, Wahoo and Zwift write, and it is the only
one of the three that carries power and per-record data reliably. It is also binary, so unlike `.tcx`
and `.gpx` it cannot be read with `System.Xml.Linq`. The ROADMAP flagged the SDK as the phase's headline
approval and offered a graceful degradation to TCX/GPX-only if it were denied; **it was approved on
2026-07-26**, so all three formats ship and this task exists. It is isolated into its own task precisely
because it is the one place a third-party dependency enters the phase: keeping it behind 19-2's
Application-side interface means the SDK's types never appear in `Bryk.Application`, `Bryk.Domain` or
`Bryk.API`, and a future decision to drop it would delete one file and one line.

## Depends on
- **Task 19-2** — implements its `IActivityFileParser` interface and returns its `ParsedActivity` /
  `ActivitySample` records; reuses its `ActivitySampleBounds` **read-only** and its cross-format
  resolution rules (sport fallback chain, sample-derived averages, declared totals, pace units, empty
  file → `File:` validation error). Every one of those rules is stated once in `md/Tasks-19-2.md`;
  do not restate them differently here.
- **Task 19-1** — `ActivityFileFormat.Fit`.
- **ADR-0010 §1** — the SDK decision, its version, and its license.
- **Task 19-4** registers this parser. Nothing in this task may edit 19-4's files.

## Required reading
- `md/Tasks-19-2.md` — sections *"Cross-format resolution rules"* and *"`api/Bryk.Infrastructure/
  ActivityFiles/ActivitySampleBounds.cs`"*. This parser must produce results consistent with the two XML
  parsers or 19-4's tests will diverge by format.
- `api/Bryk.Infrastructure/ActivityFiles/TcxActivityParser.cs` (from 19-2) — the class shape to mirror:
  `Format` property, `ParseAsync(Stream, CancellationToken)`, the `ValidationException` failure path,
  the `ActivitySampleBounds` calls. **Read only.**
- `api/Bryk.Application/ActivityFiles/IActivityFileParser.cs` + `ParsedActivity.cs` (from 19-2) — the
  contract. **Read only.**
- `api/Bryk.Infrastructure/Bryk.Infrastructure.csproj` — the existing `PackageReference` block
  (EF Core 10 ×3, `Microsoft.Extensions.Configuration.Json` 10.0.0,
  `Microsoft.Extensions.Hosting.Abstractions` 10.0.0). The new entry joins that `ItemGroup`.
- `api/Bryk.API.Tests/Bryk.API.Tests.csproj` — confirm 19-2's
  `<None Update="Fixtures\ActivityFiles\**">` glob is present. **This task must not edit that file** —
  the glob already covers the `.fit` fixture.
- `CLAUDE.md` → "When to ask for Sr. Dev approval before proceeding" → *New NuGet or npm packages*. This
  task's package is the one exception in Phase 19 and it is **already approved** (below).

## Acceptance criteria

### 1. `api/Bryk.Infrastructure/Bryk.Infrastructure.csproj` (edit — exactly one line)

Add to the existing `PackageReference` `ItemGroup`:
```xml
<PackageReference Include="Garmin.FIT.Sdk" Version="21.205.0" />
```

**Approval record — do not re-litigate this.** Approved by the Sr. Dev on **2026-07-26** (ADR-0010 §1).
Verified at approval time: publisher-verified **Garmin International**; ships `net46 / netcoreapp2.0 /
netstandard2.0`, and `netstandard2.0` is `net10.0`-compatible; the license is Garmin's proprietary
**royalty-free FIT Protocol License Agreement**, shipped as `LICENSE.txt` inside the package — it is
**not** an OSI license, which is expected and accepted for this dependency. Record this in the commit
body so the next reviewer does not reopen it.

Constraints on the reference:
- **`Bryk.Infrastructure` only.** It must not appear in `Bryk.Domain`, `Bryk.Application`, `Bryk.API`,
  or either test project. `Bryk.API` picks it up transitively at runtime; that is fine and is not a
  reason to add a direct reference anywhere.
- **Exactly this version.** If NuGet resolves something else, or the restore pulls an unexpected
  transitive dependency, or the package raises a new `NU1903`-class audit warning — **STOP and ask**
  before proceeding. The build's warning count is a gate (see *Verification commands*).
- No other package. No `Directory.Packages.props`, no central package management change.

### 2. `api/Bryk.Infrastructure/ActivityFiles/FitActivityParser.cs` (new)

`public class FitActivityParser : IActivityFileParser`, namespace `Bryk.Infrastructure.ActivityFiles`,
`Format => ActivityFileFormat.Fit`.

**SDK surface — verified against the package, 2026-07-26.** The coordinator downloaded
`Garmin.FIT.Sdk` 21.205.0 and inspected the shipped assembly, so the following are confirmed present
and are safe to write against (do not re-derive them):

- Assembly: `lib/netstandard2.0/**FitSDK.dll**` (note: `FitSDK.dll`, not `Fit.dll`).
- Namespace: **`Dynastream.Fit`** (the assembly also carries `Dynastream.Utility`).
- Types confirmed: `Decode`, `MesgBroadcaster`, `MesgEventArgs`, `SessionMesg`, `RecordMesg`,
  `LapMesg`, `ActivityMesg`, `FitException`.
- Accessors confirmed: `GetTimestamp`, `GetHeartRate`, `GetPower`, `GetSpeed`, `GetSport`,
  `GetTotalTimerTime`, `GetTotalDistance`, `GetAvgHeartRate`, `GetMaxHeartRate`, `GetAvgPower`.

It is a decode-and-broadcast library: construct a `Decode`, attach a `MesgBroadcaster`, subscribe to
its per-message events, and read typed messages via the `Get…()` accessors above. Every accessor
returns a **nullable** value (`byte?`, `ushort?`, `float?` …) — absent fields are the norm, so null-guard
every read rather than assuming a field is populated.

What is **not** verified: exact return types and overload signatures per accessor, and the event names
on `MesgBroadcaster`. Confirm those against IntelliSense once the package restores. If anything
diverges from the list above, follow the package and note the divergence in the commit body — but the
type and accessor **names** above are checked, so a compile error on one of them means a wrong `using`,
not a wrong name.

Behaviour the parser must produce:

- **Decode + collect.** Read the stream once, collecting record messages into a list and keeping the last
  session message (if any). Do not hold the whole stream in a second buffer.
- **Timestamps.** FIT timestamps are seconds since the FIT epoch `1989-12-31T00:00:00Z`; the SDK exposes
  them as `System.DateTime` via its `Dynastream.Fit.DateTime` wrapper. Convert to a UTC
  `System.DateTime` and use the first record's timestamp as `StartTimeUtc`; each sample's
  `ElapsedSeconds` is the whole-second difference from it.
- **Sport.** The session message's sport: `Cycling` → `Sport.Bike`, `Running` → `Sport.Run`,
  `Swimming` → `Sport.Swim`. Anything else, or no session message, falls through to Task 19-2's chain
  (power present → Bike, else Run). Do **not** invent a mapping for triathlon/multisport files — a
  multisport FIT decodes to whichever session appears; note the limitation in the class comment as a
  future item, do not implement a split.
- **Samples.** One `ActivitySample` per record message with a timestamp:
  `Hr = ActivitySampleBounds.Hr(record.GetHeartRate())`,
  `Power = ActivitySampleBounds.Power(record.GetPower())` — reuse 19-2's type, do **not** redeclare
  the constants. Per-sample pace (Run/Swim only) from the cumulative-distance delta between consecutive
  records, exactly as the TCX parser derives it.
- **Session aggregates.** `AvgHr`/`AvgPower`/`MaxHr` derived from the retained **samples** (arithmetic
  mean rounded to `int`, max), *not* from the session message's own averages — the single rule 19-2
  fixed across all three formats. `DurationSeconds`/`DistanceMeters` prefer the session message's
  totals when present (`GetTotalTimerTime` seconds rounded, `GetTotalDistance` metres rounded),
  otherwise derive from the records. `AvgPace` = `DurationSeconds / (DistanceMeters / unit)` for Run/Swim
  only.
- **Failure.** Any SDK decode failure (`FitException` or whatever the installed package throws), any
  `EndOfStreamException`, and zero retained records all become
  `new Bryk.Application.Exceptions.ValidationException(new[] { "File: The .fit file could not be decoded." })`
  / `"File: The file contains no track data."`. No raw SDK exception may escape into the middleware,
  where it would become a 500.
- **No file I/O, no clock read, no configuration.** The parser is a pure function of its stream.

### 3. Fixture — `api/Bryk.API.Tests/Fixtures/ActivityFiles/sample-ride.fit`

- A **real device-written FIT file** (a short indoor or outdoor ride with HR and power), ≤ 200 KB. Do
  **not** hand-craft bytes and do **not** generate one with the SDK's encoder just to satisfy the test —
  the point of the fixture is that it exercises a genuine file's message mix.
- Commit it as a binary. 19-2's `<None Update="Fixtures\ActivityFiles\**">` glob already copies it to
  the output directory; **do not add a second csproj entry**.
- Record the fixture's provenance (device/source, duration, whether it carries power) in a comment at the
  top of the test file, since the pinned assertions below are derived from it.

## Non-goals
- **No package other than `Garmin.FIT.Sdk` 21.205.0**, and it goes in `Bryk.Infrastructure.csproj` only.
  Any second package — including a "small helper" — is a **STOP and ask**.
- **No migration**, no entity change, no `ApplicationDbContext` edit. **STOP and ask** if one seems needed.
- **Do not edit `api/Bryk.Application/Training/Load/LoadCalculator.cs`** — frozen for Phase 19. Power
  read from a FIT file reaches the load math through 19-4's synthetic `WorkoutStepResult` (ADR-0010 §3),
  never through a calculator change. If you find yourself adding a session-power branch there —
  **STOP and ask**.
- **Do not add `Workout.SourceFileId`** and **do not create a `WorkoutZoneDuration` table** — neither is
  approved (ADR-0010 §4). **STOP and ask**.
- **Do not edit any Task 19-2 file**: `IActivityFileParser.cs`, `ParsedActivity.cs`,
  `ZoneHistogramEntry.cs`, `ZoneHistogramCalculator.cs`, `ActivitySampleBounds.cs`,
  `TcxActivityParser.cs`, `GpxActivityParser.cs`, `Bryk.API.Tests.csproj`, or the three XML fixtures.
  If the FIT parser needs something 19-2's contract does not expose, **STOP and ask** rather than
  widening the interface unilaterally.
- Do not write files owned by other siblings: `ActivityFile.cs` / `IActivityFileRepository.cs` /
  `ActivityFileRepository.cs` / `ApplicationDbContext.cs` / `Program.cs` (19-1 and 19-4),
  `Bryk.Application/ActivityFiles/` service, DTO and validator files + `ActivityFilesController.cs`
  (19-4), anything under `ui/` (19-5), `TimeInZoneCalculator.cs` / `TimeInZoneResponse.cs` /
  `AnalyticsService.cs` (19-6).
- **No DI registration.** 19-4 registers all three parsers together.
- **No per-second sample persistence** (ADR-0010 §6); no lap-level output; no power curves, decoupling
  or lap deep-dives; no push-to-device; no bulk/multi-file upload; no vendor OAuth or auto-sync.
- **No auth code** — Phase 12 stays deferred and approval-gated.
- **No ProblemDetails / error-contract rework** and **no new `ExceptionHandlingMiddleware` case** —
  decode failures reuse `ValidationException` → 400. A middleware change is cross-cutting: **STOP and ask**.
- **Do not fix** the two pre-existing nullable warnings in `WorkoutsControllerTests.cs:121,150`.
- **Do not** revert, stash, or commit unrelated working-tree changes.

## Test expectations

`api/Bryk.API.Tests/ActivityFiles/FitActivityParserTests.cs` (new file; the folder is created by 19-2).
The FIT parser lives in `Bryk.Infrastructure`, so its tests belong here — `Bryk.Application.Tests`
references `Bryk.Application` alone and **must not** gain a project reference (Sr. Dev slow-down gate).

Because the fixture is a real device file rather than a hand-authored one, the assertions below are
**structural**. Once the fixture is chosen, read its actual figures once and **promote them to `private
const` values at the top of the test class**, then assert on those constants — so the suite pins real
numbers rather than inequalities from the second commit onward.

- `Format_IsFit` — `new FitActivityParser().Format.Should().Be(ActivityFileFormat.Fit)`.
- `ParseAsync_RideFixture_ReturnsBikeSessionWithSamples` — `Sport.Should().Be(Sport.Bike)`,
  `Samples.Should().NotBeEmpty()`, `DurationSeconds.Should().BePositive()`,
  `DistanceMeters.Should().BePositive()`, `StartTimeUtc.Kind.Should().Be(DateTimeKind.Utc)`.
- `ParseAsync_RideFixture_DerivesAveragePowerFromSamples` — `AvgPower.Should().BePositive()` and
  `AvgPower.Should().Be(ExpectedAvgPower)` against the promoted constant.
- `ParseAsync_RideFixture_KeepsEveryHeartRateSampleInRange` — every sample with a non-null `Hr` satisfies
  `BeInRange(30, 230)` (the `ActivitySampleBounds` contract, proven on real data).
- `ParseAsync_RideFixture_ElapsedSecondsAreMonotonicAndStartAtZero` —
  `Samples[0].ElapsedSeconds.Should().Be(0)` and the series `Should().BeInAscendingOrder(s => s.ElapsedSeconds)`.
- `ParseAsync_BikeSport_HasNullAvgPace` — pace is Run/Swim only.
- `ParseAsync_GarbageBytes_ThrowsValidationExceptionWithFilePrefix` — a stream of
  `new byte[] { 0x00, 0x01, 0x02, 0x03 }` → `Bryk.Application.Exceptions.ValidationException` whose
  single `Errors` entry starts with `"File:"`. **Explicitly assert it is not a raw SDK exception**
  (`act.Should().ThrowExactly<ValidationException>()`), because that is the difference between a clean
  400 and a 500.
- `ParseAsync_TcxFixtureBytes_ThrowsValidationException` — feed `sample-run.tcx`'s bytes to the FIT
  parser; the guard 19-4's magic-byte sniffing backs up must hold here too.
- `ParseAsync_RideFixture_HistogramIsComputableFromTheResult` — call
  `ZoneHistogramCalculator.Compute(parsed, powerBands, maxHr: null)` with a small inline
  `SportZonesResponse` and assert the five buckets sum to a positive number ≤ `DurationSeconds`. This is
  the integration point 19-4 depends on; proving it here catches a unit mismatch early.

## Verification commands
```
dotnet build api/Bryk.sln
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```
xUnit must rise from the **262** baseline (173 `Bryk.Application.Tests` + 89 `Bryk.API.Tests`) plus what
19-1 and 19-2 added, with zero failures. Vitest stays at exactly **252 / 56 files** — this task touches
no UI. Warnings must not exceed **16**: this is the one task in the phase that adds a package, so a new
`NU1901`–`NU1904` audit warning would push past the gate. If it does, **STOP and ask** — do not suppress
it and do not add a countervailing direct reference.

## Review checklist
- [ ] `Garmin.FIT.Sdk` **21.205.0** appears in `Bryk.Infrastructure.csproj` and **nowhere else**
      (`git grep "Garmin.FIT.Sdk"` returns one line plus documentation).
- [ ] No SDK type appears in `Bryk.Domain`, `Bryk.Application` or `Bryk.API` — the parser's only public
      surface is 19-2's `IActivityFileParser`.
- [ ] The SDK API used was **verified against the restored package**, not written from memory; any
      divergence from this doc's description is noted in the commit body.
- [ ] Decode failure and empty-record files both produce
      `Bryk.Application.Exceptions.ValidationException` with a `"File: …"` message; no raw SDK exception
      can reach the middleware.
- [ ] `ActivitySampleBounds` is **reused**, not redeclared; the sanity constants appear once in the repo.
- [ ] Session averages come from the samples (the one cross-format rule), not from `SessionMesg`.
- [ ] The `.fit` fixture is a real device file, ≤ 200 KB, committed, with its provenance recorded; no
      second csproj glob was added.
- [ ] Build warnings still ≤ 16 after the restore.
- [ ] `git diff --stat` shows no `Program.cs`, no migration, no Task 19-2 file, and nothing under `ui/`.
- [ ] Commit message carries **no** AI co-author trailer (project convention).

## Suggested commit
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
