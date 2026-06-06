using Bryk.Domain.Entities;

namespace Bryk.Application.Zones;

/// <summary>
/// Computes the current athlete's training zones from their <see cref="AthleteSportProfile"/>
/// thresholds (bike = 7-zone power from FTP; run/swim = 5-zone pace from threshold pace) and applies
/// any saved per-sport overrides. Athlete identity comes from <see cref="Common.ICurrentUserService"/>.
/// </summary>
public interface IZoneService
{
    /// <summary>Returns the effective zones for every sport the athlete has a threshold for.</summary>
    Task<ZonesResponse> GetZonesAsync(CancellationToken ct = default);

    /// <summary>
    /// Replaces the saved overrides for one sport with the supplied bands and returns that sport's
    /// effective zones. Throws <see cref="Exceptions.ValidationException"/> if the sport has no zone
    /// model, has no threshold set, or a band is out of range.
    /// </summary>
    Task<SportZonesResponse> SetOverridesAsync(Sport sport, ZoneOverrideRequest request, CancellationToken ct = default);

    /// <summary>Clears the saved overrides for one sport, reverting it to computed zones.</summary>
    Task ResetOverridesAsync(Sport sport, CancellationToken ct = default);
}
