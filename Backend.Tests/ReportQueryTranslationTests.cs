using API.StoredProcedureToLinq;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;

namespace Backend.Tests;

public sealed class ReportQueryTranslationTests
{
    [Fact]
    public void Hs_code_empty_branch_can_still_translate_dynamic_sorting()
    {
        using var db = ReportTestHelper.CreateSqlServerDbContext();
        var query = sp_HSCodeReport.Query(
            db,
            new sp_HSCodeReportRequest
            {
                FormType = "Unknown",
                FromDate = new DateTime(2024, 1, 1),
                ToDate = new DateTime(2024, 1, 31)
            });

        var sql = query
            .OrderBy("SakhanId DESC")
            .Skip(0)
            .Take(10)
            .ToQueryString();

        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(EmptySwitchBranchQueries))]
    public void Empty_switch_branch_can_still_translate_dynamic_sorting(
        IQueryable query,
        string sortColumn)
    {
        var sql = query
            .OrderBy($"{sortColumn} DESC")
            .Skip(0)
            .Take(10)
            .ToQueryString();

        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Export Licence")]
    [InlineData("Import Licence")]
    [InlineData("Export Permit")]
    [InlineData("Import Permit")]
    [InlineData("Border Export Licence")]
    [InlineData("Border Import Licence")]
    [InlineData("Border Export Permit")]
    [InlineData("Border Import Permit")]
    public void New_report_form_type_branch_translates_to_sql(string formType)
    {
        using var db = ReportTestHelper.CreateSqlServerDbContext();
        var query = sp_NewReport.Query(
            db,
            new sp_NewReportRequest
            {
                FormType = formType,
                FromDate = ReportTestHelper.FromDate,
                ToDate = ReportTestHelper.ToDate
            });

        // ToQueryString throws if EF cannot translate the LINQ to SQL.
        var sql = query.Skip(0).Take(10).ToQueryString();

        Assert.False(string.IsNullOrWhiteSpace(sql));
    }

    public static IEnumerable<object[]> EmptySwitchBranchQueries()
    {
        var db = ReportTestHelper.CreateSqlServerDbContext();
        var fromDate = ReportTestHelper.FromDate;
        var toDate = ReportTestHelper.ToDate;

        yield return
        [
            sp_AmendReport.Query(
                db,
                new sp_AmendReportRequest
                {
                    FormType = "Unknown",
                    FromDate = fromDate,
                    ToDate = toDate
                }),
            "SakhanId"
        ];
        yield return
        [
            sp_ActualAmendReport.Query(
                db,
                new sp_ActualAmendReportRequest
                {
                    FormType = "Unknown",
                    FromDate = fromDate,
                    ToDate = toDate
                }),
            "SakhanId"
        ];
        yield return
        [
            sp_CancelReport.Query(
                db,
                new sp_CancelReportRequest
                {
                    FormType = "Unknown",
                    FromDate = fromDate,
                    ToDate = toDate
                }),
            "SakhanId"
        ];
        yield return
        [
            sp_ExtensionReport.Query(
                db,
                new sp_ExtensionReportRequest
                {
                    FormType = "Unknown",
                    FromDate = fromDate,
                    ToDate = toDate
                }),
            "SakhanId"
        ];
        yield return
        [
            sp_NewReport.Query(
                db,
                new sp_NewReportRequest
                {
                    FormType = "Unknown",
                    FromDate = fromDate,
                    ToDate = toDate
                }),
            "SakhanId"
        ];
    }
}
