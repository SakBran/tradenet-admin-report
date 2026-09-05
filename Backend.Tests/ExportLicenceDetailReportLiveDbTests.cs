using System.Data;
using System.Globalization;
using API.DBContext;
using API.Model;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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

    // The grid renders one row per licence ITEM, so its total has to be the item count. It used to
    // report the licence count instead (454 where the old report and the Excel export both said
    // 1967). The oracle counts items per licence with a correlated subquery - a different
    // formulation from the report's own join - so it fails if the grain regresses again.
    [Fact]
    public async Task Detail_page_may_1_to_may_2_2025_exact_count_matches_live_db()
        => await AssertDetailPageLoads(
            includeTotalCount: true,
            fromDate: new DateTime(2025, 5, 1),
            toDate: new DateTime(2025, 5, 2, 23, 59, 59),
            pageSize: 10,
            requireRows: false,
            assertItemGrainTotal: true,
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

    // Old-vs-new parity. The oracle is the LEGACY procedure the Tradenet 2.0 admin still calls
    // (dbo.sp_ExportLicenceDetailReport, 'Oversea', end-of-day ToDate). The grid procedure with
    // @PageSize = 0 must return the same rows -- same count, same values, one per licence ITEM --
    // and the API's TotalCount and the Excel export's row count must both equal that count.
    // Cases: the customer's kind of window (two days), the earlier 454-vs-1967 window, and the two
    // filters the customer said returned nothing (Method of export = CMP, Incoterms = CIF).
    public static TheoryData<int, int, int, int, int> ParityCases() => new()
    {
        // year, month, day (two-day window from there), methodId, incotermId
        { 2025, 8, 31, 0, 0 },
        { 2025, 5, 1, 0, 0 },
        { 2025, 8, 31, 3, 0 },
        { 2025, 8, 31, 0, 12 },
    };

    [Theory]
    [MemberData(nameof(ParityCases))]
    public async Task Detail_grid_matches_legacy_sp_ExportLicenceDetailReport(
        int year,
        int month,
        int day,
        int methodId,
        int incotermId)
    {
        var db = TryConnect();
        if (db is null)
        {
            return;
        }

        await using (db)
        {
            var fromDate = new DateTime(year, month, day);
            var toDate = fromDate.AddDays(2).AddSeconds(-1);

            var legacy = await ReadLegacyRowKeysAsync(db, fromDate, toDate, methodId, incotermId);
            if (legacy is null)
            {
                output.WriteLine("SKIPPED: dbo.sp_ExportLicenceDetailReport is not on this database.");
                return;
            }

            var procedureRequest = new sp_ExportLicenceDetailReportRequest
            {
                Type = "Oversea",
                FromDate = fromDate,
                ToDate = toDate,
                ExportImportMethodId = methodId,
                ExportImportIncotermId = incotermId,
                CompanyRegistrationNo = string.Empty,
                Auto = string.Empty,
            };

            // Every row, straight from the grid procedure.
            var grid = await sp_ExportLicenceDetailReportV3.ExecuteAsync(db, procedureRequest, pageIndex: 0, pageSize: 0);
            output.WriteLine($"legacy={legacy.Count}, grid={grid.Count}");
            Assert.Equal(legacy.Count, grid.Count);
            Assert.All(grid, row => Assert.Equal(legacy.Count, row.TotalCount));
            Assert.Equal(
                legacy.OrderBy(key => key, StringComparer.Ordinal).ToList(),
                grid.Select(RowKey).OrderBy(key => key, StringComparer.Ordinal).ToList());

            // The API page the grid actually requests: exact item-grain total, a full page of rows.
            var controller = (ExportLicenceDetailReportController)ReportTestHelper.CreateController(
                typeof(ExportLicenceDetailReportController), db);
            var result = await controller.Post(new ExportLicenceDetailReportRequest
            {
                FromDate = fromDate,
                ToDate = toDate,
                ExportImportMethodId = methodId,
                ExportImportIncotermId = incotermId,
                PageIndex = 0,
                PageSize = 10,
                IncludeTotalCount = true,
            });
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var api = Assert.IsType<ApiResult<sp_ExportLicenceDetailReportResult>>(ok.Value);
            Assert.True(api.IsTotalCountExact);
            Assert.Equal(legacy.Count, api.TotalCount);
            Assert.Equal(Math.Min(10, legacy.Count), api.Data.Count);
            Assert.Null(api.CurrencyTotals);

            // The Excel export streams the same universe (this is what said 1967 when the grid said 454).
            var excelRows = 0;
            await foreach (var chunk in sp_ExportLicenceDetailReport_Fast.StreamResolvedChunksAsync(
                db, new MemoryCache(new MemoryCacheOptions()), procedureRequest, 500))
            {
                excelRows += chunk.Count;
            }

            Assert.Equal(legacy.Count, excelRows);
        }
    }

    /// <summary>
    /// The rows the old report prints, keyed on the columns a user compares: the legacy procedure
    /// itself, called exactly the way the old admin app called it. Null when it is not deployed here.
    /// </summary>
    private static async Task<List<string>?> ReadLegacyRowKeysAsync(
        TradeNetDbContext db,
        DateTime fromDate,
        DateTime toDate,
        int methodId,
        int incotermId)
    {
        var connection = (SqlConnection)db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT CASE WHEN OBJECT_ID(N'dbo.sp_ExportLicenceDetailReport', 'P') IS NULL THEN 0 ELSE 1 END;";
        if ((int)(await exists.ExecuteScalarAsync() ?? 0) == 0)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "dbo.sp_ExportLicenceDetailReport";
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = 180;
        // Explicit types: new SqlParameter("@X", 0) binds the (string, SqlDbType) overload, not a value.
        command.Parameters.AddRange(new[]
        {
            new SqlParameter("@Type", SqlDbType.NVarChar, 20) { Value = "Oversea" },
            new SqlParameter("@FromDate", SqlDbType.DateTime) { Value = fromDate },
            new SqlParameter("@ToDate", SqlDbType.DateTime) { Value = toDate },
            new SqlParameter("@PaThaKaTypeId", SqlDbType.Int) { Value = 0 },
            new SqlParameter("@ExportImportSectionId", SqlDbType.Int) { Value = 0 },
            new SqlParameter("@ExportImportMethodId", SqlDbType.Int) { Value = methodId },
            new SqlParameter("@ExportImportIncotermId", SqlDbType.Int) { Value = incotermId },
            new SqlParameter("@BuyerCountryId", SqlDbType.Int) { Value = 0 },
            new SqlParameter("@CompanyRegistrationNo", SqlDbType.NVarChar, 50) { Value = string.Empty },
            new SqlParameter("@SakhanId", SqlDbType.Int) { Value = 0 },
        });

        var keys = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            keys.Add(string.Join("|",
                Text(reader, "LicenceNo"),
                Text(reader, "SectionName"),
                Text(reader, "CompanyRegistrationNo"),
                Text(reader, "BuyerName"),
                Text(reader, "MethodName"),
                Text(reader, "PortofExport"),
                Text(reader, "DestinationCountry"),
                Text(reader, "HSCode"),
                Text(reader, "HSDescription"),
                Text(reader, "Unit"),
                Money(reader, "Price"),
                Money(reader, "Quantity"),
                Money(reader, "Amount"),
                Text(reader, "Currency")));
        }

        return keys;
    }

    private static string RowKey(sp_ExportLicenceDetailReportRow row) => string.Join("|",
        row.LicenceNo,
        row.SectionName,
        row.CompanyRegistrationNo,
        row.BuyerName,
        row.MethodName,
        row.PortofExport ?? string.Empty,
        row.DestinationCountry ?? string.Empty,
        row.HSCode,
        row.HSDescription ?? string.Empty,
        row.Unit ?? string.Empty,
        row.Price.ToString(CultureInfo.InvariantCulture),
        row.Quantity.ToString(CultureInfo.InvariantCulture),
        row.Amount.ToString(CultureInfo.InvariantCulture),
        row.Currency ?? string.Empty);

    private static string Text(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
    }

    private static string Money(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? string.Empty
            : reader.GetDecimal(ordinal).ToString(CultureInfo.InvariantCulture);
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
        bool assertItemGrainTotal = false,
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

            // The legacy-shaped procedure counts its materialised key table on every call, so the
            // total is exact whether or not the client asked for it (the grid asks eagerly).
            Assert.True(api.IsTotalCountExact);
            Assert.True(api.TotalCount >= api.Data.Count);

            if (assertItemGrainTotal)
            {
                var itemRows = await CountDetailItemRowsAsync(db, fromDate, toDate);
                output.WriteLine($"oracle item rows={itemRows}");
                Assert.True(api.IsTotalCountExact);
                Assert.Equal(itemRows, api.TotalCount);
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

    /// <summary>
    /// Rows the detail grid should show for this window, counted independently of the report's own
    /// SQL: per licence, how many items it has, summed.
    /// </summary>
    private static async Task<int> CountDetailItemRowsAsync(
        TradeNetDbContext db,
        DateTime fromDate,
        DateTime toDate)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandTimeout = 180;
        command.CommandText =
                """
                SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

                SELECT CAST(COALESCE(SUM(licence.ItemCount), 0) AS int) AS Value
                FROM (
                    SELECT (
                        SELECT COUNT_BIG(*)
                        FROM dbo.ExportLicenceItem AS item
                        WHERE item.ExportLicenceId = l.Id
                    ) AS ItemCount
                    FROM dbo.ExportLicence AS l
                    INNER JOIN dbo.PaThaKa AS p ON p.Id = l.PaThaKaId
                    WHERE l.ApplyType = N'New'
                      AND l.Status = N'Approved'
                      AND l.CreatedDate >= @FromDate
                      AND l.CreatedDate <= @ToDate
                ) AS licence
                OPTION (RECOMPILE);
                """;
        command.Parameters.Add(new SqlParameter("@FromDate", fromDate));
        command.Parameters.Add(new SqlParameter("@ToDate", toDate));

        return (int)(await command.ExecuteScalarAsync() ?? 0);
    }

    private static void Set(object target, string propertyName, object value)
    {
        var property = target.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"{target.GetType().Name} is missing {propertyName}.");
        property.SetValue(target, value);
    }
}
