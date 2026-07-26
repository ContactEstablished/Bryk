# Impl 20-2 — Build order: wellness DTOs, validators, summary math, service, controller

**Executor:** architect-implementer (per CLAUDE.md).
**Acceptance contract:** `md/Tasks-20-2.md`.
**Decision lock:** `md/decisions/0011-wellness-metrics.md` (written by Task 20-1) **§1** (the service never
reads or writes `Athlete`; no `IAthleteRepository` in the ctor), **§2** (the upsert is the *service's*
job — read-then-update; PUT replaces the whole day; no DELETE; the unique index is real but unenforced
by the InMemory provider so **no test may assert a duplicate insert throws**), **§3** (nothing here
feeds the PMC) — plus ADR-0006 (PMC stays pure) and the frozen error contract in
`ExceptionHandlingMiddleware.cs:33–55`.
**Scope:** Backend only. One new Application slice under `api/Bryk.Application/Wellness/`, one thin
controller, **one** `AddScoped` line in `Program.cs`, tests in both test projects. **No migration, no
entity change, no `ApplicationDbContext` edit, no new package, no UI.**

This is the step-by-step build order. Execute top-to-bottom; each step's **Verify** line is the gate to
the next. The slice is written inside-out (DTOs → validators → validator tests → pure calculator →
calculator tests → service → service tests → controller → DI → integration tests → runtime smoke) so
`dotnet build`/`dotnet test` stays a meaningful gate at every step instead of one big edit at the end.
One commit at the end, with the message from `Tasks-20-2.md`.

**The two traps this task exists to survive** — both are called out again at the step that handles them,
and neither is retired by a green build:

1. **`Program.cs:32–33` sets `SuppressModelStateInvalidFilter = true`.** The automatic model-state 400
   is **off**. A route value that fails to bind does **not** produce a 400 — the parameter silently
   receives `default(T)` and **the action still runs**. An unguarded `PUT /api/v1/wellness/{date}` would
   cheerfully upsert `0001-01-01`. The defence is **two layers, both mandatory** (Steps 3 and 10, proved
   at Step 12). Turning the flag off is **not** an option — see the STOP box in Step 10.
2. **EF InMemory enforces no unique index** (`BrykWebApplicationFactory.cs:11–23` says so in its own doc
   comment). Idempotency is therefore a **service-side** read-then-update guarantee (Step 8), proved by
   counting rows through the API (Step 12). **No test may assert that a duplicate insert throws** — such
   a test would pass for the wrong reason on InMemory and fail against SQL Server.

---

## Step 0 — Pre-flight

- `git status` clean on `main`.
- **Confirm Task 20-1 has actually landed.** This task consumes its entity, its repository contract and
  its table; none of it is optional. Check for the presence of, at minimum:
  - `md/decisions/0011-wellness-metrics.md`, status **Accepted**.
  - `api/Bryk.Domain/Entities/DailyWellness.cs` — read it and confirm the seven metric properties are
    named exactly `SleepHours` (`decimal?`), `SleepQuality` (`int?`), `RestingHr` (`int?`),
    `WeightKg` (`decimal?`), `Soreness` (`int?`), `HrvMs` (`int?`), `Notes` (`string?`), plus
    `Id`/`AthleteId`/`Date` and the two `IAuditable` fields.
  - `api/Bryk.Domain/Interfaces/IDailyWellnessRepository.cs` — read it and copy the **exact** four
    signatures into your head; you will implement a stub against them at Step 9:
    `GetByAthleteAndDateTrackedAsync(Guid, DateOnly, CancellationToken)`,
    `GetByAthleteInRangeAsync(Guid, DateOnly, DateOnly, CancellationToken)`,
    `AddAsync(DailyWellness, CancellationToken)`, `Update(DailyWellness)`.
  - `api/Bryk.Infrastructure/Repositories/DailyWellnessRepository.cs`, the `DbSet<DailyWellness>` +
    configuration block in `api/Bryk.Infrastructure/Data/ApplicationDbContext.cs`, the
    `Migrations/*_AddDailyWellness.cs` migration, and the
    `builder.Services.AddScoped<IDailyWellnessRepository, DailyWellnessRepository>();` line in
    `api/Bryk.API/Program.cs` (20-1 inserts it after `IActivityFileRepository`, i.e. at **L108**).
  - If any of these is missing — **STOP**. This task cannot start.
- `dotnet build api/Bryk.sln` green. Confirm the warning count is **16** on a clean compile:
  ```
  dotnet build api/Bryk.sln --no-incremental
  ```
  (An *incremental* build reports 14 because it skips recompiling `Bryk.API.Tests`. 14 of the 16 are the
  design-time `System.Security.Cryptography.Xml` NU1903 advisory; the other two are the pre-existing
  nullable warnings at `api/Bryk.API.Tests/Training/WorkoutsControllerTests.cs:121` and `:150` —
  **deliberately not fixed; do not fix them.**)
