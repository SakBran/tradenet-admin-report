using API.DBContext;
using API.Model;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Backend.Tests;

public sealed class ExportLicenceDetailReportLiveDbTests(ITestOutputHelper output)
{
    private const string SkipReason =
        "Set TRADENET_REPORT_TEST_CONNECTION_STRING to a reachable TradeNetDB to run this live integration test.";

    private TradeNetDbContext? TryConnect()
    {
        var cs = Environment.GetEnvironmentVariable("TRADENET_REPORT_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(cs))
        {
            output.WriteLine("SKIPPED: " + SkipReason);
            return null;
        }

        var options = new DbContextOptionsBuilder<TradeNetDbContext>()
            .UseSqlServer(cs, sql => sql.CommandTimeout(60))
            .Options;
        var db = new TradeNetDbContext(options);

        try
        {
            if (db.Database.CanConnect())
            {
                return db;
            }

            output.WriteLine("SKIPPED: connection string set but database unreachable.");
            db.Dispose();
            return null;
        }
        catch (Exception ex)
        {
            output.WriteLine("SKIPPED: " + ex.Message);
            db.Dispose();
            return null;
        }
    }

    [Fact]
    public async Task Detail_page_against_live_db_returns_first_page_without_stored_procedure_timeout()
        => await AssertDetailPageLoads(includeTotalCount: false);

    [Fact]
    public async Task Detail_page_exact_count_against_live_db_returns_without_stored_procedure_timeout()
        => await AssertDetailPageLoads(includeTotalCount: true);

    [Fact]
    public async Task Detail_page_ui_default_three_month_range_returns_rows_against_live_db()
        => await AssertDetailPageLoads(
            includeTotalCount: false,
            fromDate: new DateTime(2026, 4, 1),
            toDate: new DateTime(2026, 6, 11, 23, 59, 59),
            pageSize: 10);

    [Fact]
    public async Task Detail_page_may_2025_three_day_slice_returns_without_timeout()
        => await AssertDetailPageLoads(
            includeTotalCount: false,
            fromDate: new DateTime(2025, 5, 1),
            toDate: new DateTime(2025, 5, 3, 23, 59, 59),
            pageSize: 10,
            requireRows: false);

    [Fact]
    public async Task Detail_page_may_1_to_may_2_2025_returns_without_timeout()
        => await AssertDetailPageLoads(
            includeTotalCount: false,
            fromDate: new DateTime(2025, 5, 1),
            toDate: new DateTime(2025, 5, 2, 23, 59, 59),
            pageSize: 10,
            requireRows: false);

    [Fact]
    public async Task Detail_page_may_1_to_may_2_2025_exact_count_matches_live_db()
        => await AssertDetailPageLoads(
            includeTotalCount: true,
            fromDate: new DateTime(2025, 5, 1),
            toDate: new DateTime(2025, 5, 2, 23, 59, 59),
            pageSize: 10,
            requireRows: false,
            expectedTotalCount: 2420,
            requireItemValues: true);

    [Theory]
    [InlineData(typeof(ExportLicenceBySectionReportController))]
    [InlineData(typeof(ExportLicenceByMethodReportController))]
    [InlineData(typeof(ExportLicenceBySellerCountryReportController))]
    [InlineData(typeof(ExportLicenceCompanyListReportController))]
    [InlineData(typeof(ExportLicenceDailyReportNewLicenceReportController))]
    [InlineData(typeof(ExportLicenceTotalValueLicencesReportController))]
    public async Task List_report_against_live_db_returns_rows_without_old_detail_timeout(Type controllerType)
    {
        var db = TryConnect();
        if (db is null)
        {
            return;
        }

        await using (db)
        {
            var controller = ReportTestHelper.CreateController(controllerType, db);
            var request = Activator.CreateInstance(ReportTestHelper.GetRequestType(controllerType))
                ?? throw new InvalidOperationException($"Could not create request for {controllerType.Name}.");

            Set(request, "FromDate", new DateTime(2026, 4, 1));
            Set(request, "ToDate", new DateTime(2026, 6, 11, 23, 59, 59));
            Set(request, "PageIndex", 0);
            Set(request, "PageSize", 10);
            Set(request, "IncludeTotalCount", false);

            var post = controllerType.GetMethod("Post")
                ?? throw new InvalidOperationException($"{controllerType.Name} is missing Post.");
            var task = Assert.IsAssignableFrom<Task>(post.Invoke(controller, [request]));
            await task;

            var result = ReportTestHelper.GetTaskResult(task);
            var resultObject = result?.GetType().GetProperty("Result")?.GetValue(result);
            var ok = Assert.IsType<OkObjectResult>(resultObject);
            var api = Assert.IsType<ApiResult<ReportAggregateResult>>(ok.Value);

            output.WriteLine($"{controllerType.Name}: rows={api.Data.Count}, total={api.TotalCount}, exact={api.IsTotalCountExact}");
            Assert.NotEmpty(api.Data);
        }
    }

    private async Task AssertDetailPageLoads(bool includeTotalCount)
        => await AssertDetailPageLoads(
            includeTotalCount,
            fromDate: new DateTime(2026, 4, 1),
            toDate: new DateTime(2026, 5, 31, 23, 59, 59),
            pageSize: includeTotalCount ? 1 : 5);

    private async Task AssertDetailPageLoads(
        bool includeTotalCount,
        DateTime fromDate,
        DateTime toDate,
        int pageSize,
        bool requireRows = true,
        int? expectedTotalCount = null,
        bool requireItemValues = false)
    {
        var db = TryConnect();
        if (db is null)
        {
            return;
        }

        await using (db)
        {
            var controller = (ExportLicenceDetailReportController)ReportTestHelper.CreateController(
                typeof(ExportLicenceDetailReportController), db);

            var result = await controller.Post(new ExportLicenceDetailReportRequest
            {
                FromDate = fromDate,
                ToDate = toDate,
                PageIndex = 0,
                PageSize = pageSize,
                IncludeTotalCount = includeTotalCount,
            });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var api = Assert.IsType<ApiResult<sp_ExportLicenceDetailReportResult>>(ok.Value);

            output.WriteLine($"rows={api.Data.Count}, total={api.TotalCount}, exact={api.IsTotalCountExact}, hasNext={api.HasNextPage}");
            foreach (var row in api.Data.Take(3))
            {
                output.WriteLine(
                    $"sample licence={row.LicenceNo}, port={row.PortofExport}, destination={row.DestinationCountry}, hs={row.HSCode}, unit={row.Unit}, price={row.Price}, qty={row.Quantity}, amount={row.Amount}, currency={row.Currency}");
            }

            if (requireRows)
            {
                Assert.NotEmpty(api.Data);
            }

            Assert.Equal(includeTotalCount, api.IsTotalCountExact);
            if (includeTotalCount)
            {
                Assert.True(api.TotalCount >= api.Data.Count);
            }

            if (expectedTotalCount.HasValue)
            {
                Assert.True(api.IsTotalCountExact);
                Assert.Equal(expectedTotalCount.Value, api.TotalCount);
            }

            if (requireItemValues)
            {
                Assert.NotEmpty(api.Data);
                Assert.All(api.Data, row => Assert.False(string.IsNullOrWhiteSpace(row.Unit)));
                Assert.All(api.Data, row => Assert.False(string.IsNullOrWhiteSpace(row.Currency)));
                Assert.All(api.Data, row => Assert.True(row.Price > 0));
                Assert.All(api.Data, row => Assert.True(row.Quantity > 0));
                Assert.All(api.Data, row => Assert.True(row.Amount > 0));
                Assert.All(api.Data, row => Assert.False(string.IsNullOrWhiteSpace(row.PortofExport)));
                Assert.All(api.Data, row => Assert.False(string.IsNullOrWhiteSpace(row.DestinationCountry)));
            }

            Assert.All(api.Data, row => Assert.False(string.IsNullOrWhiteSpace(row.LicenceNo)));
        }
    }

    private static void Set(object target, string propertyName, object value)
    {
        var property = target.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"{target.GetType().Name} is missing {propertyName}.");
        property.SetValue(target, value);
    }
}
