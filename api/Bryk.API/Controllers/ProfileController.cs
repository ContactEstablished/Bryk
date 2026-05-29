using Asp.Versioning;
using Bryk.Application.Profile;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProfileController(IProfileService profileService) : ControllerBase
{
    /// <summary>Returns the current athlete's required profile fields. 404 if the required onboarding step is not complete.</summary>
    [HttpGet("required")]
    public async Task<IActionResult> GetRequiredAsync(CancellationToken cancellationToken)
    {
        ProfileRequiredResponse? result = await profileService.GetRequiredAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Returns the current athlete's recommended profile data (HR fields and sport thresholds). 404 if the required onboarding step is not complete.</summary>
    [HttpGet("recommended")]
    public async Task<IActionResult> GetRecommendedAsync(CancellationToken cancellationToken)
    {
        ProfileRecommendedResponse? result = await profileService.GetRecommendedAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Returns the current athlete's saved events and goals. 404 if the required onboarding step is not complete.</summary>
    [HttpGet("goals")]
    public async Task<IActionResult> GetGoalsAsync(CancellationToken cancellationToken)
    {
        ProfileGoalsResponse? result = await profileService.GetGoalsAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
