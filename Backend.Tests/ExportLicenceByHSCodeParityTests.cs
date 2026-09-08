using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests;

/// <summary>
/// Pins the row grain of Export Licence By HS Code and Border Export Licence By HS Code to the
/// grain their RDLCs print.
///
/// Complaint 2026-09-08: for 31/08-01/09/2026 the old reports show 304 and 33 rows, the new ones
/// showed 1058 and 76, while "Total No of License" (961 / 41) already matched. Cause: the buyer
/// company was in the grouping key, so one HS code printed once per buyer -- invisibly, because
/// neither grid has a company column -- each row carrying only that buyer's slice of Total Value.
/// HSCodeReport.rdlc:1150-1160 and BorderHSCodeReport.rdlc:1158-1168 group on
/// <c>=Fields!HSCodeId.Value</c> + <c>=Fields!Currency.Value</c> and nothing else.
///
/// The HS Code DETAIL drills share these controllers and DO render Company Name
/// (HSCodeDetailReport.rdlc:1261-1265, keyed on HSCodeId + CompanyRegistrationNo), so they ask for
/// that shape explicitly with <c>GroupBy=Company</c>. Both halves are asserted here: a regression
/// in either direction is a customer-visible row count.
/// </summary>
public sealed class ExportLicenceByHSCodeParityTests
{
    public static TheoryData<Type, string> SummaryControllers() => new()
    {
        { typeof(ExportLicenceByHSCodeReportController), "Export Licence" },
        { typeof(BorderExportLicenceByHSCodeReportController), "Border Export Licence" },
    };

    [Theory]
    [MemberData(nameof(SummaryControllers))]
    public void Summary_does_not_ask_for_company_grouping(Type controllerType, string formType)
    {
        var request = Assert.IsType<sp_HSCodeReportRequest>(
            ReportTestHelper.CreateProcedureRequest(controllerType));

        Assert.Equal(formType, request.FormType);
        Assert.False(request.GroupByCompany);
        // These two keep the paged procedure (owner decision 2026-09-08): HS-code-string order,
        // not the legacy HSCodeId order that forces the LINQ twin.
        Assert.False(request.LegacyOrder);
    }

    [Theory]
    [MemberData(nameof(SummaryControllers))]
    public void Summary_groups_on_hs_code_and_currency_without_the_company(Type controllerType, string formType)
    {
        // The .xlsx rows come from this query while the grid pages through
        // sp_HSCodeReport_pagination, so the two GROUP BYs have to agree or the sheet and the
        // table disagree -- which is exactly what the customer asked us to fix.
        _ = controllerType;
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var sql = sp_HSCodeReport.AggregateQuery(db, Request(formType, groupByCompany: false)).ToQueryString();
        var groupBy = GroupByClause(sql);

        // HSCodeId, HSCode.Code, HSCode.Description, Currency.Code -- four columns, no company.
        // EF renders the currency as its joined column ([c].[Code]), never as the word
        // "Currency", so the key is pinned by its column COUNT plus the absences below.
        Assert.Equal(4, GroupByColumnCount(groupBy));
        Assert.Contains("[HSCodeId]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyRegistrationNo]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyName]", groupBy, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(SummaryControllers))]
    public void Drill_still_groups_on_hs_code_and_company_registration_number(Type controllerType, string formType)
    {
        _ = controllerType;
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var groupBy = GroupByClause(
            sp_HSCodeReport.AggregateQuery(db, Request(formType, groupByCompany: true)).ToQueryString());

        // HSCodeId, HSCode.Code, PaThaKa.CompanyRegistrationNo -- three, and no currency or
        // company-NAME column: HSCodeDetailReport.rdlc renders neither Currency nor Total Value,
        // and keying on the name would split a company whose name was re-spelled.
        Assert.Equal(3, GroupByColumnCount(groupBy));
        Assert.Contains("[HSCodeId]", groupBy, StringComparison.Ordinal);
        Assert.Contains("[CompanyRegistrationNo]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyName]", groupBy, StringComparison.Ordinal);
    }

