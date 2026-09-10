using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests;

/// <summary>
/// Pins that Border Import Permit By HS Code reads the BORDER tables and that its Sakhan filter
/// works.
///
/// History, because this decision has been reversed twice. The old Tradenet 2.0 screen sets
/// <c>model.FormType = AppConfig.ImportPermit</c> (legacy ReportsController.cs:15465), so it has
/// always run <c>dbo.sp_HSCodeReport</c>'s OVERSEA Import Permit branch -- oversea tables, @SakhanId
/// ignored -- i.e. it lists oversea permits under a Border title and its Sakhan dropdown cannot work.
/// On 2026-09-05 the owner asked for that same result and this report reproduced it bug-for-bug.
/// On 2026-09-10 the customer asked for the Sakhan filter to work instead, saying the old report is
/// the thing that is wrong ("Old Reportမှာမှားနေလို့ပါ"), and the report was switched to the Border
/// tables. Measured cost on their window (2024-05-01..2026-09-06): 1,014 rows / 1,328 permits ->
/// 31 / 112. See docs/BorderPermitByHSCodeSakhanSwitch_2026-09-10.md.
///
/// The predecessor of this file was BorderImportPermitByHSCodeLegacyParityTests, which pinned the
/// opposite. Do not restore the oversea FormType without a new decision.
/// </summary>
public sealed class BorderImportPermitByHSCodeBorderSourceTests
{
    [Fact]
    public void Summary_routes_to_the_Border_Import_Permit_branch()
    {
        var request = Assert.IsType<sp_HSCodeReportRequest>(
            ReportTestHelper.CreateProcedureRequest(typeof(BorderImportPermitByHSCodeReportController)));

        Assert.Equal("Border Import Permit", request.FormType);
        Assert.False(request.GroupByCompany);
        // Still true, and still load-bearing: it forces the LINQ path and keeps AggregateQuery's
        // (HSCodeId, Currency) branch ahead of the per-company split.
        Assert.True(request.LegacyOrder);
    }

    [Fact]
    public void Summary_no_longer_mirrors_the_oversea_report()
    {
        // The inverse of the assertion this file used to make. Until 2026-09-10 the Border report
        // handed the query layer a request identical to ImportPermitByHSCodeReport's, so the two
        // returned the same rows by construction. That is exactly what the customer rejected.
        var border = Assert.IsType<sp_HSCodeReportRequest>(
            ReportTestHelper.CreateProcedureRequest(typeof(BorderImportPermitByHSCodeReportController)));
        var oversea = Assert.IsType<sp_HSCodeReportRequest>(
            ReportTestHelper.CreateProcedureRequest(typeof(ImportPermitByHSCodeReportController)));

        Assert.NotEqual(oversea.FormType, border.FormType);
        Assert.Equal("Import Permit", oversea.FormType);
    }

    [Fact]
    public void Sakhan_and_section_reach_the_query()
    {
        // The whole point of the 2026-09-10 change. Both were bound on the DTO and silently
        // dropped before it: SakhanId was mapped but the oversea branch has no such predicate, and
        // ExportImportSectionId had no DTO property at all on this controller.
        var method = ReportTestHelper.GetTryCreateReportRequest(typeof(BorderImportPermitByHSCodeReportController));
        var controller = ReportTestHelper.CreateController(
            typeof(BorderImportPermitByHSCodeReportController),
            ReportTestHelper.CreateInMemoryDbContext(nameof(Sakhan_and_section_reach_the_query)));

        var request = Invoke(method, controller, new BorderImportPermitByHSCodeReportRequest
        {
            FromDate = new DateTime(2025, 1, 1),
            ToDate = new DateTime(2025, 12, 31, 23, 59, 59),
            FilterType = "Start",
            SakhanId = 5,
            ExportImportSectionId = 10,
        });

        Assert.Equal(5, request.SakhanId);
        Assert.Equal(10, request.ExportImportSectionId);
    }

    [Fact]
    public void Choosing_a_Sakhan_changes_the_generated_sql()
    {
        // Sakhan 0 means "all" and drops out of the WHERE; a real Sakhan must narrow it.
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var all = sp_HSCodeReport.AggregateQuery(db, LegacyRequest(groupByCompany: false)).ToQueryString();
        var oneSakhan = sp_HSCodeReport
            .AggregateQuery(db, LegacyRequest(groupByCompany: false, sakhanId: 5))
            .ToQueryString();

        Assert.NotEqual(all, oneSakhan);
        Assert.Contains("[SakhanId]", oneSakhan, StringComparison.Ordinal);
    }

