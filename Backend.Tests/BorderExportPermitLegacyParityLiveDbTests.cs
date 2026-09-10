using System.Data;
using System.Text.RegularExpressions;
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

/// <summary>
/// The OLD Tradenet 2.0 Border Export Permit By HS Code / HS Code drill / Voucher reports versus the new
/// controllers, on the SAME database. Runs only where a TradeNetDB is reachable
/// (<c>TRADENET_REPORT_TEST_CONNECTION_STRING</c>): from a developer Mac against UAT, from the Build Server
/// against production.
///
/// The oracle for By HS Code is the LEGACY SQL TEXT of <c>dbo.sp_HSCodeReport</c>'s 'Export Permit' branch
/// (docs/StoredProcedureDefinitions.sql:4727-4737 and :4743-4754, exported 2026-05-28) executed verbatim --
/// not <c>EXEC dbo.sp_HSCodeReport</c>, because on UAT that procedure was ALTERed into a paginated aggregate
/// on 2026-06-01 (docs/sp_HSCodeReport_AggregatePagination.sql) and no longer returns the columns the old
/// admin app reads. The RDLC shaping is replayed here: BorderHSCodeReport.rdlc groups on (HSCodeId,
/// Currency) in first-appearance order with CountDistinct(LicenceNo) and Sum(Amount) and a TOTAL of
/// CountDistinct(LicenceNo); HSCodeDetailReport.rdlc groups on (HSCodeId, CompanyRegistrationNo) and
/// prints the first row's CompanyName. The voucher oracle is <c>EXEC dbo.sp_VoucherReport</c> itself, exactly
/// as legacy ReportsController.BorderExportPermitVoucherReport calls it, shaped like BorderVoucherReport.rdlc.
/// </summary>
public sealed class BorderExportPermitLegacyParityLiveDbTests(ITestOutputHelper output)
{
    private const string SkipReason =
        "Set TRADENET_REPORT_TEST_CONNECTION_STRING to a reachable TradeNetDB to run this live parity test.";

    // docs/StoredProcedureDefinitions.sql:4727-4737 -- dbo.sp_HSCodeReport, @FormType='Export Permit', @HSCode=''.
    private const string LegacySummarySql = """
        SELECT section.Code sectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
        ExportPermit.ExportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
        FROM ExportPermit
        INNER JOIN ExportPermitItem ON ExportPermit.Id = ExportPermitItem.ExportPermitId
        INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
        INNER JOIN HSCode ON ExportPermitItem.HSCodeId = HSCode.Id
        INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
        INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
        WHERE ApplyType='New' AND ExportPermit.Status='Approved'
        AND (ExportPermit.LicenceDate>=@FromDate AND ExportPermit.LicenceDate<=@ToDate)
        ORDER BY HSCode.Id
        """;

    // docs/StoredProcedureDefinitions.sql:4743-4754 -- same branch, @FilterType='Start' with an HS code.
    private const string LegacyDrillSql = """
        SELECT section.Code SectionCode,HSCodeId,HSCode.Code HSCode,HSCode.Description HSDescription,Amount,currency.Code Currency,
        ExportPermit.ExportPermitNo LicenceNo,CompanyRegistrationNo,CompanyName
        FROM ExportPermit
        INNER JOIN ExportPermitItem ON ExportPermit.Id = ExportPermitItem.ExportPermitId
        INNER JOIN PaThaKa ON ExportPermit.PaThaKaId = PaThaKa.Id
        INNER JOIN HSCode ON ExportPermitItem.HSCodeId = HSCode.Id
        INNER JOIN Currency currency ON ExportPermitItem.CurrencyId = currency.Id
        INNER JOIN ExportImportSection section ON ExportPermit.ExportImportSectionId = section.Id
        WHERE ApplyType='New' AND ExportPermit.Status='Approved'
        AND (ExportPermit.LicenceDate>=@FromDate AND ExportPermit.LicenceDate<=@ToDate)
        AND HSCode.Code LIKE @HSCode+'%'
        ORDER BY HSCode.Id
        """;

