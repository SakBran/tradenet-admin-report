using API.Model;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests;

/// <summary>
/// Pins the DELIBERATE Tradenet 2.0 bug-for-bug behaviour of Border Export Permit By HS Code.
///
/// The old screen sets <c>model.FormType = AppConfig.ExportPermit</c> and posts it back through a
/// hidden field (legacy ReportsController.cs:14120, Views/Reports/BorderExportPermitByHSCodeReport.cshtml:21),
/// so it has always run <c>dbo.sp_HSCodeReport</c>'s OVERSEA Export Permit branch: oversea tables,
/// LicenceDate window, @SakhanId ignored, no section parameter, and BorderHSCodeReport.rdlc groups the
/// rows on (HSCodeId, Currency) with no sort over a result the procedure returns ORDER BY HSCode.Id.
/// The customer compares the new report against that screen ("record မကိုက်ပါ", 2026-09-07) and the
/// owner's instruction for the Border Import Permit twin (2026-09-05) is "same result as the old
/// report". These tests exist so nobody "fixes" the FormType back to Border without a new decision.
/// </summary>
public sealed class BorderExportPermitByHSCodeLegacyParityTests
{
    [Fact]
    public void Summary_routes_to_the_legacy_oversea_Export_Permit_branch_in_legacy_order()
    {
        var request = Assert.IsType<sp_HSCodeReportRequest>(
            ReportTestHelper.CreateProcedureRequest(typeof(BorderExportPermitByHSCodeReportController)));

        Assert.Equal("Export Permit", request.FormType);
        Assert.False(request.GroupByCompany);
        Assert.True(request.LegacyOrder);
        // Legacy dbo.sp_HSCodeReport has no section parameter; the old Export Section box was dead.
        Assert.Equal(0, request.ExportImportSectionId);
    }

    [Fact]
    public void Summary_filters_exactly_like_the_oversea_report()
    {
        // The live-API parity assertion, pinned without a database: for the same filters the
        // Border report must hand the query layer the oversea ExportPermitByHSCodeReport's filter
        // set, so both read the same rows. Only the presentation flags may differ (the oversea
        // report keeps its own grouping/order; the Border one prints the old screen's).
        var border = Assert.IsType<sp_HSCodeReportRequest>(
            ReportTestHelper.CreateProcedureRequest(typeof(BorderExportPermitByHSCodeReportController)));
        var oversea = Assert.IsType<sp_HSCodeReportRequest>(
            ReportTestHelper.CreateProcedureRequest(typeof(ExportPermitByHSCodeReportController)));

        var presentationFlags = new[] { nameof(sp_HSCodeReportRequest.LegacyOrder), nameof(sp_HSCodeReportRequest.GroupByCompany) };
        foreach (var property in typeof(sp_HSCodeReportRequest).GetProperties())
        {
            if (presentationFlags.Contains(property.Name))
            {
                continue;
            }

            Assert.Equal(property.GetValue(oversea), property.GetValue(border));
        }
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
        // Both still run the legacy oversea branch in legacy order.
        Assert.Equal("Export Permit", drill.FormType);
        Assert.Equal("Export Permit", summary.FormType);
        Assert.True(drill.LegacyOrder);
        Assert.True(summary.LegacyOrder);
    }

    [Fact]
    public void Summary_groups_on_hs_code_and_currency_without_the_company_and_reads_the_oversea_table()
    {
        // BorderHSCodeReport.rdlc groups on HSCodeId + Currency (rdlc:1159-1162) and has no company
        // column. Without LegacyOrder the Export Permit source takes the company split that the
        // oversea ExportPermitHSCodeDetailReport renders -- that is the shape this test forbids.
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var summarySql = sp_HSCodeReport.AggregateQuery(db, LegacyRequest(groupByCompany: false)).ToQueryString();
        var groupBy = GroupByClause(summarySql);

        // HSCodeId, HSCode.Code, HSCode.Description, Currency.Code -- four columns, no company.
        Assert.Equal(4, GroupByColumnCount(groupBy));
        Assert.Contains("[HSCodeId]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyRegistrationNo]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyName]", groupBy, StringComparison.Ordinal);
        // ...the oversea table, not BorderExportPermit...
        Assert.Contains("[ExportPermit]", summarySql, StringComparison.Ordinal);
        Assert.DoesNotContain("[BorderExportPermit]", summarySql, StringComparison.Ordinal);
        // ...in the old report's order: HS code ID, then the permits as created.
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

        var drillSql = sp_HSCodeReport.AggregateQuery(db, LegacyRequest(groupByCompany: true)).ToQueryString();
        var groupBy = GroupByClause(drillSql);

        Assert.Equal(3, GroupByColumnCount(groupBy));
        Assert.Contains("[HSCodeId]", groupBy, StringComparison.Ordinal);
        Assert.Contains("[CompanyRegistrationNo]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyName]", groupBy, StringComparison.Ordinal);
        Assert.Contains("[ExportPermit]", drillSql, StringComparison.Ordinal);
    }

    [Fact]
    public void The_oversea_report_itself_is_untouched_by_the_legacy_flags()
    {
        // The LegacyOrder branch was moved ahead of the per-FormType company split. Without the
        // flag the oversea ExportPermitByHSCodeReport must still get its (HS code, company, currency)
        // rows -- its ExportPermitHSCodeDetailReport config renders Company Name off this query.
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var request = LegacyRequest(groupByCompany: false);
        request.LegacyOrder = false;
        var groupBy = GroupByClause(sp_HSCodeReport.AggregateQuery(db, request).ToQueryString());

        Assert.Contains("[CompanyRegistrationNo]", groupBy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_grids_initial_SakhanId_sort_does_not_break_the_legacy_path()
    {
        // BasicTable posts the config's initialSortColumn ('SakhanId' on every HS Code config) with
        // every request. ReportAggregateResult has no such property and ApiResult.ApplySort throws for
        // an unknown column, so the LINQ-only legacy path answered the grid's FIRST page with HTTP 500
        // -- measured on production for the Border Import Permit twin on 2026-09-07. The legacy order
        // must simply stand; only a real column (a header the user clicked) is a sort.
        using var db = ReportTestHelper.CreateInMemoryDbContext(nameof(The_grids_initial_SakhanId_sort_does_not_break_the_legacy_path));
        var paging = new ReportQueryRequest { PageIndex = 0, PageSize = 10, SortColumn = "SakhanId", SortOrder = "ASC", IncludeTotalCount = true };

        var result = await sp_HSCodeReport.CreateAggregateResultAsync(db, LegacyRequest(groupByCompany: false), paging);

        Assert.Empty(result.Data);
        Assert.True(result.IsTotalCountExact);
    }

    private static sp_HSCodeReportRequest LegacyRequest(bool groupByCompany) => new()
    {
        FormType = "Export Permit",
        FromDate = new DateTime(2025, 1, 1),
        ToDate = new DateTime(2026, 9, 6, 23, 59, 59),
        FilterType = "Start",
        HSCode = string.Empty,
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
