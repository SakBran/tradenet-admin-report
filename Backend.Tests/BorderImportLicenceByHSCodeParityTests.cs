using System.Reflection;
using API.Service.ExcelExport;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests;

/// <summary>
/// Pins the row grain of Border Import Licence By HS Code to the grain BorderHSCodeReport.rdlc
/// prints.
///
/// Complaint 2026-09-14 ("Data ထွက်ရှိမှု မမှန်ပါ အဟောင်းနဲ့ အသစ် မတူပါ"): the new report carried an extra
/// company-name grouping, so one HS code applied three times in USD printed three rows where the old
/// report printed ONE row with the values summed. Measured on the live PROD API (FilterType Start,
/// HSCode '', Sakhan 0, Section 0): 2025 returned 9,213 rows against the old report's 2,881
/// distinct (HS code, currency) pairs -- 1,413 pairs were split, the worst (3506990000/THB) into
/// 95 rows -- and 2026 to 14/09 returned 7,420 against 2,690, while "Total No of License" (12,435)
/// and the grand Total Value already matched. Cause: the buyer company was in the grouping key of
/// both the paged procedure and its LINQ twin, invisibly, because the grid has no company column.
/// BorderHSCodeReport.rdlc's ONLY row group is <c>=Fields!HSCodeId.Value</c> +
/// <c>=Fields!Currency.Value</c> (rdlc:1159-1162); the legacy dbo.sp_HSCodeReport returns raw item
/// rows and the RDLC does the summing.
///
/// The HS Code DETAIL drill (BorderImportLicenceHSCodeDetailReport -- the old screen's
/// BorderHSCodeDetailReport action, ReportsController.cs:10526 on origin/master) shares this controller and DOES
/// render Company Name (HSCodeDetailReport.rdlc:1262-1265, keyed on HSCodeId +
/// CompanyRegistrationNo), so it asks for that shape explicitly with <c>GroupBy=Company</c>. Both
/// halves are asserted here: a regression in either direction is a customer-visible row count.
/// Same defect and same fix as ExportLicenceByHSCodeParityTests (commit d22e96f, 2026-09-08).
/// </summary>
public sealed class BorderImportLicenceByHSCodeParityTests
{
    private const string FormType = "Border Import Licence";

    [Fact]
    public void Summary_does_not_ask_for_company_grouping()
    {
        var request = Assert.IsType<sp_HSCodeReportRequest>(
            ReportTestHelper.CreateProcedureRequest(typeof(BorderImportLicenceByHSCodeReportController)));

        Assert.Equal(FormType, request.FormType);
        Assert.False(request.GroupByCompany);
        // Owner decision carried over from 2026-09-08: keep the paged procedure with HS-code-STRING
        // order (HSCode, Currency, HSCodeId). LegacyOrder would force the LINQ twin, whose
        // HSCodeId / first-appearance order needs PermitCreatedDate and PermitId -- columns the
        // licence sources do not fill.
        Assert.False(request.LegacyOrder);
    }

    [Fact]
    public void Summary_groups_on_hs_code_and_currency_without_the_company()
    {
        // The .xlsx rows come from this query while the grid pages through
        // sp_HSCodeReport_pagination, so the two GROUP BYs have to agree or the sheet and the
        // table disagree -- which is exactly what the customer asked us to fix.
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var sql = sp_HSCodeReport.AggregateQuery(db, Request(groupByCompany: false)).ToQueryString();
        var groupBy = GroupByClause(sql);

        // HSCodeId, HSCode.Code, HSCode.Description, Currency.Code -- four columns, no company.
        // This FormType's source is a UNION (Pa Tha Ka + Individual Trading licences), so EF
        // renders the GROUP BY over the union's alias; the key is pinned by its column COUNT plus
        // the absences below, not by the alias-qualified spelling.
        Assert.Equal(4, GroupByColumnCount(groupBy));
        Assert.Contains("[HSCodeId]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyRegistrationNo]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyName]", groupBy, StringComparison.Ordinal);
    }