    private static readonly (DateTime From, DateTime To)[] Windows =
    [
        (new DateTime(2025, 1, 1), new DateTime(2026, 9, 6, 23, 59, 59)),   // the customer's complaint window
        (new DateTime(2025, 1, 1), new DateTime(2025, 12, 31, 23, 59, 59)), // one calendar year
    ];

    private sealed record LegacyItem(
        int HSCodeId, string HSCode, string? HSDescription, decimal Amount, string Currency,
        string LicenceNo, string CompanyRegistrationNo, string CompanyName);

    // ---------------------------------------------------------------- oracle text integrity (no DB)

    [Fact]
    public void Embedded_legacy_sql_is_verbatim_from_the_definitions_export()
    {
        // The oracle must stay the OLD query. If someone edits the constants above (or the export),
        // this fails before any database is involved.
        var lines = File.ReadAllLines(Path.Combine(RepositoryRoot, "docs", "StoredProcedureDefinitions.sql"));

        Assert.Equal(Normalize(string.Join('\n', lines[4726..4737])), Normalize(LegacySummarySql));
        Assert.Equal(Normalize(string.Join('\n', lines[4742..4754])), Normalize(LegacyDrillSql));
    }

    // ---------------------------------------------------------------- live comparisons

    [Fact]
    public async Task By_hs_code_summary_equals_the_legacy_report_for_both_windows()
    {
        using var db = TryConnect();
        if (db == null)
        {
            return;
        }

        foreach (var (from, to) in Windows)
        {
            var legacy = ReplaySummary(await ReadLegacyItemsAsync(db, LegacySummarySql, from, to));
            var (rows, footer) = await PostSummaryAsync(db, from, to);
            var actual = rows.Select(r => (r.HSCode ?? "", r.Currency ?? "", r.NoOfLicences, Round4(r.TotalValue ?? 0m))).ToList();

            output.WriteLine($"summary {from:yyyy-MM-dd}..{to:yyyy-MM-dd}: legacy {legacy.Rows.Count} rows / TOTAL {legacy.Total}; new {actual.Count} rows / TOTAL {footer}");
            Assert.Equal(legacy.Rows.Count, actual.Count);
            Assert.Equal(legacy.Total, footer);
            // Same groups with the same figures, and in the old report's order (HS code id, first appearance).
            Assert.Equal(
                legacy.Rows.OrderBy(r => r.HsCode).ThenBy(r => r.Currency).ToList(),
                actual.OrderBy(r => r.Item1).ThenBy(r => r.Item2).ToList());
            var firstOutOfOrder = legacy.Rows.Zip(actual).Select((pair, i) => (pair, i))
                .FirstOrDefault(x => x.pair.First.HsCode != x.pair.Second.Item1 || x.pair.First.Currency != x.pair.Second.Item2);
            output.WriteLine(firstOutOfOrder.pair.First == default
                ? "  order identical"
                : $"  order differs at row {firstOutOfOrder.i}: legacy {firstOutOfOrder.pair.First.HsCode}/{firstOutOfOrder.pair.First.Currency}, new {firstOutOfOrder.pair.Second.Item1}/{firstOutOfOrder.pair.Second.Item2}");
        }
    }