- `dotnet test api/Bryk.sln` green. Expected baseline: **343** (Phase 19 close: 196
  `Bryk.Application.Tests` + 147 `Bryk.API.Tests`) **+ 7** (20-1's `DailyWellnessRepositoryTests`) =
  **350**. Record the number you actually see — every later count in this document is *(confirmed
  baseline) + N*. A divergence is a reason to go re-read what landed, not something to silently absorb.
- `cd ui; pnpm run build` green; `pnpm exec vitest run --no-file-parallelism` at **exactly 288 tests /
  61 files**. This task touches no UI; that number must be byte-for-byte identical at Step 16.
- Re-read `md/Tasks-20-2.md` in full. Open in the editor:
  - `md/decisions/0011-wellness-metrics.md` §1 and §2 (the decision lock).
  - `api/Bryk.API/Program.cs` — **lines 32–33** (the trap), **line 35** (the validator assembly scan:
    `AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>()` — no manual registration
    needed), **lines 100–127** (repositories then services; the services block ends with
    `Bryk.Application.ActivityFiles.IActivityFileService`).
  - `api/Bryk.Application/Goals/GoalService.cs` — the service template: primary ctor
    `(ICurrentUserService, IValidator<T>, IXRepository, IUnitOfWork)`; `currentUser.GetCurrentAthleteId()`;
    `DateOnly.FromDateTime(DateTime.UtcNow)` for today (L19); `await validator.ValidateOrThrowAsync(...)`
    **first** (L39, L58); exactly **one** `unitOfWork.SaveChangesAsync(ct)` per operation (L51, L71);
    `private static Map` at the bottom (L88–94).
  - `api/Bryk.Application/Analytics/AnalyticsRangeRequest.cs` +
    `Analytics/Validators/AnalyticsRangeRequestValidator.cs` — the range contract you mirror
    member-for-member at Steps 1 and 4 (`MaxRangeDays = 400`, the four messages, the
    `When(both have values)` block).
  - `api/Bryk.Application/ActivityFiles/Validators/ActivityFileUploadRequestValidator.cs:16–28` — the
    **field-prefix message convention** (`"Content: The uploaded file is empty."`).
  - `api/Bryk.Application/Common/Validation/ValidationExtensions.cs:16–27` — `ValidateOrThrowAsync`
    collects `e.ErrorMessage` **only** and drops the property name. That single line is *why* every
    message in Step 3 has to name its own field.
  - `api/Bryk.Application/Analytics/WeeklyLoadCalculator.cs` — the pure-calculator precedent (`static`,
    no I/O, `Math.Round(x, 2)`).
  - `api/Bryk.API/Controllers/GoalsController.cs` (thin-controller shape) and
    `api/Bryk.API/Controllers/AnalyticsController.cs:19–26` (the only `DateOnly` parameter style in the
    repo today — `[FromQuery] DateOnly? from`; **there is no `DateOnly` route parameter anywhere**, this
    task introduces the first one).
  - `api/Bryk.API/Middleware/ExceptionHandlingMiddleware.cs:33–55` — read-only, **frozen**:
    `Bryk.Application.Exceptions.ValidationException` → **400** `{status, error, errors[], traceId}`;
    `KeyNotFoundException` → 404; `InvalidOperationException` → 409.
  - `api/Bryk.Application.Tests/Goals/GoalServiceTests.cs:136–171` — the stub harness you mirror at
    Step 9 (`StubCurrentUserService`, `StubUnitOfWork` with `SaveCount`, `StubGoalRepository` with
    `ToReturn`/`Added`/`Updated`). **Do not extract a shared stub library** — each service test file
    carries its own private stubs.
  - `api/Bryk.API.Tests/Goals/GoalsControllerTests.cs:17–20` — `JsonOptions`; a fresh
    `BrykWebApplicationFactory` per test.
  - `api/Bryk.API.Tests/Fixtures/BrykWebApplicationFactory.cs:11–23` (no unique-index enforcement) and
    `:31–32` (`TestAthleteId = 11111111-1111-1111-1111-111111111111`; each factory instance gets its own
    database name, so a fresh factory = a fresh athlete).
- Confirm `api/Bryk.Application/Wellness/` and `api/Bryk.Application.Tests/Wellness/` do **not** exist
  yet. `api/Bryk.API.Tests/Wellness/` **does** exist and already holds 20-1's
  `DailyWellnessRepositoryTests.cs` — this task adds `WellnessControllerTests.cs` alongside it and
  touches that file not at all.

---

## Step 1 — Request DTOs

**New file** `api/Bryk.Application/Wellness/WellnessEntryRequest.cs`:

```csharp
namespace Bryk.Application.Wellness;

public class WellnessEntryRequest
{
    // Populated by the service from the {date} route segment before validation — the route always
    // wins over anything a client puts in the body. Present on the DTO so one validator can carry both
    // the date rules and the metric rules (see WellnessEntryRequestValidator).
    public DateOnly Date { get; set; }

    public decimal? SleepHours { get; set; }
    public int? SleepQuality { get; set; }
    public int? RestingHr { get; set; }
    public decimal? WeightKg { get; set; }
    public int? Soreness { get; set; }
    public int? HrvMs { get; set; }
    public string? Notes { get; set; }
}
```

**New file** `api/Bryk.Application/Wellness/WellnessRangeRequest.cs`:

```csharp
namespace Bryk.Application.Wellness;

// Range contract for GET /wellness. Nullable so the validator can require both ends explicitly —
// the controller binds optional query params (mirrors Analytics/AnalyticsRangeRequest.cs).
public class WellnessRangeRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}
```

Property order in `WellnessEntryRequest` is normative — it is 20-1's entity order, and the DTOs, the
validator, the service's field copy and the UI form all read in that one sequence.

**Verify:** `dotnet build api/Bryk.sln` green (two new, unreferenced types — trivial).

---

## Step 2 — `WellnessResponses.cs` (all four read shapes in one file)

**New file** `api/Bryk.Application/Wellness/WellnessResponses.cs` — the `ActivityFileResponses.cs`
precedent (one file per slice for the response shapes):

```csharp
namespace Bryk.Application.Wellness;

public class WellnessEntryResponse
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal? SleepHours { get; set; }
    public int? SleepQuality { get; set; }
    public int? RestingHr { get; set; }
    public decimal? WeightKg { get; set; }
    public int? Soreness { get; set; }
    public int? HrvMs { get; set; }
    public string? Notes { get; set; }
}

// One metric's 7-day picture. Average is over the days that CARRY a value — a missing day is missing,
// not a zero. Delta is Average - PriorAverage, null unless both windows have data.
public class WellnessMetricSummaryDto
{
    public decimal? Average { get; set; }
    public decimal? PriorAverage { get; set; }
    public decimal? Delta { get; set; }
    public int DaysWithData { get; set; }
}

// One entered day, metrics only (no id, no notes) — the sparkline series.
public class WellnessDailyPointDto
{
    public DateOnly Date { get; set; }
    public decimal? SleepHours { get; set; }
    public int? SleepQuality { get; set; }
    public int? RestingHr { get; set; }
    public decimal? WeightKg { get; set; }
    public int? Soreness { get; set; }
    public int? HrvMs { get; set; }
}

public class WellnessSummaryResponse
{
    public DateOnly To { get; set; }        // today (UTC)
    public DateOnly From { get; set; }      // To.AddDays(-6)  — the current 7-day window
    public DateOnly PriorFrom { get; set; } // To.AddDays(-13) — start of the prior window and of Days
    public WellnessMetricSummaryDto SleepHours { get; set; } = new();
    public WellnessMetricSummaryDto SleepQuality { get; set; } = new();
    public WellnessMetricSummaryDto RestingHr { get; set; } = new();
    public WellnessMetricSummaryDto WeightKg { get; set; } = new();
    public WellnessMetricSummaryDto Soreness { get; set; } = new();
    public WellnessMetricSummaryDto HrvMs { get; set; } = new();
    public IReadOnlyList<WellnessDailyPointDto> Days { get; set; } = [];
    public bool HasAnyEntries { get; set; }
}
```

Notes (do not "simplify" any of these away):
- Averages are `decimal?` for **every** metric, including the integer ones — the mean of two heart rates
  is not an integer.
- `Days` is **sparse** and **ascending** over `[PriorFrom, To]` — 14 days of context, so `Sparkline`
  (which renders only at ≥ 2 points) has something to draw for an athlete who logs a few times a week,
  and so the dashboard fills its tiles in exactly one request.
- `HasAnyEntries` is what Task 20-4's Resting HR fallback keys on: `false` means "this athlete has never
  logged wellness", which is a different statement from "logged, but not this metric".
- `IReadOnlyList<T>` with a public setter deserializes fine under `System.Text.Json` — the same shape
  `ActivityFileUploadResponse.ZoneSeconds`/`MatchCandidates` already round-trip through the Phase-19
  integration tests.

**Verify:** `dotnet build api/Bryk.sln` green.

---

## Step 3 — `WellnessEntryRequestValidator.cs` — the **second** layer of the date defence

**New file** `api/Bryk.Application/Wellness/Validators/WellnessEntryRequestValidator.cs` (new folder):

```csharp
using FluentValidation;

namespace Bryk.Application.Wellness.Validators;

/// <summary>
/// Entry rules for a single wellness day. Every bound is the ROADMAP's Phase 20 number, inclusive, and
/// only applies when the metric is present — partial entries are the norm.
///
/// The two <c>Date</c> rules are not decorative. <c>Program.cs:32–33</c> sets
/// <c>SuppressModelStateInvalidFilter = true</c>, so a route segment that fails to bind produces no
/// 400: the parameter silently arrives as <c>default(DateOnly)</c> and the action still executes. The
/// <c>{date:datetime}</c> route constraint on <c>WellnessController.PutAsync</c> is the first line of
/// defence (a non-date segment 404s before binding); this validator is the second (a well-formed
/// segment that still fails <c>DateOnly</c> binding arrives as <c>0001-01-01</c> and is rejected 400).
/// Neither layer alone is sufficient.
///
/// Every message names its own field because
/// <see cref="Common.Validation.ValidationExtensions.ValidateOrThrowAsync{T}"/> collects
/// <c>ErrorMessage</c> only and drops the property name (the ActivityFileUploadRequestValidator
/// convention).
/// </summary>
public class WellnessEntryRequestValidator : AbstractValidator<WellnessEntryRequest>
{
    public WellnessEntryRequestValidator()
    {
        RuleFor(x => x.Date)
            .Must(d => d != default)
            .WithMessage("Date: A valid date is required (yyyy-MM-dd).");

        // Guarded on "not default" so a default(DateOnly) produces ONE message (the "valid date" one),
        // not two.
        RuleFor(x => x.Date)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date: A wellness entry cannot be in the future.")
            .When(x => x.Date != default);

        RuleFor(x => x.SleepHours)
            .InclusiveBetween(0m, 16m)
            .WithMessage("SleepHours: Sleep must be between 0 and 16 hours.")
            .When(x => x.SleepHours.HasValue);

        RuleFor(x => x.SleepQuality)
            .InclusiveBetween(1, 5)
            .WithMessage("SleepQuality: Sleep quality must be between 1 and 5.")
            .When(x => x.SleepQuality.HasValue);

        RuleFor(x => x.RestingHr)
            .InclusiveBetween(25, 120)
            .WithMessage("RestingHr: Resting HR must be between 25 and 120 bpm.")
            .When(x => x.RestingHr.HasValue);

        RuleFor(x => x.WeightKg)
            .InclusiveBetween(30m, 250m)
            .WithMessage("WeightKg: Weight must be between 30 and 250 kg.")
            .When(x => x.WeightKg.HasValue);

        RuleFor(x => x.Soreness)
            .InclusiveBetween(1, 10)
            .WithMessage("Soreness: Soreness must be between 1 and 10.")
            .When(x => x.Soreness.HasValue);

        RuleFor(x => x.HrvMs)
            .InclusiveBetween(10, 250)
            .WithMessage("HrvMs: HRV must be between 10 and 250 ms.")
            .When(x => x.HrvMs.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .WithMessage("Notes: Notes must be 1000 characters or fewer.")
            .When(x => x.Notes != null);

        RuleFor(x => x)
            .Must(HasAtLeastOneMetric)
            .WithMessage("Entry: At least one metric is required.");
    }

    // Notes deliberately does NOT count as a metric: a row carrying only prose contributes to no tile
    // and no average, and the ROADMAP's rule is ">= 1 metric present".
    private static bool HasAtLeastOneMetric(WellnessEntryRequest r) =>
        r.SleepHours.HasValue
        || r.SleepQuality.HasValue
        || r.RestingHr.HasValue
        || r.WeightKg.HasValue
        || r.Soreness.HasValue
        || r.HrvMs.HasValue;
}
```

- `InclusiveBetween` on a nullable property is the form `TrainingPlanUpdateRequestValidator.cs:21`
  (`RuleFor(x => x.BuildWeeks).InclusiveBetween(1, 8).When(x => x.BuildWeeks.HasValue)`) and
  `LogWorkoutRequestValidator.cs:16` already use — FluentValidation has the `TProperty?` overload, so
  these compile without a cast.
- **No `Program.cs` registration.** The assembly scan at `Program.cs:35` finds this automatically.

**Verify:** `dotnet build api/Bryk.sln` green, warning count unchanged.

---

## Step 4 — `WellnessRangeRequestValidator.cs`

**New file** `api/Bryk.Application/Wellness/Validators/WellnessRangeRequestValidator.cs` — a
member-for-member mirror of `AnalyticsRangeRequestValidator`. Deliberate consistency: do **not** invent
a different range policy for wellness.

```csharp
using FluentValidation;

namespace Bryk.Application.Wellness.Validators;

/// <summary>
/// Range rules for <c>GET /wellness</c>: both bounds required, <c>from ≤ to</c>, span ≤ 400 days, and
/// <c>to</c> not in the future. Mirrors <see cref="Analytics.Validators.AnalyticsRangeRequestValidator"/>
/// member-for-member — same bound, same messages, same source of "today".
/// </summary>
public class WellnessRangeRequestValidator : AbstractValidator<WellnessRangeRequest>
{
    private const int MaxRangeDays = 400;

    public WellnessRangeRequestValidator()
    {
        RuleFor(x => x.From)
            .NotNull().WithMessage("from is required.");

        RuleFor(x => x.To)
            .NotNull().WithMessage("to is required.");

        When(x => x.From.HasValue && x.To.HasValue, () =>
        {
            RuleFor(x => x)
                .Must(x => x.From!.Value <= x.To!.Value)
                .WithMessage("from must be on or before to.");

            RuleFor(x => x)
                .Must(x => x.To!.Value.DayNumber - x.From!.Value.DayNumber <= MaxRangeDays)
                .WithMessage($"range cannot exceed {MaxRangeDays} days.");

            RuleFor(x => x.To)
                .Must(to => to!.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("to cannot be in the future.");
        });
    }
}
```

(The range messages are lower-case and un-prefixed on purpose — they are byte-for-byte the analytics
strings, and the client already renders them.)

**Verify:** `dotnet build api/Bryk.sln` green.

---

## Step 5 — Unit tests: `WellnessEntryRequestValidatorTests.cs`

**New file** `api/Bryk.Application.Tests/Wellness/WellnessEntryRequestValidatorTests.cs` (new folder).
Pure validator test, no host, no stubs. Every boundary below is pinned exactly as `Tasks-20-2.md`
specifies — **do not soften a single one**.

```csharp
using System.Globalization;
using Bryk.Application.Wellness;
using Bryk.Application.Wellness.Validators;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Wellness;

public class WellnessEntryRequestValidatorTests
{
    private static readonly WellnessEntryRequestValidator Validator = new();
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    // A minimal valid entry: a real date plus exactly one metric.
    private static WellnessEntryRequest Entry() => new() { Date = Today, RestingHr = 50 };

    private static decimal Dec(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    [Fact]
    public void Date_Default_IsRejectedWithADateMessage()
    {
        var request = new WellnessEntryRequest { Date = default, RestingHr = 50 };

        var result = Validator.Validate(request);

        result.IsValid.Should().BeFalse();
        // Exactly one message: the future rule is guarded off so a default date does not fire twice.
        result.Errors.Should().ContainSingle();
        result.Errors[0].ErrorMessage.Should().StartWith("Date:");
    }

    [Fact]
    public void Date_Today_IsAccepted()
    {
        var request = Entry();
        request.Date = Today;

        Validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Date_Yesterday_IsAccepted()
    {
        var request = Entry();
        request.Date = Today.AddDays(-1);

        Validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Date_Tomorrow_IsRejected()
    {
        var request = Entry();
        request.Date = Today.AddDays(1);

        var result = Validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("Date:"));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("16", true)]
    [InlineData("-0.01", false)]
    [InlineData("16.01", false)]
    public void SleepHours_BoundsAreInclusive(string value, bool expected)
    {
        var request = Entry();
        request.SleepHours = Dec(value);

        var result = Validator.Validate(request);

        result.IsValid.Should().Be(expected);
        if (!expected)
        {
            result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("SleepHours:"));
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(6, false)]
    public void SleepQuality_BoundsAreInclusive(int value, bool expected)
    {
        var request = Entry();
        request.SleepQuality = value;

        var result = Validator.Validate(request);

        result.IsValid.Should().Be(expected);
        if (!expected)
        {
            result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("SleepQuality:"));
        }
    }

    [Theory]
    [InlineData(25, true)]
    [InlineData(120, true)]
    [InlineData(24, false)]
    [InlineData(121, false)]
    public void RestingHr_BoundsAreInclusive(int value, bool expected)
    {
        var request = Entry();
        request.RestingHr = value;

        var result = Validator.Validate(request);

        result.IsValid.Should().Be(expected);
        if (!expected)
        {
            result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("RestingHr:"));
        }
    }

    [Theory]
    [InlineData("30", true)]
    [InlineData("250", true)]
    [InlineData("29.99", false)]
    [InlineData("250.01", false)]
    public void WeightKg_BoundsAreInclusive(string value, bool expected)
    {
        var request = Entry();
        request.WeightKg = Dec(value);

        var result = Validator.Validate(request);

        result.IsValid.Should().Be(expected);
        if (!expected)
        {
            result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("WeightKg:"));
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(0, false)]
    [InlineData(11, false)]
    public void Soreness_BoundsAreInclusive(int value, bool expected)
    {
        var request = Entry();
        request.Soreness = value;

        var result = Validator.Validate(request);

        result.IsValid.Should().Be(expected);
        if (!expected)
        {
            result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("Soreness:"));
        }
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(250, true)]
    [InlineData(9, false)]
    [InlineData(251, false)]
    public void HrvMs_BoundsAreInclusive(int value, bool expected)
    {
        var request = Entry();
        request.HrvMs = value;

        var result = Validator.Validate(request);

        result.IsValid.Should().Be(expected);
        if (!expected)
        {
            result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("HrvMs:"));
        }
    }

    [Fact]
    public void SingleMetric_IsAccepted()
    {
        // Partial entries are the norm — one metric is a complete, valid day.
        var request = new WellnessEntryRequest { Date = Today, Soreness = 4 };

        Validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void AllMetricsNull_IsRejected()
    {
        var request = new WellnessEntryRequest { Date = Today };

        var result = Validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("Entry:"));
    }

    [Fact]
    public void NotesOnly_IsRejected()
    {
        // Notes is not a metric: prose feeds no tile and no average.
        var request = new WellnessEntryRequest { Date = Today, Notes = "felt rough" };

        var result = Validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("Entry:"));
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void Notes_Over1000Characters_IsRejected(int length, bool expected)
    {
        var request = Entry();
        request.Notes = new string('x', length);

        var result = Validator.Validate(request);

        result.IsValid.Should().Be(expected);
        if (!expected)
        {
            result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("Notes:"));
        }
    }
}
```

The two decimal metrics take their `InlineData` as **strings** parsed with `CultureInfo.InvariantCulture`
rather than `double` literals — `decimal` is not a legal attribute constant, and the string form removes
any doubt about the double→decimal conversion at the boundary values.

**Verify:**
```
dotnet test api/Bryk.sln --filter FullyQualifiedName~WellnessEntryRequestValidatorTests
```
All pass — **7 facts + 26 theory rows = 33 test cases** reported by xUnit. `dotnet build api/Bryk.sln`
still 0 errors with the warning count unchanged.

---

## Step 6 — `WellnessSummaryCalculator.cs` (pure)

**New file** `api/Bryk.Application/Wellness/WellnessSummaryCalculator.cs`. `static`, no I/O, no clock
read, no repository — `today` is a parameter, exactly as the analytics calculators take their inputs.
That is what makes Step 7's arithmetic testable with literal numbers and zero stubs.

```csharp
using Bryk.Domain.Entities;

namespace Bryk.Application.Wellness;

/// <summary>
/// Pure summary math for <c>GET /wellness/summary</c> (ADR-0011 §2). No I/O and no clock read — the
/// caller passes the athlete's entries and <paramref name="today"/>, so the window edges, the averages
/// and the rounding are all deterministic and unit-tested directly, the same shape as
/// <see cref="Analytics.WeeklyLoadCalculator"/>.
///
/// Current window is <c>[today-6, today]</c> (7 days inclusive); the prior window is
/// <c>[today-13, today-7]</c> (7 days inclusive, non-overlapping). An average is taken over the days
/// that CARRY a value — a missing day is missing, never a zero — so a window with no values yields
/// <c>null</c>, not <c>0</c>.
/// </summary>
public static class WellnessSummaryCalculator
{
    public static WellnessSummaryResponse Compute(IReadOnlyList<DailyWellness> entries, DateOnly today)
    {
        var to = today;
        var from = today.AddDays(-6);
        var priorFrom = today.AddDays(-13);
        var priorTo = today.AddDays(-7);

        var current = entries.Where(e => e.Date >= from && e.Date <= to).ToList();
        var prior = entries.Where(e => e.Date >= priorFrom && e.Date <= priorTo).ToList();

        return new WellnessSummaryResponse
        {
            To = to,
            From = from,
            PriorFrom = priorFrom,
            // Integer metrics are cast to decimal before averaging — never integer-divided.
            SleepHours = Summarize(current, prior, e => e.SleepHours),
            SleepQuality = Summarize(current, prior, e => e.SleepQuality),
            RestingHr = Summarize(current, prior, e => e.RestingHr),
            WeightKg = Summarize(current, prior, e => e.WeightKg),
            Soreness = Summarize(current, prior, e => e.Soreness),
            HrvMs = Summarize(current, prior, e => e.HrvMs),
            // Sparse and ascending over the full 14 days; entries outside that span are ignored even if
            // the caller passes them.
            Days = entries
                .Where(e => e.Date >= priorFrom && e.Date <= to)
                .OrderBy(e => e.Date)
                .Select(e => new WellnessDailyPointDto
                {
                    Date = e.Date,
                    SleepHours = e.SleepHours,
                    SleepQuality = e.SleepQuality,
                    RestingHr = e.RestingHr,
                    WeightKg = e.WeightKg,
                    Soreness = e.Soreness,
                    HrvMs = e.HrvMs
                })
                .ToList(),
            // The caller loads exactly the 14-day window, so this answers "has this athlete logged
            // recently" — Task 20-4's Resting HR fallback is keyed on it.
            HasAnyEntries = entries.Count > 0
        };
    }

    private static WellnessMetricSummaryDto Summarize(
        IReadOnlyList<DailyWellness> current,
        IReadOnlyList<DailyWellness> prior,
        Func<DailyWellness, decimal?> select)
    {
        var currentValues = current.Select(select).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var priorValues = prior.Select(select).Where(v => v.HasValue).Select(v => v!.Value).ToList();

        decimal? average = currentValues.Count > 0 ? Math.Round(currentValues.Average(), 2) : null;
        decimal? priorAverage = priorValues.Count > 0 ? Math.Round(priorValues.Average(), 2) : null;

        return new WellnessMetricSummaryDto
        {
            Average = average,
            PriorAverage = priorAverage,
            Delta = average.HasValue && priorAverage.HasValue
                ? Math.Round(average.Value - priorAverage.Value, 2)
                : null,
            DaysWithData = currentValues.Count
        };
    }
}
```

The `e => e.SleepQuality` lambdas bind to `Func<DailyWellness, decimal?>` by implicit `int? → decimal?`
conversion, which is precisely the "average integers as decimal" requirement — no cast needed at the
call site, and no integer division is possible.

**Verify:** `dotnet build api/Bryk.sln` green, 0 new warnings.

---

## Step 7 — Unit tests: `WellnessSummaryCalculatorTests.cs`

**New file** `api/Bryk.Application.Tests/Wellness/WellnessSummaryCalculatorTests.cs`. Anchored on a
**fixed** `today = 2026-07-26` so every number in the file is literal and the suite cannot rot.

```csharp
using Bryk.Application.Wellness;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Wellness;

public class WellnessSummaryCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 7, 26);
    private static readonly Guid AthleteId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static DailyWellness Entry(DateOnly date, decimal? sleepHours = null, int? restingHr = null) => new()
    {
        Id = Guid.NewGuid(),
        AthleteId = AthleteId,
        Date = date,
        SleepHours = sleepHours,
        RestingHr = restingHr
    };

    [Fact]
    public void Compute_NoEntries_ReturnsNullAveragesAndHasAnyEntriesFalse()
    {
        var result = WellnessSummaryCalculator.Compute([], Today);

        foreach (var metric in new[]
                 {
                     result.SleepHours, result.SleepQuality, result.RestingHr,
                     result.WeightKg, result.Soreness, result.HrvMs
                 })
        {
            metric.Average.Should().BeNull();
            metric.PriorAverage.Should().BeNull();
            metric.Delta.Should().BeNull();
            metric.DaysWithData.Should().Be(0);
        }

        result.HasAnyEntries.Should().BeFalse();
        result.Days.Should().BeEmpty();
    }

    [Fact]
    public void Compute_WindowBoundsAreTodayMinusSixAndTodayMinusThirteen()
    {
        var result = WellnessSummaryCalculator.Compute([], Today);

        result.To.Should().Be(new DateOnly(2026, 7, 26));
        result.From.Should().Be(new DateOnly(2026, 7, 20));
        result.PriorFrom.Should().Be(new DateOnly(2026, 7, 13));
    }

    [Fact]
    public void Compute_AveragesOnlyTheDaysThatCarryAValue()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 7, 24), sleepHours: 8m),
            Entry(new DateOnly(2026, 7, 25), restingHr: 50),   // no sleep value on this day
            Entry(new DateOnly(2026, 7, 26), sleepHours: 7m)
        };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.SleepHours.Average.Should().Be(7.5m); // not 5m — the missing day is missing, not a zero
        result.SleepHours.DaysWithData.Should().Be(2);
    }

    [Fact]
    public void Compute_RoundsAveragesToTwoDecimals()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 7, 24), sleepHours: 7m),
            Entry(new DateOnly(2026, 7, 25), sleepHours: 7m),
            Entry(new DateOnly(2026, 7, 26), sleepHours: 8m)
        };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.SleepHours.Average.Should().Be(7.33m);
    }

    [Fact]
    public void Compute_DeltaIsCurrentMinusPrior()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 7, 15), sleepHours: 7m),  // prior window mean 7
            Entry(new DateOnly(2026, 7, 25), sleepHours: 8m),
            Entry(new DateOnly(2026, 7, 26), sleepHours: 7m)   // current window mean 7.5
        };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.SleepHours.Average.Should().Be(7.5m);
        result.SleepHours.PriorAverage.Should().Be(7m);
        result.SleepHours.Delta.Should().Be(0.5m);
    }

    [Fact]
    public void Compute_DeltaIsNullWhenThePriorWindowHasNoData()
    {
        var entries = new[] { Entry(new DateOnly(2026, 7, 25), sleepHours: 8m) };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.SleepHours.Average.Should().Be(8m);
        result.SleepHours.PriorAverage.Should().BeNull();
        result.SleepHours.Delta.Should().BeNull();
    }

    [Fact]
    public void Compute_IntegerMetricsAverageAsDecimal()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 7, 25), restingHr: 48),
            Entry(new DateOnly(2026, 7, 26), restingHr: 49)
        };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.RestingHr.Average.Should().Be(48.5m); // never integer-divided to 48
    }

    [Fact]
    public void Compute_DaysAreSparseAndAscending()
    {
        // Deliberately supplied newest-first — the calculator orders them.
        var entries = new[]
        {
            Entry(new DateOnly(2026, 7, 26), sleepHours: 7m),
            Entry(new DateOnly(2026, 7, 13), sleepHours: 8m)
        };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.Days.Should().HaveCount(2);
        result.Days[0].Date.Should().Be(new DateOnly(2026, 7, 13));
        result.Days[1].Date.Should().Be(new DateOnly(2026, 7, 26));
    }

    [Fact]
    public void Compute_IgnoresEntriesOutsideTheFourteenDayWindow()
    {
        var entries = new[]
        {
            Entry(new DateOnly(2026, 7, 12), sleepHours: 3m), // one day before PriorFrom
            Entry(new DateOnly(2026, 7, 26), sleepHours: 8m)
        };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.SleepHours.Average.Should().Be(8m);      // not 5.5m
        result.SleepHours.PriorAverage.Should().BeNull();
        result.Days.Should().ContainSingle();
        result.Days[0].Date.Should().Be(new DateOnly(2026, 7, 26));
    }

    [Fact]
    public void Compute_TodayIsIncludedInTheCurrentWindow()
    {
        // The off-by-one guard: the current window is inclusive of today.
        var entries = new[] { Entry(Today, sleepHours: 6m) };

        var result = WellnessSummaryCalculator.Compute(entries, Today);

        result.SleepHours.Average.Should().Be(6m);
        result.SleepHours.DaysWithData.Should().Be(1);
    }
}
```

Note on `Compute_IgnoresEntriesOutsideTheFourteenDayWindow`: `HasAnyEntries` is `entries.Count > 0` by
contract, so it is **true** in that test — do not add an assertion that it is false.

**Verify:**
```
dotnet test api/Bryk.sln --filter FullyQualifiedName~WellnessSummaryCalculatorTests
```
All **10 facts** pass with the exact literals above (`7.5m`, `7.33m`, `0.5m`, `48.5m`, `8m`).

---

## Step 8 — `IWellnessService.cs` + `WellnessService.cs` — the read-then-update upsert

**New file** `api/Bryk.Application/Wellness/IWellnessService.cs`:

```csharp
namespace Bryk.Application.Wellness;

/// <summary>
/// The whole wellness surface (ADR-0011 §2). Athlete identity always comes from
/// <see cref="Common.ICurrentUserService"/>; this service never reads or writes
/// <see cref="Bryk.Domain.Entities.Athlete"/> (§1).
/// </summary>
public interface IWellnessService
{
    /// <summary>
    /// Creates or replaces the athlete's entry for <paramref name="date"/>. The route date always wins
    /// over the body's. PUT replaces the whole day: a metric omitted from the request is cleared, not
    /// preserved. Idempotent — re-submitting the same day updates the existing row rather than adding a
    /// second one. 400 on an invalid date or an out-of-range/all-null body.
    /// </summary>
    Task<WellnessEntryResponse> UpsertAsync(DateOnly date, WellnessEntryRequest request, CancellationToken ct = default);

    /// <summary>
    /// The athlete's entries in <c>[from, to]</c>, sparse and ascending by date. Both bounds are
    /// required; <c>from ≤ to</c>, span ≤ 400 days, <c>to</c> not in the future (else 400).
    /// </summary>
    Task<IReadOnlyList<WellnessEntryResponse>> GetRangeAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);

    /// <summary>
    /// The dashboard's one call: 7-day averages ending today, deltas versus the prior 7, and a sparse
    /// 14-day daily series. No parameters — the window is always anchored on today (UTC).
    /// </summary>
    Task<WellnessSummaryResponse> GetSummaryAsync(CancellationToken ct = default);
}
```

**New file** `api/Bryk.Application/Wellness/WellnessService.cs`:

```csharp
using Bryk.Application.Common;
using Bryk.Application.Common.Validation;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentValidation;

namespace Bryk.Application.Wellness;

public class WellnessService(
    ICurrentUserService currentUser,
    IValidator<WellnessEntryRequest> validator,
    IValidator<WellnessRangeRequest> rangeValidator,
    IDailyWellnessRepository wellnessRepo,
    IUnitOfWork unitOfWork) : IWellnessService
{
    public async Task<WellnessEntryResponse> UpsertAsync(DateOnly date, WellnessEntryRequest request, CancellationToken ct = default)
    {
        // The {date} route segment wins over anything in the body, unconditionally — the URL is the
        // identity of the resource being replaced.
        request.Date = date;

        // Validate FIRST, before any repository call, so an invalid request never touches the database.
        await validator.ValidateOrThrowAsync(request, ct);

        var athleteId = currentUser.GetCurrentAthleteId();

        // THIS READ-THEN-WRITE IS THE IDEMPOTENCY GUARANTEE. The {AthleteId, Date} unique index backs it
        // in SQL Server, but the EF InMemory provider the integration suite runs on enforces no unique
        // index (BrykWebApplicationFactory.cs:11-23), so the service must never rely on the database
        // rejecting a duplicate — it must look first (ADR-0011 §2).
        var existing = await wellnessRepo.GetByAthleteAndDateTrackedAsync(athleteId, date, ct);

        DailyWellness entity;
        if (existing is null)
        {
            entity = new DailyWellness
            {
                Id = Guid.NewGuid(),
                AthleteId = athleteId,
                Date = date,
                SleepHours = request.SleepHours,
                SleepQuality = request.SleepQuality,
                RestingHr = request.RestingHr,
                WeightKg = request.WeightKg,
                Soreness = request.Soreness,
                HrvMs = request.HrvMs,
                Notes = request.Notes
            };
            await wellnessRepo.AddAsync(entity, ct);
        }
        else
        {
            // All seven fields, including the nulls: PUT replaces the whole day (ADR-0011 §2).
            existing.SleepHours = request.SleepHours;
            existing.SleepQuality = request.SleepQuality;
            existing.RestingHr = request.RestingHr;
            existing.WeightKg = request.WeightKg;
            existing.Soreness = request.Soreness;
            existing.HrvMs = request.HrvMs;
            existing.Notes = request.Notes;

            wellnessRepo.Update(existing);
            entity = existing;
        }

        // One commit, covering both branches. CreatedAt/UpdatedAt are the interceptor's — never set here.
        await unitOfWork.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task<IReadOnlyList<WellnessEntryResponse>> GetRangeAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        await rangeValidator.ValidateOrThrowAsync(new WellnessRangeRequest { From = from, To = to }, ct);

        var athleteId = currentUser.GetCurrentAthleteId();
        var entries = await wellnessRepo.GetByAthleteInRangeAsync(athleteId, from!.Value, to!.Value, ct);

        // Sparse and already ascending — the repository orders by Date.
        return entries.Select(Map).ToList();
    }

    public async Task<WellnessSummaryResponse> GetSummaryAsync(CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Exactly the 14 days the calculator needs: the current 7-day window plus the prior 7.
        var entries = await wellnessRepo.GetByAthleteInRangeAsync(athleteId, today.AddDays(-13), today, ct);

        return WellnessSummaryCalculator.Compute(entries, today);
    }

    private static WellnessEntryResponse Map(DailyWellness w) => new()
    {
        Id = w.Id,
        Date = w.Date,
        SleepHours = w.SleepHours,
        SleepQuality = w.SleepQuality,
        RestingHr = w.RestingHr,
        WeightKg = w.WeightKg,
        Soreness = w.Soreness,
        HrvMs = w.HrvMs,
        Notes = w.Notes
    };
}
```

Hard constraints re-stated at the point of writing, because they are cheap to violate here:
- The ctor takes **no `IAthleteRepository`** and this file contains no reference to `Athlete`
  (ADR-0011 §1). Step 9 asserts that structurally.
- `ValidateOrThrowAsync`, never FluentValidation's `ValidateAndThrowAsync` (wrong exception type → the
  middleware ignores it → 500 instead of 400).
