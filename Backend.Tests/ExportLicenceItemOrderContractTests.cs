using System.Text.RegularExpressions;

namespace Backend.Tests;

/// <summary>
/// The Export Licence Cancellation report must pick "the item" the same way the legacy
/// Tradenet 2.0 <c>dbo.sp_CancelReport</c> does, and its per-currency footer must pick it the
/// same way the grid does — otherwise the Total is not the sum of the rows on screen.
///
/// The legacy procedure uses a bare <c>TOP 1</c> with no <c>ORDER BY</c>, so it returns whatever
/// the plan's index order yields, and the three sub-selects can each land on a different item of
/// the same licence. <c>ExportLicenceItem</c>'s clustered primary key is (Id, UniqueId), and that
/// is the key measured to reproduce the legacy procedure (Cancel 466/466 over Aug-2025).
///
/// The key is NOT transferable: <c>ExportPermitItem</c> reproduces its own legacy procedure on
/// (HSCodeId, ItemNo) and scores 14/17 on Id — see <see cref="ExportPermitItemOrderContractTests"/>.
/// It has to be measured per item table.
///
/// These assertions are deliberately textual: the procedures are applied by hand, so the
/// repository file is the only thing CI can check.
/// </summary>
public sealed class ExportLicenceItemOrderContractTests
{
    private const string ItemKey = "ORDER BY ExportLicenceItem.Id, ExportLicenceItem.UniqueId";

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

    /// <summary>The <c>@FormType = N'Export Licence'</c> branch, which is not the Border one.</summary>
    private static string ExportLicenceBranch(string fileName)
    {
        var text = File.ReadAllText(Path.Combine(MigrationsRoot, fileName));
        var start = text.IndexOf("@FormType = N'Export Licence'", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{fileName} has no Export Licence branch.");

        var rest = text[(start + 10)..];
        var next = Regex.Match(rest, @"\n    ELSE(?: IF @FormType)?\b");
        return next.Success ? rest[..next.Index] : rest;
    }

    [Fact]
    public void Cancel_grid_picks_currency_hscode_and_amount_from_the_same_item()
    {
        var branch = ExportLicenceBranch("sp_CancelReport_pagination.sql");

        // Currency + HSCode + Amount.
        Assert.Equal(3, Regex.Matches(branch, Regex.Escape(ItemKey)).Count);

        var unordered = Regex.Matches(
            branch,
            @"SELECT top 1(?:(?!ORDER BY)(?!SUM\().)*?WHERE ExportLicenceItem\.ExportLicenceId=pg\.__k_Id\)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        Assert.True(
            unordered.Count == 0,
            $"{unordered.Count} unordered TOP 1 over ExportLicenceItem in the Export Licence Cancel "
            + "branch — Currency / HS Code / Total Value can each come from a different item.");
    }

    [Fact]
    public void Cancel_footer_uses_the_same_item_key_as_the_grid()
    {
        var text = File.ReadAllText(
            Path.Combine(MigrationsRoot, "sp_ExportLicenceListingCurrencyTotals.sql"));

        var start = text.IndexOf("ELSE IF @ApplyType = N'Cancel'", StringComparison.Ordinal);
        Assert.True(start >= 0, "sp_ExportLicenceListingCurrencyTotals has no Cancel branch.");

        // The Border half branches on @FormType first, so the LAST Cancel branch is the
        // non-Border one this report runs.
        var last = text.LastIndexOf("ELSE IF @ApplyType = N'Cancel'", StringComparison.Ordinal);
        var rest = text[last..];
        var next = Regex.Match(rest, @"\n        ELSE\b");
        var branch = next.Success ? rest[..next.Index] : rest;

        // Currency + Amount. Both must carry the key the grid uses, or the footer stops
        // being the sum of the visible Total Value column.
        Assert.Equal(2, Regex.Matches(branch, Regex.Escape(ItemKey)).Count);
    }
}
