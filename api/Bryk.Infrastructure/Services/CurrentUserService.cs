using Bryk.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Bryk.Infrastructure.Services;

public class CurrentUserService(
    IHostEnvironment hostEnvironment,
    IConfiguration configuration) : ICurrentUserService
{
    public Guid GetCurrentAthleteId()
    {
        if (!hostEnvironment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "CurrentUserService dev stub invoked outside Development environment. " +
                "Real authentication must be implemented before deploying to any non-Development environment.");
        }

        var configValue = configuration["DevAuth:CurrentAthleteId"];
        if (string.IsNullOrEmpty(configValue) || !Guid.TryParse(configValue, out var athleteId))
        {
            throw new InvalidOperationException(
                "DevAuth:CurrentAthleteId is missing or invalid in configuration. " +
                "Add a valid Guid to appsettings.Development.json.");
        }

        return athleteId;
    }
}
