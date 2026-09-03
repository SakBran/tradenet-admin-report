using API.Model;
using API.Service.ExcelExport;

namespace Backend.Tests;

/// <summary>
/// The exported footer has to reconcile with the grid footer cell for cell — users
/// compare the two. Each fact pins one rule from BasicTable.tsx's tfoot.
/// </summary>
public sealed class ExcelFooterBuilderTests
{
    private static ExcelReportLayout Layout(
        bool showRowNumber = true,
        ExcelCurrencyTotalsColumns? currencyColumns = null)
    {
        var columns = new List<ExcelColumn>();
        if (showRowNumber)
        {
            columns.Add(ExcelColumn.RowNumber());
        }

        columns.Add(ExcelColumn.Untyped(
            "Currency", ExcelCellFormat.Text, (_, _) => null, key: "Currency", dataIndex: "currency"));
        columns.Add(ExcelColumn.Untyped(
            "Company Name", ExcelCellFormat.Text, (_, _) => null, key: "CompanyName", dataIndex: "companyName"));
        columns.Add(ExcelColumn.Untyped(
            "No of Licences", ExcelCellFormat.Number, (_, _) => null, isNumeric: true,
            key: "NoOfLicences", dataIndex: "noOfLicences"));
        columns.Add(ExcelColumn.Untyped(
            "Total Value", ExcelCellFormat.Money, (_, _) => null, isNumeric: true,
            key: "TotalValue", dataIndex: "totalValue"));

        return new ExcelReportLayout { Columns = columns, CurrencyTotalsColumns = currencyColumns };
    }

    private static ReportCurrencyTotalsSummary Currencies() => new()
    {
        Currencies =
        [
            new ReportCurrencyTotal { Currency = "USD", NoOfLicences = 3, TotalValue = 1234m },
            new ReportCurrencyTotal { Currency = "EUR", NoOfLicences = 4, TotalValue = 56.5m },
        ],
        GrandTotalLicences = 7,
    };

    private static object?[] Values(ExcelFooterRow row) => row.Cells.Select(cell => cell?.Value).ToArray();

    [Fact]
    public void No_totals_or_no_rows_means_no_footer_at_all()
    {
        var totals = new ReportFooterTotals(new Dictionary<string, decimal> { ["totalValue"] = 1m }, null);

        Assert.Empty(ExcelFooterBuilder.Build(Layout(), null, 10));
        Assert.Empty(ExcelFooterBuilder.Build(Layout(), totals, 0));
        Assert.Empty(ExcelFooterBuilder.Build(Layout(), new ReportFooterTotals(null, null), 10));
    }

    [Fact]
    public void The_total_row_puts_each_total_under_its_column_and_the_label_in_the_first_untotalled_one()
    {
        var totals = new ReportFooterTotals(
            new Dictionary<string, decimal> { ["noOfLicences"] = 7m, ["totalValue"] = 1290.5m },
            null);

        var row = Assert.Single(ExcelFooterBuilder.Build(Layout(), totals, 10));

        // [No, Currency, CompanyName, NoOfLicences, TotalValue]
        Assert.Equal([null, "Total", null, 7m, 1290.5m], Values(row));
        Assert.Equal(ExcelCellFormat.Number, row.Cells[3]!.Format);
        Assert.Equal(ExcelCellFormat.Money, row.Cells[4]!.Format);
    }

    [Fact]
    public void A_columnTotals_key_that_matches_nothing_produces_no_total_row()
    {
        var totals = new ReportFooterTotals(new Dictionary<string, decimal> { ["nope"] = 7m }, null);

        Assert.Empty(ExcelFooterBuilder.Build(Layout(), totals, 10));
    }

