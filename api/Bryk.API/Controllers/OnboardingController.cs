using Asp.Versioning;
using Bryk.Application.Onboarding;
using Microsoft.AspNetCore.Mvc;

namespace Bryk.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class OnboardingController(IOnboardingService onboardingService) : ControllerBase
{
    /// <summary>Returns onboarding completion flags for the current athlete.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatusAsync(CancellationToken cancellationToken)
    {
        OnboardingStatusResponse result = await onboardingService.GetStatusAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Submits the required onboarding step (identity and core profile).</summary>
    [HttpPost("required")]
    public async Task<IActionResult> SubmitRequiredAsync(
        [FromBody] OnboardingRequiredRequest request,
        CancellationToken cancellationToken)
    {
        await onboardingService.SubmitRequiredAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Submits the recommended onboarding step (HR data and sport thresholds). Returns 409 if the required step is not yet complete.</summary>
    [HttpPost("recommended")]
    public async Task<IActionResult> SubmitRecommendedAsync(
        [FromBody] OnboardingRecommendedRequest request,
        CancellationToken cancellationToken)
    {
        await onboardingService.SubmitRecommendedAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Submits the goals onboarding step (events and goals). Append-only — multiple submissions accumulate.</summary>
    [HttpPost("goals")]
    public async Task<IActionResult> SubmitGoalsAsync(
        [FromBody] OnboardingGoalsRequest request,
        CancellationToken cancellationToken)
    {
        await onboardingService.SubmitGoalsAsync(request, cancellationToken);
        return NoContent();
    }
}
