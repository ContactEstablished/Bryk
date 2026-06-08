using Bryk.Application.Common;
using Bryk.Application.Common.Validation;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;
using FluentValidation;
using ValidationException = Bryk.Application.Exceptions.ValidationException;

namespace Bryk.Application.Zones;

public class ZoneService(
    ICurrentUserService currentUser,
    IValidator<ZoneOverrideRequest> validator,
    IAthleteRepository athleteRepo,
    IAthleteSportZoneRepository zoneRepo,
    IUnitOfWork unitOfWork) : IZoneService
{
    // Sports with a computed zone model. Strength and Triathlon have none.
    private static readonly Sport[] ZoneSports = { Sport.Bike, Sport.Run, Sport.Swim };

    // Coggan 7-zone power, as fractions of FTP. High null = open-ended top zone.
    private static readonly (int Zone, decimal Low, decimal? High)[] PowerBands =
    {
        (1, 0.00m, 0.55m), (2, 0.55m, 0.75m), (3, 0.75m, 0.90m),
        (4, 0.90m, 1.05m), (5, 1.05m, 1.20m), (6, 1.20m, 1.50m), (7, 1.50m, null)
    };

    // 5-zone pace, as multiples of threshold pace (seconds-per-unit). Pace is inverse — a faster
    // effort is a *smaller* number — so Z1 (recovery) is the slowest/largest and open-ended at the
    // top, Z5 (fastest) bottoms out at 0. Bounds stay numerically Low <= High within each zone.
    private static readonly (int Zone, decimal Low, decimal? High)[] PaceBands =
    {
        (1, 1.25m, null), (2, 1.15m, 1.25m), (3, 1.05m, 1.15m), (4, 1.00m, 1.05m), (5, 0.00m, 1.00m)
    };

    public async Task<ZonesResponse> GetZonesAsync(CancellationToken ct = default)
    {
        var athleteId = currentUser.GetCurrentAthleteId();
        var athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct);
        var overrides = await zoneRepo.GetByAthleteIdAsync(athleteId, ct);

        var sports = new List<SportZonesResponse>();
        if (athlete is not null)
        {
            foreach (var sport in ZoneSports)
            {
                var threshold = athlete.SportProfiles.FirstOrDefault(p => p.Sport == sport)?.ThresholdValue;
                if (threshold is not { } t || t <= 0)
                {
                    continue; // no threshold → no zones for this sport (graceful)
                }

                var metric = PrimaryMetric(sport);
                var zones = ComputeBands(sport, t)
                    .Select(b =>
                    {
                        var ovr = overrides.FirstOrDefault(o =>
                            o.Sport == sport && o.Metric == metric && o.ZoneNumber == b.Zone);
                        return new ZoneDto
                        {
                            ZoneNumber = b.Zone,
                            LowerBound = ovr?.LowerBound ?? b.Low,
                            UpperBound = ovr is not null ? ovr.UpperBound : b.High,
                            IsOverride = ovr is not null
                        };
                    })
                    .ToList();

                sports.Add(new SportZonesResponse { Sport = sport, Metric = metric, Zones = zones });
            }
        }

        return new ZonesResponse { Sports = sports };
    }

    public async Task<SportZonesResponse> SetOverridesAsync(Sport sport, ZoneOverrideRequest request, CancellationToken ct = default)
    {
        await validator.ValidateOrThrowAsync(request, ct);
        EnsureZoneSport(sport);

        var max = MaxZones(sport);
        if (request.Zones.Any(z => z.ZoneNumber < 1 || z.ZoneNumber > max))
        {
            throw new ValidationException(new[] { $"Zone numbers for {sport} must be between 1 and {max}." });
        }

        var athleteId = currentUser.GetCurrentAthleteId();
        var athlete = await athleteRepo.GetWithSportProfilesAsync(athleteId, ct);
        var threshold = athlete?.SportProfiles.FirstOrDefault(p => p.Sport == sport)?.ThresholdValue;
        if (threshold is not { } t || t <= 0)
        {
            throw new ValidationException(new[] { $"Set your {sport} threshold before customizing its zones." });
        }

        await ReplaceOverridesAsync(athleteId, sport, request.Zones, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var refreshed = await GetZonesAsync(ct);
        return refreshed.Sports.First(s => s.Sport == sport);
    }

    public async Task ResetOverridesAsync(Sport sport, CancellationToken ct = default)
    {
        EnsureZoneSport(sport);
        var athleteId = currentUser.GetCurrentAthleteId();
        await ReplaceOverridesAsync(athleteId, sport, Array.Empty<ZoneBoundDto>(), ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    // Stage-delete the existing overrides for this sport's metric, then stage the supplied bands.
    // Existing rows come from a no-tracking read; Remove re-attaches them by key as Deleted.
    private async Task ReplaceOverridesAsync(Guid athleteId, Sport sport, IReadOnlyCollection<ZoneBoundDto> zones, CancellationToken ct)
    {
        var metric = PrimaryMetric(sport);
        var existing = await zoneRepo.GetByAthleteIdAsync(athleteId, ct);
        foreach (var stale in existing.Where(o => o.Sport == sport && o.Metric == metric))
        {
            zoneRepo.Remove(stale);
        }

        foreach (var z in zones)
        {
            await zoneRepo.AddAsync(new AthleteSportZone
            {
                Id = Guid.NewGuid(),
                AthleteId = athleteId,
                Sport = sport,
                Metric = metric,
                ZoneNumber = z.ZoneNumber,
                LowerBound = z.LowerBound,
                UpperBound = z.UpperBound
            }, ct);
        }
    }

    private static IEnumerable<(int Zone, decimal Low, decimal? High)> ComputeBands(Sport sport, decimal threshold)
    {
        var bands = PrimaryMetric(sport) == ZoneMetric.Power ? PowerBands : PaceBands;
        return bands.Select(b => (b.Zone, threshold * b.Low, b.High is { } h ? threshold * h : (decimal?)null));
    }

    private static ZoneMetric PrimaryMetric(Sport sport) => sport switch
    {
        Sport.Bike => ZoneMetric.Power,
        Sport.Run or Sport.Swim => ZoneMetric.Pace,
        _ => throw new ValidationException(new[] { $"Zones are not defined for {sport}." })
    };

    private static int MaxZones(Sport sport) => ZoneScheme.Count(sport);

    private static void EnsureZoneSport(Sport sport)
    {
        if (!ZoneSports.Contains(sport))
        {
            throw new ValidationException(new[] { $"Zones are not defined for {sport}." });
        }
    }
}
