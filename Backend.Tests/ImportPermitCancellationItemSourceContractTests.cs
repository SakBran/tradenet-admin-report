using System.Text.RegularExpressions;

namespace Backend.Tests;

/// <summary>
/// The Import Permit Cancellation report must read Currency / HS Code / Total Value through the
/// permit that actually holds the items.
///
/// An Import Permit cancellation record normally carries no ImportPermitItem rows of its own, so
/// keying the correlated <c>TOP 1</c> lookups on the cancellation's own Id returned NULL and the
/// grid printed "N/A" — 10 of the 20 production rows for 2023-2026, with the per-currency footer
/// collapsing the same permits into a blank-currency line totalling 0. Both procedures now fall
/// back to the permit being cancelled (<c>OldImportPermitNo</c> -> <c>ImportPermitNo</c>) and take
/// the item with <c>ORDER BY ImportPermitItem.Id</c>, which is also the key the footer branch
/// already used — so a row and its footer can no longer disagree.
///
/// Unlike its Export Permit sibling the ordering key here is Id: ImportPermitItem.Id is an int
/// identity, and the footer branch has shipped <c>ORDER BY ImportPermitItem.Id</c> since
/// 2026-09-04. (Export Permit had to use (HSCodeId, ItemNo) because its Id is a GUID string.)
///
/// These assertions are deliberately textual: the procedures are applied to the server by hand,
/// so the repository file is the only thing CI can check.
/// </summary>
public sealed class ImportPermitCancellationItemSourceContractTests
{
    private const string ItemOrder = "ORDER BY ImportPermitItem.Id";

    private static string MigrationsRoot => Path.Combine(RepoRoot(), "StoredProcedureMigrations");

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

    /// <summary>The <c>@FormType = N'Import Permit'</c> branch, up to where the next one starts.</summary>
    private static string ImportPermitBranch()
    {
        var text = File.ReadAllText(Path.Combine(MigrationsRoot, "sp_CancelReport_pagination.sql"));
        var start = text.IndexOf("@FormType = N'Import Permit'", StringComparison.Ordinal);
        Assert.True(start >= 0, "sp_CancelReport_pagination.sql has no Import Permit branch.");

        var rest = text[(start + 10)..];
        var next = Regex.Match(rest, @"\n    ELSE(?: IF @FormType)?\b");
        return WithoutComments(next.Success ? rest[..next.Index] : rest);
    }

    /// <summary>The <c>@DbApplyType = N'Cancel'</c> branch of the footer procedure.</summary>
    private static string FooterCancelBranch()
    {
        var text = File.ReadAllText(Path.Combine(MigrationsRoot, "sp_ImportPermitListingCurrencyTotals.sql"));
        var start = text.IndexOf("@DbApplyType = N'Cancel'", StringComparison.Ordinal);
        Assert.True(start >= 0, "sp_ImportPermitListingCurrencyTotals.sql has no Cancel branch.");

        var rest = text[(start + 10)..];
        var next = Regex.Match(rest, @"\n    ELSE(?: IF @DbApplyType)?\b");
        return WithoutComments(next.Success ? rest[..next.Index] : rest);
    }

    /// <summary>
    /// Drops <c>--</c> comments, so counting occurrences measures the SQL and not the prose
    /// explaining it — these branches are commented with the very keys being asserted.
    /// </summary>
    private static string WithoutComments(string sql)
        => string.Join('\n', sql
            .Split('\n')
            .Select(line =>
            {
                var comment = line.IndexOf("--", StringComparison.Ordinal);
                return comment >= 0 ? line[..comment] : line;
            }));

    [Fact]
    public void Grid_reads_the_items_through_the_resolved_item_permit()
    {
        var branch = ImportPermitBranch();

        // Every one of the three item lookups (Currency, HSCode, Amount) must go through the
        // resolved id...
        Assert.Equal(3, Occurrences(branch, "ImportPermitItem.ImportPermitId=pg.__k_ItemId"));

        // ...and none may still key on the cancellation record itself.
        Assert.Equal(0, Occurrences(branch, "ImportPermitItem.ImportPermitId=pg.__k_Id"));
    }

