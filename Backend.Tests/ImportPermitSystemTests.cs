using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Backend.Tests;

/// <summary>
/// Level 3 (system): boots the whole API in-process via WebApplicationFactory and exercises it
/// over real HTTP — proving the app assembles (full DI graph, the 180s DbContext pool, JWT auth,
/// controller routing) and that the Import Permit report endpoints are wired and protected. The
/// data path itself is covered by the integration + live-DB tests; here we assert the system
/// boundary: anonymous calls are rejected (auth pipeline) and routing resolves.
/// </summary>
public sealed class ImportPermitSystemTests : IClassFixture<ImportPermitSystemTests.ReportApiFactory>
{
    private readonly ReportApiFactory _factory;

    public ImportPermitSystemTests(ReportApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ImportPermit_BySection_endpoint_is_routed_and_requires_authentication()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/ImportPermitBySectionReport",
            new { FromDate = "2025-01-01T00:00:00", ToDate = "2025-12-31T00:00:00", PageSize = 10, IncludeTotalCount = true });

        // The endpoint exists (routing resolved) and the [Authorize]/JWT pipeline rejects the
        // anonymous request rather than reaching the controller.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ImportPermit_Voucher_endpoint_is_routed_and_requires_authentication()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/ImportPermitVoucherReport",
            new { FromDate = "2025-01-01T00:00:00", ToDate = "2025-12-31T00:00:00", PageSize = 10, IncludeTotalCount = true });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_route_returns_404_confirming_the_host_booted()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/__definitely_not_a_real_endpoint__");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Boots the real Program with test-safe config (a JWT signing key — Program requires one —
    /// and a well-formed placeholder connection string) and drops the Excel background workers
    /// so the host starts without reaching TemplateDB. The auth/404 assertions never open the DB.
    /// </summary>
    public sealed class ReportApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWT:Key"] = "tradenet-system-test-signing-key-0123456789-abcdef",
                    ["ConnectionStrings:TradeNetDBTest"] =
                        "Server=localhost;Database=SystemTestPlaceholder;Trusted_Connection=True;TrustServerCertificate=True;",
                });
            });

            builder.ConfigureServices(services =>
            {
                foreach (var hostedService in services
                    .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                    .ToList())
                {
                    services.Remove(hostedService);
                }
            });
        }
    }
}
