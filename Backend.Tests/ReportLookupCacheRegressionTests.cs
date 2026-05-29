using API.Model;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Tests;

public sealed class ReportLookupCacheRegressionTests
{
    [Fact]
    public async Task Import_licence_detail_fast_report_reuses_shared_country_lookup_cache()
    {
        await using var db = ReportTestHelper.CreateInMemoryDbContext(
            $"{nameof(ReportLookupCacheRegressionTests)}_{Guid.NewGuid():N}");
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await ReportLookupCache.GetCountryNamesAsync(db, cache);

        var result = await sp_ImportLicenceDetailReport_Fast.CreatePagedResultAsync(
            db,
            cache,
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
