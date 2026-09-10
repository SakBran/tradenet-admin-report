using API.Model;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests;

/// <summary>
/// Pins that Border Export Permit By HS Code reads the BORDER tables and that its Sakhan filter
/// works. Twin of <see cref="BorderImportPermitByHSCodeBorderSourceTests"/>; the history is there.
///
/// In short: the old Tradenet 2.0 screen sets <c>model.FormType = AppConfig.ExportPermit</c> and
/// posts it back through a hidden field (legacy ReportsController.cs:14120,
/// Views/Reports/BorderExportPermitByHSCodeReport.cshtml:21), so it lists OVERSEA permits under a
/// Border title and its Sakhan dropdown cannot work. This report reproduced that bug-for-bug from
/// 2026-09-07 until 2026-09-10, when the customer asked for the Sakhan filter to work instead
/// ("Old Reportမှာမှားနေလို့ပါ"). Measured cost on their window (2024-05-01..2026-09-06):
/// 578 rows / 2,637 permits -> 12 / 8. See docs/BorderPermitByHSCodeSakhanSwitch_2026-09-10.md.
/// </summary>
public sealed class BorderExportPermitByHSCodeBorderSourceTests
{
    [Fact]
    public void Summary_routes_to_the_Border_Export_Permit_branch_in_legacy_order()
    {
        var request = Assert.IsType<sp_HSCodeReportRequest>(
            ReportTestHelper.CreateProcedureRequest(typeof(BorderExportPermitByHSCodeReportController)));

        Assert.Equal("Border Export Permit", request.FormType);
        Assert.False(request.GroupByCompany);
        // Still true, and now load-bearing for a second reason: GroupsByCompany returns true
        // unconditionally for "Border Export Permit", so without LegacyOrder the summary would
        // split per buyer company (the defect fixed on 2026-09-08).
        Assert.True(request.LegacyOrder);
    }

    [Fact]
    public void Summary_no_longer_mirrors_the_oversea_report()
    {
        // The inverse of the assertion this file used to make. Until 2026-09-10 the Border report
        // handed the query layer a request identical to ExportPermitByHSCodeReport's.
        var border = Assert.IsType<sp_HSCodeReportRequest>(
            ReportTestHelper.CreateProcedureRequest(typeof(BorderExportPermitByHSCodeReportController)));
        var oversea = Assert.IsType<sp_HSCodeReportRequest>(
            ReportTestHelper.CreateProcedureRequest(typeof(ExportPermitByHSCodeReportController)));

        Assert.NotEqual(oversea.FormType, border.FormType);
        Assert.Equal("Export Permit", oversea.FormType);
    }

    [Fact]
    public void Sakhan_and_section_reach_the_query()
    {
        var method = ReportTestHelper.GetTryCreateReportRequest(typeof(BorderExportPermitByHSCodeReportController));
        var controller = ReportTestHelper.CreateController(
            typeof(BorderExportPermitByHSCodeReportController),
            ReportTestHelper.CreateInMemoryDbContext(nameof(Sakhan_and_section_reach_the_query)));

        var request = Invoke(method, controller, new BorderExportPermitByHSCodeReportRequest
        {
            FromDate = new DateTime(2025, 1, 1),
            ToDate = new DateTime(2026, 9, 6, 23, 59, 59),
            FilterType = "Start",
            SakhanId = 5,
            ExportImportSectionId = 5,
        });

        Assert.Equal(5, request.SakhanId);
        Assert.Equal(5, request.ExportImportSectionId);
    }

    [Fact]
    public void Choosing_a_Sakhan_changes_the_generated_sql()
    {
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var all = sp_HSCodeReport.AggregateQuery(db, BorderRequest(groupByCompany: false)).ToQueryString();
        var oneSakhan = sp_HSCodeReport
            .AggregateQuery(db, BorderRequest(groupByCompany: false, sakhanId: 5))
            .ToQueryString();

        Assert.NotEqual(all, oneSakhan);
        Assert.Contains("[SakhanId]", oneSakhan, StringComparison.Ordinal);
    }

    [Fact]
    public void Drill_request_asks_for_company_grouping_and_the_summary_does_not()
    {
        var method = ReportTestHelper.GetTryCreateReportRequest(typeof(BorderExportPermitByHSCodeReportController));
        var controller = ReportTestHelper.CreateController(
            typeof(BorderExportPermitByHSCodeReportController),
            ReportTestHelper.CreateInMemoryDbContext(nameof(Drill_request_asks_for_company_grouping_and_the_summary_does_not)));

        static BorderExportPermitByHSCodeReportRequest Request(string groupBy) => new()
        {
            FromDate = new DateTime(2025, 1, 1),
            ToDate = new DateTime(2026, 9, 6, 23, 59, 59),
            FilterType = "Start",
            HSCode = "0901112000",
            GroupBy = groupBy,
        };

        var drill = Invoke(method, controller, Request("Company"));
        var summary = Invoke(method, controller, Request(string.Empty));

        Assert.True(drill.GroupByCompany);
        Assert.False(summary.GroupByCompany);
        // Both run the Border branch, in legacy order.
        Assert.Equal("Border Export Permit", drill.FormType);
        Assert.Equal("Border Export Permit", summary.FormType);
        Assert.True(drill.LegacyOrder);
        Assert.True(summary.LegacyOrder);
    }

