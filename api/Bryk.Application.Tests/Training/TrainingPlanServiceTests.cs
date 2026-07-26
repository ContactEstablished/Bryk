using Bryk.Application.Calendar.Validators;
using Bryk.Application.Common;
using Bryk.Application.Training;
using Bryk.Application.Training.Validators;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Training;

public class TrainingPlanServiceTests
{
    private static readonly Guid AthleteId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TrainingPlanRequest ValidPlan(string name = "Base Block") => new()
    {
        Name = name,
        Methodology = MethodologyChoice.Polarized,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(28)
    };

    private static PlannedWorkoutDto ValidWorkout(string title = "Easy Run") => new()
    {
        Sport = Sport.Run,
        ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
        Title = title,
        PlannedDurationMinutes = 60,
        PlannedLoad = 50.0m
    };

    private static TrainingPlanUpdateRequest ValidUpdate(string name = "Updated Block") => new()
    {
        Name = name,
        Methodology = MethodologyChoice.Polarized,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(28)
    };

    private static TrainingPlanService NewService(StubTrainingPlanRepository repo, StubUnitOfWork uow, Guid? athleteId = null, StubEventRepository? eventRepo = null) =>
        new(new StubCurrentUserService(athleteId ?? AthleteId),
            new TrainingPlanRequestValidator(),
            new PlannedWorkoutDtoValidator(),
            new ScheduleRequestValidator(),
            new TrainingPlanUpdateRequestValidator(),
            repo,
            eventRepo ?? new StubEventRepository(),
            uow);

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsForCurrentAthleteWithChildren()
    {
        var repo = new StubTrainingPlanRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = ValidPlan();
        request.PlannedWorkouts = new List<PlannedWorkoutDto> { ValidWorkout("Long Ride") };

        var result = await service.CreateAsync(request);

        repo.Added.Should().NotBeNull();
        repo.Added!.AthleteId.Should().Be(AthleteId);
        repo.Added.PlannedWorkouts.Should().ContainSingle();
        repo.Added.PlannedWorkouts.Single().AthleteId.Should().Be(AthleteId);
        repo.Added.PlannedWorkouts.Single().TrainingPlanId.Should().Be(repo.Added.Id);
        result.Id.Should().NotBeEmpty().And.Be(repo.Added.Id);
        result.Name.Should().Be("Base Block");
        result.PlannedWorkouts.Should().ContainSingle(pw => pw.Title == "Long Ride" && pw.Id != Guid.Empty);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_EndDateBeforeStartDate_ThrowsValidation()
    {
        var repo = new StubTrainingPlanRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var request = ValidPlan();
        request.EndDate = request.StartDate.AddDays(-1);

        var act = () => service.CreateAsync(request);

        await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.Added.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task AddPlannedWorkoutAsync_OwnedPlan_StagesAndReturnsResponse()
    {
        var planId = Guid.NewGuid();
        var plan = new TrainingPlan { Id = planId, AthleteId = AthleteId };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var result = await service.AddPlannedWorkoutAsync(planId, ValidWorkout("Tempo"));

        repo.AddedPlannedWorkout.Should().NotBeNull();
        repo.AddedPlannedWorkout!.TrainingPlanId.Should().Be(planId);
        repo.AddedPlannedWorkout.AthleteId.Should().Be(AthleteId);
        result.Id.Should().NotBeEmpty();
        result.Title.Should().Be("Tempo");
        result.TrainingPlanId.Should().Be(planId);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task AddPlannedWorkoutAsync_EmptyTitle_ThrowsValidation()
    {
        var planId = Guid.NewGuid();
        var plan = new TrainingPlan { Id = planId, AthleteId = AthleteId };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var invalid = ValidWorkout();
        invalid.Title = "";

        var act = () => service.AddPlannedWorkoutAsync(planId, invalid);

        await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        repo.AddedPlannedWorkout.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdatePlannedWorkoutAsync_ForeignPlan_ThrowsKeyNotFound()
    {
        var planId = Guid.NewGuid();
        var plan = new TrainingPlan { Id = planId, AthleteId = Guid.NewGuid() }; // belongs to someone else
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.UpdatePlannedWorkoutAsync(planId, Guid.NewGuid(), ValidWorkout());

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repo.UpdatedPlannedWorkout.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task RemovePlannedWorkoutAsync_ForeignPlan_ThrowsKeyNotFound()
    {
        var planId = Guid.NewGuid();
        var plan = new TrainingPlan { Id = planId, AthleteId = Guid.NewGuid() }; // belongs to someone else
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        var act = () => service.RemovePlannedWorkoutAsync(planId, Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
        repo.RemovedPlannedWorkout.Should().BeNull();
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task RemovePlannedWorkoutAsync_OwnedPlannedWorkout_Removes()
    {
        var planId = Guid.NewGuid();
        var pwId = Guid.NewGuid();
        var plan = new TrainingPlan
        {
            Id = planId,
            AthleteId = AthleteId,
            PlannedWorkouts = new List<PlannedWorkout>
            {
                new() { Id = pwId, AthleteId = AthleteId, TrainingPlanId = planId, Title = "Drop me" }
            }
        };
        var repo = new StubTrainingPlanRepository { ToReturn = plan };
        var uow = new StubUnitOfWork();
        var service = NewService(repo, uow);

        await service.RemovePlannedWorkoutAsync(planId, pwId);

        repo.RemovedPlannedWorkout.Should().NotBeNull();
        repo.RemovedPlannedWorkout!.Id.Should().Be(pwId);
        uow.SaveCount.Should().Be(1);
    }

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

    private sealed class StubTrainingPlanRepository : ITrainingPlanRepository
    {
        public TrainingPlan? ToReturn { get; init; }
        public IReadOnlyList<TrainingPlan> ByAthlete { get; init; } = new List<TrainingPlan>();
        public TrainingPlan? Added { get; private set; }
        public TrainingPlan? Updated { get; private set; }
        public TrainingPlan? Deleted { get; private set; }
        public PlannedWorkout? AddedPlannedWorkout { get; private set; }
        public PlannedWorkout? UpdatedPlannedWorkout { get; private set; }
        public PlannedWorkout? RemovedPlannedWorkout { get; private set; }

        public Task<TrainingPlan?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task<IReadOnlyList<TrainingPlan>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default) => Task.FromResult(ByAthlete);
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsInRangeWithStructureAsync(Guid athleteId, DateOnly start, DateOnly end, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlannedWorkout>> GetPlannedWorkoutsByIdsWithStructureAsync(IEnumerable<Guid> ids, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<TrainingPlan>> GetByEventIdsAsync(IEnumerable<Guid> eventIds, CancellationToken ct = default) => throw new NotImplementedException();

        public Task AddAsync(TrainingPlan entity, CancellationToken ct = default)
        {
            Added = entity;
            return Task.CompletedTask;
        }

        public void Update(TrainingPlan entity) => Updated = entity;
        public void Delete(TrainingPlan entity) => Deleted = entity;

        public Task AddPlannedWorkoutAsync(PlannedWorkout plannedWorkout, CancellationToken ct = default)
        {
            AddedPlannedWorkout = plannedWorkout;
            return Task.CompletedTask;
        }

        public void UpdatePlannedWorkout(PlannedWorkout plannedWorkout) => UpdatedPlannedWorkout = plannedWorkout;
        public void RemovePlannedWorkout(PlannedWorkout plannedWorkout) => RemovedPlannedWorkout = plannedWorkout;

        public Task<PlannedWorkout?> GetPlannedWorkoutWithStructureAsync(Guid plannedWorkoutId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddWorkoutBlockAsync(WorkoutBlock block, CancellationToken ct = default) => throw new NotImplementedException();
        public void UpdateWorkoutBlock(WorkoutBlock block) => throw new NotImplementedException();
        public void RemoveWorkoutBlock(WorkoutBlock block) => throw new NotImplementedException();
        public Task AddWorkoutStepAsync(WorkoutStep step, CancellationToken ct = default) => throw new NotImplementedException();
        public void UpdateWorkoutStep(WorkoutStep step) => throw new NotImplementedException();
        public void RemoveWorkoutStep(WorkoutStep step) => throw new NotImplementedException();
    }

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
}
