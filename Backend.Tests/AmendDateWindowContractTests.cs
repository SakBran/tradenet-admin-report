using System.Reflection;
using API.Service.ExcelExport;

namespace Backend.Tests;

/// <summary>
/// Locks the Amend / Actual Amendment listing reports to the old Tradenet 2.0 date window.
///
/// The old admin app called dbo.sp_AmendReport / dbo.sp_ActualAmendReport with
/// @ToDate = "&lt;day&gt; 23:59:59". Commit e88c13e switched the procedures to
/// "&lt; DATEADD(day, 1, @ToDate)", which — given that 23:59:59 argument — admitted the whole
/// NEXT day, so the new reports listed more rows than the old ones (customer complaint, 2026-09).
/// The window is now the selected calendar day on both sides: controllers pass
/// request.ToDate.Date and every branch they reach filters
/// "&lt; DATEADD(day, 1, CONVERT(date, @ToDate))".
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

        // The bare form (no CONVERT) is the bug: it adds a whole day to a 23:59:59 @ToDate.
        Assert.DoesNotContain("CreatedDate < DATEADD(day, 1, @ToDate)", sql);
        Assert.DoesNotContain("CreatedDate <= @ToDate)", sql);
        // 8 FormType branches, each with a COUNT part and a page part; the Border Licence
        // branches have two halves (Pa Tha Ka + Individual Trading) in both parts.
        // Table-qualified so the explanatory header comment is not counted.
        Assert.Equal(20, CountOccurrences(sql, ".CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))"));
    }

    [Theory]
    // Every branch an Amend / Actual Amendment controller reaches must use the calendar-date form,
    // because those controllers now send ToDate as a date: '<= @ToDate' would match only midnight.
    [InlineData("sp_ExportLicenceListingCurrencyTotals.sql", 3)]
    [InlineData("sp_ExportPermitListingCurrencyTotals.sql", 3)]
    [InlineData("sp_ImportLicenceListingCurrencyTotals.sql", 4)]
    [InlineData("sp_ImportPermitListingCurrencyTotals.sql", 3)]
    public void Footer_branches_for_amend_reports_use_the_calendar_date_window(string fileName, int expected)
        => Assert.Equal(expected, CountOccurrences(ReadMigration(fileName), "CONVERT(date, @ToDate)"));

    [Fact]
    public void Only_the_border_export_permit_new_footer_still_carries_the_bare_dateadd_form()
    {
        // Its grid (sp_NewReport_pagination, Border Export Permit branch) still uses the bare form;
        // the two must be flipped together, so the footer keeps mirroring its own grid until then.
        foreach (var fileName in new[]
                 {
                     "sp_ExportLicenceListingCurrencyTotals.sql",
                     "sp_ImportLicenceListingCurrencyTotals.sql",
                     "sp_ImportPermitListingCurrencyTotals.sql",
                 })
        {
            Assert.DoesNotContain("CreatedDate < DATEADD(day, 1, @ToDate)", ReadMigration(fileName));
        }

        Assert.Equal(1, CountOccurrences(
            ReadMigration("sp_ExportPermitListingCurrencyTotals.sql"), "CreatedDate < DATEADD(day, 1, @ToDate)"));
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

                Assert.Contains("ToDate = request.ToDate.Date,", source);
                Assert.DoesNotContain("ToDate = request.ToDate,", source);

                var type = typeof(API.DBContext.TradeNetDbContext).Assembly
                    .GetTypes().Single(t => t.Name == name);
                var version = type.GetCustomAttribute<ExcelFormatVersionAttribute>()?.Version ?? 1;
                Assert.True(version >= 2,
                    $"{name} changes which rows an export contains, so it must bump ExcelFormatVersion.");
            }
        }
    }

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