    [Fact]
    public void Drill_still_groups_on_hs_code_and_company_registration_number()
    {
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var groupBy = GroupByClause(
            sp_HSCodeReport.AggregateQuery(db, Request(groupByCompany: true)).ToQueryString());

        // HSCodeId, HSCode.Code, CompanyRegistrationNo -- three, and no currency or company-NAME
        // column: HSCodeDetailReport.rdlc renders neither Currency nor Total Value, and keying on
        // the name would split a company whose name was re-spelled.
        Assert.Equal(3, GroupByColumnCount(groupBy));
        Assert.Contains("[HSCodeId]", groupBy, StringComparison.Ordinal);
        Assert.Contains("[CompanyRegistrationNo]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyName]", groupBy, StringComparison.Ordinal);
    }

    [Fact]
    public void Drill_request_asks_for_company_grouping_and_the_summary_does_not()
    {
        AssertGroupByRoundTrip<BorderImportLicenceByHSCodeReportRequest>(
            typeof(BorderImportLicenceByHSCodeReportController),
            groupBy => new()
            {
                FromDate = new DateTime(2025, 1, 1),
                ToDate = new DateTime(2025, 12, 31, 23, 59, 59),
                FilterType = "Start",
                HSCode = "3506990000",
                SakhanId = 0,
                GroupBy = groupBy,
            });
    }

    [Fact]
    public void Paged_procedure_groups_on_the_rdlc_key_in_every_sub_branch()
    {
        // The grid runs the procedure, not the LINQ twin above. Its branches are hand-written per
        // FormType, so a text assertion is the only way to keep them in step from here. Comment
        // lines are stripped first: the branch's explanatory comment names the key it replaced.
        var branch = WithoutLineComments(ProcedureBranch(FormType));

        Assert.DoesNotContain("GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo", branch);
        // Three sub-branches (@HSCode='' / FilterType Start / End); this FormType has no
        // @IncludeTotalCount=0 fast page -- it always returns COUNT(*) OVER().
        var groupBys = CountOccurrences(branch, "GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency");
        Assert.Equal(3, groupBys);
        // The page window must be a unique key or OFFSET/FETCH can repeat one row across pages and
        // drop another: ORDER BY (HSCode, CompanyName, Currency) over a key that also held
        // CompanyRegistrationNo and HSDescription was not unique.
        Assert.Equal(3, CountOccurrences(branch, "ORDER BY result.HSCode,result.Currency,result.HSCodeId"));
        Assert.DoesNotContain("ORDER BY result.HSCode,result.CompanyName,result.Currency", branch);
        // The company columns stay on the result set as typed NULLs so sp_HSCodeAggregateReportResult
        // keeps its 8-column shape -- one per sub-branch.
        Assert.Equal(
            3,
            CountOccurrences(branch, "CAST(NULL AS nvarchar(200)) CompanyRegistrationNo,CAST(NULL AS nvarchar(500)) CompanyName"));
    }

    [Fact]
    public void Excel_format_version_was_bumped_so_the_cache_cannot_serve_the_company_split_workbook()
    {
        // ExcelExportJobService reuses an already-generated file for a closed date range whenever
        // the request hashes the same; the hash includes this version, so without the bump a 2025
        // request keeps receiving the 9,213-row company-split workbook until it expires.
        var version = typeof(BorderImportLicenceByHSCodeReportController)
            .GetCustomAttribute<ExcelFormatVersionAttribute>(inherit: false);

        Assert.NotNull(version);
        Assert.True(
            version!.Version >= 2,
            $"ExcelFormatVersion is {version.Version}; the (HS code, currency) regrouping needs at least 2.");
    }

    [Fact]
    public void The_procedure_and_the_linq_twin_agree_for_this_form_type()
    {
        // sp_HSCodeReport.GroupsByCompany is private; its only observable output for the summary
        // is AggregateQuery's GROUP BY, which is what the .xlsx streams. The grid pages through
        // the procedure branch instead. Pin that the two make the same decision -- and that the
        // decision is "no company".
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var linqGroupBy = GroupByClause(
            sp_HSCodeReport.AggregateQuery(db, Request(groupByCompany: false)).ToQueryString());
        var branch = WithoutLineComments(ProcedureBranch(FormType));

        var linqKeysOnCompany = linqGroupBy.Contains("[CompanyRegistrationNo]", StringComparison.Ordinal);
        var procedureKeysOnCompany = branch
            .Split('\n')
            .Any(line => line.Contains("GROUP BY", StringComparison.Ordinal)
                && line.Contains("CompanyRegistrationNo", StringComparison.Ordinal));

        Assert.Equal(procedureKeysOnCompany, linqKeysOnCompany);
        Assert.False(linqKeysOnCompany);
    }