    [Fact]
    public async Task Hs_code_drill_equals_the_legacy_detail_report()
    {
        using var db = TryConnect();
        if (db == null)
        {
            return;
        }

        var (from, to) = Windows[0];
        var items = await ReadLegacyItemsAsync(db, LegacySummarySql, from, to);
        if (items.Count == 0)
        {
            output.WriteLine("SKIPPED: no Export Permit rows in the window on this database.");
            return;
        }

        // Three drill targets picked from the data itself: an HS code with two currencies, the one with
        // the most companies, and one with a single permit.
        var byCode = items.GroupBy(i => i.HSCode).ToDictionary(g => g.Key, g => g.ToList());
        var targets = new List<string>();
        var twoCurrencies = byCode.FirstOrDefault(kv => kv.Value.Select(i => i.Currency).Distinct().Count() > 1).Key;
        if (twoCurrencies != null) targets.Add(twoCurrencies);
        targets.Add(byCode.OrderByDescending(kv => kv.Value.Select(i => i.CompanyRegistrationNo).Distinct().Count()).First().Key);
        var single = byCode.FirstOrDefault(kv => kv.Value.Select(i => i.LicenceNo).Distinct().Count() == 1 && !targets.Contains(kv.Key)).Key;
        if (single != null) targets.Add(single);

        foreach (var hsCode in targets.Distinct())
        {
            var legacy = ReplayDrill(await ReadLegacyItemsAsync(db, LegacyDrillSql, from, to, hsCode));
            var (rows, footer) = await PostSummaryAsync(db, from, to, hsCode: hsCode, groupBy: "Company");
            var actual = rows.Select(r => (r.HSCode ?? "", r.CompanyRegistrationNo ?? "", r.CompanyName ?? "", r.NoOfLicences)).ToList();

            output.WriteLine($"drill {hsCode}: legacy {legacy.Rows.Count} rows / TOTAL {legacy.Total}; new {actual.Count} rows / TOTAL {footer}");
            Assert.Equal(legacy.Total, footer);
            Assert.Equal(
                legacy.Rows.OrderBy(r => r.HsCode).ThenBy(r => r.RegistrationNo).ToList(),
                actual.OrderBy(r => r.Item1).ThenBy(r => r.Item2).ToList());
        }
    }

    [Fact]
    public async Task Sakhan_and_export_section_boxes_are_dead_like_the_old_form()
    {
        using var db = TryConnect();
        if (db == null)
        {
            return;
        }

        var (from, to) = Windows[0];
        var baseline = await PostSummaryAsync(db, from, to);
        // Legacy dbo.sp_HSCodeReport's Export Permit branch has no section parameter and never reads
        // @SakhanId; the old screen's two dropdowns changed nothing. Neither may ours.
        //
        // The UI stopped rendering both boxes on 2026-09-10 (customer complaint: picking a Sakhan
        // returned the same 494 rows as All), but the DTO still binds them for inbound
        // compatibility -- so this test remains the standing proof that the values are ignored, and
        // that a bookmarked drill URL still carrying sakhanId gets the same rows it always did.
        foreach (var alternative in new[] { await PostSummaryAsync(db, from, to, sakhanId: 5), await PostSummaryAsync(db, from, to, sectionId: 1) })
        {
            Assert.Equal(Shape(baseline.Rows), Shape(alternative.Rows));
            Assert.Equal(baseline.Footer, alternative.Footer);
        }

        output.WriteLine($"dead boxes: {baseline.Rows.Count} rows / TOTAL {baseline.Footer} with Sakhan=0/Section=0, Sakhan=5 and Section=1 alike");

        static List<(string, string, int, decimal)> Shape(List<ReportAggregateResult> rows)
            => rows.Select(r => (r.HSCode ?? "", r.Currency ?? "", r.NoOfLicences, Round4(r.TotalValue ?? 0m))).ToList();
    }

    [Fact]
    public async Task Voucher_rows_and_total_equal_the_legacy_procedure()
    {
        using var db = TryConnect();
        if (db == null)
        {
            return;
        }

        foreach (var (from, to) in Windows)
        {
            var legacyVoucher = await ReadLegacyVoucherAsync(db, from, to);
            if (legacyVoucher == null)
            {
                output.WriteLine("SKIPPED: dbo.sp_VoucherReport is not on this database.");
                return;
            }

            var legacy = legacyVoucher.Value;

            var controller = (BorderExportPermitVoucherReportController)ReportTestHelper.CreateController(
                typeof(BorderExportPermitVoucherReportController), db);
            var result = await controller.Post(new BorderExportPermitVoucherReportRequest
            {
                FromDate = from,
                ToDate = to,
                ApplyType = string.Empty,   // the grid's "--- All ---" default, mapped to New like the old form's default
                PageIndex = 0,
                PageSize = 1000,
                IncludeTotalCount = true,
            });
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var api = Assert.IsType<ApiResult<sp_VoucherReportResult>>(ok.Value);

            // BorderVoucherReport.rdlc cells after "No.": Sakhan, Licence No (IIF New), Application Date,
            // Application No, [header2 hidden for New], Licence Date, Company Registration No, Company Name,
            // Voucher No, Voucher Date, Commodity Type, Total Amount (N0).
            var actual = api.Data.Select(r => (
                    r.SakhanCode ?? "",
                    r.ApplyType == "New" ? r.LicenceNo : r.OldLicenceNo ?? "",
                    r.ApplicationDate,
                    r.ApplicationNo,
                    r.SLicenceDate ?? "",
                    r.CompanyRegistrationNo,
                    r.CompanyName,
                    r.VoucherNo ?? "",
                    r.SVoucherDate ?? "",
                    r.CommodityType ?? "",
                    Math.Round((decimal)r.Amount, 0)))
                .OrderBy(r => r.Item8).ThenBy(r => r.Item2).ToList();
            var expected = legacy.Rows.OrderBy(r => r.Item8).ThenBy(r => r.Item2).ToList();

            output.WriteLine($"voucher {from:yyyy-MM-dd}..{to:yyyy-MM-dd}: legacy {expected.Count} rows / TOTAL {legacy.Total}; new {api.Data.Count} rows / TOTAL {(api.ColumnTotals != null && api.ColumnTotals.TryGetValue("amount", out var t) ? t : (decimal?)null)}");
            Assert.Equal(expected, actual);
            Assert.NotNull(api.ColumnTotals);
            Assert.Equal(legacy.Total, api.ColumnTotals!["amount"]);
        }
    }

