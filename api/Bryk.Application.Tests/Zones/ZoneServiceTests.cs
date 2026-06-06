using Bryk.Application.Common;
using Bryk.Application.Zones;
using Bryk.Application.Zones.Validators;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Zones;

public class ZoneServiceTests
{
    private static readonly Guid AthleteId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Athlete AthleteWith(params (Sport Sport, decimal? Threshold)[] profiles) => new()
    {
        Id = AthleteId,
        SportProfiles = profiles
            .Select(p => new AthleteSportProfile
            {
                Id = Guid.NewGuid(),
                AthleteId = AthleteId,
                Sport = p.Sport,
                ThresholdValue = p.Threshold
            })
            .ToList()
    };

    private static ZoneService NewService(Athlete? athlete, StubZoneRepository zoneRepo, StubUnitOfWork uow) =>
        new(new StubCurrentUserService(AthleteId),
            new ZoneOverrideRequestValidator(),
            new StubAthleteRepository(athlete),
            zoneRepo, uow);

    [Fact]
    public async Task GetZonesAsync_Bike_ComputesSevenPowerZonesFromFtp()
    {
        var service = NewService(AthleteWith((Sport.Bike, 250m)), new StubZoneRepository(), new StubUnitOfWork());

        var result = await service.GetZonesAsync();

        var bike = result.Sports.Single(s => s.Sport == Sport.Bike);
        bike.Metric.Should().Be(ZoneMetric.Power);
        bike.Zones.Should().HaveCount(7);
        bike.Zones.Single(z => z.ZoneNumber == 1).LowerBound.Should().Be(0m);
        bike.Zones.Single(z => z.ZoneNumber == 1).UpperBound.Should().Be(137.5m);   // 250 * 0.55
        bike.Zones.Single(z => z.ZoneNumber == 2).UpperBound.Should().Be(187.5m);   // 250 * 0.75
        bike.Zones.Single(z => z.ZoneNumber == 7).LowerBound.Should().Be(375m);     // 250 * 1.50
        bike.Zones.Single(z => z.ZoneNumber == 7).UpperBound.Should().BeNull();     // open-ended top
        bike.Zones.Should().OnlyContain(z => !z.IsOverride);
    }

