# Impl 17-1 — Build order: Event & Goal GET endpoints + linked-plan reverse lookup

**Executor:** architect-implementer (per CLAUDE.md).
**Acceptance contract:** `md/Tasks-17-1.md`. **Decision lock:** ADR-0003 (`TrainingPlan.EventId` field
shape) + ROADMAP Phase 17 "Decisions needed" (quantitative goal progress deferred; plan↔event write
surface waits for Phase 18).
**Scope:** Backend only. No migration, no new package.

This is the step-by-step build order. Execute top-to-bottom; each step's verification is the gate to
the next. Commit once at the end with the message in `Tasks-17-1.md`.

## Step 0 — Pre-flight

- `git status` clean on `main`. `dotnet build api/Bryk.sln` green (baseline).
- Re-read `md/Tasks-17-1.md` in full. Open in editor:
  `api/Bryk.Application/Events/EventService.cs`, `IEventService.cs`, `EventResponse.cs`,
  `api/Bryk.Application/Goals/GoalService.cs`, `IGoalService.cs`, `GoalResponse.cs`,
  `api/Bryk.Application/Profile/ProfileService.cs`,
  `api/Bryk.Domain/Interfaces/IEventRepository.cs`, `IGoalRepository.cs`, `ITrainingPlanRepository.cs`,
  `api/Bryk.Domain/Entities/{TrainingPlan,Event,Goal}.cs`,
  `api/Bryk.Infrastructure/Repositories/TrainingPlanRepository.cs`,
  `api/Bryk.API/Controllers/EventsController.cs`, `GoalsController.cs`, `AnalyticsController.cs`,
  `api/Bryk.API/Program.cs` (the manual `AddScoped` list — confirm no assembly scan for services),
  `api/Bryk.API.Tests/Analytics/AnalyticsControllerTests.cs`, `Profile/ProfileControllerTests.cs`,
  `api/Bryk.API.Tests/Fixtures/BrykWebApplicationFactory.cs`.
- Confirm current shapes (already verified during spec-writing): `EventService`/`GoalService` are
  primary-ctor classes with `ICurrentUserService currentUser`, `IValidator<TDto> validator`,
  the repo, `IUnitOfWork unitOfWork`; both throw `KeyNotFoundException` from `Update`/`Delete` only.
  `ITrainingPlanRepository` has no reverse-`EventId` read yet. `Program.cs` registers services with an
  explicit `AddScoped<TInterface, TImpl>()` list (no assembly scan) — new services/repos need a line
  added there.

## Step 1 — Domain/Interface: `ITrainingPlanRepository.GetByEventIdsAsync`

**File:** `api/Bryk.Domain/Interfaces/ITrainingPlanRepository.cs` (add one method to the interface,
alongside `GetPlannedWorkoutsByIdsWithStructureAsync`).

```csharp
/// <summary>
/// The <see cref="TrainingPlan"/>s whose <see cref="TrainingPlan.EventId"/> is in <paramref name="eventIds"/>
/// — entity only, no <see cref="TrainingPlan.PlannedWorkouts"/> include (callers only need <c>Id</c> +
/// <c>Name</c> for the linked-plan chip). No-tracking. An empty <paramref name="eventIds"/> returns an
/// empty list with no query (mirrors <see cref="GetPlannedWorkoutsByIdsWithStructureAsync"/>).
/// </summary>
Task<IReadOnlyList<TrainingPlan>> GetByEventIdsAsync(IEnumerable<Guid> eventIds, CancellationToken ct = default);
```

**File:** `api/Bryk.Infrastructure/Repositories/TrainingPlanRepository.cs` (add the implementation,
next to `GetPlannedWorkoutsByIdsWithStructureAsync`).

```csharp
public async Task<IReadOnlyList<TrainingPlan>> GetByEventIdsAsync(IEnumerable<Guid> eventIds, CancellationToken ct = default)
{
    var idList = eventIds.Distinct().ToList();
    if (idList.Count == 0)
    {
        return new List<TrainingPlan>();
    }

    return await db.TrainingPlans
        .AsNoTracking()
        .Where(p => p.EventId != null && idList.Contains(p.EventId!.Value))
        .ToListAsync(ct);
}
```