- Exactly **one** `SaveChangesAsync` per write path, **zero** on every rejection path (validation throws
  before the first repository call).
- `GetSummaryAsync` performs no validation and no write.
- **If this method appears to need a fifth repository member, a column, or an index — STOP and ask**
  (Sr. Dev gate). `IDailyWellnessRepository` is Task 20-1's complete surface and is read-only here.

**Verify:** `dotnet build api/Bryk.sln` green, 0 new warnings. Nothing calls the service yet — this is a
compile-only gate; Step 9 is the behavioural one.

---

## Step 9 — Unit tests: `WellnessServiceTests.cs`

**New file** `api/Bryk.Application.Tests/Wellness/WellnessServiceTests.cs`. Private stubs modelled on
`GoalServiceTests.cs:136–171` — **do not extract a shared stub library**.

```csharp
using Bryk.Application.Common;
using Bryk.Application.Wellness;
using Bryk.Application.Wellness.Validators;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Wellness;

public class WellnessServiceTests
{
    private static readonly Guid AthleteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static WellnessService NewService(StubDailyWellnessRepository repo, StubUnitOfWork uow) =>
        new(new StubCurrentUserService(AthleteId),
            new WellnessEntryRequestValidator(),
            new WellnessRangeRequestValidator(),
            repo,
            uow);

    [Fact]
    public async Task UpsertAsync_WhenTheDayHasNoRow_AddsForTheCurrentAthleteAndSavesOnce()
    {
        var repo = new StubDailyWellnessRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);
        var date = Today.AddDays(-1);

        var result = await service.UpsertAsync(date, new WellnessEntryRequest
        {
            SleepHours = 7.5m,
            RestingHr = 48,
            Soreness = 3
        });

        repo.Added.Should().NotBeNull();
        repo.Added!.AthleteId.Should().Be(AthleteId);
        repo.Added.Date.Should().Be(date);
        repo.Added.SleepHours.Should().Be(7.5m);
        repo.Added.RestingHr.Should().Be(48);
        repo.Added.Soreness.Should().Be(3);
        repo.Updated.Should().BeNull();
        uow.SaveCount.Should().Be(1);
        result.Id.Should().Be(repo.Added.Id);
        result.Date.Should().Be(date);
    }

    [Fact]
    public async Task UpsertAsync_WhenTheDayAlreadyHasARow_MutatesItAndDoesNotAdd()
    {
        // THE IDEMPOTENCY FACT at the unit level: the service looks first and updates in place.
        var date = Today.AddDays(-1);
        var existing = new DailyWellness
        {
            Id = Guid.NewGuid(),
            AthleteId = AthleteId,
            Date = date,
            SleepHours = 7m
        };
        var repo = new StubDailyWellnessRepository { ToReturn = existing };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var result = await service.UpsertAsync(date, new WellnessEntryRequest
        {
            SleepHours = 8m,
            RestingHr = 47
        });

        repo.Added.Should().BeNull();
        existing.SleepHours.Should().Be(8m);
        existing.RestingHr.Should().Be(47);
        repo.Updated.Should().BeSameAs(existing);
        uow.SaveCount.Should().Be(1);
        result.Id.Should().Be(existing.Id);
    }

    [Fact]
    public async Task UpsertAsync_ClearsAMetricOmittedFromTheRequest()
    {
        // PUT replaces the whole day (ADR-0011 §2).
        var date = Today.AddDays(-1);
        var existing = new DailyWellness
        {
            Id = Guid.NewGuid(),
            AthleteId = AthleteId,
            Date = date,
            RestingHr = 50
        };
        var repo = new StubDailyWellnessRepository { ToReturn = existing };
        var service = NewService(repo, new StubUnitOfWork());

        await service.UpsertAsync(date, new WellnessEntryRequest { SleepHours = 7m });

        existing.SleepHours.Should().Be(7m);
        existing.RestingHr.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_UsesTheRouteDateNotTheBodyDate()
    {
        var repo = new StubDailyWellnessRepository();
        var service = NewService(repo, new StubUnitOfWork());
        var routeDate = Today.AddDays(-1);

        await service.UpsertAsync(routeDate, new WellnessEntryRequest
        {
            Date = Today.AddDays(-5), // the body lies; the route wins
            RestingHr = 50
        });

        repo.Added!.Date.Should().Be(routeDate);
    }

    [Fact]
    public async Task UpsertAsync_FutureDate_ThrowsValidationExceptionAndPersistsNothing()
    {
        var repo = new StubDailyWellnessRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.UpsertAsync(Today.AddDays(1), new WellnessEntryRequest { RestingHr = 50 });

        await act.Should().ThrowExactlyAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.Added.Should().BeNull();
        repo.Updated.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task UpsertAsync_AllNullMetrics_ThrowsValidationExceptionAndPersistsNothing()
    {
        var repo = new StubDailyWellnessRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.UpsertAsync(Today, new WellnessEntryRequest());

        await act.Should().ThrowExactlyAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.Added.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public void UpsertAsync_DoesNotResolveAnAthleteRepository()
    {
        // ADR-0011 §1 — cheap, permanent, structural. Wellness never reads or writes Athlete.
        var parameters = typeof(WellnessService).GetConstructors().Single().GetParameters();

        parameters.Should().NotContain(p => p.ParameterType == typeof(IAthleteRepository));
    }

    [Fact]
    public async Task GetRangeAsync_MissingBounds_ThrowsValidationException()
    {
        var repo = new StubDailyWellnessRepository();
        var service = NewService(repo, new StubUnitOfWork());

        var act = () => service.GetRangeAsync(null, null);

        await act.Should().ThrowExactlyAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.RangeQuery.Should().BeNull(); // the repository was never called
    }

    [Fact]
    public async Task GetRangeAsync_FromAfterTo_ThrowsValidationException()
    {
        var repo = new StubDailyWellnessRepository();
        var service = NewService(repo, new StubUnitOfWork());

        var act = () => service.GetRangeAsync(Today, Today.AddDays(-1));

        await act.Should().ThrowExactlyAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.RangeQuery.Should().BeNull();
    }

    [Fact]
    public async Task GetSummaryAsync_LoadsExactlyFourteenDaysEndingToday()
    {
        var repo = new StubDailyWellnessRepository();
        var service = NewService(repo, new StubUnitOfWork());

        await service.GetSummaryAsync();

        repo.RangeQuery.Should().NotBeNull();
        repo.RangeQuery!.Value.From.Should().Be(Today.AddDays(-13));
        repo.RangeQuery.Value.To.Should().Be(Today);
    }

    private sealed class StubCurrentUserService(Guid athleteId) : ICurrentUserService
    {
        public Guid GetCurrentAthleteId() => athleteId;
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class StubDailyWellnessRepository : IDailyWellnessRepository
    {
        public DailyWellness? ToReturn { get; init; }
        public IReadOnlyList<DailyWellness> RangeResult { get; init; } = [];

        public DailyWellness? Added { get; private set; }
        public DailyWellness? Updated { get; private set; }
        public (Guid AthleteId, DateOnly From, DateOnly To)? RangeQuery { get; private set; }

        public Task<DailyWellness?> GetByAthleteAndDateTrackedAsync(Guid athleteId, DateOnly date, CancellationToken ct = default)
            => Task.FromResult(ToReturn);

        public Task<IReadOnlyList<DailyWellness>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly from, DateOnly to, CancellationToken ct = default)
        {
            RangeQuery = (athleteId, from, to);
            return Task.FromResult(RangeResult);
        }

        public Task AddAsync(DailyWellness entity, CancellationToken ct = default)
        {
            Added = entity;
            return Task.CompletedTask;
        }

        public void Update(DailyWellness entity) => Updated = entity;
    }
}
```