    [Fact]
    public async Task GetZonesAsync_Run_ComputesFivePaceZonesWithInverseOrdering()
    {
        var service = NewService(AthleteWith((Sport.Run, 300m)), new StubZoneRepository(), new StubUnitOfWork());

        var result = await service.GetZonesAsync();

        var run = result.Sports.Single(s => s.Sport == Sport.Run);
        run.Metric.Should().Be(ZoneMetric.Pace);
        run.Zones.Should().HaveCount(5);
        run.Zones.Single(z => z.ZoneNumber == 5).LowerBound.Should().Be(0m);        // fastest, floor
        run.Zones.Single(z => z.ZoneNumber == 5).UpperBound.Should().Be(300m);      // = threshold pace
        run.Zones.Single(z => z.ZoneNumber == 1).LowerBound.Should().Be(375m);      // 300 * 1.25
        run.Zones.Single(z => z.ZoneNumber == 1).UpperBound.Should().BeNull();      // open-ended slow
        // Pace is inverse: a higher zone number is a faster effort = a lower bound.
        run.Zones.OrderBy(z => z.ZoneNumber).Select(z => z.LowerBound).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetZonesAsync_OverrideReplacesComputedBand()
    {
        var zoneRepo = new StubZoneRepository();
        zoneRepo.Rows.Add(new AthleteSportZone
        {
            Id = Guid.NewGuid(),
            AthleteId = AthleteId,
            Sport = Sport.Bike,
            Metric = ZoneMetric.Power,
            ZoneNumber = 2,
            LowerBound = 140m,
            UpperBound = 190m
        });
        var service = NewService(AthleteWith((Sport.Bike, 250m)), zoneRepo, new StubUnitOfWork());

        var result = await service.GetZonesAsync();

        var bike = result.Sports.Single(s => s.Sport == Sport.Bike);
        var z2 = bike.Zones.Single(z => z.ZoneNumber == 2);
        z2.LowerBound.Should().Be(140m);
        z2.UpperBound.Should().Be(190m);
        z2.IsOverride.Should().BeTrue();
        bike.Zones.Single(z => z.ZoneNumber == 1).IsOverride.Should().BeFalse(); // others stay computed
    }

    [Fact]
    public async Task GetZonesAsync_NoThresholdForSport_OmitsThatSport()
    {
        var service = NewService(AthleteWith((Sport.Bike, 250m), (Sport.Run, null)), new StubZoneRepository(), new StubUnitOfWork());

        var result = await service.GetZonesAsync();

        result.Sports.Should().ContainSingle(s => s.Sport == Sport.Bike);
        result.Sports.Should().NotContain(s => s.Sport == Sport.Run);
    }

    [Fact]
    public async Task SetOverridesAsync_NoThreshold_ThrowsValidation()
    {
        var zoneRepo = new StubZoneRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(AthleteWith((Sport.Bike, 250m)), zoneRepo, uow); // no Run threshold

        var req = new ZoneOverrideRequest { Zones = { new ZoneBoundDto { ZoneNumber = 1, LowerBound = 100m } } };
        var act = () => service.SetOverridesAsync(Sport.Run, req);

        await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        zoneRepo.Added.Should().Be(0);
        uow.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task SetOverridesAsync_ValidOverride_StagesSavesAndReturnsSport()
    {
        var zoneRepo = new StubZoneRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(AthleteWith((Sport.Bike, 250m)), zoneRepo, uow);

        var req = new ZoneOverrideRequest { Zones = { new ZoneBoundDto { ZoneNumber = 2, LowerBound = 140m, UpperBound = 190m } } };
        var result = await service.SetOverridesAsync(Sport.Bike, req);

        result.Sport.Should().Be(Sport.Bike);
        var z2 = result.Zones.Single(z => z.ZoneNumber == 2);
        z2.LowerBound.Should().Be(140m);
        z2.IsOverride.Should().BeTrue();
        zoneRepo.Added.Should().Be(1);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task SetOverridesAsync_UpperBelowLower_ThrowsValidation()
    {
        var zoneRepo = new StubZoneRepository();
        var uow = new StubUnitOfWork();
        var service = NewService(AthleteWith((Sport.Bike, 250m)), zoneRepo, uow);

        var req = new ZoneOverrideRequest { Zones = { new ZoneBoundDto { ZoneNumber = 2, LowerBound = 200m, UpperBound = 100m } } };
        var act = () => service.SetOverridesAsync(Sport.Bike, req);

        await act.Should().ThrowAsync<Bryk.Application.Exceptions.ValidationException>();
        uow.SaveCount.Should().Be(0);
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

    private sealed class StubZoneRepository : IAthleteSportZoneRepository
    {
        public List<AthleteSportZone> Rows { get; } = new();
        public int Added { get; private set; }
        public int Removed { get; private set; }

        public Task<IReadOnlyList<AthleteSportZone>> GetByAthleteIdAsync(Guid athleteId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AthleteSportZone>>(Rows.Where(r => r.AthleteId == athleteId).ToList());

        public Task AddAsync(AthleteSportZone entity, CancellationToken ct = default)
        {
            Rows.Add(entity);
            Added++;
            return Task.CompletedTask;
        }

        public void Remove(AthleteSportZone entity)
        {
            Rows.RemoveAll(r => r.Id == entity.Id);
            Removed++;
        }
    }

    private sealed class StubAthleteRepository(Athlete? athlete) : IAthleteRepository
    {
        public Task<Athlete?> GetWithSportProfilesAsync(Guid id, CancellationToken ct = default) => Task.FromResult(athlete);

        public Task<Athlete?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Athlete?> GetFullProfileAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Athlete>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(Athlete athlete, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(Athlete athlete) => throw new NotImplementedException();
        public void Delete(Athlete athlete) => throw new NotImplementedException();
        public Task<AthleteSportProfile?> GetSportProfileAsync(Guid athleteId, Sport sport, CancellationToken ct = default) => throw new NotImplementedException();
        public void AddSportProfile(AthleteSportProfile profile) => throw new NotImplementedException();
        public void UpdateSportProfile(AthleteSportProfile profile) => throw new NotImplementedException();
    }
}