**Verify:** `dotnet build api/Bryk.sln` green. (No tests yet — they land with the service/controller
integration tests in Step 8.)

## Step 2 — New DTOs: `LinkedPlanDto`, `EventListItemResponse`

**New file** `api/Bryk.Application/Events/LinkedPlanDto.cs`:

```csharp
namespace Bryk.Application.Events;

// Id + name only — the chip navigates to /plans/{id}; no plan body needed (Tasks-17-1).
public class LinkedPlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

**New file** `api/Bryk.Application/Events/EventListItemResponse.cs`:

```csharp
using Bryk.Domain.Entities;

namespace Bryk.Application.Events;

// GET-only shape: all EventResponse fields plus the reverse-EventId linked plan(s) (display-only —
// the plan<->event write path waits for Phase 18's plan PUT). GET /events and GET /events/{id} both
// return this shape (single item for the by-id route).
public class EventListItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public Sport? Sport { get; set; }
    public TriathlonDistance? TriathlonDistance { get; set; }
    public string? CustomDistanceName { get; set; }
    public EventPriority Priority { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<LinkedPlanDto> LinkedPlans { get; set; } = new List<LinkedPlanDto>();
}
```

**Verify:** `dotnet build` green.

## Step 3 — New DTOs: `GoalStatus`, `GoalListItemResponse`

**New file** `api/Bryk.Application/Goals/GoalStatus.cs`:

```csharp
namespace Bryk.Application.Goals;

/// <summary>Date-based goal status, computed by <see cref="GoalProgress"/> (Tasks-17-1). Quantitative
/// (target-value) progress is deferred — see the ROADMAP Phase 17 decision.</summary>
public enum GoalStatus
{
    NoDate = 0,
    Upcoming = 1,
    DueSoon = 2,
    Overdue = 3
}
```

**New file** `api/Bryk.Application/Goals/GoalListItemResponse.cs`:

```csharp
using Bryk.Domain.Entities;

namespace Bryk.Application.Goals;

// GET-only shape: all GoalResponse fields plus computed DaysRemaining + Status (GoalProgress.Compute).
// No TargetValue/Unit/CurrentValue — quantitative progress is deferred (ROADMAP Phase 17).
public class GoalListItemResponse
{
    public Guid Id { get; set; }
    public GoalType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateOnly? TargetDate { get; set; }
    public int? DaysRemaining { get; set; }
    public GoalStatus Status { get; set; }
}
```

**Verify:** `dotnet build` green.

## Step 4 — Pure `GoalProgress` helper

**New file** `api/Bryk.Application/Goals/GoalProgress.cs`:

```csharp
namespace Bryk.Application.Goals;

/// <summary>
/// Pure date-based goal progress (Tasks-17-1). No I/O, no DateTime.UtcNow — the caller passes
/// <paramref name="today"/> in (the calculators-take-today convention from
/// WeeklyLoadCalculator/PmcCalculator), so this is deterministic under test.
/// </summary>
public static class GoalProgress
{
    private const int DueSoonThresholdDays = 14;

    public static (int? DaysRemaining, GoalStatus Status) Compute(DateOnly? targetDate, DateOnly today)
    {
        if (targetDate is not { } target)
        {
            return (null, GoalStatus.NoDate);
        }

        var daysRemaining = target.DayNumber - today.DayNumber;

        var status = daysRemaining switch
        {
            < 0 => GoalStatus.Overdue,
            <= DueSoonThresholdDays => GoalStatus.DueSoon,
            _ => GoalStatus.Upcoming
        };

        return (daysRemaining, status);
    }
}
```

**Verify:** `dotnet build` green.

## Step 5 — Unit tests for `GoalProgress`

**New file** `api/Bryk.Application.Tests/Goals/GoalProgressTests.cs`. Mirror the
`WeeklyLoadCalculatorTests`/`ComplianceClassifierTests` style: xUnit + FluentAssertions, a fixed
`today` (e.g. `new DateOnly(2026, 7, 1)`), one test per pinned bullet from `Tasks-17-1.md`:

```csharp
using Bryk.Application.Goals;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Goals;

public class GoalProgressTests
{
    private static readonly DateOnly Today = new(2026, 7, 1);

