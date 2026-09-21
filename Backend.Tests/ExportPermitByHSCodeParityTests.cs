using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests;

/// <summary>
/// Pins the row grain of Export Permit By HS Code to the grain HSCodeReport.rdlc prints.
///
/// Complaint 2026-09-21 ("HS Code တစ်ခုမှာ Description / USD တန်ဖိုးတူတယ်ဆိုရင် ပေါင်းဖော်ပြပေးပါရန်"):
/// for 01-03/01/2025 the report showed 8807300000 in USD on three rows (1 + 4 + 1 licences,
/// 500 + 3,450 + 9,000) where the old report prints one row of 6 / 12,950.0000; across 2025 that
/// is 605 rows against the old report's 353. Cause: the buyer company was in the grouping key, so
/// one HS code printed once per buyer -- invisibly, because the grid has no company column.
/// HSCodeReport.rdlc:1150-1160 groups on <c>=Fields!HSCodeId.Value</c> +
/// <c>=Fields!Currency.Value</c> and nothing else. "Total No of License" already matched (1,147
/// for 2025): it is a separate whole-set COUNT(DISTINCT LicenceNo), rdlc:978.
///
/// The HS Code DETAIL drill shares this controller and DOES render Company Name
/// (HSCodeDetailReport.rdlc:1262-1265, keyed on HSCodeId + CompanyRegistrationNo), so
/// ExportPermitHSCodeDetailReport asks for that shape explicitly with <c>GroupBy=Company</c>.
/// Both halves are asserted here: a regression in either direction is a customer-visible row count.
/// </summary>
public sealed class ExportPermitByHSCodeParityTests
{
    [Fact]
    public void Summary_does_not_ask_for_company_grouping()
    {
        var request = Assert.IsType<sp_HSCodeReportRequest>(
            ReportTestHelper.CreateProcedureRequest(typeof(ExportPermitByHSCodeReportController)));

        Assert.Equal("Export Permit", request.FormType);
        Assert.False(request.GroupByCompany);
        // Keeps the paged procedure: HS-code-string order, not the legacy HSCodeId order that
        // forces the LINQ twin (only the Border screens run that).
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
        // EF renders the currency as its joined column ([c].[Code]), never as the word
        // "Currency", so the key is pinned by its column COUNT plus the absences below.
        Assert.Equal(4, GroupByColumnCount(groupBy));
        Assert.Contains("[HSCodeId]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyRegistrationNo]", groupBy, StringComparison.Ordinal);
        Assert.DoesNotContain("[CompanyName]", groupBy, StringComparison.Ordinal);
        // The oversea tables, not the Border twin's.
        Assert.Contains("[ExportPermit]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Drill_still_groups_on_hs_code_and_company_registration_number()
    {
        using var db = ReportTestHelper.CreateSqlServerDbContext();

        var groupBy = GroupByClause(
            sp_HSCodeReport.AggregateQuery(db, Request(groupByCompany: true)).ToQueryString());

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
        var method = ReportTestHelper.GetTryCreateReportRequest(typeof(ExportPermitByHSCodeReportController));
        var controller = ReportTestHelper.CreateController(
            typeof(ExportPermitByHSCodeReportController),
            ReportTestHelper.CreateInMemoryDbContext(nameof(ExportPermitByHSCodeParityTests) + "_GroupBy"));

        ExportPermitByHSCodeReportRequest Build(string groupBy) => new()
        {
            FromDate = new DateTime(2025, 1, 1),
            ToDate = new DateTime(2025, 1, 3, 23, 59, 59),
            FilterType = "Start",
            HSCode = "8807300000",
            GroupBy = groupBy,
        };

        Assert.True(Invoke(method, controller, Build("Company")).GroupByCompany);
        Assert.False(Invoke(method, controller, Build(string.Empty)).GroupByCompany);
        // The drill posts the constant verbatim; accept it case-insensitively so a config typo in
        // casing cannot silently fall back to the summary shape.
        Assert.True(Invoke(method, controller, Build("company")).GroupByCompany);
    }

    [Fact]
    public void The_export_section_filter_reaches_the_query()
    {
        // The old ExportPermitByHSCodeReport.cshtml:40-48 has an Export Section dropdown that the
        // new config was missing. A non-zero value also takes the report off the procedure
        // (sp_HSCodeReport_pagination has no @SectionId), so the LINQ twin is the only path that
        // can honour it -- losing the mapping would silently widen the report.
        var method = ReportTestHelper.GetTryCreateReportRequest(typeof(ExportPermitByHSCodeReportController));
        var controller = ReportTestHelper.CreateController(
            typeof(ExportPermitByHSCodeReportController),
            ReportTestHelper.CreateInMemoryDbContext(nameof(ExportPermitByHSCodeParityTests) + "_Section"));

        var request = Invoke(method, controller, new ExportPermitByHSCodeReportRequest
        {
            FromDate = new DateTime(2025, 1, 1),
            ToDate = new DateTime(2025, 1, 3, 23, 59, 59),
            FilterType = "Start",
            ExportImportSectionId = 7,
        });

        Assert.Equal(7, request.ExportImportSectionId);

        using var db = ReportTestHelper.CreateSqlServerDbContext();
        Assert.Contains(
            "[ExportImportSectionId]",
            sp_HSCodeReport.AggregateQuery(db, request).ToQueryString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Paged_procedure_groups_on_the_rdlc_key_in_every_sub_branch()
    {
        // The grid runs the procedure, not the LINQ twin above. Its branches are hand-written per
        // FormType, so a text assertion is the only way to keep them in step from here.
        var branch = ProcedureBranch("Export Permit");

        Assert.DoesNotContain("GROUP BY tmp.HSCode,tmp.HSDescription,tmp.CompanyRegistrationNo", branch);
        // Three sub-branches: @HSCode='' plus FilterType Start / End. No fast page here.
        var groupBys = CountOccurrences(branch, "GROUP BY tmp.HSCodeId,tmp.HSCode,tmp.HSDescription,tmp.Currency");
        Assert.Equal(3, groupBys);
        // The page window must be a unique key or OFFSET/FETCH can repeat one row across pages and
        // drop another: ORDER BY (HSCode, CompanyName, Currency) over a key whose CompanyName is
        // now a literal NULL would leave the page boundary to the optimiser.
        Assert.Equal(groupBys, CountOccurrences(branch, "ORDER BY result.HSCode,result.Currency,result.HSCodeId"));
        Assert.DoesNotContain("ORDER BY result.HSCode,result.CompanyName,result.Currency", branch);
        // The DTO shape is unchanged -- the two company columns still come back, as NULLs.
        Assert.Equal(
            groupBys,
            CountOccurrences(branch, "CAST(NULL AS nvarchar(200)) CompanyRegistrationNo,CAST(NULL AS nvarchar(500)) CompanyName"));
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

    private static sp_HSCodeReportRequest Request(bool groupByCompany) => new()
    {
        FormType = "Export Permit",
        FromDate = new DateTime(2025, 1, 1),
        ToDate = new DateTime(2025, 1, 3, 23, 59, 59),
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
