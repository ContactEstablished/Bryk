# Impl 18-2 — Build order: `PUT /api/v1/trainingplans/{id}` (plan metadata + the event write path)

**Executor:** architect-implementer (per CLAUDE.md).
**Acceptance contract:** `md/Tasks-18-2.md`. **Decision lock:** ADR-0009 §5 (orphan policy = 400) + §6
(`RecoveryWeekPercentage` percent-scale, 30–90 on this validator) — written by Task 18-1, no code
dependency on that task; ADR-0008 §2 (the reschedule-rejection message shape and 404-vs-400 split this
mirrors); ADR-0003 §1 (`TrainingPlan` field list + `Event → TrainingPlan` `SetNull` FK); Task 9-3's
staging discipline (fresh nav-free entity carrying `CreatedAt`, `LoadOwnedPlanAsync` ownership → 404).
**Scope:** Backend only. No migration, no new package, no `Program.cs` line (validators are picked up
by `AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>()` at `Program.cs:35`).

This is the step-by-step build order. Execute top-to-bottom; each step's verification is the gate to
the next. Commit once at the end with the message in `Tasks-18-2.md`.

**Shared-file note:** `TrainingPlansController.cs` is also touched by Task 18-3. Implement this task
(18-2) fully — through the final commit — before starting 18-3. Do not have both in flight in parallel
editor sessions.

## Step 0 — Pre-flight

- `git status` clean on `main`. `dotnet build api/Bryk.sln` green (baseline). `dotnet test api/Bryk.sln`
  green at the **201** xUnit baseline. `cd ui; pnpm run build` green; `pnpm exec vitest run
  --no-file-parallelism` at **229 / 53 files** (this task touches no frontend file — these numbers must
  be unchanged at the end).
- Re-read `md/Tasks-18-2.md` in full. Open in editor:
  `api/Bryk.Application/Training/TrainingPlanService.cs`, `ITrainingPlanService.cs`,
  `TrainingPlanRequest.cs`, `Validators/TrainingPlanRequestValidator.cs` (read-only — frozen),
  `TrainingPlanResponse.cs`,
  `api/Bryk.Domain/Interfaces/ITrainingPlanRepository.cs`, `IEventRepository.cs`,
  `api/Bryk.Domain/Entities/TrainingPlan.cs`,
  `api/Bryk.API/Controllers/TrainingPlansController.cs`,
  `api/Bryk.API/Program.cs` (lines 35 and 100–120 only — confirm no change needed at either),
  `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs`,
  `api/Bryk.Application.Tests/Training/TrainingPlanServiceTests.cs`.
- Confirm current shapes (already verified during spec-writing):
  - `TrainingPlanService`'s primary ctor currently takes 6 params — `(ICurrentUserService currentUser,
    IValidator<TrainingPlanRequest> planValidator, IValidator<PlannedWorkoutDto> plannedWorkoutValidator,
    IValidator<ScheduleRequest> scheduleValidator, ITrainingPlanRepository planRepo, IUnitOfWork
    unitOfWork)`. It gains **two** more in this task.
  - `ITrainingPlanRepository.Update(TrainingPlan entity)` already exists (used by nothing today) and
    `GetByEventIdsAsync` already exists (added in Phase 17, unrelated to this task) — neither repository
    file changes in this task.
  - `IEventRepository` and `ITrainingPlanRepository` are both already registered in `Program.cs`
    (`AddScoped<IEventRepository, EventRepository>()` at L101, `AddScoped<ITrainingPlanRepository,
    TrainingPlanRepository>()` at L104) — the new `TrainingPlanService` ctor param resolves with no DI
    change.
  - `TrainingPlanServiceTests.NewService(...)` (L33–38) currently passes exactly 6 ctor args positionally
    — it **will not compile** the moment the ctor signature changes. This is expected and handled as its
    own step (Step 4) — do not skip ahead and try to fix it inline while writing the service.
  - `TrainingPlansControllerTests.cs` has a foreign-athlete seeding block at L172–227
    (`Reschedule_ForeignPlan_Returns404`) that seeds a `TrainingPlan` + `Athlete` directly through
    `ApplicationDbContext` — reuse this pattern verbatim for the new foreign-plan and foreign-event
    integration tests.

## Step 1 — New DTO: `TrainingPlanUpdateRequest`

**File:** `api/Bryk.Application/Training/TrainingPlanUpdateRequest.cs` (new).

```csharp
using Bryk.Domain.Entities;

namespace Bryk.Application.Training;

// Metadata-only replace-style update — no PlannedWorkouts (children are edited through their own
// endpoints, ADR-0003 aggregate boundary; this DTO must never add, replace or delete them).
// RecoveryWeekPercentage is percent-scale (60.0m = 60%, ADR-0009 §6). EventId = null clears the link.
public class TrainingPlanUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public MethodologyChoice Methodology { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid? EventId { get; set; }
    public int? BuildWeeks { get; set; }
    public int? RecoveryWeeks { get; set; }
    public decimal? RecoveryWeekPercentage { get; set; }
}
```

**Verify:** `dotnet build api/Bryk.sln` green (new, unreferenced type — trivial).

## Step 2 — New validator: `TrainingPlanUpdateRequestValidator`

**File:** `api/Bryk.Application/Training/Validators/TrainingPlanUpdateRequestValidator.cs` (new).

```csharp
using FluentValidation;