    [Fact]
    public void NullTargetDate_ReturnsNoDate()
    {
        var (daysRemaining, status) = GoalProgress.Compute(null, Today);

        daysRemaining.Should().BeNull();
        status.Should().Be(GoalStatus.NoDate);
    }

    [Fact]
    public void TargetIsToday_ReturnsZeroDaysDueSoon()
    {
        var (daysRemaining, status) = GoalProgress.Compute(Today, Today);

        daysRemaining.Should().Be(0);
        status.Should().Be(GoalStatus.DueSoon);
    }

    [Fact]
    public void TargetIsTodayPlus14_ReturnsDueSoonBoundaryInclusive()
    {
        var (daysRemaining, status) = GoalProgress.Compute(Today.AddDays(14), Today);

        daysRemaining.Should().Be(14);
        status.Should().Be(GoalStatus.DueSoon);
    }

    [Fact]
    public void TargetIsTodayPlus15_ReturnsUpcoming()
    {
        var (daysRemaining, status) = GoalProgress.Compute(Today.AddDays(15), Today);

        daysRemaining.Should().Be(15);
        status.Should().Be(GoalStatus.Upcoming);
    }

    [Fact]
    public void TargetIsYesterday_ReturnsOverdue()
    {
        var (daysRemaining, status) = GoalProgress.Compute(Today.AddDays(-1), Today);

        daysRemaining.Should().Be(-1);
        status.Should().Be(GoalStatus.Overdue);
    }
}
```

**Verify:** `dotnet test api/Bryk.sln` green — the 5 new `GoalProgressTests` pass; nothing else broke.

## Step 6 — `EventService`/`IEventService` additions

**File:** `api/Bryk.Application/Events/IEventService.cs` — add to the interface:

```csharp
Task<IReadOnlyList<EventListItemResponse>> GetAllAsync(bool upcomingOnly, CancellationToken ct = default);

/// <summary>Returns null when the event does not exist or belongs to another athlete — the controller
/// maps null to 404 (this is a GET; it does not throw KeyNotFoundException).</summary>
Task<EventListItemResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
```

Update the XML `<summary>` on the interface if it currently only documents create/update/delete —
add a line noting the two new read methods return `null`/filtered results rather than throwing.

**File:** `api/Bryk.Application/Events/EventService.cs` — add `ITrainingPlanRepository planRepo` as a
new primary-ctor parameter (append after `eventRepo`, before `unitOfWork`, matching the existing
parameter order style), and add the two methods:

```csharp
public class EventService(
    ICurrentUserService currentUser,
    IValidator<EventDto> validator,
    IEventRepository eventRepo,
    ITrainingPlanRepository planRepo,
    IUnitOfWork unitOfWork) : IEventService
{
    public async Task<IReadOnlyList<EventListItemResponse>> GetAllAsync(bool upcomingOnly, CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();
        var events = await eventRepo.GetByAthleteIdAsync(athleteId, ct);

        if (upcomingOnly)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            events = events.Where(e => e.EventDate >= today).ToList();
        }

        var linkedPlans = await planRepo.GetByEventIdsAsync(events.Select(e => e.Id), ct);
        var plansByEventId = linkedPlans
            .GroupBy(p => p.EventId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        return events.Select(e => Map(e, plansByEventId)).ToList();
    }

    public async Task<EventListItemResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await eventRepo.GetByIdAsync(id, ct);
        if (entity is null || entity.AthleteId != currentUser.GetCurrentAthleteId())
        {
            return null;
        }

        var linkedPlans = await planRepo.GetByEventIdsAsync(new[] { entity.Id }, ct);
        var plansByEventId = new Dictionary<Guid, List<TrainingPlan>> { [entity.Id] = linkedPlans.ToList() };

        return Map(entity, plansByEventId);
    }
    // ... existing CreateAsync/UpdateAsync/DeleteAsync unchanged ...
