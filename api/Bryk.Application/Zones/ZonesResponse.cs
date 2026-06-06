namespace Bryk.Application.Zones;

// The current athlete's effective zones across every sport that has a threshold. Sports without a
// threshold (or Strength/Triathlon, which have no zone model) are simply absent.
public class ZonesResponse
{
    public IReadOnlyList<SportZonesResponse> Sports { get; set; } = new List<SportZonesResponse>();
}