    [Fact]
    public void Drill_request_asks_for_company_grouping_and_the_summary_does_not()
    {
        AssertGroupByRoundTrip<ExportLicenceByHSCodeReportRequest>(
            typeof(ExportLicenceByHSCodeReportController),
            groupBy => new()
            {
                FromDate = new DateTime(2026, 8, 31),
                ToDate = new DateTime(2026, 9, 1, 23, 59, 59),
                FilterType = "Start",
                HSCode = "0302490030",
                GroupBy = groupBy,
            });

        AssertGroupByRoundTrip<BorderExportLicenceByHSCodeReportRequest>(
            typeof(BorderExportLicenceByHSCodeReportController),
            groupBy => new()
            {
                FromDate = new DateTime(2026, 8, 31),
                ToDate = new DateTime(2026, 9, 1, 23, 59, 59),
                FilterType = "Start",
                HSCode = "0302490030",
                GroupBy = groupBy,
            });
    }

    [Theory]
    [InlineData("Export Licence")]
    [InlineData("Border Export Licence")]
    public void Paged_procedure_groups_on_the_rdlc_key_in_every_sub_branch(string formType)
    {
        // The grid runs the procedure, not the LINQ twin above. Its branches are hand-written per
        // FormType, so a text assertion is the only way to keep them in step from here.
        var branch = ProcedureBranch(formType);

        Assert.DoesNotContain("GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo", branch);
        var groupBys = CountOccurrences(branch, "GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency");
        // Export Licence has four sub-branches (the fast page plus ''/Start/End); Border Export
        // Licence has three (no fast page -- it always returns COUNT(*) OVER()).
        Assert.Equal(formType == "Export Licence" ? 4 : 3, groupBys);
        // The page window must be a unique key or OFFSET/FETCH can repeat one row across pages and
        // drop another: ORDER BY (HSCode, CompanyName, Currency) over a key containing
        // CompanyRegistrationNo was how 1058 rows yielded ~962 distinct ones while paging.
        Assert.Equal(groupBys, CountOccurrences(branch, "ORDER BY result.HSCode,result.Currency,result.HSCodeId"));
        Assert.DoesNotContain("ORDER BY result.HSCode,result.CompanyName,result.Currency", branch);
    }

    [Fact]
    public void Export_Licence_fast_page_filters_on_the_section_like_the_counted_branches()
    {
        // Legacy dbo.sp_HSCodeReport joins ExportImportSection in every branch. The fast page did
        // not, so it could show a licence whose ExportImportSectionId has no section row -- one the
        // old report never printed and the exact-count branch does not count, which is a pager
        // that offers more pages than there are rows.
        var branch = ProcedureBranch("Export Licence");

        Assert.Equal(
            4,
            CountOccurrences(branch, "INNER JOIN ExportImportSection section ON ExportLicence.ExportImportSectionId = section.Id"));
    }

    private static void AssertGroupByRoundTrip<TRequest>(Type controllerType, Func<string, TRequest> build)
        where TRequest : class
    {
        var method = ReportTestHelper.GetTryCreateReportRequest(controllerType);
        var controller = ReportTestHelper.CreateController(
            controllerType,
            ReportTestHelper.CreateInMemoryDbContext(controllerType.Name + "_GroupBy"));

        Assert.True(Invoke(method, controller, build("Company")).GroupByCompany);
        Assert.False(Invoke(method, controller, build(string.Empty)).GroupByCompany);
        // The drill posts the constant verbatim; accept it case-insensitively so a config typo in
        // casing cannot silently fall back to the summary shape.
        Assert.True(Invoke(method, controller, build("company")).GroupByCompany);
    }

    private static sp_HSCodeReportRequest Invoke(
        System.Reflection.MethodInfo tryCreate,
        object controller,
        object request)
    {
        var args = new object?[] { request, null, null };
        var ok = Assert.IsType<bool>(tryCreate.Invoke(controller, args));
        Assert.True(ok, "TryCreateReportRequest rejected a valid request.");
        return Assert.IsType<sp_HSCodeReportRequest>(args[1]);
    }

    private static sp_HSCodeReportRequest Request(string formType, bool groupByCompany) => new()
    {
        FormType = formType,
        FromDate = new DateTime(2026, 8, 31),
        ToDate = new DateTime(2026, 9, 1, 23, 59, 59),
        FilterType = "Start",
        HSCode = string.Empty,
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

    private static int GroupByColumnCount(string groupBy)
        => groupBy["GROUP BY".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;

    private static string GroupByClause(string sql)
    {
        var start = sql.IndexOf("GROUP BY", StringComparison.OrdinalIgnoreCase);
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