If the stub does not compile, the four member signatures came from Step 0's reading of
`IDailyWellnessRepository.cs` — fix the **stub** to match the shipped interface. **Do not edit the
interface** (Task 20-1's file, read-only here).

**Verify:**
```
dotnet test api/Bryk.sln --filter FullyQualifiedName~WellnessServiceTests
```
All **10 facts** pass. Together with Steps 5 and 7 this is **53 new `Bryk.Application.Tests` cases**
(33 + 10 + 10); run
`dotnet test api/Bryk.sln --filter FullyQualifiedName~Bryk.Application.Tests.Wellness` to confirm the
whole folder is green.

---

## Step 10 — `WellnessController.cs` — the **first** layer of the date defence

> ### STOP — the one "fix" you must not apply
> If a malformed date does not behave the way you expect, **do not** touch
> `builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);`
> at `Program.cs:32–33`. Turning that off would re-enable the automatic model-state 400 for **every
> endpoint in the application** — a cross-cutting change to the whole request pipeline and the shipped
> error shape, well outside this task's fence (`Tasks-20-2.md` Non-goals). It is a Sr. Dev gate:
> **STOP and ask.** Guard the parameter instead, with both layers below.

**New file** `api/Bryk.API/Controllers/WellnessController.cs`:

```csharp
using Asp.Versioning;
using Bryk.Application.Wellness;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class WellnessController(IWellnessService wellnessService) : ControllerBase
{
    /// <summary>
    /// Creates or replaces the current athlete's wellness entry for <paramref name="date"/>. Returns
    /// <b>200</b> for both create and update: the URL is client-chosen and the call is idempotent, so
    /// the response is identical whichever branch ran. PUT replaces the whole day — a metric omitted
    /// from the body is cleared.
    ///
    /// The date is guarded twice on purpose. <c>SuppressModelStateInvalidFilter</c> is on app-wide
    /// (<c>Program.cs:32–33</c>), so a route segment that fails to bind produces no 400 — it arrives as
    /// <c>default(DateOnly)</c> and the action still runs. The <c>:datetime</c> route constraint makes a
    /// non-date segment a <b>404</b> before any binding happens; a segment that satisfies the constraint
    /// but still fails <c>DateOnly</c> binding falls through to the validator's <c>default</c> rule and
    /// returns <b>400</b>. Both layers are required; neither alone is sufficient.
    ///
    /// 400 also for a future date, an out-of-range metric, or a body with no metric at all.
    /// </summary>
    [HttpPut("{date:datetime}")]
    public async Task<IActionResult> PutAsync(DateOnly date, [FromBody] WellnessEntryRequest request, CancellationToken cancellationToken)
    {
        WellnessEntryResponse result = await wellnessService.UpsertAsync(date, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns the current athlete's wellness entries in <c>[from, to]</c> — sparse (days with no entry
    /// are simply absent) and ascending by date. Both bounds are required; the range must be ≤ 400 days,
    /// <c>from ≤ to</c>, and <c>to</c> not in the future (else 400).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRangeAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<WellnessEntryResponse> result = await wellnessService.GetRangeAsync(from, to, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns the dashboard summary in one call: per-metric 7-day averages ending today, deltas versus
    /// the prior 7 days, and a sparse 14-day daily series for the sparklines. Always 200 — an athlete
    /// with no entries gets null averages and <c>hasAnyEntries: false</c>, never zeros.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummaryAsync(CancellationToken cancellationToken)
    {
        WellnessSummaryResponse result = await wellnessService.GetSummaryAsync(cancellationToken);
        return Ok(result);
    }
}
```

- Routes resolve to `PUT /api/v1/wellness/{date}`, `GET /api/v1/wellness`, `GET /api/v1/wellness/summary`.
  `summary` is a literal segment on `GET` and `{date:datetime}` is on `PUT`, so there is no ambiguity —
  **do not** add an `[HttpGet("{date}")]` action "for symmetry" (not in the contract).
- No try/catch, no `ModelState` inspection, no `[FromRoute]` attribute (route-name binding is
  conventional here, as in `GoalsController`), `IActionResult` returns.
- The athlete id never comes from the route, the query or the body.

**Verify:** `dotnet build api/Bryk.sln` green. (The endpoint is not yet reachable — no DI registration
until Step 11.)

---

## Step 11 — `Program.cs` — append exactly one line, then a full build gate

**Edit** `api/Bryk.API/Program.cs` — **append only**. Anchor on the **text**, not the number: add the
line directly after the last entry of the services block,
`builder.Services.AddScoped<Bryk.Application.ActivityFiles.IActivityFileService, Bryk.Application.ActivityFiles.ActivityFileService>();`
(L126 in the Phase-19 tree; **L127** once Task 20-1's `IDailyWellnessRepository` line lands at L108).

```csharp
builder.Services.AddScoped<Bryk.Application.Wellness.IWellnessService, Bryk.Application.Wellness.WellnessService>();
```

- Fully-qualified, matching the surrounding entries (L118–126) — no new `using` directive.
- **No validator registrations.** `AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>()`
  (L35) finds `WellnessEntryRequestValidator` and `WellnessRangeRequestValidator` automatically. If you
  find yourself adding `AddScoped<IValidator<...>>`, delete it — a duplicate registration is a silent
  behaviour change.
- **Do not touch** 20-1's repository line above it, and do not reorder or reformat anything.

**Verify:**
```
git diff api/Bryk.API/Program.cs
dotnet build api/Bryk.sln --no-incremental
dotnet test api/Bryk.sln
```
- `git diff` on `Program.cs` shows **exactly one added line**, nothing else.
- Build 0 errors; warning count still **16** on the clean compile. A new warning from a file this task
  added is a **STOP and ask**.
- Test count = (Step 0 confirmed baseline, expected 350) **+ 53** = expected **403**, zero failures.
  Every pre-existing test still green — production code is complete and nothing regressed.

---

## Step 12 — Integration tests: the idempotency proof and the two date guards

**New file** `api/Bryk.API.Tests/Wellness/WellnessControllerTests.cs` — alongside 20-1's
`DailyWellnessRepositoryTests.cs` in the same folder; do not touch that file.

Write the class skeleton plus **only** the three headline tests in this step. These are the ones the
task exists for; everything else is appended once this gate passes.

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Wellness;
using Bryk.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bryk.API.Tests.Wellness;

public class WellnessControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed class ApiError
    {
        public int Status { get; set; }
        public string? Error { get; set; }
        public string[]? Errors { get; set; }
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static string Url(DateOnly date) => $"/api/v1/wellness/{date:yyyy-MM-dd}";

    [Fact]
    public async Task Put_CreatesTheDayAndReturnsOkWithTheStoredValues()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();
        var yesterday = Today.AddDays(-1);

        var response = await client.PutAsJsonAsync(Url(yesterday), new
        {
            sleepHours = 7.5m,
            restingHr = 48,
            soreness = 3
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK); // 200, not 201 — the URL is client-chosen
        var body = await response.Content.ReadFromJsonAsync<WellnessEntryResponse>(JsonOptions);
        body!.Id.Should().NotBeEmpty();
        body.Date.Should().Be(yesterday);
        body.SleepHours.Should().Be(7.5m);
        body.RestingHr.Should().Be(48);
        body.Soreness.Should().Be(3);
    }

    [Fact]
    public async Task Put_Twice_UpdatesInPlaceAndLeavesExactlyOneRow()
    {
        // THE HEADLINE FACT. Idempotency is proven by counting rows through the API, NOT by asserting a
        // duplicate insert throws: the {AthleteId, Date} unique index is real in SQL Server but the EF
        // InMemory provider enforces no unique index (BrykWebApplicationFactory.cs:11-23), so a
        // "duplicate throws" test would pass for the wrong reason here and fail against SQL Server.
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();
        var day = Today.AddDays(-1);

        var first = await client.PutAsJsonAsync(Url(day), new { sleepHours = 7m });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PutAsJsonAsync(Url(day), new { sleepHours = 8m, restingHr = 47 });
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var range = await client.GetFromJsonAsync<List<WellnessEntryResponse>>(
            $"/api/v1/wellness?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}", JsonOptions);

        range.Should().ContainSingle();
        range![0].SleepHours.Should().Be(8m);
        range[0].RestingHr.Should().Be(47);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.DailyWellness.CountAsync(w => w.Date == day)).Should().Be(1);
    }

    [Fact]
    public async Task Put_MalformedDateSegment_Returns404()
    {
        // LAYER ONE: the {date:datetime} route constraint rejects the segment before any binding
        // happens. Pinned precisely because SuppressModelStateInvalidFilter (Program.cs:32-33) means an
        // UNCONSTRAINED route would have bound 0001-01-01 and RUN THE ACTION. A 200 here is a data bug.
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/v1/wellness/not-a-date", new { sleepHours = 7m });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_MinValueDateSegment_Returns400WithADateMessage()
    {
        // LAYER TWO: 0001-01-01 is a well-formed date, so it satisfies the route constraint and binds
        // cleanly — the validator's default(DateOnly) rule is the only thing that stops it.
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/v1/wellness/0001-01-01", new { sleepHours = 7m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        error!.Errors.Should().NotBeNull();
        error.Errors![0].Should().StartWith("Date:");
    }
}
```

**Verify — this is the actual gate for this task's headline risks:**
```
dotnet test api/Bryk.sln --filter FullyQualifiedName~WellnessControllerTests
```
All 4 facts pass, with **404** for `not-a-date` and **400** for `0001-01-01`.

If `Put_MalformedDateSegment_Returns404` returns **400**, the `:datetime` constraint is missing from the
`[HttpPut]` template. If it returns **200**, both layers are broken and the API just wrote `0001-01-01`
— fix the controller and the validator, **never** `ApiBehaviorOptions`. If
`Put_Twice_...` finds two rows, the service is inserting blind — fix `UpsertAsync`'s read-then-update,
and do **not** "fix" it by leaning on the database.

---

## Step 13 — Integration tests: the remaining PUT surface, including the untouched `Athlete` row

Append to `WellnessControllerTests.cs`, after the Step 12 tests.

```csharp
    [Fact]
    public async Task Put_FutureDate_Returns400WithADateMessage()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(Url(Today.AddDays(1)), new { sleepHours = 7m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        error!.Errors![0].Should().StartWith("Date:");
    }

    [Fact]
    public async Task Put_OutOfRangeMetric_Returns400WithTheFieldName()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(Url(Today), new { restingHr = 200 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        // The ROADMAP's "field messages" criterion: ValidateOrThrowAsync drops property names, so the
        // message has to carry its own.
        error!.Errors.Should().Contain(e => e.StartsWith("RestingHr:"));
    }

    [Fact]
    public async Task Put_EmptyBody_Returns400WithTheEntryMessage()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(Url(Today), new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        error!.Errors.Should().Contain(e => e.StartsWith("Entry:"));
    }

    [Fact]
    public async Task Put_DoesNotModifyTheAthleteRow()
    {
        // ADR-0011 §1 — wellness is independent of Athlete and never writes back to it.
        await using var factory = new BrykWebApplicationFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Athletes.Add(new Athlete
            {
                Id = BrykWebApplicationFactory.TestAthleteId,
                Name = "Test Athlete",
                Gender = Gender.Male,
                DateOfBirth = new DateOnly(1990, 1, 1),
                HeightCm = 180,
                WeightKg = 70m,
                RestingHr = 55,
                TypicalWeeklyHours = 10,
                Methodology = MethodologyChoice.Polarized
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var response = await client.PutAsJsonAsync(Url(Today), new { restingHr = 44, weightKg = 68.5m });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var freshScope = factory.Services.CreateScope();
        var freshDb = freshScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var athlete = await freshDb.Athletes.AsNoTracking()
            .SingleAsync(a => a.Id == BrykWebApplicationFactory.TestAthleteId);

        athlete.RestingHr.Should().Be(55);
        athlete.WeightKg.Should().Be(70m);
    }
```

`Put_DoesNotModifyTheAthleteRow` needs `using Bryk.Domain.Entities;` added to the file's using block for
`Athlete`, `Gender` and `MethodologyChoice`.

**Verify:**
```
dotnet test api/Bryk.sln --filter FullyQualifiedName~WellnessControllerTests
```
All 4 tests added in this step pass, plus the 4 from Step 12 (**8 in the file so far**).

---

## Step 14 — Integration tests: range and summary

Append to `WellnessControllerTests.cs`, after the Step 13 tests — this closes out the file.

```csharp
    [Fact]
    public async Task Get_Range_ReturnsOnlyDaysWithEntriesAscending()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();
        var d3 = Today.AddDays(-3);
        var d2 = Today.AddDays(-2);
        var d1 = Today.AddDays(-1);

        await client.PutAsJsonAsync(Url(d3), new { sleepHours = 6m });
        await client.PutAsJsonAsync(Url(d2), new { sleepHours = 7m });
        await client.PutAsJsonAsync(Url(d1), new { sleepHours = 8m });

        var range = await client.GetFromJsonAsync<List<WellnessEntryResponse>>(
            $"/api/v1/wellness?from={d3:yyyy-MM-dd}&to={d2:yyyy-MM-dd}", JsonOptions);

        range.Should().HaveCount(2);
        range.Should().BeInAscendingOrder(e => e.Date);
        range![0].Date.Should().Be(d3);
        range[1].Date.Should().Be(d2);
    }

    [Fact]
    public async Task Get_Range_MissingBounds_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/wellness");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_Range_FromAfterTo_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/wellness?from={Today:yyyy-MM-dd}&to={Today.AddDays(-1):yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_Summary_WithNoEntries_ReturnsNullAveragesAndHasAnyEntriesFalse()
    {
        // A fresh factory means a fresh database, so this athlete has never logged wellness.
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var summary = await client.GetFromJsonAsync<WellnessSummaryResponse>("/api/v1/wellness/summary", JsonOptions);

        summary!.HasAnyEntries.Should().BeFalse();
        summary.SleepHours.Average.Should().BeNull();   // null, never 0
        summary.SleepHours.PriorAverage.Should().BeNull();
        summary.SleepHours.Delta.Should().BeNull();
        summary.SleepHours.DaysWithData.Should().Be(0);
        summary.Days.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_Summary_ReturnsTheSevenDayAverageAndTheDailySeries()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await client.PutAsJsonAsync(Url(Today), new { sleepHours = 8m });
        await client.PutAsJsonAsync(Url(Today.AddDays(-1)), new { sleepHours = 7m });

        var summary = await client.GetFromJsonAsync<WellnessSummaryResponse>("/api/v1/wellness/summary", JsonOptions);

        summary!.HasAnyEntries.Should().BeTrue();
        summary.SleepHours.Average.Should().Be(7.5m);
        summary.SleepHours.DaysWithData.Should().Be(2);
        summary.Days.Should().HaveCount(2);
        summary.Days.Should().BeInAscendingOrder(d => d.Date);
    }

    [Fact]
    public async Task Get_Summary_DeltaIsNullWithNoPriorWeekData()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await client.PutAsJsonAsync(Url(Today), new { sleepHours = 8m });
        await client.PutAsJsonAsync(Url(Today.AddDays(-1)), new { sleepHours = 7m });

        var summary = await client.GetFromJsonAsync<WellnessSummaryResponse>("/api/v1/wellness/summary", JsonOptions);

        summary!.SleepHours.PriorAverage.Should().BeNull();
        summary.SleepHours.Delta.Should().BeNull(); // never fabricated as 0
    }
}
```

**Verify:**
```
dotnet test api/Bryk.sln --filter FullyQualifiedName~WellnessControllerTests
```
All 6 tests added in this step pass, plus the 8 from Steps 12–13 — **14 tests total** in
`WellnessControllerTests.cs`.

---

## Step 15 — Runtime smoke against the dev stack

A green suite proves the *service* is idempotent on a provider with no unique index. It does **not**
prove the endpoint behaves against SQL Server with the index live, and it does not prove the route
constraint against the real host's routing table. Run the sequence below before committing.

**Before you start:**
- **Stop any already-running API instance.** A live host holds `Bryk.Infrastructure.dll` open and the
  next `dotnet build` fails with **MSB3027 / MSB3021** ("cannot copy … being used by another process").
  That is a file lock, not a code error — stop the host and rebuild. Likewise, stop the API again before
  running `dotnet build`/`dotnet test` in Step 16.
- The dev database must already have the `DailyWellness` table — i.e. **Task 20-1's approved migration
  has been applied**. If it has not, every wellness call returns 500 with *"Invalid object name
  'DailyWellness'"*. The fix is 20-1's Sr. Dev-gated `dotnet ef database update`, **not** a code change
  here. If that approval has not happened, **STOP and ask**; do not apply a migration to satisfy a smoke
  test.

**Start the API** (the `DevAuth` stub throws outside Development, and user-secrets carry
`ConnectionStrings:DefaultConnection` + `DevAuth:CurrentAthleteId`):

```powershell
cd C:\Projects\Bryk\Site\Bryk\api\Bryk.API
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run
```

**In a second PowerShell 7 window** (`-SkipCertificateCheck` for the dev cert, `-SkipHttpErrorCheck` so
4xx responses come back instead of throwing; on Windows PowerShell 5.1 use `curl.exe -k -i` instead):

```powershell
$base  = 'https://localhost:60129/api/v1/wellness'
$today = [DateTime]::UtcNow.ToString('yyyy-MM-dd')
$yday  = [DateTime]::UtcNow.AddDays(-1).ToString('yyyy-MM-dd')
$tom   = [DateTime]::UtcNow.AddDays(1).ToString('yyyy-MM-dd')

function Call($method, $uri, $json) {
  $r = Invoke-WebRequest -SkipCertificateCheck -SkipHttpErrorCheck -Method $method -Uri $uri `
       -ContentType 'application/json' -Body $json
  "$($r.StatusCode)  $($r.Content)"
}
```

Run these **in order** — the summary check must come first, before any PUT gives the dev athlete data:

| # | Call | Expected |
|---|---|---|
| 1 | `Call GET "$base/summary" $null` | **200**, `hasAnyEntries: false`, `sleepHours.average: null` (not `0`), `days: []`. *(If the dev athlete already has wellness rows from an earlier session this is not meaningful — read the body and move on; there is no DELETE endpoint and you must not add one.)* |
| 2 | `Call PUT "$base/$yday" '{"sleepHours":7.5,"restingHr":48,"soreness":3}'` | **200**, body echoes the three values with a non-empty `id` |
| 3 | `Call PUT "$base/$yday" '{"sleepHours":8,"restingHr":47}'` | **200** |
| 4 | `Call GET "$base?from=$yday&to=$yday" $null` | **200**, **exactly one** entry, `sleepHours: 8`, `restingHr: 47`, `soreness: null` — idempotent upsert **and** whole-day replacement, this time with the unique index actually live |
| 5 | `Call PUT "$base/$yday" '{}'` | **400**, `errors[0]` == `"Entry: At least one metric is required."` |
| 6 | `Call PUT "$base/$tom" '{"sleepHours":7}'` | **400**, `errors[0]` starts `"Date:"` |
| 7 | `Call PUT "$base/not-a-date" '{"sleepHours":7}'` | **404** — the route constraint, *not* a 400. A 400 means the `:datetime` constraint is missing; a **200** means a bad date reached the database — **stop and fix** |
| 8 | `Call PUT "$base/0001-01-01" '{"sleepHours":7}'` | **400**, `errors[0]` starts `"Date:"` — the validator, the second layer |
| 9 | `Call PUT "$base/$yday" '{"sleepHours":16.01}'` | **400** `"SleepHours: Sleep must be between 0 and 16 hours."` |
| 10 | `Call PUT "$base/$yday" '{"sleepQuality":6}'` | **400** `"SleepQuality: Sleep quality must be between 1 and 5."` |
| 11 | `Call PUT "$base/$yday" '{"restingHr":200}'` | **400** `"RestingHr: Resting HR must be between 25 and 120 bpm."` |
| 12 | `Call PUT "$base/$yday" '{"weightKg":29.99}'` | **400** `"WeightKg: Weight must be between 30 and 250 kg."` |
| 13 | `Call PUT "$base/$yday" '{"soreness":11}'` | **400** `"Soreness: Soreness must be between 1 and 10."` |
| 14 | `Call PUT "$base/$yday" '{"hrvMs":9}'` | **400** `"HrvMs: HRV must be between 10 and 250 ms."` |
| 15 | `Call PUT "$base/$yday" ('{"restingHr":48,"notes":"' + ('x' * 1001) + '"}')` | **400** `"Notes: Notes must be 1000 characters or fewer."` |
| 16 | `Call GET $base $null` | **400**, `errors` contains `"from is required."` and `"to is required."` |
| 17 | `Call GET "$base?from=$today&to=$yday" $null` | **400** `"from must be on or before to."` |
| 18 | `Call GET "$base/summary" $null` | **200**, `hasAnyEntries: true`, `sleepHours.average: 8`, `daysWithData: 1`, `delta: null`, `days` has one point |
| 19 | *(optional)* `Call PUT "$base/${yday}T10:00:00" '{"sleepHours":7}'` | **400** or **404** — never 200. A date-with-time segment satisfies `:datetime` but fails `DateOnly` binding, which is exactly the case layer two exists for |

**The `Athlete` row must be provably untouched.** Before step 2 and after step 18, read the row directly
(SSMS / `sqlcmd`), using the `DevAuth:CurrentAthleteId` value from user-secrets:

```sql
SELECT RestingHr, WeightKg FROM Athletes WHERE Id = '<DevAuth:CurrentAthleteId>';
SELECT COUNT(*) FROM DailyWellness WHERE AthleteId = '<DevAuth:CurrentAthleteId>' AND [Date] = '<yesterday>';
```

The two `Athletes` values must be **identical** before and after (ADR-0011 §1 — the service takes no
`IAthleteRepository`, so any change here means something outside this task's fence moved), and the
`DailyWellness` count must be **1** despite the repeated PUTs.

**Then stop the API (Ctrl+C)** before Step 16 — otherwise the build fails on the locked
`Bryk.Infrastructure.dll`.

**Verify:** every row of the table above matches, both SQL checks pass, and the API shut down cleanly.

---

## Step 16 — Final verification, diff-stat sanity, and commit

Run the full command set from `Tasks-20-2.md` (API stopped):

```
dotnet build api/Bryk.sln --no-incremental
dotnet test api/Bryk.sln
cd ui; pnpm run build
cd ui; pnpm exec vitest run --no-file-parallelism
```

- `dotnet build` — 0 errors, warnings **16** on the clean compile (14 on an incremental one, which skips
  `Bryk.API.Tests` — compare like for like). A new warning from a file this task added is a **STOP and
  ask**.
- `dotnet test api/Bryk.sln` — every existing test still green, plus this task's **67 new test cases**:
  53 in `Bryk.Application.Tests` (33 validator, incl. theory rows + 10 calculator + 10 service) and 14 in
  `Bryk.API.Tests`. Expected total **417** if Step 0's baseline of 350 was confirmed; if the baseline
  differed, the expected total is *(confirmed baseline) + 67*. Zero failures either way. (The Task doc's
  "roughly forty more" counts test *methods*; xUnit counts each `[InlineData]` row, so the reported
  number is higher — the gate is "rises by at least 40, zero failures".)
- `pnpm run build` green and `pnpm exec vitest run --no-file-parallelism` at **exactly 288 tests / 61
  files**, unchanged. This task touches no `ui/` file — if that number moved, something outside scope
  changed; stop and investigate before committing. (Project memory: a Vitest *worker crash* with all
  tests passing is transient — re-run once before debugging.)
- `git status` / `git add -A && git diff --cached --stat` — confirm **only** these files appear:
  - `api/Bryk.Application/Wellness/WellnessEntryRequest.cs` (new)
  - `api/Bryk.Application/Wellness/WellnessRangeRequest.cs` (new)
  - `api/Bryk.Application/Wellness/WellnessResponses.cs` (new)
  - `api/Bryk.Application/Wellness/Validators/WellnessEntryRequestValidator.cs` (new)
  - `api/Bryk.Application/Wellness/Validators/WellnessRangeRequestValidator.cs` (new)
  - `api/Bryk.Application/Wellness/WellnessSummaryCalculator.cs` (new)
  - `api/Bryk.Application/Wellness/IWellnessService.cs` (new)
  - `api/Bryk.Application/Wellness/WellnessService.cs` (new)
  - `api/Bryk.API/Controllers/WellnessController.cs` (new)
  - `api/Bryk.API/Program.cs` (extended — exactly **1** added line)
  - `api/Bryk.Application.Tests/Wellness/WellnessEntryRequestValidatorTests.cs` (new)
  - `api/Bryk.Application.Tests/Wellness/WellnessSummaryCalculatorTests.cs` (new)
  - `api/Bryk.Application.Tests/Wellness/WellnessServiceTests.cs` (new)
  - `api/Bryk.API.Tests/Wellness/WellnessControllerTests.cs` (new)
- **Absent-file check — run it explicitly, do not eyeball it:**
  ```
  git diff --cached --stat -- api/Bryk.Domain/Entities/Athlete.cs api/Bryk.Application/Onboarding/OnboardingService.cs api/Bryk.Application/Profile/ProfileService.cs
  ```
  must print **nothing** (ADR-0011 §1). Likewise nothing for
  `api/Bryk.Domain/`, `api/Bryk.Infrastructure/`, `ui/`, `api/Bryk.API/Middleware/ExceptionHandlingMiddleware.cs`,
  any analytics/load calculator (`PmcCalculator.cs`, `AcwrCalculator.cs`, `LoadCalculator.cs`,
  `TimeInZoneCalculator.cs`, `AnalyticsService.cs`), any migration, or any `*.csproj`. If any of these
  appears — **STOP**, that is scope creep past `Tasks-20-2.md`'s Non-goals fence.
- Re-confirm the review checklist from `Tasks-20-2.md` by eye:
  - PUT carries **both** guards — `{date:datetime}` in the route template *and* the validator's
    `default(DateOnly)` rule — each with its own pinned test (404 and 400).
  - Every validation message names its field (`"SleepHours: …"`), because `ValidateOrThrowAsync` drops
    property names; the range messages are the analytics strings verbatim.
  - Bounds are inclusive and exactly the ROADMAP's numbers, both edges pinned.
  - An all-null body is 400 `"Entry: …"`; notes alone does not qualify as a metric.
  - The upsert is a read-then-update in the **service**, commits **once**, zero saves on rejection, and
    **no test asserts a duplicate insert throws**.
  - `WellnessService`'s ctor takes no `IAthleteRepository` (asserted structurally) and
    `Put_DoesNotModifyTheAthleteRow` passes.
  - `WellnessSummaryCalculator` is `static` and pure — takes `today` as a parameter, reads no clock, no
    repository, no configuration.
  - `Program.cs` diff is exactly one `AddScoped` line; no validator registration was added.
  - The controller is thin: no try/catch, no `ModelState` inspection, `IActionResult` returns, XML
    `<summary>` on all three actions.
- Commit with the message from `Tasks-20-2.md` — **no AI co-author trailer** (project convention):

```
feat: wellness endpoints - idempotent per-day upsert, range and 7-day summary

PUT /api/v1/wellness/{date} is the whole write surface: one idempotent
per-day upsert that replaces the day, so re-submitting updates rather than
duplicating. That guarantee lives in the service as a read-then-update, not in
the database - the {AthleteId, Date} unique index is real, but the InMemory
provider the integration suite runs on enforces no unique index, so the test
that proves idempotency PUTs twice and counts rows instead of catching a
constraint violation.

The date parameter is guarded twice on purpose. SuppressModelStateInvalidFilter
is on app-wide, so a route segment that fails to bind does not produce a 400 -
it silently arrives as default(DateOnly) and the action still runs. The
:datetime route constraint turns a non-date segment into a 404 before binding,
and the validator rejects default(DateOnly) and any future date with a
field-prefixed message. Every metric bound is the ROADMAP's number, inclusive,
and only applies when the metric is present; an all-null body is a 400 rather
than an empty row, and notes alone does not count as a metric.

GET /wellness?from=&to= mirrors the analytics range contract exactly (both
bounds required, from <= to, 400-day span, no future to). GET /wellness/summary
carries 7-day averages, deltas versus the prior 7 and a sparse 14-day daily
series in one call, because the dashboard fills four tiles from it and
Sparkline needs at least two points. The summary math is a pure static
calculator taking today as a parameter, so the averages, the rounding and the
window edges are pinned with literal numbers. Nothing here reads or writes
Athlete: the service takes no IAthleteRepository, and a test asserts it
(ADR-0011 1).
```