namespace Bryk.Application.Training.Validators;

public class TrainingPlanUpdateRequestValidator : AbstractValidator<TrainingPlanUpdateRequest>
{
    public TrainingPlanUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Methodology)
            .IsInEnum();

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("EndDate must be on or after StartDate.");

        RuleFor(x => x.BuildWeeks)
            .InclusiveBetween(1, 8)
            .When(x => x.BuildWeeks.HasValue);

        RuleFor(x => x.RecoveryWeeks)
            .GreaterThanOrEqualTo(1)
            .When(x => x.RecoveryWeeks.HasValue);

        RuleFor(x => x.RecoveryWeekPercentage)
            .InclusiveBetween(30m, 90m)
            .When(x => x.RecoveryWeekPercentage.HasValue);
    }
}
```

No `EventId` rule (ownership needs a repository read — that lives in the service, Step 3). No
cross-field "all three or none" rule — a partial cadence is legal at the boundary (ADR-0009 §2). No
`Program.cs` line — the assembly scan at `Program.cs:35` picks this up automatically.

> **STOP condition, do not act on it:** this validator's bounds (`BuildWeeks` 1–8, `RecoveryWeeks` ≥ 1,
> `RecoveryWeekPercentage` 30–90) are **deliberately tighter** than `TrainingPlanRequestValidator`'s
> (`BuildWeeks` > 0, `RecoveryWeeks` > 0, `RecoveryWeekPercentage` 0–100 — see line 20–30 of that file,
> read-only). This divergence is accepted tech debt, recorded in the commit body (Step 8). If anything
> in review suggests tightening the POST validator to match — **stop and ask the Sr. Dev**; that is an
> API breaking change on a shipped endpoint and explicitly out of this task's scope. Do not touch
> `TrainingPlanRequest.cs` or `TrainingPlanRequestValidator.cs`.

**Verify:** `dotnet build api/Bryk.sln` green.

## Step 3 — `ITrainingPlanService.UpdateAsync` + `TrainingPlanService` implementation

**File:** `api/Bryk.Application/Training/ITrainingPlanService.cs` — insert the new method directly after
`GetByIdAsync` (before `AddPlannedWorkoutAsync`), mirroring the controller's placement in Step 6:

```csharp
    /// <summary>Returns one plan with its planned workouts; 404 if missing or foreign.</summary>
    Task<TrainingPlanResponse> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Replaces an owned plan's metadata (name, methodology, window, event link, periodization fields).
    /// Planned workouts are untouched. 400 when the body is invalid, when the requested window would
    /// strand existing planned workouts (ADR-0009 §5), or when EventId names an event the athlete does
    /// not own; 404 when the plan is missing or foreign.
    /// </summary>
    Task<TrainingPlanResponse> UpdateAsync(Guid id, TrainingPlanUpdateRequest request, CancellationToken ct = default);

    /// <summary>Adds a planned workout to an owned plan; 404 if the plan is missing or foreign.</summary>
    Task<PlannedWorkoutResponse> AddPlannedWorkoutAsync(Guid planId, PlannedWorkoutDto request, CancellationToken ct = default);
```

(Only the `UpdateAsync` block is new — `GetByIdAsync` and `AddPlannedWorkoutAsync` are shown for
insertion-point context, unchanged.)

**File:** `api/Bryk.Application/Training/TrainingPlanService.cs` — two edits.

**3a. Ctor** — append the two new params in the order the task specifies (`updateValidator` after
`scheduleValidator`, `eventRepo` after `planRepo`):

```csharp
public class TrainingPlanService(
    ICurrentUserService currentUser,
    IValidator<TrainingPlanRequest> planValidator,
    IValidator<PlannedWorkoutDto> plannedWorkoutValidator,
    IValidator<ScheduleRequest> scheduleValidator,
    IValidator<TrainingPlanUpdateRequest> updateValidator,
    ITrainingPlanRepository planRepo,
    IEventRepository eventRepo,
    IUnitOfWork unitOfWork) : ITrainingPlanService
