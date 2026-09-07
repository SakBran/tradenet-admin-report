using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests;

/// <summary>
/// Pins the DELIBERATE Tradenet 2.0 bug-for-bug behaviour of Border Import Permit By HS Code.
///
/// The old screen sets <c>model.FormType = AppConfig.ImportPermit</c> (legacy
/// ReportsController.cs:15465), so it has always run <c>dbo.sp_HSCodeReport</c>'s OVERSEA
/// Import Permit branch: oversea tables, LicenceDate window, @SakhanId ignored, and
/// BorderHSCodeReport.rdlc groups the rows on (HSCodeId, Currency). The customer compares the
/// new report against that screen and the owner's instruction (2026-09-05) is "same result as the
/// old report" -- the border-only figure (18 licences over 2025) was rejected against the old
/// report's 997. These tests exist so nobody "fixes" the FormType back to Border without a new
/// decision.
/// </summary>
public sealed class BorderImportPermitByHSCodeLegacyParityTests
{
    [Fact]
    public void Summary_routes_to_the_legacy_oversea_Import_Permit_branch()
    {
        var request = Assert.IsType<sp_HSCodeReportRequest>(
            ReportTestHelper.CreateProcedureRequest(typeof(BorderImportPermitByHSCodeReportController)));

        Assert.Equal("Import Permit", request.FormType);
        Assert.False(request.GroupByCompany);
    }

    [Fact]
    public void Summary_builds_the_same_procedure_request_as_the_oversea_report()
    {
        // The live-API parity assertion, pinned without a database: for the same filters the
        // Border report and the oversea ImportPermitByHSCodeReport must hand the query layer an
        // identical request, so they return identical rows and footers by construction.
        var border = Assert.IsType<sp_HSCodeReportRequest>(
            ReportTestHelper.CreateProcedureRequest(typeof(BorderImportPermitByHSCodeReportController)));
        var oversea = Assert.IsType<sp_HSCodeReportRequest>(
            ReportTestHelper.CreateProcedureRequest(typeof(ImportPermitByHSCodeReportController)));

        // Only the presentation flags may differ: the Border report prints the old screen's
        // order (LegacyOrder, round 3) while the oversea report keeps its own; the filter set --
        // and therefore the row set -- must be identical.
        var presentationFlags = new[] { nameof(sp_HSCodeReportRequest.LegacyOrder), nameof(sp_HSCodeReportRequest.GroupByCompany) };
        foreach (var property in typeof(sp_HSCodeReportRequest).GetProperties())
        {
            if (presentationFlags.Contains(property.Name))
            {
                continue;
            }

            Assert.Equal(property.GetValue(oversea), property.GetValue(border));
        }

        Assert.True(border.LegacyOrder);
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
        // Both still run the legacy oversea branch.
        Assert.Equal("Import Permit", drill.FormType);
        Assert.Equal("Import Permit", summary.FormType);
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
    public void Summary_groups_on_hs_code_and_currency_without_the_company()
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
        // ...and the oversea table, not BorderImportPermit.
        Assert.Contains("[ImportPermit]", summarySql, StringComparison.Ordinal);
        Assert.DoesNotContain("[BorderImportPermit]", summarySql, StringComparison.Ordinal);
    }

    private static sp_HSCodeReportRequest LegacyRequest(bool groupByCompany) => new()
    {
        FormType = "Import Permit",
        FromDate = new DateTime(2025, 1, 1),
        ToDate = new DateTime(2025, 12, 31, 23, 59, 59),
        FilterType = "Start",
        HSCode = string.Empty,
        GroupByCompany = groupByCompany,
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
