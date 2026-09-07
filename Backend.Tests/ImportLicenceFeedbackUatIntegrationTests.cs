using API.DBContext;
using API.Model;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Backend.Tests;

public sealed class ImportLicenceFeedbackUatIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "LiveDatabase")]
    public async Task Daily_report_exposes_all_15_groups_across_two_10_row_pages()
    {
        await using var db = TryCreateUatContext();
        if (db is null)
        {
            return;
        }

        var request = new ImportLicenceDailyReportNewLicenceReportRequest
        {
            FromDate = new DateTime(2025, 1, 6),
            ToDate = new DateTime(2025, 1, 8, 23, 59, 59),
            PageIndex = 0,
            PageSize = 10,
        };
        var controller = new ImportLicenceDailyReportNewLicenceReportController(db, null!);

        var firstPage = ReadOk(await controller.Post(request));
        request.PageIndex = 1;
        var secondPage = ReadOk(await controller.Post(request));

        Assert.Equal(15, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(10, firstPage.Data.Count);
        Assert.Equal(15, secondPage.TotalCount);
        Assert.Equal(5, secondPage.Data.Count);
        Assert.NotNull(firstPage.ColumnTotals);
        Assert.True(firstPage.ColumnTotals["noOfLicences"] > 0);

        output.WriteLine(
            $"Daily total={firstPage.TotalCount}; pages={firstPage.Data.Count}+{secondPage.Data.Count}.");
    }

    [Fact]
    [Trait("Category", "LiveDatabase")]
    public async Task Actual_amend_report_uses_the_legacy_compatible_UAT_result()
    {
        await using var db = TryCreateUatContext();
        if (db is null)
        {
            return;
        }

        var fromDate = new DateTime(2025, 1, 6);
        var toDate = new DateTime(2025, 1, 8, 23, 59, 59);
        var controller = new ImportLicenceActualAmendmentReportController(db, null!);
        var result = ReadOk(await controller.Post(
            new ImportLicenceActualAmendmentReportRequest
            {
                FromDate = fromDate,
                ToDate = toDate,
                PageIndex = 0,
                PageSize = 10,
                IncludeTotalCount = true,
            }));

        Assert.Equal(6, result.TotalCount);
        Assert.Equal(6, result.Data.Count);
        Assert.All(result.Data, row => Assert.InRange(row.Date!.Value, fromDate, toDate));
        Assert.NotNull(result.CurrencyTotals);
        Assert.Equal(6, result.CurrencyTotals.GrandTotalLicences);

        output.WriteLine($"Actual Amend total={result.TotalCount}.");
    }

    [Fact]
    [Trait("Category", "LiveDatabase")]
    public async Task Amendment_report_excludes_a_populated_following_day()
    {
        await using var db = TryCreateUatContext();
        if (db is null)
        {
            return;
        }

        // December 31 has eight matching UAT rows. Sending December 30 at
        // end-of-day reproduces the old UI request shape; none of those eight
        // following-day rows may enter this five-day result.
        var fromDate = new DateTime(2025, 12, 26);
        var toDate = new DateTime(2025, 12, 30, 23, 59, 59);
        var controller = new ImportLicenceAmendmentReportController(db, null!);
        var result = ReadOk(await controller.Post(
            new ImportLicenceAmendmentReportRequest
            {
                FromDate = fromDate,
                ToDate = toDate,
                PageIndex = 0,
                PageSize = 100,
                IncludeTotalCount = true,
            }));

        Assert.Equal(18, result.TotalCount);
        Assert.Equal(18, result.Data.Count);
        Assert.All(result.Data, row => Assert.InRange(row.Date!.Value, fromDate, toDate));
        Assert.DoesNotContain(result.Data, row => row.Date!.Value.Date == new DateTime(2025, 12, 31));
        Assert.NotNull(result.CurrencyTotals);
        Assert.Equal(18, result.CurrencyTotals.GrandTotalLicences);

        output.WriteLine(
            $"Amend selected-range total={result.TotalCount}; excluded following-day rows=8.");
    }

    [Fact]
    [Trait("Category", "LiveDatabase")]
    public async Task Border_new_report_matches_the_selected_day_and_footer_population()
    {
        await using var db = TryCreateUatContext();
        if (db is null)
        {
            return;
        }

        var fromDate = new DateTime(2023, 8, 1);
        var toDate = new DateTime(2023, 8, 1, 23, 59, 59);
        var controller = new BorderImportLicenceNewReportNewReportController(db, null!);
        var result = ReadOk(await controller.Post(
            new BorderImportLicenceNewReportNewReportRequest
            {
                FromDate = fromDate,
                ToDate = toDate,
                PageIndex = 0,
                PageSize = 10,
                IncludeTotalCount = true,
                Auto = "stale-ui-value",
            }));

        Assert.Equal(824, result.TotalCount);
        Assert.Equal(10, result.Data.Count);
        Assert.All(result.Data, row => Assert.InRange(row.Date!.Value, fromDate, toDate));
        Assert.NotNull(result.CurrencyTotals);
        Assert.Equal(824, result.CurrencyTotals.GrandTotalLicences);

        output.WriteLine(
            $"Border New total={result.TotalCount}; footer total={result.CurrencyTotals.GrandTotalLicences}.");
    }

    private TradeNetDbContext? TryCreateUatContext()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "TRADENET_REPORT_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            output.WriteLine(
                "SKIPPED: set TRADENET_REPORT_TEST_CONNECTION_STRING to the UAT TradeNetDB.");
            return null;
        }

        var connection = new SqlConnectionStringBuilder(connectionString);
        Assert.Equal("TradeNetDB", connection.InitialCatalog);
        Assert.Contains(
            connection.DataSource,
            new[] { "100.64.91.190", "203.81.66.111,14330" });

        var options = new DbContextOptionsBuilder<TradeNetDbContext>()
            .UseSqlServer(connection.ConnectionString, sql => sql.CommandTimeout(180))
            .Options;
        return new TradeNetDbContext(options);
    }

    private static ApiResult<T> ReadOk<T>(ActionResult<ApiResult<T>> response)
    {
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<ApiResult<T>>(ok.Value);
    }
}