    private static void AssertGroupByRoundTrip<TRequest>(Type controllerType, Func<string, TRequest> build)
        where TRequest : class
    {
        var method = ReportTestHelper.GetTryCreateReportRequest(controllerType);
        var controller = ReportTestHelper.CreateController(
            controllerType,
            ReportTestHelper.CreateInMemoryDbContext(controllerType.Name + "_GroupBy"));

        var drill = Invoke(method, controller, build("Company"));
        var summary = Invoke(method, controller, build(string.Empty));

        Assert.True(drill.GroupByCompany);
        Assert.False(summary.GroupByCompany);
        // Both run the Border Import Licence branch.
        Assert.Equal(FormType, drill.FormType);
        Assert.Equal(FormType, summary.FormType);
        // The drill posts the constant verbatim; accept it case-insensitively so a config typo in
        // casing cannot silently fall back to the summary shape.
        Assert.True(Invoke(method, controller, build("company")).GroupByCompany);
    }

    private static sp_HSCodeReportRequest Invoke(
        MethodInfo tryCreate,
        object controller,
        object request)
    {
        var args = new object?[] { request, null, null };
        var ok = Assert.IsType<bool>(tryCreate.Invoke(controller, args));
        Assert.True(ok, "TryCreateReportRequest rejected a valid request.");
        return Assert.IsType<sp_HSCodeReportRequest>(args[1]);
    }

    private static sp_HSCodeReportRequest Request(bool groupByCompany) => new()
    {
        FormType = FormType,
        FromDate = new DateTime(2025, 1, 1),
        ToDate = new DateTime(2025, 12, 31, 23, 59, 59),
        FilterType = "Start",
        HSCode = string.Empty,
        SakhanId = 0,
        GroupByCompany = groupByCompany,
    };

    /// <summary>
    /// The text of one <c>@FormType</c> branch of sp_HSCodeReport_pagination, so an assertion
    /// cannot be satisfied by a different report's branch.
    /// </summary>
    private static string ProcedureBranch(string formType)
    {
        var sql = File.ReadAllText(Path.Combine(
            RepositoryRoot, "StoredProcedureMigrations", "sp_HSCodeReport_pagination.sql"));

        var start = sql.IndexOf($"(@FormType='{formType}')", StringComparison.Ordinal);
        Assert.True(start >= 0, $"No @FormType='{formType}' branch in sp_HSCodeReport_pagination.sql.");

        var end = sql.IndexOf("ELSE IF(@FormType=", start + 1, StringComparison.Ordinal);
        return end > start ? sql[start..end] : sql[start..];
    }

    /// <summary>Drops <c>--</c> line comments so prose about a key is not counted as the key.</summary>
    private static string WithoutLineComments(string sql)
        => string.Join(
            '\n',
            sql.Split('\n').Select(line =>
            {
                var comment = line.IndexOf("--", StringComparison.Ordinal);
                return comment >= 0 ? line[..comment] : line;
            }));

    private static int GroupByColumnCount(string groupBy)
        => groupBy["GROUP BY".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;

    /// <summary>
    /// The outermost GROUP BY: this FormType's source is a UNION ALL of two sub-selects, so the
    /// LAST occurrence is the one over the union alias that AggregateQuery adds.
    /// </summary>
    private static string GroupByClause(string sql)
    {
        var start = sql.LastIndexOf("GROUP BY", StringComparison.OrdinalIgnoreCase);
        Assert.True(start >= 0, "Expected a GROUP BY in:\n" + sql);
        var end = sql.IndexOf("ORDER BY", start, StringComparison.OrdinalIgnoreCase);
        return end > start ? sql[start..end] : sql[start..];
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null
                && !Directory.Exists(Path.Combine(directory.FullName, "Frontend")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}
