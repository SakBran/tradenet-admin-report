using System.Text.RegularExpressions;

namespace Backend.Tests;

/// <summary>
/// The Export Permit listing reports must pick "the item" the same way the legacy Tradenet 2.0
/// procedures do, or Currency / HS Code / Total Value differ from the old report.
///
/// The legacy procedures use a bare <c>TOP 1</c> with no <c>ORDER BY</c>, so they return whatever
/// the plan's index order yields — the IX_ExportPermitItem_ReportCover seek order
/// (ExportPermitId, HSCodeId, ItemNo). Measured against them over 2025, ordering by
/// (HSCodeId, ItemNo) reproduces the legacy Amount on 17/17 Export Permit cancellations;
/// ORDER BY ItemNo scores 16/17 and ORDER BY Id 14/17 — Id is a char(36) GUID string with no
/// relation to item order, and shipping it produced the customer-reported 5,769.2300 where the
/// old report shows 27,230.7600.
///
/// These assertions are deliberately textual: the procedures are applied by hand, so the
/// repository file is the only thing CI can check.
/// </summary>
public sealed class ExportPermitItemOrderContractTests
{
    private const string ItemKey = "ORDER BY ExportPermitItem.HSCodeId, ExportPermitItem.ItemNo";

    private static string MigrationsRoot =>
        Path.Combine(RepoRoot(), "StoredProcedureMigrations");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "StoredProcedureMigrations")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    /// <summary>The <c>@FormType = N'Export Permit'</c> branch of a listing procedure.</summary>
    private static string ExportPermitBranch(string fileName)
    {
        var text = File.ReadAllText(Path.Combine(MigrationsRoot, fileName));
        var start = text.IndexOf("@FormType = N'Export Permit'", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{fileName} has no Export Permit branch.");

        // Ends where the next branch begins.
        var rest = text[(start + 10)..];
        var next = Regex.Match(rest, @"\n    ELSE(?: IF @FormType)?\b");
        return next.Success ? rest[..next.Index] : rest;
    }

    public static TheoryData<string, int> GridProcedures() => new()
    {
        // file, number of TOP 1 sub-selects over ExportPermitItem that must carry the key
        { "sp_CancelReport_pagination.sql", 3 },       // Currency + HSCode + Amount
        { "sp_AmendReport_pagination.sql", 3 },
        { "sp_ActualAmendReport_pagination.sql", 3 },
        { "sp_NewReport_pagination.sql", 2 },          // Amount is SUM
        { "sp_ExtensionReport_pagination.sql", 1 },    // Amount is SUM, no HS Code column
    };

    [Theory]
    [MemberData(nameof(GridProcedures))]
    public void Export_permit_grid_procedures_pick_the_item_by_hscode_then_itemno(string fileName, int expected)
    {
        var branch = ExportPermitBranch(fileName);

        Assert.Equal(expected, Regex.Matches(branch, Regex.Escape(ItemKey)).Count);
        Assert.DoesNotContain("ORDER BY ExportPermitItem.Id", branch);
    }

    [Fact]
    public void Every_unordered_top1_over_ExportPermitItem_is_gone_from_the_grid_procedures()
    {
        foreach (var row in GridProcedures())
        {
            var fileName = (string)row[0];
            var branch = ExportPermitBranch(fileName);

            // A `TOP 1` whose WHERE ends at the correlation with no ORDER BY is non-deterministic —
            // unless it wraps an aggregate, where TOP 1 is a no-op over the single result row
            // (`SELECT top 1 ISNULL(SUM(Amount),0)`, which is how New and Extension report value).
            var unordered = Regex.Matches(
                branch,
                @"SELECT top 1(?:(?!ORDER BY)(?!SUM\().)*?WHERE ExportPermitItem\.ExportPermitId=pg\.__k_Id\)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            Assert.True(
                unordered.Count == 0,
                $"{fileName}: {unordered.Count} unordered TOP 1 over ExportPermitItem — "
                + "Currency / HS Code / Amount can each come from a different item.");
        }
    }

    /// <summary>
    /// The footer must be the sum of exactly the rows the grid shows, so it has to pick the item
    /// with the identical expression. This is the pair that produced USD:10,038.1050 against the
    /// old report's USD:33,835.1200.
    /// </summary>
    [Theory]
    // Amend/ActualAmend and Cancel each key Currency + Amount; the New/Extension branch keys
    // Currency only, because its Amount is a SUM over every item.
    [InlineData("sp_ExportPermitListingCurrencyTotals.sql", 5)]
    [InlineData("sp_ExtensionReportCurrencyTotals.sql", 1)]     // Currency only; Amount is SUM
    public void Export_permit_footer_procedures_use_the_same_item_key(string fileName, int expected)
    {
        var text = File.ReadAllText(Path.Combine(MigrationsRoot, fileName));

        Assert.Equal(expected, Regex.Matches(text, Regex.Escape(ItemKey)).Count);
        Assert.DoesNotContain("ORDER BY ExportPermitItem.Id", text);
    }

    /// <summary>
    /// A scalar aggregate cannot be ordered by a column outside its select list — SQL Server
    /// rejects it at execution. Guards the sweep that added the key.
    /// </summary>
    [Fact]
    public void No_aggregate_subselect_carries_an_order_by()
    {
        foreach (var file in Directory.GetFiles(MigrationsRoot, "sp_*.sql"))
        {
            foreach (Match sub in Regex.Matches(
                File.ReadAllText(file), @"\(SELECT[^()]*(?:\([^()]*\)[^()]*)*?\)", RegexOptions.Singleline))
            {
                if (sub.Value.Contains("SUM(", StringComparison.Ordinal))
                {
                    Assert.DoesNotContain("ORDER BY", sub.Value);
                }
            }
        }
    }
}