    [Fact]
    public void Grid_falls_back_to_the_permit_being_cancelled()
    {
        var branch = ImportPermitBranch();

        Assert.Contains("AS __k_ItemId", branch);
        // The fallback join: the cancelled permit is found by its number.
        Assert.Contains("parent.ImportPermitNo = ImportPermit.OldImportPermitNo", branch);
        // ...and only if it actually has items, so a parent with none cannot mask a
        // cancellation that has its own.
        Assert.Contains("EXISTS (SELECT 1 FROM ImportPermitItem pi WHERE pi.ImportPermitId = parent.Id)", branch);
    }

    [Fact]
    public void Grid_and_footer_pick_the_same_item()
    {
        // Three lookups in the grid, two in the footer (it has no HS Code column).
        Assert.Equal(3, Occurrences(ImportPermitBranch(), ItemOrder));
        Assert.Equal(2, Occurrences(FooterCancelBranch(), ItemOrder));
    }

    [Fact]
    public void Footer_reads_the_items_through_the_same_resolved_permit()
    {
        var branch = FooterCancelBranch();

        Assert.Equal(2, Occurrences(branch, "ImportPermitItem.ImportPermitId = src.ItemPermitId"));
        Assert.Equal(0, Occurrences(branch, "ImportPermitItem.ImportPermitId = ImportPermit.Id"));
        Assert.Contains("parent.ImportPermitNo = ImportPermit.OldImportPermitNo", branch);
    }

    [Fact]
    public void No_unordered_top_1_over_ImportPermitItem_survives_in_either_branch()
    {
        foreach (var branch in new[] { ImportPermitBranch(), FooterCancelBranch() })
        {
            var checked_ = 0;

            // Each "(SELECT TOP 1 <per-item value> ... FROM ImportPermitItem ...)" must reach an
            // ORDER BY before its own closing bracket, or which item it lands on is left to the
            // query plan. Only the lookups whose PROJECTION varies per item count: the two
            // sub-selects that resolve __k_ItemId project the id they filter on, so which row
            // they hit cannot change the answer.
            foreach (Match match in Regex.Matches(branch, @"\(\s*SELECT\s+top\s+1\b", RegexOptions.IgnoreCase))
            {
                var subSelect = BracketedAt(branch, match.Index);
                if (!Regex.IsMatch(
                        subSelect,
                        @"top\s+1\s+(currency\.Code|HSCode\.Code|ISNULL\(ImportPermitItem\.Amount)",
                        RegexOptions.IgnoreCase))
                {
                    continue;
                }

                Assert.Contains(ItemOrder, subSelect);
                checked_++;
            }

            // A boundary bug in the scan above would silently check nothing.
            Assert.True(checked_ >= 2, $"expected at least 2 item sub-selects, scanned {checked_}.");
        }
    }

    /// <summary>
    /// The text from the "(" at <paramref name="open"/> through its matching ")", so a nested
    /// call such as ISNULL(x,0) cannot be mistaken for the end of the sub-select.
    /// </summary>
    private static string BracketedAt(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')' && --depth == 0)
            {
                return text[open..(i + 1)];
            }
        }

        Assert.Fail("unbalanced parentheses in the procedure branch.");
        return string.Empty;
    }

    /// <summary>Occurrences of <paramref name="needle"/>, ignoring how the file wraps lines.</summary>
    private static int Occurrences(string haystack, string needle)
    {
        var flat = Regex.Replace(haystack, @"\s+", " ");
        var flatNeedle = Regex.Replace(needle, @"\s+", " ");

        var count = 0;
        for (var i = flat.IndexOf(flatNeedle, StringComparison.Ordinal);
             i >= 0;
             i = flat.IndexOf(flatNeedle, i + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