    [Fact]
    public void Summary_groups_on_hs_code_and_currency_without_the_company_and_reads_the_border_table()
    {
        // BorderHSCodeReport.rdlc groups on HSCodeId + Currency (rdlc:1159-1162) and has no company
        // column. Without LegacyOrder the Border Export Permit source takes GroupsByCompany's
        // unconditional company split -- that is the shape this test forbids.
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var summarySql = sp_HSCodeReport.AggregateQuery(db, BorderRequest(groupByCompany: false)).ToQueryString();
        var groupBy = GroupByClause(summarySql);

        // HSCodeId, HSCode.Code, HSCode.Description, Currency.Code -- four columns, no company.
        Assert.Equal(4, GroupByColumnCount(groupBy));
        Assert.Contains("[HSCodeId]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyRegistrationNo]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyName]", groupBy, StringComparison.Ordinal);
        // ...the BORDER table now, not the oversea one (reversed 2026-09-10)...
        Assert.Contains("[BorderExportPermit]", summarySql, StringComparison.Ordinal);
        Assert.DoesNotContain("[ExportPermit]", summarySql, StringComparison.Ordinal);
        // ...in the rdlc's order: HS code ID, then the permits as created. [CreatedDate] here is
        // the canary for BorderExportPermitRows projecting PermitCreatedDate / PermitId -- unset,
        // the tie-break collapses to a constant null and paging goes unstable.
        var orderBy = summarySql[summarySql.LastIndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase)..];
        Assert.Contains("[HSCodeId]", orderBy, StringComparison.Ordinal);
        Assert.Contains("[CreatedDate]", orderBy, StringComparison.Ordinal);
    }

    [Fact]
    public void Drill_groups_on_hs_code_and_company_registration_number_only()
    {
        // HSCodeDetailReport.rdlc groups on HSCodeId + CompanyRegistrationNo (rdlc:1263-1264) and
        // renders no Currency / Total Value, so the drill key must not contain the currency or
        // the company NAME -- either would split one company into several visually identical rows.
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var drillSql = sp_HSCodeReport.AggregateQuery(db, BorderRequest(groupByCompany: true)).ToQueryString();
        var groupBy = GroupByClause(drillSql);

        Assert.Equal(3, GroupByColumnCount(groupBy));
        Assert.Contains("[HSCodeId]", groupBy, StringComparison.Ordinal);
        Assert.Contains("[CompanyRegistrationNo]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyName]", groupBy, StringComparison.Ordinal);
        Assert.Contains("[BorderExportPermit]", drillSql, StringComparison.Ordinal);
    }

    [Fact]
    public void The_oversea_report_itself_is_untouched_by_the_legacy_flags()
    {
        // Unchanged by the 2026-09-10 switch, and deliberately built on its OWN oversea request:
        // the LegacyOrder branch sits ahead of the per-FormType company split, and without the flag
        // the oversea ExportPermitByHSCodeReport must still get its (HS code, company, currency)
        // rows -- its ExportPermitHSCodeDetailReport config renders Company Name off this query.
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var request = BorderRequest(groupByCompany: false);
        request.FormType = "Export Permit";
        request.LegacyOrder = false;
        var groupBy = GroupByClause(sp_HSCodeReport.AggregateQuery(db, request).ToQueryString());

        Assert.Contains("[CompanyRegistrationNo]", groupBy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_grid_sort_column_does_not_break_the_legacy_path()
    {
        // BasicTable used to post 'SakhanId' as every HS Code config's initialSortColumn.
        // ReportAggregateResult has no such property and ApiResult.ApplySort throws for an unknown
        // column, so the LINQ-only legacy path answered the grid's FIRST page with HTTP 500 --
        // measured on production for the Border Import Permit twin on 2026-09-07. The configs no
        // longer carry that initialSortColumn, but the guard must stay: the legacy order simply
        // stands, and only a real column (a header the user clicked) is a sort.
        using var db = ReportTestHelper.CreateInMemoryDbContext(nameof(An_unknown_grid_sort_column_does_not_break_the_legacy_path));
        var paging = new ReportQueryRequest { PageIndex = 0, PageSize = 10, SortColumn = "SakhanId", SortOrder = "ASC", IncludeTotalCount = true };

        var result = await sp_HSCodeReport.CreateAggregateResultAsync(db, BorderRequest(groupByCompany: false), paging);

        Assert.Empty(result.Data);
        Assert.True(result.IsTotalCountExact);
    }

    private static sp_HSCodeReportRequest BorderRequest(bool groupByCompany, int sakhanId = 0) => new()
    {
        FormType = "Border Export Permit",
        FromDate = new DateTime(2025, 1, 1),
        ToDate = new DateTime(2026, 9, 6, 23, 59, 59),
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
        var closing = sql.IndexOf(')', start);
        if (closing > start && (end < 0 || closing < end))
        {
            end = closing;
        }

        return end > start ? sql[start..end] : sql[start..];
    }

    private static sp_HSCodeReportRequest Invoke(
        System.Reflection.MethodInfo tryCreate,
        object controller,
        BorderExportPermitByHSCodeReportRequest request)
    {
        var args = new object?[] { request, null, null };
        var ok = Assert.IsType<bool>(tryCreate.Invoke(controller, args));
        Assert.True(ok, "TryCreateReportRequest rejected a valid request.");
        return Assert.IsType<sp_HSCodeReportRequest>(args[1]);
    }
}
