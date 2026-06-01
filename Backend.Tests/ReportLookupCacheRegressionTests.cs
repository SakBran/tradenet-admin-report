using API.Model;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Tests;

public sealed class ReportLookupCacheRegressionTests
{
    [Fact]
    public async Task Import_licence_detail_fast_report_resolves_countries_from_in_memory_cache()
    {
        await using var db = ReportTestHelper.CreateInMemoryDbContext(
            $"{nameof(ReportLookupCacheRegressionTests)}_{Guid.NewGuid():N}");

        var services = new ServiceCollection();
        services.AddSingleton(db);
        await using var provider = services.BuildServiceProvider();

        var countryCache = new CountryCache(provider.GetRequiredService<IServiceScopeFactory>());
        await countryCache.EnsureLoadedAsync();

        var result = await sp_ImportLicenceDetailReport_Fast.CreatePagedResultAsync(
            db,
            countryCache,
            new sp_ImportLicenceDetailReportRequest
            {
                Type = "Oversea",
                FromDate = ReportTestHelper.FromDate,
                ToDate = ReportTestHelper.ToDate,
                CompanyRegistrationNo = string.Empty
            },
            new ReportQueryRequest
            {
                PageSize = 10
            });

        Assert.Empty(result.Data);
        Assert.Equal(0, result.TotalCount);
    }
}