```

Extend the private `Map` (or add a second overload — pick whichever keeps `Map(Event)` used by
`CreateAsync`/`UpdateAsync` untouched, since those callers don't need `LinkedPlans`):

```csharp
    private static EventListItemResponse Map(Event e, IReadOnlyDictionary<Guid, List<TrainingPlan>> plansByEventId) => new()
    {
        Id = e.Id,
        Name = e.Name,
        EventDate = e.EventDate,
        Sport = e.Sport,
        TriathlonDistance = e.TriathlonDistance,
        CustomDistanceName = e.CustomDistanceName,
        Priority = e.Priority,
        Notes = e.Notes,
        LinkedPlans = plansByEventId.TryGetValue(e.Id, out var plans)
            ? plans.Select(p => new LinkedPlanDto { Id = p.Id, Name = p.Name }).ToList()
            : new List<LinkedPlanDto>()
    };
```

Keep the existing private `Map(Event) => EventResponse` used by `CreateAsync`/`UpdateAsync` exactly as
is — do not touch `EventResponse` or its call sites. Add the necessary `using
Bryk.Domain.Interfaces;` (for `ITrainingPlanRepository`) if not already present via `Bryk.Domain.Entities`.

**Verify:** `dotnet build api/Bryk.sln` green. `Program.cs` DI: `EventService`'s ctor now needs
`ITrainingPlanRepository`, which is already registered (`AddScoped<ITrainingPlanRepository,
TrainingPlanRepository>()` exists) — confirm no DI change is required, just re-run build to catch any
resolution error at startup-test time (Step 8 integration tests will exercise the container).

## Step 7 — `GoalService`/`IGoalService` additions

**File:** `api/Bryk.Application/Goals/IGoalService.cs` — add to the interface:

```csharp
Task<IReadOnlyList<GoalListItemResponse>> GetAllAsync(CancellationToken ct = default);
```

**File:** `api/Bryk.Application/Goals/GoalService.cs` — add the method (no new ctor dependency needed;
`GoalProgress` is a static pure helper):

```csharp
public async Task<IReadOnlyList<GoalListItemResponse>> GetAllAsync(CancellationToken ct = default)
{
    var athleteId = currentUser.GetCurrentAthleteId();
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var goals = await goalRepo.GetByAthleteIdAsync(athleteId, ct);

    return goals.Select(g =>
    {
        var (daysRemaining, status) = GoalProgress.Compute(g.TargetDate, today);
        return new GoalListItemResponse
        {
            Id = g.Id,
            Type = g.Type,
            Description = g.Description,
            TargetDate = g.TargetDate,
            DaysRemaining = daysRemaining,
            Status = status
        };
    }).ToList();
}
```

Leave the existing private `Map(Goal) => GoalResponse` and `CreateAsync`/`UpdateAsync`/`DeleteAsync`
untouched.

**Verify:** `dotnet build api/Bryk.sln` green.

## Step 8 — Controllers: additive `[HttpGet]` actions

**File:** `api/Bryk.API/Controllers/EventsController.cs` — add above or below the existing actions
(match existing ordering; existing file has Create/Update/Delete — insert the GETs first to read
naturally as list-then-mutate, mirroring REST convention, but this is a style choice, not load-bearing):

```csharp
/// <summary>
/// Returns the current athlete's events ordered by <see cref="Bryk.Domain.Entities.Event.EventDate"/>
/// ascending, each carrying its <c>Notes</c> and linked-plan ids/names (reverse EventId lookup,
/// display-only). When <paramref name="upcoming"/> is true, filters to events whose date is today or
/// later; defaults to false (all events).
/// </summary>
[HttpGet]
public async Task<IActionResult> GetAllAsync([FromQuery] bool upcoming, CancellationToken cancellationToken)
{
    IReadOnlyList<EventListItemResponse> result = await eventService.GetAllAsync(upcoming, cancellationToken);
    return Ok(result);
}

