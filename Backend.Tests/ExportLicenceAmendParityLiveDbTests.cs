using System.Data;
using API.DBContext;
using API.Model;
using Backend.Controllers.Report;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Backend.Tests;

/// <summary>
/// Old-vs-new parity for the Export Licence Amendment / Actual Amendment listings.
///
/// The customer reported the Actual Amendment report showing 38 rows where Tradenet 2.0 showed
/// 17 for 31-Aug-2026 to 1-Sep-2026. Rather than pin that one number, each test asks the LEGACY
/// procedure (<c>dbo.sp_AmendReport</c> / <c>dbo.sp_ActualAmendReport</c>, untouched by this
/// project) how many rows the old report would print, then asserts the API returns exactly that
/// many for the same filters. Any future drift in the pagination procs, the date window, or the
/// controller's ToDate handling fails here for every window, not just the reported one.
///
/// Needs a reachable TradeNetDB, so it is skipped in environments without one (the Mac dev box
/// cannot reach the CGNAT-internal production DB; run it from the Build Server).
/// </summary>
public sealed class ExportLicenceAmendParityLiveDbTests(ITestOutputHelper output)
{
    private const string SkipReason =
        "Set TRADENET_REPORT_TEST_CONNECTION_STRING to a reachable TradeNetDB to run this live integration test.";

    // The old admin app sent ToDate as end-of-day and the legacy procs filter CreatedDate <= @ToDate;
    // the controllers send ToDate.Date and the pagination procs use < DATEADD(day, 1, CONVERT(date, @ToDate)).
    // Both must therefore cover the same calendar day - that equivalence is what these tests prove.
    public static TheoryData<int, int, int> Windows() => new()
    {
        { 2026, 8, 31 },
        { 2025, 5, 1 },
        { 2025, 11, 3 },
    };

    [Theory]
    [MemberData(nameof(Windows))]
    public Task Amendment_row_count_matches_legacy_sp_AmendReport(int year, int month, int day)
        => AssertParity(
            typeof(ExportLicenceAmendmentReportController),
            "dbo.sp_AmendReport",
            new DateTime(year, month, day),
            new DateTime(year, month, day).AddDays(1));

    [Theory]
    [MemberData(nameof(Windows))]
    public Task Actual_amendment_row_count_matches_legacy_sp_ActualAmendReport(int year, int month, int day)
        => AssertParity(
            typeof(ExportLicenceActualAmendmentReportController),
            "dbo.sp_ActualAmendReport",
            new DateTime(year, month, day),
            new DateTime(year, month, day).AddDays(1));

    private async Task AssertParity(Type controllerType, string legacyProcedure, DateTime fromDate, DateTime toDate)
    {
        var db = TryConnect();
        if (db is null)
        {
            return;
        }

        await using (db)
        {
            var legacyCount = await CountLegacyRowsAsync(db, legacyProcedure, fromDate, toDate);
            if (legacyCount is null)
            {
                output.WriteLine($"SKIPPED: {legacyProcedure} is not present on this database.");
                return;
            }

            var controller = ReportTestHelper.CreateController(controllerType, db);
            var request = Activator.CreateInstance(ReportTestHelper.GetRequestType(controllerType))
                ?? throw new InvalidOperationException($"Could not create request for {controllerType.Name}.");

            Set(request, "FormType", "Export Licence");
            Set(request, "FromDate", fromDate);
            Set(request, "ToDate", toDate);
            Set(request, "PageIndex", 0);
            Set(request, "PageSize", 10);
            Set(request, "IncludeTotalCount", true);

            var post = controllerType.GetMethod("Post")
                ?? throw new InvalidOperationException($"{controllerType.Name} is missing Post.");
            var task = Assert.IsAssignableFrom<Task>(post.Invoke(controller, [request]));
            await task;

            var result = ReportTestHelper.GetTaskResult(task);
            var resultObject = result?.GetType().GetProperty("Result")?.GetValue(result);
            var ok = Assert.IsType<OkObjectResult>(resultObject);
            var api = Assert.IsAssignableFrom<IReportTotals>(ok.Value);
            var totalCount = (int)(ok.Value!.GetType().GetProperty("TotalCount")?.GetValue(ok.Value)
                ?? throw new InvalidOperationException("Response is missing TotalCount."));

            output.WriteLine(
                $"{controllerType.Name} {fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd}: " +
                $"api={totalCount}, legacy={legacyCount}, footerLicences={api.CurrencyTotals?.GrandTotalLicences}");

            Assert.Equal(legacyCount.Value, totalCount);

            // The footer counts the same licences the grid does - a footer built from a different
            // date window or ApplyType spelling silently disagrees with the rows above it.
            if (api.CurrencyTotals is { Currencies.Count: > 0 })
            {
                Assert.Equal(totalCount, api.CurrencyTotals.GrandTotalLicences);
            }
        }
    }

    /// <summary>
    /// Rows the old report would print: the legacy procedure itself, called the way the old admin
    /// app called it (end-of-day ToDate), so it is an oracle rather than a copy of the new SQL.
    /// Null when the legacy procedure does not exist on this database.
    /// </summary>
    private static async Task<int?> CountLegacyRowsAsync(
        TradeNetDbContext db,
        string procedure,
        DateTime fromDate,
        DateTime toDate)
    {
        var connection = (SqlConnection)db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT CASE WHEN OBJECT_ID(@name, 'P') IS NULL THEN 0 ELSE 1 END;";
        exists.Parameters.Add(new SqlParameter("@name", procedure));
        if ((int)(await exists.ExecuteScalarAsync() ?? 0) == 0)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = procedure;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = 180;
        command.Parameters.AddRange(new[]
        {
            new SqlParameter("@FormType", "Export Licence"),
            new SqlParameter("@FromDate", fromDate.Date),
            new SqlParameter("@ToDate", toDate.Date.AddDays(1).AddSeconds(-1)),
            new SqlParameter("@ExportImportSectionId", 0),
            new SqlParameter("@AmendRemarkId", 0),
            new SqlParameter("@CompanyRegistrationNo", string.Empty),
            new SqlParameter("@SakhanId", 0),
        });

        var rows = 0;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows++;
        }

        return rows;
    }

    private TradeNetDbContext? TryConnect()
    {
        var cs = Environment.GetEnvironmentVariable("TRADENET_REPORT_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(cs))
        {
            output.WriteLine("SKIPPED: " + SkipReason);
            return null;
        }

        var options = new DbContextOptionsBuilder<TradeNetDbContext>()
            .UseSqlServer(cs, sql => sql.CommandTimeout(180))
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

    private static void Set(object target, string propertyName, object value)
    {
        var property = target.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"{target.GetType().Name} is missing {propertyName}.");
        property.SetValue(target, value);
    }
}
