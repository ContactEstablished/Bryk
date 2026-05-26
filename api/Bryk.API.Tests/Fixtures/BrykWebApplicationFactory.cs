using Bryk.Application.Common;
using Bryk.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bryk.API.Tests.Fixtures;

/// <summary>
/// WebApplicationFactory for integration tests against the Bryk API.
///
/// Test-DB strategy (Phase 6 bootstrap): EF Core InMemory provider. This was picked
/// to land the first integration slice quickly without adding Docker as a CI/dev
/// dependency. InMemory diverges from SQL Server (no real constraints, no unique-index
/// enforcement, no transactions) and is therefore only adequate for the controller
/// surface tests that exist today.
///
/// The preferred future strategy is <c>Testcontainers.MsSql</c> for high-fidelity
/// integration coverage as Phase 7 adds persistence-boundary behavior worth testing
/// against real SQL Server semantics. Decision is tracked for the Task 6-6 ADR.
/// </summary>
public class BrykWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Athlete id surfaced through the test ICurrentUserService for every request
    /// in tests. Tests that need fresh state can construct their own factory instance —
    /// each factory gets its own InMemory database name.
    /// </summary>
    public static readonly Guid TestAthleteId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly string _databaseName = $"BrykTests-{Guid.NewGuid()}";

    static BrykWebApplicationFactory()
    {
        // The Windows .NET SDK running against a WSL UNC path can recurse while wiring
        // reload-on-change file watchers for appsettings.json during WebApplication.CreateBuilder.
        // Disable host config reload for tests; production configuration behavior is unchanged.
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "InMemory-not-used",
                ["DevAuth:CurrentAthleteId"] = TestAthleteId.ToString()
            });
        });

        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(ApplicationDbContext) ||
                    d.ServiceType == typeof(ICurrentUserService) ||
                    d.ServiceType.Name.Contains("DbContextOptionsConfiguration", StringComparison.Ordinal))
                .ToList();

            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            services.AddScoped<ICurrentUserService>(_ => new TestCurrentUserService(TestAthleteId));
        });
    }

    private sealed class TestCurrentUserService(Guid athleteId) : ICurrentUserService
    {
        public Guid GetCurrentAthleteId() => athleteId;
    }
}