```

Add `using Bryk.Domain.Interfaces;` if not already present (the file already has it, for
`ITrainingPlanRepository`/`IUnitOfWork` — `IEventRepository` lives in the same namespace, no new using
needed).

**3b. `UpdateAsync` method** — insert directly after `GetByIdAsync` (before `AddPlannedWorkoutAsync`),
mirroring the interface's ordering:

```csharp
    public async Task<TrainingPlanResponse> UpdateAsync(Guid id, TrainingPlanUpdateRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateOrThrowAsync(request, ct);

        var plan = await LoadOwnedPlanAsync(id, ct);

        // Orphan guard (ADR-0009 §5): a window that would leave existing planned workouts stranded is
        // rejected — the client reschedules or removes them first. Window containment is inclusive on
        // both ends (a workout scheduled exactly on StartDate or EndDate is NOT stranded).
        var stranded = plan.PlannedWorkouts
            .Where(pw => pw.ScheduledDate < request.StartDate || pw.ScheduledDate > request.EndDate)
            .ToList();
        if (stranded.Count > 0)
        {
            throw new Exceptions.ValidationException(new[]
            {
                $"PlanWindow: {stranded.Count} planned workout(s) fall outside the requested window " +
                $"({request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}); reschedule or remove them first " +
                $"(earliest {stranded.Min(pw => pw.ScheduledDate):yyyy-MM-dd}, latest {stranded.Max(pw => pw.ScheduledDate):yyyy-MM-dd})."
            });
        }

        // Event-ownership guard. A null EventId clears the link with no read.
        if (request.EventId is { } eventId)
        {
            var ev = await eventRepo.GetByIdAsync(eventId, ct);
            if (ev is null || ev.AthleteId != plan.AthleteId)
            {
                throw new Exceptions.ValidationException(new[]
                {
                    "EventId: The selected event does not exist or belongs to another athlete."
                });
            }
        }

        // Stage a fresh, nav-free entity: the loaded `plan` came from a no-tracking Include, so
        // re-attaching it would drag PlannedWorkouts into the change tracker. CreatedAt is carried
        // over; the interceptor sets UpdatedAt. Never set UpdatedAt here.
        var updated = new TrainingPlan
        {
            Id = plan.Id,
            AthleteId = plan.AthleteId,
            Name = request.Name,
            Methodology = request.Methodology,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            EventId = request.EventId,
            BuildWeeks = request.BuildWeeks,
            RecoveryWeeks = request.RecoveryWeeks,
            RecoveryWeekPercentage = request.RecoveryWeekPercentage,
            CreatedAt = plan.CreatedAt
        };

        planRepo.Update(updated);
        await unitOfWork.SaveChangesAsync(ct);

        // TRAP: Map(updated) alone returns an EMPTY PlannedWorkouts — `updated` is nav-free by design
        // (see the staging comment above). Re-attach the untouched children from the originally loaded
        // `plan` for the projection only; do not mutate `updated.PlannedWorkouts` after SaveChangesAsync.
        var response = Map(updated);
        response.PlannedWorkouts = plan.PlannedWorkouts.OrderBy(pw => pw.ScheduledDate).Select(Map).ToList();
        return response;
    }
```

Do **not** touch `CreateAsync`, `GetByAthleteAsync`, `GetByIdAsync`, the planned-workout methods, or
`RescheduleAsync`. `Map(TrainingPlan)` and `Map(PlannedWorkout)` are reused unchanged.

**Verify:** `dotnet build api/Bryk.Application/Bryk.Application.csproj` and
`dotnet build api/Bryk.API/Bryk.API.csproj` green (production code only). **Do not** run
`dotnet build api/Bryk.sln` yet — it will fail with a `CS7036`-class error at
`TrainingPlanServiceTests.cs`'s `NewService(...)` call (still passing the old 6-arg positional list).
That failure is expected and is fixed in the very next step, not here.

## Step 4 — Fix the unit-test harness (`TrainingPlanServiceTests.cs`)

The ctor change in Step 3 breaks `NewService(...)`. Fix the harness *before* writing any new test.

**File:** `api/Bryk.Application.Tests/Training/TrainingPlanServiceTests.cs`.

**4a.** Update `NewService` (L33–38) — append the two new dependencies, matching the ctor's parameter
order. Give the new `eventRepo` an optional parameter (default to a fresh stub) so the five existing
call sites (`NewService(repo, uow)`) keep compiling unchanged:

```csharp
    private static TrainingPlanService NewService(StubTrainingPlanRepository repo, StubUnitOfWork uow, Guid? athleteId = null, StubEventRepository? eventRepo = null) =>
        new(new StubCurrentUserService(athleteId ?? AthleteId),
            new TrainingPlanRequestValidator(),
            new PlannedWorkoutDtoValidator(),
            new ScheduleRequestValidator(),
            new TrainingPlanUpdateRequestValidator(),
            repo,
            eventRepo ?? new StubEventRepository(),
            uow);
```

**4b.** Add a `ValidUpdate()` factory alongside `ValidPlan()` (near L16–22):

```csharp
    private static TrainingPlanUpdateRequest ValidUpdate(string name = "Updated Block") => new()
    {
        Name = name,
        Methodology = MethodologyChoice.Polarized,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(28)
    };
```

**4c.** Add `StubEventRepository` as a private nested class, next to `StubTrainingPlanRepository`
(after L234, before the closing brace of the test class) — `GetByIdAsync` returns a settable `ToReturn`
and counts reads via `ReadCount`; every other member throws, matching the file's existing stub style:

```csharp
    private sealed class StubEventRepository : IEventRepository
    {
        public Event? ToReturn { get; set; }
        public int ReadCount { get; private set; }

        public Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            ReadCount++;
            return Task.FromResult(ToReturn);
        }

        public Task<IReadOnlyList<Event>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Event>> GetByAthleteInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Event>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(Event entity, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(Event entity) => throw new NotImplementedException();
        public void Delete(Event entity) => throw new NotImplementedException();
    }
