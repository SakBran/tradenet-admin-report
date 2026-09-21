using System.Globalization;
using System.Text.RegularExpressions;
using API.Model;
using API.Service.Reports;

namespace Backend.Tests;

/// <summary>
/// Pins the three Border "Daily" reports to the OLD reports' grain (customer complaint
/// 2026-09-21 against Border Import Licence Daily: "တစ်ရက်ချင်းစီကို Currency စီတူရင်
/// ပေါင်းဖော်ပြပေးပါရန် (အဟောင်းမှာက ရက်စွဲတူ/currency တူရင် ပေါင်းဖော်ပြပါတယ်)" — 2026-01-01 / THB
/// printed three times, 5 + 3 + 45, instead of once as 53).
///
/// Each legacy screen hands raw detail rows to its RDLC, which groups on exactly two keys:
///     BorderImportLicenceByDailyReport.rdlc:1269-1272  =Fields!sLicenceDate.Value, =Fields!Currency.Value
///     BorderImportPermitByDailyReport.rdlc:1269-1270   same
///     BorderExportPermitByDailyReport.rdlc:1277-1278   same
/// Sakhan is a *filter* on the old search forms, never a group key, and none of the three
/// tablixes has a Sakhan column. Passing includeSakhan: true split one (Date, Currency) row
/// into one row per border office, with nothing in the grid to tell the copies apart.
///
/// The TOTAL row is count-plus-USD: rdlc:1039 prints =CountDistinct(Fields!LicenceNo.Value)
/// under "No of Licences", the Total Value cell (Textbox7) stays empty, and the last cell
/// prints =FORMAT(Sum(Fields!totalUSDAmount.Value), "N4") (rdlc:1200) — which is exactly what
/// ReportColumnTotalsMode.CountOnly produces for the Daily dimension.
/// </summary>
public sealed class BorderDailyReportSakhanSplitTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Theory]
    // (controller, config key, expected Excel format version)
    [InlineData("BorderImportLicenceDailyReportNewLicenceReportController", "BorderImportLicenceDailyReportNewLicenceReport", 2)]
    [InlineData("BorderExportPermitDailyReportNewPermitReportController", "BorderExportPermitDailyReportNewPermitReport", 2)]
    // Already on v2 from the 2026-09-10 count-only footer round; the row shape changes here,
    // so it has to go to v3 or the queue serves the cached per-Sakhan sheet.
    [InlineData("BorderImportPermitDailyReportNewPermitReportController", "BorderImportPermitDailyReportNewPermitReport", 3)]
    public void Grid_and_excel_group_on_date_and_currency_only_with_a_count_only_footer(
        string controllerName,
        string configKey,
        int excelFormatVersion)
    {
        var controller = File.ReadAllText(Path.Combine(
            RepositoryRoot, "Backend", "Controllers", "Report", $"{controllerName}.cs"));

        // All three surfaces -- grid (CreateAggregateResultAsync), Excel rows
        // (GetAggregateRowsAsync) and the Excel ordering (OrderGroups) -- must drop the
        // Sakhan key together, or the sheet stops matching the grid.
        Assert.Equal(
            3,
            Regex.Matches(controller, @"ReportAggregateDimension\.Daily, includeSakhan: false").Count);
        Assert.DoesNotContain("includeSakhan: true", controller);

        // rdlc:1039 + empty Textbox7: the footer keeps the licence count and leaves Total Value
        // blank (each row is one currency, so summing them is meaningless). BuildColumnTotals
        // still emits totalUSDValue for Daily, which rdlc:1200 does print.
        Assert.Contains("columnTotalsMode: ReportColumnTotalsMode.CountOnly", controller);

        // Without the bump the export queue keeps serving the cached pre-fix .xlsx.
        Assert.Contains($"[ExcelFormatVersion({excelFormatVersion})]", controller);
        // IExcelNoFooterReport would hide the surviving count and USD total from the sheet.
        Assert.DoesNotContain("IExcelNoFooterReport", controller);

        // The grid has no Sakhan column -- that is what made the split invisible.
        var config = ExtractReportConfig(configKey);
        Assert.Equal(
            ["Date", "No of Licences", "Total Value", "Currency", "Total USD Value"],
            ExtractColumnTitles(config));
        Assert.DoesNotContain("sakhanCode", config);
    }

    /// <summary>
    /// Border Export Licence Daily is the ONE Border report whose legacy RDLC really does
    /// group by Sakhan (BorderExportLicenceByDailyReport.rdlc:1445-1447 = sLicenceDate,
    /// Currency, SakhanId) and whose grid renders a Sakhan column. The flag was copied FROM it
    /// onto its twins, so this guards against "fixing" the original too.
    /// </summary>
    [Fact]
    public void Border_export_licence_daily_keeps_its_sakhan_grain()
    {
        var controller = File.ReadAllText(Path.Combine(
            RepositoryRoot, "Backend", "Controllers", "Report",
            "BorderExportLicenceDailyReportNewLicenceReportController.cs"));

        Assert.Equal(
            3,
            Regex.Matches(controller, @"ReportAggregateDimension\.Daily, includeSakhan: true").Count);

        var config = ExtractReportConfig("BorderExportLicenceDailyReportNewLicenceReport");
        Assert.Contains("sakhanCode", config);
        Assert.Contains("Sakhan", ExtractColumnTitles(config));
    }

    /// <summary>
    /// The complaint, replayed with the rows from the customer's own screenshot
    /// (01/01/2026-06/01/2026, first two dates). The per-row values are the served ones; the
    /// Sakhan codes are stand-ins, because the grid never printed them -- which is the whole
    /// point: only the invisible key differed. Seven rows must collapse to four.
    /// </summary>
    [Fact]
    public void The_complaint_window_collapses_duplicate_date_currency_rows()
    {
        // (date, currency, sakhanCode, noOfLicences, totalValue) as served pre-fix.
        (string Date, string Currency, string Sakhan, int Count, decimal Value)[] served =
        [
            ("2026-01-01", "THB", "MWD",  5,  7875576.5160m),
            ("2026-01-01", "THB", "MYI",  3,  4250873.2000m),
            ("2026-01-01", "THB", "TCL", 45, 45274830.7402m),
            ("2026-01-01", "USD", "MWD",  1,    47293.5080m),
            ("2026-01-02", "THB", "MWD",  3,  3897496.0000m),
            ("2026-01-02", "THB", "MYI",  3,  4808461.1600m),
            ("2026-01-02", "THB", "TCL", 23, 32428011.3525m),
            ("2026-01-02", "CNY", "KTH",  2,   549526.4600m),
        ];

        // One detail line per licence, so CountDistinct(LicenceNo) reproduces the counts.
        var rows = served.SelectMany(group => Enumerable.Range(0, group.Count).Select(index =>
            new AggregateSourceRow
            {
                LicenceDate = DateTime.ParseExact(group.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Currency = group.Currency,
                SakhanCode = group.Sakhan,
                LicenceNo = $"{group.Date}-{group.Sakhan}-{group.Currency}-{index}",
                Amount = index == 0 ? group.Value : 0m,
            })).ToList();

        var grouped = ReportAggregationService.Aggregate(
            rows, ReportAggregateDimension.Daily, includeSakhan: false);

        // Stand in for ReportUsdConversionService.FillDailyUsdValuesAsync, which runs after
        // grouping and needs the ExchangeRate table. The factor is per (date, currency), so it
        // applies to the merged total exactly as it did to each per-Sakhan slice.
        var usdFactor = new Dictionary<string, decimal> { ["THB"] = 0.03169m, ["CNY"] = 0.14054m, ["USD"] = 1m };
        foreach (var group in grouped)
        {
            group.TotalUSDValue = decimal.Round(group.TotalValue!.Value * usdFactor[group.Currency!], 4);
        }

        var result = ReportAggregationService.CreatePagedResultFromGroups(
            grouped,
            ReportAggregateDimension.Daily,
            includeSakhan: false,
            new ReportQueryRequest { PageIndex = 0, PageSize = 1000, IncludeTotalCount = true },
            includeColumnTotals: true,
            ReportColumnTotalsMode.CountOnly);

        // Order(): Date, then Currency -- no Sakhan tie-break any more. The three THB rows on
        // 2026-01-01 are one row of 53 licences, as the old report printed them.
        Assert.Equal(
            [
                ("2026-01-01", "THB", 53, 57401280.4562m),
                ("2026-01-01", "USD",  1,    47293.5080m),
                ("2026-01-02", "CNY",  2,   549526.4600m),
                ("2026-01-02", "THB", 29, 41133968.5125m),
            ],
            result.Data.Select(row => (row.Date!, row.Currency!, row.NoOfLicences, row.TotalValue!.Value)));

        // The Sakhan is gone from the payload entirely.
        Assert.All(result.Data, row => Assert.Null(row.SakhanCode));

        // A licence belongs to exactly one Sakhan, so the per-group distinct counts never
        // overlapped: the footer count is unchanged by the merge.
        Assert.Equal(85m, result.ColumnTotals!["noOfLicences"]);
        Assert.Equal(served.Sum(group => group.Count), result.ColumnTotals["noOfLicences"]);

        // rdlc: Textbox7 blank, Textbox6 = FORMAT(Sum(totalUSDAmount), "N4").
        Assert.False(result.ColumnTotals.ContainsKey("totalValue"));
        Assert.Equal(
            decimal.Round(grouped.Sum(group => group.TotalUSDValue!.Value), 4),
            result.ColumnTotals["totalUSDValue"]);
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