    [Fact]
    public void Per_currency_rows_then_a_grand_row_land_in_the_configured_columns()
    {
        var totals = new ReportFooterTotals(null, Currencies());
        var layout = Layout(currencyColumns: new ExcelCurrencyTotalsColumns("CompanyName", "TotalValue"));

        var rows = ExcelFooterBuilder.Build(layout, totals, 10);

        Assert.Equal(3, rows.Count);
        Assert.Equal([null, null, "USD:3 licence(s)", null, "USD:1,234.0000"], Values(rows[0]));
        Assert.Equal([null, null, "EUR:4 licence(s)", null, "EUR:56.5000"], Values(rows[1]));
        Assert.Equal(["TOTAL", null, "Total:7 licence(s)", null, null], Values(rows[2]));
    }

    [Fact]
    public void Without_configured_columns_the_first_text_and_first_numeric_column_are_used()
    {
        var rows = ExcelFooterBuilder.Build(Layout(), new ReportFooterTotals(null, Currencies()), 10);

        // Currency is the first non-numeric data column; No of Licences the first numeric.
        Assert.Equal([null, "USD:3 licence(s)", null, "USD:1,234.0000", null], Values(rows[0]));
        Assert.Equal(["TOTAL", "Total:7 licence(s)", null, null, null], Values(rows[2]));
    }

    [Fact]
    public void Without_a_row_number_column_TOTAL_goes_in_the_first_data_column()
    {
        var layout = Layout(showRowNumber: false, currencyColumns: new ExcelCurrencyTotalsColumns("CompanyName", "TotalValue"));

        var rows = ExcelFooterBuilder.Build(layout, new ReportFooterTotals(null, Currencies()), 10);

        Assert.Equal(["TOTAL", "Total:7 licence(s)", null, null], Values(rows[^1]));
    }

    [Fact]
    public void When_the_label_column_is_already_the_first_column_TOTAL_is_not_written_twice()
    {
        var layout = Layout(showRowNumber: false, currencyColumns: new ExcelCurrencyTotalsColumns("Currency", "TotalValue"));

        var rows = ExcelFooterBuilder.Build(layout, new ReportFooterTotals(null, Currencies()), 10);

        Assert.Equal(["Total:7 licence(s)", null, null, null], Values(rows[^1]));
    }

    [Fact]
    public void A_report_with_both_kinds_of_totals_gets_the_total_row_first()
    {
        var totals = new ReportFooterTotals(
            new Dictionary<string, decimal> { ["totalValue"] = 1290.5m },
            Currencies());

        var rows = ExcelFooterBuilder.Build(Layout(), totals, 10);

        Assert.Equal(4, rows.Count);
        Assert.Equal("Total", rows[0].Cells[1]!.Value);
        Assert.Equal("USD:3 licence(s)", rows[1].Cells[1]!.Value);
        Assert.Equal("Total:7 licence(s)", rows[3].Cells[1]!.Value);
    }

    [Fact]
    public void A_configured_currency_key_that_matches_no_column_writes_nothing_there()
    {
        // BasicTable's fallback is `configuredKey ?? firstColumn`, so a configured key
        // that matches no column makes the GRID render no label/value cell at all. The
        // sheet must not silently park the total in some other column either.
        var layout = Layout(currencyColumns: new ExcelCurrencyTotalsColumns("LicenceNo", "Amount"));

        var rows = ExcelFooterBuilder.Build(layout, new ReportFooterTotals(null, Currencies()), 10);

        Assert.Equal([null, null, null, null, null], Values(rows[0]));
        Assert.Equal(["TOTAL", null, null, null, null], Values(rows[^1]));
    }

    [Fact]
    public void The_currency_value_uses_the_legacy_N4_format()
    {
        var totals = new ReportFooterTotals(null, new ReportCurrencyTotalsSummary
        {
            Currencies = [new ReportCurrencyTotal { Currency = "MMK", NoOfLicences = 1, TotalValue = 1234567.891m }],
            GrandTotalLicences = 1,
        });

        var rows = ExcelFooterBuilder.Build(Layout(), totals, 1);

        Assert.Equal("MMK:1,234,567.8910", rows[0].Cells[3]!.Value);
    }
}