```

No test bodies change in this step — the five pre-existing tests must compile and pass exactly as
before.

**Verify:** `dotnet build api/Bryk.sln` green (the whole solution, including both test projects, now
compiles again). `dotnet test api/Bryk.sln --filter FullyQualifiedName~TrainingPlanServiceTests` — the
same 7 pre-existing tests in this file still pass (no new tests yet).

## Step 5 — Unit tests: `UpdateAsync` (`TrainingPlanServiceTests.cs`, extend)

Add the following 13 `[Fact]` tests to `TrainingPlanServiceTests.cs`, grouped after the existing
`RemovePlannedWorkoutAsync_OwnedPlannedWorkout_Removes` test and before the private stub classes. Every
assertion and boundary value below is pinned by `Tasks-18-2.md` — do not soften or renumber them.

```csharp
    [Fact]
    public async Task UpdateAsync_OwnedPlan_StagesFreshEntityAndCommitsOnce()
    {
        var planId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var plan = new TrainingPlan { Id = planId, AthleteId = AthleteId, CreatedAt = createdAt };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = ValidUpdate("Updated Name");
        request.BuildWeeks = 3;
        request.RecoveryWeeks = 1;
        request.RecoveryWeekPercentage = 60.0m;

        await service.UpdateAsync(planId, request);

        repo.Updated.Should().NotBeNull();
        repo.Updated!.Id.Should().Be(planId);
        repo.Updated.AthleteId.Should().Be(AthleteId);
        repo.Updated.CreatedAt.Should().Be(createdAt);
        repo.Updated.Name.Should().Be("Updated Name");
        repo.Updated.StartDate.Should().Be(request.StartDate);
        repo.Updated.EndDate.Should().Be(request.EndDate);
        repo.Updated.BuildWeeks.Should().Be(3);
        repo.Updated.RecoveryWeeks.Should().Be(1);
        repo.Updated.RecoveryWeekPercentage.Should().Be(60.0m);
        repo.Updated.PlannedWorkouts.Should().BeEmpty();
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_ForeignPlan_ThrowsKeyNotFound()
    {
        var planId = Guid.NewGuid();
        var plan = new TrainingPlan { Id = planId, AthleteId = Guid.NewGuid() }; // belongs to someone else
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.UpdateAsync(planId, ValidUpdate());

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repo.Updated.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_WindowWouldStrandPlannedWorkouts_ThrowsValidationWithPlanWindowMessage()
    {
        var planId = Guid.NewGuid();
        var start = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new TrainingPlan
        {
            Id = planId,
            AthleteId = AthleteId,
            PlannedWorkouts = new List<PlannedWorkout>
            {
                new() { Id = Guid.NewGuid(), AthleteId = AthleteId, TrainingPlanId = planId, ScheduledDate = start.AddDays(20), Title = "Stranded" }
            }
        };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = ValidUpdate();
        request.StartDate = start;
        request.EndDate = start.AddDays(10);

        var act = () => service.UpdateAsync(planId, request);

        var thrown = await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e => e.StartsWith("PlanWindow:") && e.Contains("1 planned workout(s)"));
        repo.Updated.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_WindowExactlyContainsEveryPlannedWorkout_Succeeds()
    {
        var planId = Guid.NewGuid();
        var start = DateOnly.FromDateTime(DateTime.UtcNow);
        var end = start.AddDays(14);
        var plan = new TrainingPlan
        {
            Id = planId,
            AthleteId = AthleteId,
            PlannedWorkouts = new List<PlannedWorkout>
            {
                new() { Id = Guid.NewGuid(), AthleteId = AthleteId, TrainingPlanId = planId, ScheduledDate = start, Title = "On Start" },
                new() { Id = Guid.NewGuid(), AthleteId = AthleteId, TrainingPlanId = planId, ScheduledDate = end, Title = "On End" }
            }
        };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = ValidUpdate();
        request.StartDate = start;
        request.EndDate = end;

        var result = await service.UpdateAsync(planId, request);

        repo.Updated.Should().NotBeNull();
        uow.SaveCount.Should().Be(1);
        result.PlannedWorkouts.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_ForeignEventId_ThrowsValidation()
    {
        var planId = Guid.NewGuid();
        var plan = new TrainingPlan { Id = planId, AthleteId = AthleteId };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var eventRepo = new StubEventRepository { ToReturn = new Event { Id = Guid.NewGuid(), AthleteId = Guid.NewGuid() } };
        var service = NewService(repo, uow, eventRepo: eventRepo);

        var request = ValidUpdate();
        request.EventId = Guid.NewGuid();

        var act = () => service.UpdateAsync(planId, request);

        var thrown = await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e => e.StartsWith("EventId:"));
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_UnknownEventId_ThrowsValidation()
    {
        var planId = Guid.NewGuid();
        var plan = new TrainingPlan { Id = planId, AthleteId = AthleteId };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var eventRepo = new StubEventRepository { ToReturn = null };
        var service = NewService(repo, uow, eventRepo: eventRepo);

        var request = ValidUpdate();
        request.EventId = Guid.NewGuid();

        var act = () => service.UpdateAsync(planId, request);

        var thrown = await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        thrown.Which.Errors.Should().ContainSingle(e => e.StartsWith("EventId:"));
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_NullEventId_ClearsLinkWithoutReadingTheEventRepository()
    {
        var planId = Guid.NewGuid();
        var plan = new TrainingPlan { Id = planId, AthleteId = AthleteId, EventId = Guid.NewGuid() };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var eventRepo = new StubEventRepository();
        var service = NewService(repo, uow, eventRepo: eventRepo);

        var request = ValidUpdate();
        request.EventId = null;

        await service.UpdateAsync(planId, request);

        repo.Updated.Should().NotBeNull();
        repo.Updated!.EventId.Should().BeNull();
        eventRepo.ReadCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_EndDateBeforeStartDate_ThrowsValidation()
    {
        var planId = Guid.NewGuid();
        var plan = new TrainingPlan { Id = planId, AthleteId = AthleteId };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = ValidUpdate();
        request.EndDate = request.StartDate.AddDays(-1);

        var act = () => service.UpdateAsync(planId, request);

        await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.Updated.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_RecoveryWeekPercentageBelow30_ThrowsValidation()
    {
        var planId = Guid.NewGuid();
        var plan = new TrainingPlan { Id = planId, AthleteId = AthleteId };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = ValidUpdate();
        request.RecoveryWeekPercentage = 29.99m;

        var act = () => service.UpdateAsync(planId, request);

        await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.Updated.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_RecoveryWeekPercentageAt30_And90_Succeed()
    {
        foreach (var percentage in new[] { 30m, 90m })
        {
            var planId = Guid.NewGuid();
            var plan = new TrainingPlan { Id = planId, AthleteId = AthleteId };
            var repo = new StubTrainingPlanRepository { ToReturn = plan };
            var uow = new StubUnitOfWork();
            var service = NewService(repo, uow);

            var request = ValidUpdate();
            request.RecoveryWeekPercentage = percentage;

            await service.UpdateAsync(planId, request);

            uow.SaveCount.Should().Be(1);
        }
    }

    [Fact]
    public async Task UpdateAsync_BuildWeeks9_ThrowsValidation()
    {
        var planId = Guid.NewGuid();
        var plan = new TrainingPlan { Id = planId, AthleteId = AthleteId };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = ValidUpdate();
        request.BuildWeeks = 9;

        var act = () => service.UpdateAsync(planId, request);

        await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_BuildWeeks1And8_Succeed()
    {
        foreach (var buildWeeks in new[] { 1, 8 })
        {
            var planId = Guid.NewGuid();
            var plan = new TrainingPlan { Id = planId, AthleteId = AthleteId };
            var repo = new StubTrainingPlanRepository { ToReturn = plan };
            var uow = new StubUnitOfWork();
            var service = NewService(repo, uow);

            var request = ValidUpdate();
            request.BuildWeeks = buildWeeks;

            await service.UpdateAsync(planId, request);

            uow.SaveCount.Should().Be(1);
        }
    }

    [Fact]
    public async Task UpdateAsync_ResponseKeepsExistingPlannedWorkouts()
    {
        var planId = Guid.NewGuid();
        var start = DateOnly.FromDateTime(DateTime.UtcNow);
        var end = start.AddDays(28);
        var plan = new TrainingPlan
        {
            Id = planId,
            AthleteId = AthleteId,
            StartDate = start,
            EndDate = end,
            PlannedWorkouts = new List<PlannedWorkout>
            {
                new() { Id = Guid.NewGuid(), AthleteId = AthleteId, TrainingPlanId = planId, ScheduledDate = start.AddDays(10), Title = "Second" },
                new() { Id = Guid.NewGuid(), AthleteId = AthleteId, TrainingPlanId = planId, ScheduledDate = start.AddDays(2), Title = "First" }
            }
        };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = ValidUpdate();
        request.StartDate = start;
        request.EndDate = end;

        var result = await service.UpdateAsync(planId, request);

        result.PlannedWorkouts.Should().HaveCount(2);
        result.PlannedWorkouts.Select(pw => pw.Title).Should().Equal("First", "Second");
    }
```

**Verify:** `dotnet test api/Bryk.sln --filter FullyQualifiedName~TrainingPlanServiceTests` — all 20
tests in this file pass (7 pre-existing + 13 new). `dotnet build api/Bryk.sln` still shows 0 errors,
warning count unchanged from baseline.

## Step 6 — Controller: `[HttpPut("{id:guid}")]` on `TrainingPlansController`

**File:** `api/Bryk.API/Controllers/TrainingPlansController.cs` — insert directly after `GetByIdAsync`
(L31–37) and before `AddPlannedWorkoutAsync` (L39):

```csharp
    /// <summary>
    /// Replaces a training plan's metadata (name, methodology, dates, target event, periodization
    /// fields) for the current athlete. Planned workouts are untouched. 404 if the plan is missing or
    /// foreign; 400 if the body is invalid, the new window would strand planned workouts, or the target
    /// event is not the athlete's.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] TrainingPlanUpdateRequest request, CancellationToken cancellationToken)
    {
        TrainingPlanResponse result = await trainingPlanService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }
```

No try/catch — the global `ExceptionHandlingMiddleware` maps `ValidationException` → 400 and
`KeyNotFoundException` → 404. The controller ctor and every other action are unchanged. This is the
**only** edit to this file in this task — 18-3 makes its own additive edit afterward, on a fresh
working tree.

**Verify:** `dotnet build api/Bryk.sln` green.

## Step 7 — Integration tests: `TrainingPlansControllerTests.cs` (extend)

Add `using Bryk.Application.Events;` and `using Bryk.Application.Onboarding;` to the top of
`api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs` (needed for `EventDto`,
`Bryk.Application.Events.EventResponse`, `EventListItemResponse` in the event-link test below — note
this file's existing `Bryk.Application.Training` `using` does **not** cover `Event`-side DTOs).

Add the following 8 tests after the existing `Reschedule_MissingPlannedWorkout_Returns404` (the last
test in the file), before the closing class brace. Every assertion and boundary value is pinned by
`Tasks-18-2.md`.

```csharp
    [Fact]
    public async Task Update_RoundTrips_PersistsPeriodizationFieldsAndSurvivesReload()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", ValidPlan("Original Name"));
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        var updateRequest = new TrainingPlanUpdateRequest
        {
            Name = "Updated Name",
            Methodology = MethodologyChoice.Polarized,
            StartDate = created!.StartDate,
            EndDate = created.EndDate.AddDays(10),
            BuildWeeks = 3,
            RecoveryWeeks = 1,
            RecoveryWeekPercentage = 60.0m
        };

        var putResponse = await client.PutAsJsonAsync($"/api/v1/trainingplans/{created.Id}", updateRequest);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await putResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Name");
        updated.BuildWeeks.Should().Be(3);
        updated.RecoveryWeeks.Should().Be(1);
        updated.RecoveryWeekPercentage.Should().Be(60.0m);
        updated.EndDate.Should().Be(created.EndDate.AddDays(10));

        var getResponse = await client.GetAsync($"/api/v1/trainingplans/{created.Id}");
        var reloaded = await getResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);
        reloaded.Should().NotBeNull();
        reloaded!.Name.Should().Be("Updated Name");
        reloaded.BuildWeeks.Should().Be(3);
        reloaded.RecoveryWeeks.Should().Be(1);
        reloaded.RecoveryWeekPercentage.Should().Be(60.0m);
        reloaded.PlannedWorkouts.Should().HaveCount(2);
        reloaded.PlannedWorkouts.Select(pw => pw.Title).Should().Contain(new[] { "Easy Run", "Endurance Ride" });
    }

    [Fact]
    public async Task Update_NonexistentPlan_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var request = new TrainingPlanUpdateRequest
        {
            Name = "Doesn't Matter",
            Methodology = MethodologyChoice.Polarized,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(28)
        };

        var response = await client.PutAsJsonAsync($"/api/v1/trainingplans/{Guid.NewGuid()}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ForeignPlan_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();

        // Seed a plan owned by a different athlete directly through the DbContext (mirrors
        // Reschedule_ForeignPlan_Returns404's seeding block above).
        var foreignAthleteId = Guid.NewGuid();
        var foreignPlanId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.TrainingPlans.Add(new TrainingPlan
            {
                Id = foreignPlanId,
                AthleteId = foreignAthleteId,
                Name = "Foreign Plan",
                Methodology = MethodologyChoice.Polarized,
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2026, 6, 30)
            });
            db.Athletes.Add(new Athlete
            {
                Id = foreignAthleteId,
                Name = "Foreign Athlete",
                Gender = Gender.Male,
                DateOfBirth = new DateOnly(1990, 1, 1),
                HeightCm = 180,
                WeightKg = 75,
                TypicalWeeklyHours = 10,
                Methodology = MethodologyChoice.Polarized
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var request = new TrainingPlanUpdateRequest
        {
            Name = "Hijack Attempt",
            Methodology = MethodologyChoice.Polarized,
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 30)
        };

        var response = await client.PutAsJsonAsync($"/api/v1/trainingplans/{foreignPlanId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_WindowShrinkStrandingPlannedWorkouts_Returns400WithPlanWindowError()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        // ValidPlan() seeds workouts at Start+1 / Start+2 — shrinking to [Start+10, Start+20] strands both.
        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", ValidPlan("Shrink Test"));
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        var request = new TrainingPlanUpdateRequest
        {
            Name = "Shrink Test",
            Methodology = MethodologyChoice.Polarized,
            StartDate = created!.StartDate.AddDays(10),
            EndDate = created.StartDate.AddDays(20)
        };

        var response = await client.PutAsJsonAsync($"/api/v1/trainingplans/{created.Id}", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorBody = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        errorBody.Should().NotBeNull();
        errorBody!.Errors.Should().Contain(e => e.StartsWith("PlanWindow:"));
    }

    [Fact]
    public async Task Update_ForeignEventId_Returns400WithEventIdError()
    {
        await using var factory = new BrykWebApplicationFactory();

        var foreignAthleteId = Guid.NewGuid();
        var foreignEventId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Athletes.Add(new Athlete
            {
                Id = foreignAthleteId,
                Name = "Foreign Athlete",
                Gender = Gender.Male,
                DateOfBirth = new DateOnly(1990, 1, 1),
                HeightCm = 180,
                WeightKg = 75,
                TypicalWeeklyHours = 10,
                Methodology = MethodologyChoice.Polarized
            });
            db.Events.Add(new Event
            {
                Id = foreignEventId,
                AthleteId = foreignAthleteId,
                Name = "Foreign Event",
                EventDate = new DateOnly(2026, 9, 1),
                Priority = EventPriority.A
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", ValidPlan("Event Link Test"));
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        var request = new TrainingPlanUpdateRequest
        {
            Name = created!.Name,
            Methodology = MethodologyChoice.Polarized,
            StartDate = created.StartDate,
            EndDate = created.EndDate,
            EventId = foreignEventId
        };

        var response = await client.PutAsJsonAsync($"/api/v1/trainingplans/{created.Id}", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorBody = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        errorBody.Should().NotBeNull();
        errorBody!.Errors.Should().Contain(e => e.StartsWith("EventId:"));
    }

    [Fact]
    public async Task Update_OwnEventId_LinksThePlan()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var eventResponse = await client.PostAsJsonAsync("/api/v1/events", new EventDto
        {
            Name = "Target Race",
            EventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60),
            Sport = Sport.Run,
            Priority = EventPriority.A
        });
        var createdEvent = await eventResponse.Content.ReadFromJsonAsync<Bryk.Application.Events.EventResponse>(JsonOptions);

        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", ValidPlan("Linked Plan"));
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        var request = new TrainingPlanUpdateRequest
        {
            Name = created!.Name,
            Methodology = MethodologyChoice.Polarized,
            StartDate = created.StartDate,
            EndDate = created.EndDate,
            EventId = createdEvent!.Id
        };

        var putResponse = await client.PutAsJsonAsync($"/api/v1/trainingplans/{created.Id}", request);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await putResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);
        updated!.EventId.Should().Be(createdEvent.Id);

        var events = await client.GetFromJsonAsync<List<EventListItemResponse>>("/api/v1/events", JsonOptions);
        var linked = events!.Single(e => e.Id == createdEvent.Id);
        linked.LinkedPlans.Should().ContainSingle(p => p.Id == created.Id);
    }

    [Fact]
    public async Task Update_RecoveryWeekPercentageOutOfBounds_Returns400()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", ValidPlan("Bounds Test"));
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        var request = new TrainingPlanUpdateRequest
        {
            Name = created!.Name,
            Methodology = MethodologyChoice.Polarized,
            StartDate = created.StartDate,
            EndDate = created.EndDate,
            RecoveryWeekPercentage = 20.0m
        };

        var response = await client.PutAsJsonAsync($"/api/v1/trainingplans/{created.Id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_DoesNotAlterPlannedWorkouts()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", ValidPlan("Untouched Children"));
        var created = await createResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        var request = new TrainingPlanUpdateRequest
        {
            Name = "Renamed",
            Methodology = MethodologyChoice.Polarized,
            StartDate = created!.StartDate,
            EndDate = created.EndDate
        };

        var putResponse = await client.PutAsJsonAsync($"/api/v1/trainingplans/{created.Id}", request);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await client.GetAsync($"/api/v1/trainingplans/{created.Id}");
        var reloaded = await getResponse.Content.ReadFromJsonAsync<TrainingPlanResponse>(JsonOptions);

        reloaded!.PlannedWorkouts.Should().HaveCount(created.PlannedWorkouts.Count);
        reloaded.PlannedWorkouts.Select(pw => pw.Id).Should().BeEquivalentTo(created.PlannedWorkouts.Select(pw => pw.Id));
        reloaded.PlannedWorkouts.Select(pw => pw.ScheduledDate).Should().BeEquivalentTo(created.PlannedWorkouts.Select(pw => pw.ScheduledDate));
    }
```

**Verify:** `dotnet test api/Bryk.sln --filter FullyQualifiedName~TrainingPlansControllerTests` — all 16
tests in this file pass (8 pre-existing + 8 new).

## Step 8 — Final verification, smoke test, and commit

- `dotnet build api/Bryk.sln` — 0 errors. Compare the warning count to the known 16-warning baseline
  (Step 0) — it must **not** grow. Do not fix the two pre-existing nullable warnings in
  `WorkoutsControllerTests.cs:121,150` — out of scope.
- `dotnet test api/Bryk.sln --filter FullyQualifiedName~TrainingPlan` — every `TrainingPlanServiceTests`
  and `TrainingPlansControllerTests` case passes (20 + 16 = 36 in the two files touched by this task).
- `dotnet test api/Bryk.sln` — full suite green, risen from the **201** baseline with zero failures
  (21 new tests: 13 unit + 8 integration).
- `cd ui; pnpm run build` green; `pnpm exec vitest run --no-file-parallelism` still **229 / 53 files** —
  this task touched no frontend file, so these numbers must be byte-for-byte unchanged.
- **Live smoke test** against the dev API (`https://localhost:60129`), using the seed loaded from
  `db/dev-seed.sql` (re-run it first if the local DB is stale — `@AthleteId` must match
  `DevAuth:CurrentAthleteId` from `dotnet user-secrets list`). Do not hardcode a plan id — look one up
  first, since `db/dev-seed.sql` generates plan/event ids with `NEWID()` on every run:
  1. `GET /api/v1/trainingplans` → grab an `id` from the response (the seed's "Indian Wells 70.3 Build"
     plan, `BuildWeeks=3`/`RecoveryWeeks=1`/`RecoveryWeekPercentage=70.00`, is a good one — its window
     already contains its planned workouts).
  2. **Happy path:** `PUT /api/v1/trainingplans/{id}` with a body that widens the window and changes
     `buildWeeks`/`recoveryWeeks`/`recoveryWeekPercentage` → expect `200` with the new values echoed
     **and** a non-empty `plannedWorkouts` array in the same response (the re-attach-for-projection
     check — this is the trap Step 3 calls out).
  3. **404:** `PUT /api/v1/trainingplans/{random-guid}` → expect `404`.
  4. **400 (orphan):** `PUT /api/v1/trainingplans/{id}` with a window that excludes one of that plan's
     seeded planned workouts → expect `400` and an `errors[]` entry starting with `"PlanWindow:"`.
  5. **400 (event ownership):** `PUT /api/v1/trainingplans/{id}` with `eventId` set to a random `Guid`
     (or another seeded event's id, if a second-athlete fixture is available) → expect `400` and an
     `errors[]` entry starting with `"EventId:"`.
- `git diff --stat` — confirm only the expected files changed/added:
  - `api/Bryk.Application/Training/TrainingPlanUpdateRequest.cs` (new)
  - `api/Bryk.Application/Training/Validators/TrainingPlanUpdateRequestValidator.cs` (new)
  - `api/Bryk.Application/Training/ITrainingPlanService.cs` (extended)
  - `api/Bryk.Application/Training/TrainingPlanService.cs` (extended)
  - `api/Bryk.API/Controllers/TrainingPlansController.cs` (extended)
  - `api/Bryk.Application.Tests/Training/TrainingPlanServiceTests.cs` (extended)
  - `api/Bryk.API.Tests/Training/TrainingPlansControllerTests.cs` (extended)
  - No changes to `TrainingPlanRequest.cs`, `TrainingPlanRequestValidator.cs`,
    `ITrainingPlanRepository.cs`, `TrainingPlanRepository.cs`, `Program.cs`, any migration, or any
    `*.csproj`. If the diff shows any of these — **STOP**, that is scope creep beyond `Tasks-18-2.md`.
- If at any point a step appears to require a new EF model property, a migration, or a new NuGet
  package — **STOP and flag it as a blocker**; this task carries no such approval.
- Commit with the message in `Tasks-18-2.md` (no AI co-author trailer — project convention):

```
feat: plan-metadata PUT with orphan + event-ownership guards

Close a verified gap: training plans had no metadata update, so a plan's
name, window, methodology, event link and the three ADR-0003 periodization
columns were frozen at creation. PUT /api/v1/trainingplans/{id} takes a new
metadata-only TrainingPlanUpdateRequest (no planned workouts - children stay
on their own endpoints) validated at 1-8 build weeks, >=1 recovery weeks and
a 30-90 percent recovery volume (ADR-0009 6: the field is percent-scale, not
the fraction the ROADMAP prose claims).

Two guards beyond the validator: a window that would leave existing planned
workouts outside [StartDate, EndDate] is rejected 400 with a PlanWindow:
message naming the count and range - ADR-0008 2's reschedule rule applied to
the other side of the same invariant (ADR-0009 5) - and an EventId that names
an event the athlete does not own is rejected 400. This is also the write
path Phase 17 deferred, so the plan-to-event link is finally editable.

Reuses the existing unused ITrainingPlanRepository.Update and the fresh
nav-free staging discipline; the response re-attaches the untouched children
for projection so the client's plan view keeps its sessions. No migration, no
new package, no DI change. Known accepted divergence: the POST validator
keeps its looser bounds (tightening a shipped endpoint is a breaking change).
xUnit covers the round-trip, 404 foreign plan, 400 orphan and foreign event,
the inclusive window boundary and the periodization bounds at both ends.
```