    // ---------------------------------------------------------------- helpers

    private TradeNetDbContext? TryConnect()
    {
        var cs = Environment.GetEnvironmentVariable("TRADENET_REPORT_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(cs))
        {
            output.WriteLine("SKIPPED: " + SkipReason);
            return null;
        }

        var options = new DbContextOptionsBuilder<TradeNetDbContext>()
            .UseSqlServer(cs, sql => sql.CommandTimeout(300))
            .Options;
        var db = new TradeNetDbContext(options);
        try
        {
            if (db.Database.CanConnect())
            {
                return db;
            }

            output.WriteLine("SKIPPED: connection string set but database unreachable.");
        }
        catch (Exception ex)
        {
            output.WriteLine("SKIPPED: " + ex.Message);
        }

        db.Dispose();
        return null;
    }

    private static async Task<(List<ReportAggregateResult> Rows, int? Footer)> PostSummaryAsync(
        TradeNetDbContext db, DateTime from, DateTime to, string hsCode = "", string groupBy = "", int sakhanId = 0, int sectionId = 0)
    {
        var controller = (BorderExportPermitByHSCodeReportController)ReportTestHelper.CreateController(
            typeof(BorderExportPermitByHSCodeReportController), db);
        var rows = new List<ReportAggregateResult>();
        int? footer = null;
        for (var page = 0; ; page++)
        {
            var result = await controller.Post(new BorderExportPermitByHSCodeReportRequest
            {
                FromDate = from,
                ToDate = to,
                FilterType = "Start",
                HSCode = hsCode,
                GroupBy = groupBy,
                SakhanId = sakhanId,
                ExportImportSectionId = sectionId,
                PageIndex = page,
                PageSize = 1000,
                IncludeTotalCount = true,
            });
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var api = Assert.IsType<ApiResult<ReportAggregateResult>>(ok.Value);
            rows.AddRange(api.Data);
            if (api.ColumnTotals != null && api.ColumnTotals.TryGetValue("noOfLicences", out var total))
            {
                footer = (int)total;
            }

            if (api.Data.Count < 1000)
            {
                return (rows, footer);
            }
        }
    }

    private static (List<(string HsCode, string Currency, int Licences, decimal Total)> Rows, int Total) ReplaySummary(List<LegacyItem> items)
        => (items
                .GroupBy(i => (i.HSCodeId, i.Currency))   // Enumerable.GroupBy keeps first-appearance order = the RDLC
                .Select(g => (g.First().HSCode, g.Key.Currency, g.Select(i => i.LicenceNo).Distinct().Count(), Round4(g.Sum(i => i.Amount))))
                .ToList(),
            items.Select(i => i.LicenceNo).Distinct().Count());

    private static (List<(string HsCode, string RegistrationNo, string CompanyName, int Licences)> Rows, int Total) ReplayDrill(List<LegacyItem> items)
        => (items
                .GroupBy(i => (i.HSCodeId, i.CompanyRegistrationNo))
                .Select(g => (g.First().HSCode, g.Key.CompanyRegistrationNo, g.First().CompanyName, g.Select(i => i.LicenceNo).Distinct().Count()))
                .ToList(),
            items.Select(i => i.LicenceNo).Distinct().Count());

    private static async Task<List<LegacyItem>> ReadLegacyItemsAsync(
        TradeNetDbContext db, string sql, DateTime from, DateTime to, string? hsCode = null)
    {
        var connection = (SqlConnection)db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 300;
        command.Parameters.Add(new SqlParameter("@FromDate", SqlDbType.DateTime) { Value = from });
        command.Parameters.Add(new SqlParameter("@ToDate", SqlDbType.DateTime) { Value = to });
        if (hsCode != null)
        {
            command.Parameters.Add(new SqlParameter("@HSCode", SqlDbType.NVarChar, 50) { Value = hsCode });
        }

        var items = new List<LegacyItem>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new LegacyItem(
                reader.GetInt32(reader.GetOrdinal("HSCodeId")),
                Text(reader, "HSCode"),
                reader.IsDBNull(reader.GetOrdinal("HSDescription")) ? null : Text(reader, "HSDescription"),
                reader.GetDecimal(reader.GetOrdinal("Amount")),
                Text(reader, "Currency"),
                Text(reader, "LicenceNo"),
                Text(reader, "CompanyRegistrationNo"),
                Text(reader, "CompanyName")));
        }

