using System.Text.RegularExpressions;
using API.Model;
using API.Service.Reports;

namespace Backend.Tests;

/// <summary>
/// Pins Border Import Licence By Method to the OLD report's grain (customer complaint
/// 2026-09-18: "Method တူနေတာကိုမှ currency တူတာတွေ ပေါင်းဖော်ပြရမှာကို ခွဲပြီး ပြနေတယ်" — the same
/// Method with the same Currency was split across several rows).
///
/// The legacy screen hands raw detail rows to BorderImportLicenceByMethodReport.rdlc, which
/// groups on exactly two keys (rdlc:1078-1079):
///     =Fields!MethodName.Value
///     =Fields!Currency.Value
/// Sakhan is a *filter* on the old search form, never a group key, and the tablix has no
/// Sakhan column (rdlc:281-556 = Sr.No., Method, No of Licences, Total Value, Currency).
/// Passing includeSakhan: true split one (Method, Currency) row into one row per border
/// office, with nothing in the grid to tell the copies apart.
///
/// The TOTAL row is count-only: rdlc:905 prints =CountDistinct(Fields!LicenceNo.Value) under
/// "No of Licences" while the Total Value and Currency cells (Textbox7/Textbox8) stay empty.
/// </summary>
public sealed class BorderImportLicenceByMethodLegacyParityTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Grid_and_excel_group_on_method_and_currency_only_with_a_count_only_footer()
    {
        var controller = File.ReadAllText(Path.Combine(
            RepositoryRoot, "Backend", "Controllers", "Report", "BorderImportLicenceByMethodReportController.cs"));

        // All three surfaces -- grid (CreateAggregateResultAsync), Excel rows
        // (GetAggregateRowsAsync) and the Excel ordering (OrderGroups) -- must drop the
        // Sakhan key together, or the sheet stops matching the grid.
        Assert.Equal(
            3,
            Regex.Matches(controller, @"ReportAggregateDimension\.Method, includeSakhan: false").Count);
        Assert.DoesNotContain("includeSakhan: true", controller);

        // rdlc:905 + empty Textbox7/Textbox8: the footer keeps the licence count and leaves
        // Total Value blank (each row is one currency, so summing them is meaningless).
        Assert.Contains("columnTotalsMode: ReportColumnTotalsMode.CountOnly", controller);

        // Without the bump the export queue keeps serving the cached pre-fix .xlsx.
        Assert.Contains("[ExcelFormatVersion(2)]", controller);
        // IExcelNoFooterReport would hide the surviving count from the sheet.
        Assert.DoesNotContain("IExcelNoFooterReport", controller);
    }

    [Fact]
    public void Ui_matches_BorderImportLicenceByMethodReport_rdlc()
    {
        var config = ExtractReportConfig("BorderImportLicenceByMethodReport");

        // rdlc:281-556 header cells, in order (Sr.No. is the row-number column) -- notably
        // there is no Sakhan column, which is why the Sakhan split was invisible.
        Assert.Equal(["Method", "No of Licences", "Total Value", "Currency"], ExtractColumnTitles(config));
        Assert.DoesNotContain("sakhanCode", config);
        Assert.Contains("showRowNumber: true", config);
    }

    /// <summary>
    /// The complaint, replayed with the exact rows PROD returned for 01/09/2026-15/09/2026
    /// (measured 2026-09-18 against reportapi.myanmartradenet.com, the pre-fix build). Every
    /// duplicate was a different border office -- KTH, MWD, MYI, TCL, YGN -- and the method
    /// name maps 1:1 onto its id (CMP=15, Normal LC OR TT=13, Normal TT=12), so the Sakhan
    /// key was the only splitter. Twelve rows must collapse to five.
    /// </summary>
    [Fact]
    public void The_complaint_window_collapses_from_twelve_rows_to_five()
    {
        // (methodName, methodId, currency, sakhanCode, noOfLicences, totalValue) as served.
        (string Method, int MethodId, string Currency, string Sakhan, int Count, decimal Value)[] served =
        [
            ("CMP",             15, "USD", "MWD", 103,  13147976.6689m),
            ("Normal LC OR TT", 13, "USD", "KTH",   1,    242355.0000m),
            ("Normal TT",       12, "THB", "KTH",  11,  18037486.1208m),
            ("Normal TT",       12, "THB", "MWD",  48,  71325558.4820m),
            ("Normal TT",       12, "USD", "MWD",   4,    121200.0000m),
            ("Normal TT",       12, "THB", "MYI",  11,  18087756.1500m),
            ("Normal TT",       12, "USD", "MYI",   6,   2376117.0000m),
            ("Normal TT",       12, "CNY", "TCL",   5,   1748768.6000m),
            ("Normal TT",       12, "THB", "TCL",   4,   6217323.2400m),
            ("Normal TT",       12, "USD", "TCL",   2,     99984.8900m),
            ("Normal TT",       12, "THB", "YGN",  94, 149570302.6233m),
            ("Normal TT",       12, "USD", "YGN",  12,   1092773.6400m),
        ];

        // One detail line per licence, so CountDistinct(LicenceNo) reproduces the counts.
        var rows = served.SelectMany(group => Enumerable.Range(0, group.Count).Select(index =>
            new AggregateSourceRow
            {
                MethodName = group.Method,
                MethodId = group.MethodId,
                Currency = group.Currency,
                SakhanCode = group.Sakhan,
                LicenceNo = $"{group.Sakhan}-{group.Currency}-{index}",
                Amount = index == 0 ? group.Value : 0m,
            })).ToList();

        var grouped = ReportAggregationService.Aggregate(
            rows, ReportAggregateDimension.Method, includeSakhan: false);

        var result = ReportAggregationService.CreatePagedResultFromGroups(
            grouped,
            ReportAggregateDimension.Method,
            includeSakhan: false,
            new ReportQueryRequest { PageIndex = 0, PageSize = 50, IncludeTotalCount = true },
            includeColumnTotals: true,
            ReportColumnTotalsMode.CountOnly);

        // Order(): MethodName, then Currency -- no Sakhan tie-break any more.
        Assert.Equal(
            [
                ("CMP", "USD", 103, 13147976.6689m),
                ("Normal LC OR TT", "USD", 1, 242355.0000m),
                ("Normal TT", "CNY", 5, 1748768.6000m),
                ("Normal TT", "THB", 168, 263238426.6161m),
                ("Normal TT", "USD", 24, 3690075.5300m),
            ],
            result.Data.Select(row => (row.MethodName!, row.Currency!, row.NoOfLicences, row.TotalValue!.Value)));

        // Every row keeps the id the drill-through needs, and the Sakhan is gone from the payload.
        Assert.Equal([15, 13, 12, 12, 12], result.Data.Select(row => row.MethodId));
        Assert.All(result.Data, row => Assert.Null(row.SakhanCode));

        // rdlc:905 TOTAL row: the distinct licence count survives, Total Value stays blank.
        Assert.Equal(301m, result.ColumnTotals!["noOfLicences"]);
        Assert.False(result.ColumnTotals.ContainsKey("totalValue"));
    }

    // --- reportConfigs.ts source extraction -------------------------------------------------

    private static string ExtractReportConfig(string key)
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot, "Frontend", "src", "Report", "config", "reportConfigs.ts"));
        var start = source.IndexOf($"\n  {key}: {{", StringComparison.Ordinal);
        Assert.True(start >= 0, $"reportConfigs.ts has no '{key}' entry.");
        var end = source.IndexOf("\n  },\n", start, StringComparison.Ordinal);
        Assert.True(end > start);
        return source.Substring(start, end - start);
    }

    private static string[] ExtractColumnTitles(string config)
    {
        var columns = config[config.IndexOf("columns: [", StringComparison.Ordinal)..];
        return Regex.Matches(columns, @"title: '([^']*)'").Select(match => match.Groups[1].Value).ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
            && !(Directory.Exists(Path.Combine(directory.FullName, "Backend"))
                && Directory.Exists(Path.Combine(directory.FullName, "Frontend"))
                && Directory.Exists(Path.Combine(directory.FullName, "StoredProcedureMigrations"))))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
