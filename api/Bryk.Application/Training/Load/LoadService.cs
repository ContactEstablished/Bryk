using Bryk.Application.Zones;
using Bryk.Domain.Entities;
using Bryk.Domain.Interfaces;

namespace Bryk.Application.Training.Load;

public class LoadService(
    IAthleteRepository athleteRepo,
    IZoneService zoneService) : ILoadService
{
    public async Task<decimal?> ComputePlannedLoadAsync(PlannedWorkout workout, CancellationToken ct = default)
    {
        var profile = await athleteRepo.GetSportProfileAsync(workout.AthleteId, workout.Sport, ct);

        SportZonesResponse? zones = null;
        if (workout.Sport is Sport.Bike or Sport.Run or Sport.Swim)
        {
            var all = await zoneService.GetZonesAsync(ct);
            zones = all.Sports.FirstOrDefault(s => s.Sport == workout.Sport);
        }

        return LoadCalculator.ComputePlannedLoad(workout, profile, zones);
    }
}