        return items;
    }

    private static async Task<(List<(string, string, DateTime, string, string, string, string, string, string, string, decimal)> Rows, decimal Total)?> ReadLegacyVoucherAsync(
        TradeNetDbContext db, DateTime from, DateTime to)
    {
        var connection = (SqlConnection)db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT CASE WHEN OBJECT_ID(N'dbo.sp_VoucherReport', 'P') IS NULL THEN 0 ELSE 1 END;";
        if ((int)(await exists.ExecuteScalarAsync() ?? 0) == 0)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "dbo.sp_VoucherReport";
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = 300;
        command.Parameters.AddRange(new[]
        {
            new SqlParameter("@FormType", SqlDbType.NVarChar, 50) { Value = "Border Export Permit" },
            new SqlParameter("@FromDate", SqlDbType.DateTime) { Value = from },
            new SqlParameter("@ToDate", SqlDbType.DateTime) { Value = to },
            new SqlParameter("@ExportImportSectionId", SqlDbType.Int) { Value = 0 },
            new SqlParameter("@PaymentType", SqlDbType.NVarChar, 50) { Value = string.Empty },
            new SqlParameter("@ApplyType", SqlDbType.NVarChar, 50) { Value = "New" },
            new SqlParameter("@CompanyRegistrationNo", SqlDbType.NVarChar, 50) { Value = string.Empty },
            new SqlParameter("@SakhanId", SqlDbType.Int) { Value = 0 },
        });

        var rows = new List<(string, string, DateTime, string, string, string, string, string, string, string, decimal)>();
        var total = 0m;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var amount = Math.Round(Convert.ToDecimal(reader["Amount"]), 0);
            total += Convert.ToDecimal(reader["Amount"]);
            rows.Add((
                Text(reader, "SakhanCode"),
                Text(reader, "ApplyType") == "New" ? Text(reader, "LicenceNo") : Text(reader, "OldLicenceNo"),
                reader.GetDateTime(reader.GetOrdinal("ApplicationDate")),
                Text(reader, "ApplicationNo"),
                Text(reader, "sLicenceDate"),
                Text(reader, "CompanyRegistrationNo"),
                Text(reader, "CompanyName"),
                Text(reader, "VoucherNo"),
                Text(reader, "sVoucherDate"),
                Text(reader, "CommodityType"),
                amount));
        }

        return (rows, Math.Round(total, 0));
    }

    private static string Text(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
    }

    private static decimal Round4(decimal value) => Math.Round(value, 4);

    private static string Normalize(string sql) => Regex.Replace(sql, @"\s+", " ").Trim();

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Backend")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
        }
    }
}
