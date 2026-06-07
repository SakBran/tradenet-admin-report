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

    public static readonly string[] StandardFormTypes =
    [
        "Export Licence",
        "Import Licence",
        "Export Permit",
        "Import Permit",
        "Border Export Licence",
        "Border Import Licence",
        "Border Export Permit",
        "Border Import Permit"
    ];

    public static IEnumerable<object[]> AggregateReportFormTypes()
    {
        var reports = new[]
        {
            "New", "Amend", "ActualAmend", "Cancel", "Extension", "Voucher"
        };
        foreach (var report in reports)
        {
            foreach (var formType in StandardFormTypes)
            {
                yield return [report, formType];
            }
        }
    }

    [Theory]
    [MemberData(nameof(AggregateReportFormTypes))]
    public void Aggregate_report_form_type_branch_translates_to_sql(string report, string formType)
    {
        using var db = ReportTestHelper.CreateSqlServerDbContext();
        var from = ReportTestHelper.FromDate;
        var to = ReportTestHelper.ToDate;

        IQueryable query = report switch
        {
            "New" => sp_NewReport.Query(db, new sp_NewReportRequest { FormType = formType, FromDate = from, ToDate = to }),
            "Amend" => sp_AmendReport.Query(db, new sp_AmendReportRequest { FormType = formType, FromDate = from, ToDate = to }),
            "ActualAmend" => sp_ActualAmendReport.Query(db, new sp_ActualAmendReportRequest { FormType = formType, FromDate = from, ToDate = to }),
            "Cancel" => sp_CancelReport.Query(db, new sp_CancelReportRequest { FormType = formType, FromDate = from, ToDate = to }),
            "Extension" => sp_ExtensionReport.Query(db, new sp_ExtensionReportRequest { FormType = formType, FromDate = from, ToDate = to }),
            "Voucher" => sp_VoucherReport.Query(db, new sp_VoucherReportRequest { FormType = formType, FromDate = from, ToDate = to }),
            _ => throw new ArgumentOutOfRangeException(nameof(report))
        };

        // ToQueryString throws if EF cannot translate the LINQ to SQL.
        var sql = query.Skip(0).Take(10).ToQueryString();

        Assert.False(string.IsNullOrWhiteSpace(sql));
    }

    [Fact]
    public void Pending_report_translates_to_sql()
    {
        using var db = ReportTestHelper.CreateSqlServerDbContext();
        var query = sp_PendingReport.Query(
            db,
            new sp_PendingReportRequest
            {
                FromDate = ReportTestHelper.FromDate,
                ToDate = ReportTestHelper.ToDate
            });

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

    [Fact]
    public void New_report_paged_stored_procedure_passes_quota_parameter()
    {
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var sql = sp_NewReport.ExecuteQueryable(
                db,
                new sp_NewReportRequest
                {
                    FormType = "Import Licence",
                    FromDate = ReportTestHelper.FromDate,
                    ToDate = ReportTestHelper.ToDate,
                    Quota = "Quota"
                },
                pageIndex: 0,
                pageSize: 10,
                includeTotalCount: false)
            .ToQueryString();

        Assert.Contains("sp_NewReport_pagination", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@quota", sql, StringComparison.OrdinalIgnoreCase);
    }
}