/// <summary>Returns a single event owned by the current athlete, with its linked plans. 404 if it does
/// not exist or belongs to another athlete.</summary>
[HttpGet("{id:guid}")]
public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken)
{
    EventListItemResponse? result = await eventService.GetByIdAsync(id, cancellationToken);
    return result is null ? NotFound() : Ok(result);
}
```

**File:** `api/Bryk.API/Controllers/GoalsController.cs` — add:

```csharp
/// <summary>
/// Returns the current athlete's goals ordered by <see cref="Bryk.Domain.Entities.Goal.TargetDate"/>
/// ascending (nulls last), each carrying computed <c>daysRemaining</c> and <c>status</c>
/// (see <see cref="Bryk.Application.Goals.GoalProgress"/>).
/// </summary>
[HttpGet]
public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
{
    IReadOnlyList<GoalListItemResponse> result = await goalService.GetAllAsync(cancellationToken);
    return Ok(result);
}
```

Both controllers keep their existing `[HttpPost]`/`[HttpPut("{id:guid}")]`/`[HttpDelete("{id:guid}")]`
actions and constructors unchanged — no new controller ctor dependency (the GET routes reuse the
already-injected `eventService`/`goalService`).

**Verify:** `dotnet build api/Bryk.sln` green. No `Program.cs` change needed — `IEventService` and
`IGoalService` are already registered; only their concrete classes changed shape (new ctor param on
`EventService`, already covered by the existing `AddScoped<ITrainingPlanRepository,
TrainingPlanRepository>()` registration).

## Step 9 — Integration tests: `EventsControllerGetTests`

**New file** `api/Bryk.API.Tests/Events/EventsControllerGetTests.cs`. Mirror the
`AnalyticsControllerTests`/`ProfileControllerTests` harness (`BrykWebApplicationFactory`, `JsonOptions`
with `JsonStringEnumConverter`, seed via the existing `POST /api/v1/events` and
`POST /api/v1/trainingplans`).

Cases (exact assertions per `Tasks-17-1.md`):

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Events;
using Bryk.Application.Training;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.Events;

public class EventsControllerGetTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private static EventDto MakeEvent(string name, DateOnly date) => new()
    {
        Name = name,
        EventDate = date,
        Sport = Sport.Run,
        Priority = EventPriority.A,
        Notes = $"{name} notes"
    };

    [Fact]
    public async Task GetAll_ReturnsEventsOrderedByDateAscending_WithNotes()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var early = Today.AddDays(10);
        var late = Today.AddDays(20);
        await client.PostAsJsonAsync("/api/v1/events", MakeEvent("Late Race", late));
        await client.PostAsJsonAsync("/api/v1/events", MakeEvent("Early Race", early));

        var events = await client.GetFromJsonAsync<List<EventListItemResponse>>("/api/v1/events", JsonOptions);

        events.Should().NotBeNull();
        events!.Select(e => e.Name).Should().Equal("Early Race", "Late Race");
        events[0].Notes.Should().Be("Early Race notes");
    }

    [Fact]
    public async Task GetAll_UpcomingTrue_ExcludesPast_IncludesTodayAndFuture()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/events", MakeEvent("Past Race", Today.AddDays(-5)));
        await client.PostAsJsonAsync("/api/v1/events", MakeEvent("Today Race", Today));
        await client.PostAsJsonAsync("/api/v1/events", MakeEvent("Future Race", Today.AddDays(5)));

        var events = await client.GetFromJsonAsync<List<EventListItemResponse>>("/api/v1/events?upcoming=true", JsonOptions);

        events.Should().NotBeNull();
        events!.Select(e => e.Name).Should().Equal("Today Race", "Future Race");
    }

    [Fact]
    public async Task GetAll_LinkedPlan_AppearsInLinkedPlans_UnlinkedEventHasEmptyList()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var eventResponse = await client.PostAsJsonAsync("/api/v1/events", MakeEvent("Linked Race", Today.AddDays(30)));
        var createdEvent = await eventResponse.Content.ReadFromJsonAsync<EventResponse>(JsonOptions);
        await client.PostAsJsonAsync("/api/v1/events", MakeEvent("Unlinked Race", Today.AddDays(40)));

        var planRequest = new TrainingPlanRequest
        {
            Name = "Race Plan",
            Methodology = MethodologyChoice.Polarized,
            StartDate = Today,
            EndDate = Today.AddDays(30),
            EventId = createdEvent!.Id
        };
        await client.PostAsJsonAsync("/api/v1/trainingplans", planRequest);

        var events = await client.GetFromJsonAsync<List<EventListItemResponse>>("/api/v1/events", JsonOptions);

        var linked = events!.Single(e => e.Name == "Linked Race");
        linked.LinkedPlans.Should().ContainSingle();
        linked.LinkedPlans[0].Name.Should().Be("Race Plan");

        var unlinked = events.Single(e => e.Name == "Unlinked Race");
        unlinked.LinkedPlans.Should().BeEmpty();
    }

    [Fact]
    public async Task GetById_ReturnsEventWithLinkedPlans()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var eventResponse = await client.PostAsJsonAsync("/api/v1/events", MakeEvent("Solo Race", Today.AddDays(10)));
        var createdEvent = await eventResponse.Content.ReadFromJsonAsync<EventResponse>(JsonOptions);

        var response = await client.GetAsync($"/api/v1/events/{createdEvent!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<EventListItemResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Id.Should().Be(createdEvent.Id);
        body.LinkedPlans.Should().BeEmpty();
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/events/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_AnotherAthletesEvent_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var eventResponse = await client.PostAsJsonAsync("/api/v1/events", MakeEvent("Owner's Race", Today.AddDays(10)));
        var createdEvent = await eventResponse.Content.ReadFromJsonAsync<EventResponse>(JsonOptions);

        // Second client bound to a different athlete against the SAME in-memory database instance —
        // WithWebHostBuilder overrides ICurrentUserService only, sharing the factory's DbContext.
        var otherAthleteId = Guid.NewGuid();
        var otherFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<Bryk.Application.Common.ICurrentUserService>();
                services.AddScoped<Bryk.Application.Common.ICurrentUserService>(
                    _ => new OtherAthleteCurrentUserService(otherAthleteId));
            });
        });
        var otherClient = otherFactory.CreateClient();

        var response = await otherClient.GetAsync($"/api/v1/events/{createdEvent!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_FreshAthlete_ReturnsEmptyArray()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await response.Content.ReadFromJsonAsync<List<EventListItemResponse>>(JsonOptions);
        events.Should().NotBeNull().And.BeEmpty();
    }

    private sealed class OtherAthleteCurrentUserService(Guid athleteId) : Bryk.Application.Common.ICurrentUserService
    {
        public Guid GetCurrentAthleteId() => athleteId;
    }
}
```