    [Fact]
    public void Drill_request_asks_for_company_grouping_and_the_summary_does_not()
    {
        var method = ReportTestHelper.GetTryCreateReportRequest(typeof(BorderImportPermitByHSCodeReportController));
        var controller = ReportTestHelper.CreateController(
            typeof(BorderImportPermitByHSCodeReportController),
            ReportTestHelper.CreateInMemoryDbContext(nameof(Drill_request_asks_for_company_grouping_and_the_summary_does_not)));

        static BorderImportPermitByHSCodeReportRequest Request(string groupBy) => new()
        {
            FromDate = new DateTime(2025, 1, 1),
            ToDate = new DateTime(2025, 12, 31, 23, 59, 59),
            FilterType = "Start",
            HSCode = "3919109900",
            GroupBy = groupBy,
        };

        var drill = Invoke(method, controller, Request("Company"));
        var summary = Invoke(method, controller, Request(string.Empty));

        Assert.True(drill.GroupByCompany);
        Assert.False(summary.GroupByCompany);
        // Both run the Border branch.
        Assert.Equal("Border Import Permit", drill.FormType);
        Assert.Equal("Border Import Permit", summary.FormType);
    }

    [Fact]
    public void Drill_groups_on_hs_code_and_company_registration_number_only()
    {
        // HSCodeDetailReport.rdlc groups on HSCodeId + CompanyRegistrationNo (rdlc:1263-1264) and
        // renders no Currency / Total Value, so the drill key must not contain the currency or
        // the company NAME -- either would split one company into several visually identical rows.
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var drillSql = sp_HSCodeReport.AggregateQuery(db, LegacyRequest(groupByCompany: true)).ToQueryString();
        var groupBy = GroupByClause(drillSql);

        // EF renders the currency as its joined column ([c].[Code]), never as the word
        // "Currency", so the key is pinned by its column COUNT: HSCodeId, HSCode.Code,
        // PaThaKa.CompanyRegistrationNo -- three, and no currency or company-name column.
        Assert.Equal(3, GroupByColumnCount(groupBy));
        Assert.Contains("[HSCodeId]", groupBy, StringComparison.Ordinal);
        Assert.Contains("[CompanyRegistrationNo]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyName]", groupBy, StringComparison.Ordinal);
    }

    [Fact]
    public void Summary_groups_on_hs_code_and_currency_and_reads_the_border_table()
    {
        // BorderHSCodeReport.rdlc groups on HSCodeId + Currency (rdlc:1157-1169) and has no
        // company column; the round-1 complaint was one HS code appearing once per buyer.
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var summarySql = sp_HSCodeReport.AggregateQuery(db, LegacyRequest(groupByCompany: false)).ToQueryString();
        var groupBy = GroupByClause(summarySql);

        // HSCodeId, HSCode.Code, HSCode.Description, Currency.Code -- four columns, no company.
        Assert.Equal(4, GroupByColumnCount(groupBy));
        Assert.Contains("[HSCodeId]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyRegistrationNo]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyName]", groupBy, StringComparison.Ordinal);
        // ...and the BORDER table now, not the oversea one (reversed 2026-09-10).
        Assert.Contains("[BorderImportPermit]", summarySql, StringComparison.Ordinal);
        Assert.DoesNotContain("[ImportPermit]", summarySql, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_order_still_has_its_tie_break_columns()
    {
        // The canary for sp_HSCodeReport.BorderImportPermitRows projecting PermitCreatedDate /
        // PermitId. LegacyOrder orders groups by Min(PermitCreatedDate) then Min(PermitId); leave
        // them unset on the Border projection and both collapse to a constant null, giving an
        // arbitrary group order and unstable OFFSET/FETCH paging.
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var summarySql = sp_HSCodeReport.AggregateQuery(db, LegacyRequest(groupByCompany: false)).ToQueryString();
        var orderBy = summarySql[summarySql.LastIndexOf("ORDER BY", StringComparison.Ordinal)..];

        Assert.Contains("[HSCodeId]", orderBy, StringComparison.Ordinal);
        Assert.Contains("[CreatedDate]", orderBy, StringComparison.Ordinal);
    }

    private static sp_HSCodeReportRequest LegacyRequest(bool groupByCompany, int sakhanId = 0) => new()
    {
        FormType = "Border Import Permit",
        FromDate = new DateTime(2025, 1, 1),
        ToDate = new DateTime(2025, 12, 31, 23, 59, 59),
        FilterType = "Start",
        HSCode = string.Empty,
        SakhanId = sakhanId,
        GroupByCompany = groupByCompany,
        LegacyOrder = true,
    };

    private static int GroupByColumnCount(string groupBy)
        => groupBy["GROUP BY".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;

    private static string GroupByClause(string sql)
    {
        var start = sql.IndexOf("GROUP BY", StringComparison.OrdinalIgnoreCase);
        Assert.True(start >= 0, "Expected a GROUP BY in:\n" + sql);
        var end = sql.IndexOf("ORDER BY", start, StringComparison.OrdinalIgnoreCase);
        return end > start ? sql[start..end] : sql[start..];
    }

    private static sp_HSCodeReportRequest Invoke(
        System.Reflection.MethodInfo tryCreate,
        object controller,
        BorderImportPermitByHSCodeReportRequest request)
    {
        var args = new object?[] { request, null, null };
        var ok = Assert.IsType<bool>(tryCreate.Invoke(controller, args));
        Assert.True(ok, "TryCreateReportRequest rejected a valid request.");
        return Assert.IsType<sp_HSCodeReportRequest>(args[1]);
    }
}
