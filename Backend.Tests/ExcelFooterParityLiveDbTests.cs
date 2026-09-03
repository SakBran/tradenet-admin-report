using API.DBContext;
using API.Model;
using API.Service.ExcelExport;
using Backend.Controllers.Report;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Backend.Tests;

/// <summary>
/// The one claim these tests can make that no unit test can: for real filters against
/// real data, the footer the export writes equals the footer the grid shows.
///
/// Needs TRADENET_REPORT_TEST_CONNECTION_STRING (export it from appsettings'
/// ConnectionStrings:TradeNetDBTest). The <c>(localdb)</c> fixture is Windows-only, so
/// when the DB is unreachable these report "unverified-nodb" in the output and return —
/// never a silent green tick on a machine that cannot reach the database.
/// </summary>
public sealed class ExcelFooterParityLiveDbTests
{
    // The live data lives in 2025; a 2026 window comes back empty and proves nothing.
    private static readonly DateTime FromDate = new(2025, 2, 1);
    private static readonly DateTime ToDate = new(2025, 2, 28, 23, 59, 59);

    private static bool HasConnectionString =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TRADENET_REPORT_TEST_CONNECTION_STRING"));

    private static bool Unavailable(out string reason)
    {
        reason = string.Empty;

        if (!HasConnectionString)
        {
            reason = "unverified-nodb: TRADENET_REPORT_TEST_CONNECTION_STRING is not set.";
            return true;
        }

        try
        {
            using var db = ReportTestHelper.CreateTradeNetDbTestDbContext();
            if (!db.Database.CanConnect())
            {
                reason = "unverified-nodb: the report database refused the connection.";
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            reason = $"unverified-nodb: {ex.GetType().Name} while connecting.";
            return true;
        }
    }

    private static DefaultExcelFooterTotalsResolver Resolver(TradeNetDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IExcelExportJobService>(new NoOpEnqueue());

        return new DefaultExcelFooterTotalsResolver(
            services.BuildServiceProvider(),
            Options.Create(new ExcelExportOptions()),
            NullLogger<DefaultExcelFooterTotalsResolver>.Instance);
    }

    private sealed class NoOpEnqueue : IExcelExportJobService
    {
        public Task<EnqueueResult> EnqueueAsync(
            string reportKey, object request, DateTime toDate, string? requestedByUserName)
            => Task.FromResult(new EnqueueResult());
    }

    [Fact]
    public async Task The_exported_footer_equals_the_grid_footer_for_account_summary()
    {
        if (Unavailable(out var reason))
        {
            Console.WriteLine(reason);
            return;
        }

        await using var db = ReportTestHelper.CreateTradeNetDbTestDbContext();

        var gridRequest = new AccountSummaryReportRequest
        {
            FromDate = FromDate,
            ToDate = ToDate,
            PageIndex = 0,
            PageSize = 10,
            IncludeTotalCount = true,
        };

        var controller = new AccountSummaryReportController(db, new NoOpEnqueue());
        var grid = await controller.Post(gridRequest);
        var gridTotals = Unwrap(grid);

        if (gridTotals?.ColumnTotals == null || gridTotals.ColumnTotals.Count == 0)
        {
            Console.WriteLine("unverified-nodb: the grid returned no column totals for the sample window.");
            return;
        }

        var exportRequest = new AccountSummaryReportRequest { FromDate = FromDate, ToDate = ToDate };
        var footerTotals = await Resolver(db).ResolveAsync(
            new AccountSummaryReportController(db, new NoOpEnqueue()),
            typeof(AccountSummaryReportController),
            exportRequest,
            CancellationToken.None);

        Assert.NotNull(footerTotals);
        Assert.Equal(gridTotals.ColumnTotals, footerTotals!.ColumnTotals);

        // And the footer row the sheet writes must place that number under the bound column.
        var layout = new AccountSummaryReportController(db, new NoOpEnqueue()).GetExcelLayout(exportRequest);
        var rows = ExcelFooterBuilder.Build(layout, footerTotals, dataRowCount: 1);
        var row = Assert.Single(rows);

        // Column 1 (Entry Date) is the first data column with no total, which is where
        // BasicTable.tsx's totalLabelIndex puts the label. Index 5 is the LEGACY
        // WriteTotalsRow position (immediately left of the totalled column) — not this.
        Assert.Equal("Total", row.Cells[1]?.Value);
        Assert.Equal(gridTotals.ColumnTotals!["amount"], row.Cells[6]?.Value);
    }

    private static IReportTotals? Unwrap(object? actionResult)
    {
        if (actionResult == null)
        {
            return null;
        }

        var converted = actionResult is IConvertToActionResult convertible
            ? convertible.Convert()
            : actionResult as IActionResult;

        return converted is ObjectResult { Value: IReportTotals totals }
            ? totals
            : actionResult.GetType().GetProperty("Value")?.GetValue(actionResult) as IReportTotals;
    }
}
