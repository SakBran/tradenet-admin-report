using System.Reflection;
using API.Service.ExcelExport;
using API.StoredProcedureToLinq;

namespace Backend.Tests;

/// <summary>
/// Locks the Amend / Actual Amendment listing reports to the old Tradenet 2.0 date window.
///
/// The old admin app called dbo.sp_AmendReport / dbo.sp_ActualAmendReport with
/// @ToDate = "&lt;day&gt; 23:59:59" against procedures filtering CreatedDate &lt;= @ToDate.
/// Commit e88c13e replaced that with "&lt; DATEADD(day, 1, @ToDate)", which — given the same
/// 23:59:59 argument — admitted the whole NEXT day, so the new reports listed more rows than
/// the old ones (customer complaint, 2026-09).
/// </summary>
public sealed class AmendDateWindowContractTests
{
    private static readonly string[] AmendControllers =
    {
        "ExportLicence", "ImportLicence", "ExportPermit", "ImportPermit",
        "BorderExportLicence", "BorderImportLicence", "BorderExportPermit", "BorderImportPermit",
    };

    [Theory]
    [InlineData("sp_ActualAmendReport_pagination.sql")]
    [InlineData("sp_AmendReport_pagination.sql")]
    public void Listing_grid_procedures_use_the_inclusive_to_date_window(string fileName)
    {
        var sql = ReadMigration(fileName);

        Assert.DoesNotContain("CreatedDate < DATEADD", sql);
        // 8 FormType branches, each with a COUNT part and a page part; the Border Licence
        // branches have two halves (Pa Tha Ka + Individual Trading) in both parts.
        Assert.Equal(20, CountOccurrences(sql, "CreatedDate <= @ToDate)"));
    }

    [Fact]
    public void Footer_procedures_match_the_corrected_grid_window()
    {
        Assert.DoesNotContain("CreatedDate < DATEADD", ReadMigration("sp_ImportLicenceListingCurrencyTotals.sql"));
        Assert.DoesNotContain("CreatedDate < DATEADD", ReadMigration("sp_ImportPermitListingCurrencyTotals.sql"));
        Assert.DoesNotContain("CreatedDate < DATEADD", ReadMigration("sp_ExportLicenceListingCurrencyTotals.sql"));

        // The Border Export Permit NEW sub-branch intentionally still uses DATEADD until its grid
        // (sp_NewReport_pagination) is fixed in the same pass; the other two sub-branches are fixed.
        Assert.Equal(1, CountOccurrences(ReadMigration("sp_ExportPermitListingCurrencyTotals.sql"), "CreatedDate < DATEADD"));
    }

    [Fact]
    public void Footer_procedures_accept_both_actual_amend_spellings()
    {
        // Callers pass "ActualAmend"; the database stores 'Actual Amend' (with a space).
        foreach (var fileName in new[]
                 {
                     "sp_ExportPermitListingCurrencyTotals.sql",
                     "sp_ImportLicenceListingCurrencyTotals.sql",
                     "sp_ImportPermitListingCurrencyTotals.sql",
                 })
        {
            var sql = ReadMigration(fileName);
            Assert.Contains("@DbApplyType", sql);
            Assert.Contains("CASE WHEN @ApplyType = N'ActualAmend' THEN N'Actual Amend' ELSE @ApplyType END", sql);
            Assert.DoesNotContain("ApplyType = @ApplyType ", sql);
        }
    }

    [Fact]
    public void Border_import_footers_read_their_own_tables()
    {
        var importLicence = ReadMigration("sp_ImportLicenceListingCurrencyTotals.sql");
        Assert.Contains("IF @FormType = N'Border Import Licence'", importLicence);
        Assert.Contains("BorderImportLicence.CardType = 'Pa Tha Ka'", importLicence);
        Assert.Contains("BorderImportLicence.CardType = 'Individual Trading'", importLicence);

        var importPermit = ReadMigration("sp_ImportPermitListingCurrencyTotals.sql");
        Assert.Contains("IF @FormType = N'Border Import Permit'", importPermit);
        // The Actual Amendment grid shows the FIRST item's amount, not the sum of all items.
        Assert.Contains("SELECT TOP 1 ISNULL(ImportPermitItem.Amount, 0)", importPermit);
    }

    [Fact]
    public void Every_amend_controller_normalises_the_to_date_and_bumps_its_export_version()
    {
        foreach (var family in AmendControllers)
        {
            foreach (var kind in new[] { "ActualAmendment", "Amendment" })
            {
                var name = $"{family}{kind}ReportController";
                var source = File.ReadAllText(Path.Combine(
                    RepositoryRoot, "Backend", "Controllers", "Report", name + ".cs"));

                Assert.Contains("ToDate = ReportDateWindow.InclusiveEndOfDay(request.ToDate)", source);
                Assert.DoesNotContain("ToDate = request.ToDate,", source);

                var type = typeof(API.DBContext.TradeNetDbContext).Assembly
                    .GetTypes().Single(t => t.Name == name);
                var version = type.GetCustomAttribute<ExcelFormatVersionAttribute>()?.Version ?? 1;
                Assert.True(version >= 2,
                    $"{name} changes which rows an export contains, so it must bump ExcelFormatVersion.");
            }
        }
    }

    [Theory]
    // A date-only ToDate becomes the whole day, exactly as the old app sent it.
    [InlineData("2026-09-01T00:00:00", "2026-09-01T23:59:59")]
    // Anything already carrying a time (what the UI sends) is left alone.
    [InlineData("2026-09-01T23:59:59", "2026-09-01T23:59:59")]
    [InlineData("2026-09-01T12:30:00", "2026-09-01T12:30:00")]
    public void Inclusive_end_of_day_matches_the_old_admin_apps_to_date(string input, string expected)
        => Assert.Equal(DateTime.Parse(expected), ReportDateWindow.InclusiveEndOfDay(DateTime.Parse(input)));

    [Fact]
    public void Inclusive_end_of_day_leaves_the_maximum_date_alone()
        => Assert.Equal(DateTime.MaxValue, ReportDateWindow.InclusiveEndOfDay(DateTime.MaxValue));

    private static string ReadMigration(string fileName)
        => File.ReadAllText(Path.Combine(RepositoryRoot, "StoredProcedureMigrations", fileName));

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
