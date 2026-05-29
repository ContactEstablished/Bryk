using Bryk.Application.Common;
using Bryk.Application.Profile;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Profile;

public class ProfileServiceTests
{
    private static readonly Guid AthleteId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetRequiredAsync_WhenAthleteExists_ReturnsMappedFields()
    {
        var athlete = new Athlete
        {
            Id = AthleteId,
            Name = "Test Athlete",
            Gender = Gender.Female,
            DateOfBirth = new DateOnly(1992, 6, 15),
            HeightCm = 170m,
            WeightKg = 65m,
            YearsTraining = 4,
            TypicalWeeklyHours = 9m,
            Methodology = MethodologyChoice.Polarized
        };

        var service = new ProfileService(
            currentUser: new StubCurrentUserService(AthleteId),
            athleteRepo: new StubAthleteRepository { ById = athlete },
            eventRepo: null!,
            goalRepo: null!);

        var result = await service.GetRequiredAsync();

        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Athlete");
        result.Gender.Should().Be(Gender.Female);
        result.DateOfBirth.Should().Be(new DateOnly(1992, 6, 15));
        result.HeightCm.Should().Be(170m);
        result.WeightKg.Should().Be(65m);
        result.YearsTraining.Should().Be(4);
        result.TypicalWeeklyHours.Should().Be(9m);
        result.Methodology.Should().Be(MethodologyChoice.Polarized);
    }

    [Fact]
    public async Task GetRequiredAsync_WhenAthleteMissing_ReturnsNull()
    {
        var service = new ProfileService(
            currentUser: new StubCurrentUserService(AthleteId),
            athleteRepo: new StubAthleteRepository { ById = null },
            eventRepo: null!,
            goalRepo: null!);

        var result = await service.GetRequiredAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRecommendedAsync_WhenAthleteExists_MapsHrAndThresholds()
    {
        var athlete = new Athlete
        {
            Id = AthleteId,
            RestingHr = 48,
            MaxHr = 190,
            SportProfiles = new List<AthleteSportProfile>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    AthleteId = AthleteId,
                    Sport = Sport.Bike,
                    IsActive = true,
                    ThresholdValue = 280m,
                    Lt1 = 200m,
                    Lt2 = 260m,
                    CustomZonesJson = null
                }
            }
        };

        var service = new ProfileService(
            currentUser: new StubCurrentUserService(AthleteId),
            athleteRepo: new StubAthleteRepository { WithSportProfiles = athlete },
            eventRepo: null!,
            goalRepo: null!);

        var result = await service.GetRecommendedAsync();

        result.Should().NotBeNull();
        result!.RestingHr.Should().Be(48);
        result.MaxHr.Should().Be(190);
        result.SportThresholds.Should().ContainSingle();
        var threshold = result.SportThresholds[0];
        threshold.Sport.Should().Be(Sport.Bike);
        threshold.IsActive.Should().BeTrue();
        threshold.ThresholdValue.Should().Be(280m);
        threshold.Lt1.Should().Be(200m);
        threshold.Lt2.Should().Be(260m);
    }

    [Fact]
    public async Task GetGoalsAsync_WhenAthleteMissing_ReturnsNull()
    {
        // Proves the existence-check 404 rule: even though events/goals query independently,
        // a missing athlete row short-circuits to null before they are read.
        var service = new ProfileService(
            currentUser: new StubCurrentUserService(AthleteId),
            athleteRepo: new StubAthleteRepository { ById = null },
            eventRepo: null!,
            goalRepo: null!);

        var result = await service.GetGoalsAsync();

        result.Should().BeNull();
    }

    private sealed class StubCurrentUserService(Guid athleteId) : ICurrentUserService
    {
        public Guid GetCurrentAthleteId() => athleteId;
    }

    private sealed class StubAthleteRepository : IAthleteRepository
    {
        public Athlete? ById { get; init; }
        public Athlete? WithSportProfiles { get; init; }

        public Task<Athlete?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ById);
        public Task<Athlete?> GetWithSportProfilesAsync(Guid id, CancellationToken ct = default) => Task.FromResult(WithSportProfiles);

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