Note on the "another athlete" test: `BrykWebApplicationFactory` bakes a single `TestAthleteId` into its
`ConfigureWebHost` and gives each factory instance its own fresh in-memory database — there is no
existing precedent in the test suite for two athletes sharing one database. `WebApplicationFactory<T>.
WithWebHostBuilder` (standard `Mvc.Testing` API, already implicitly available since
`BrykWebApplicationFactory : WebApplicationFactory<Program>`) creates a **second** factory that reuses
the **same** host configuration (same InMemory database name, set in the base factory's
`ConfigureWebHost`) while layering an additional `ICurrentUserService` override — this is what makes
"seed as athlete A, read as athlete B against the same DB" possible without inventing new fixture
surface. Add `using Microsoft.Extensions.DependencyInjection.Extensions;` for `RemoveAll<T>`. If
`WithWebHostBuilder` does not carry over the InMemory database name as expected when actually run
(verify empirically, don't assume), fall back to constructing a second `BrykWebApplicationFactory`-like
inline factory pointed at the **same** database name string — do not add new fixture infrastructure
beyond what this single test needs; keep the override local to this test file.

**Verify:** `dotnet test api/Bryk.sln` — build green, all 7 new `EventsControllerGetTests` pass.

## Step 10 — Integration tests: `GoalsControllerGetTests`

**New file** `api/Bryk.API.Tests/Goals/GoalsControllerGetTests.cs`. Same harness pattern.

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Goals;
using Bryk.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Bryk.API.Tests.Goals;

public class GoalsControllerGetTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static GoalDto MakeGoal(string description, DateOnly? targetDate) => new()
    {
        Type = GoalType.General,
        Description = description,
        TargetDate = targetDate
    };

    [Fact]
    public async Task GetAll_DueSoonGoal_ReturnsThreeDaysRemainingAndDueSoon()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/goals", MakeGoal("Due soon goal", Today.AddDays(3)));

        var goals = await client.GetFromJsonAsync<List<GoalListItemResponse>>("/api/v1/goals", JsonOptions);

        goals.Should().ContainSingle();
        goals![0].DaysRemaining.Should().Be(3);
        goals[0].Status.Should().Be(GoalStatus.DueSoon);
    }

    [Fact]
    public async Task GetAll_NullTargetGoal_ReturnsNullDaysRemainingAndNoDate()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/goals", MakeGoal("No date goal", null));

        var goals = await client.GetFromJsonAsync<List<GoalListItemResponse>>("/api/v1/goals", JsonOptions);

        goals.Should().ContainSingle();
        goals![0].DaysRemaining.Should().BeNull();
        goals[0].Status.Should().Be(GoalStatus.NoDate);
    }

    [Fact]
    public async Task GetAll_PastTargetGoal_ReturnsOverdue()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/goals", MakeGoal("Overdue goal", Today.AddDays(-10)));

        var goals = await client.GetFromJsonAsync<List<GoalListItemResponse>>("/api/v1/goals", JsonOptions);

        goals.Should().ContainSingle();
        goals![0].Status.Should().Be(GoalStatus.Overdue);
    }

    [Fact]
    public async Task GetAll_FreshAthlete_ReturnsEmptyArray()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/goals");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var goals = await response.Content.ReadFromJsonAsync<List<GoalListItemResponse>>(JsonOptions);
        goals.Should().NotBeNull().And.BeEmpty();
    }
}
```

**Verify:** `dotnet test api/Bryk.sln` — build green, all 4 new `GoalsControllerGetTests` pass.

## Step 11 — Final verification + commit

- `dotnet build api/Bryk.sln` — 0 errors (warnings unchanged from baseline).
- `dotnet test api/Bryk.sln` — all green (the 5 `GoalProgressTests` + 7 `EventsControllerGetTests` + 4
  `GoalsControllerGetTests`, plus every pre-existing test, pass).
- `git diff --stat` — confirm only the expected files changed/added:
  - `api/Bryk.Domain/Interfaces/ITrainingPlanRepository.cs` (new method signature)
  - `api/Bryk.Infrastructure/Repositories/TrainingPlanRepository.cs` (new implementation)
  - `api/Bryk.Application/Events/LinkedPlanDto.cs`, `EventListItemResponse.cs` (new)
  - `api/Bryk.Application/Events/IEventService.cs`, `EventService.cs` (extended)
  - `api/Bryk.Application/Goals/GoalStatus.cs`, `GoalListItemResponse.cs`, `GoalProgress.cs` (new)
  - `api/Bryk.Application/Goals/IGoalService.cs`, `GoalService.cs` (extended)
  - `api/Bryk.API/Controllers/EventsController.cs`, `GoalsController.cs` (extended)
  - `api/Bryk.Application.Tests/Goals/GoalProgressTests.cs` (new)
  - `api/Bryk.API.Tests/Events/EventsControllerGetTests.cs`,
    `api/Bryk.API.Tests/Goals/GoalsControllerGetTests.cs` (new)
  - No changes to `ProfileService.cs`, `EventResponse.cs`, `GoalResponse.cs`, `EventDto.cs`,
    `GoalDto.cs`, any validator, `Program.cs`, or any migration/`*.csproj`. If the diff shows any of
    these, **STOP** — that is scope creep beyond `Tasks-17-1.md`.
- If at any point a step appears to require a new EF model property (`TargetValue`/`Unit`/
  `CurrentValue`), a migration, or a new NuGet package — **STOP and flag it as a blocker** per the
  Tasks doc's "What NOT to modify"; do not proceed past that step.
- Commit with the message in `Tasks-17-1.md`:

```
feat: events & goals GET endpoints with linked-plan lookup

Promote events/goals to first-class read endpoints (the dashboard
composed them from /profile/goals before): GET /api/v1/events (date-asc,
upcoming=true filter, Notes + linked-plan ids), GET /api/v1/events/{id},
GET /api/v1/goals (computed days-remaining + status). Linked plans come
from a new additive read-only ITrainingPlanRepository.GetByEventIdsAsync
reverse EventId lookup (display-only; the write path waits for Phase 18).
Goal status/days-remaining computed by a pure GoalProgress helper (unit
tests pin the DueSoon/Upcoming/Overdue boundaries). No migration; goal
target-value tracking stays deferred per the Phase 17 decision. xUnit
covers ordering, the upcoming filter, linked/unlinked plans, and 404s.
```
</content>
