using API.Model;
using API.Model.ExcelExport;
using API.Service.ExcelExport;

namespace Backend.Tests;

/// <summary>
/// The exported cell must read the way the grid cell reads. Each fact here pins one
/// rule from GenericReportPage.tsx's toTableColumn/formatColumnValue, plus the one
/// deliberate deviation: a blank numeric/date cell is EMPTY (so SUM and sort work)
/// where the grid prints "N/A".
/// </summary>
public sealed class ExcelLayoutBuilderTests
{
    private sealed class Row
    {
        public string? CompanyName { get; init; }
        public string? OldLicenceNo { get; init; }
        public string? LicenceNo { get; init; }
        public DateTime? LicenceDate { get; init; }
        public DateTime? CreatedAt { get; init; }
        public decimal? TotalValue { get; init; }
        public int? NoOfLicences { get; init; }
        public bool? IsActive { get; init; }
        public string? TransactionAmount { get; init; }
        public decimal? MocAmount { get; init; }
        public decimal? ImAmount { get; init; }
        public decimal? MpuAmount { get; init; }
        public decimal? AmountDiff { get; init; }
    }

    private sealed class RangeRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    private sealed class SingleDateRequest : ReportQueryRequest
    {
        public DateTime Date { get; set; }
    }

    private sealed class NoDateRequest : ReportQueryRequest
    {
        public string CompanyRegistrationNo { get; set; } = string.Empty;
    }

    private static readonly DateTimeOffset ExportedAt =
        new(2026, 9, 2, 19, 40, 0, TimeSpan.Zero);

    private static object? Cell(ExcelReportLayout layout, int columnIndex, Row row)
        => layout.Columns[columnIndex].GetValue(row, 1L);

    [Fact]
    public void The_header_row_is_the_row_number_title_then_the_config_titles_in_order()
    {
        var spec = ExcelSpecFactory.Spec(
            "R",
            ExcelSpecFactory.Column("companyName", "Company Name"),
            ExcelSpecFactory.Column("licenceDate", "Licence Date", "date"));

        var layout = ExcelLayoutBuilder.Build(spec, typeof(Row));

        Assert.Equal(["No", "Company Name", "Licence Date"], layout.Columns.Select(c => c.Header));
        Assert.True(layout.Columns[0].IsRowNumber);
    }

    [Fact]
    public void A_bespoke_row_number_title_is_kept()
    {
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("companyName", "Company Name"));
        spec.RowNumberTitle = "Sr.No.";

        Assert.Equal("Sr.No.", ExcelLayoutBuilder.Build(spec, typeof(Row)).Columns[0].Header);
    }

    [Fact]
    public void A_blank_text_cell_says_N_A_but_a_blank_number_or_date_cell_is_empty()
    {
        var spec = ExcelSpecFactory.Spec(
            "R",
            ExcelSpecFactory.Column("companyName", "Company Name"),
            ExcelSpecFactory.Column("totalValue", "Total Value", "money"),
            ExcelSpecFactory.Column("licenceDate", "Licence Date", "date"));

        var layout = ExcelLayoutBuilder.Build(spec, typeof(Row));
        var row = new Row();

        Assert.Equal(ExcelLayoutBuilder.NullText, Cell(layout, 1, row));
        Assert.Null(Cell(layout, 2, row));
        Assert.Null(Cell(layout, 3, row));
    }

    [Fact]
    public void Fallback_data_indexes_are_joined_with_a_comma_when_the_primary_is_blank()
    {
        var spec = ExcelSpecFactory.Spec(
            "R",
            ExcelSpecFactory.Column("oldLicenceNo", "Licence No", null, "LicenceNo", null, "licenceNo", "companyName"));

        var layout = ExcelLayoutBuilder.Build(spec, typeof(Row));

        Assert.Equal("OLD-1", Cell(layout, 1, new Row { OldLicenceNo = "OLD-1", LicenceNo = "NEW-1" }));
        Assert.Equal("NEW-1, ACME", Cell(layout, 1, new Row { LicenceNo = "NEW-1", CompanyName = "ACME" }));
        Assert.Equal(ExcelLayoutBuilder.NullText, Cell(layout, 1, new Row()));
    }

    [Fact]
    public void Money_and_number_cells_are_numeric_and_dates_stay_real_dates()
    {
        var spec = ExcelSpecFactory.Spec(
            "R",
            ExcelSpecFactory.Column("totalValue", "Total Value", "money"),
            ExcelSpecFactory.Column("noOfLicences", "No of Licences", "number"),
            ExcelSpecFactory.Column("licenceDate", "Licence Date", "date"),
            ExcelSpecFactory.Column("createdAt", "Created", "dateTime"));

        var layout = ExcelLayoutBuilder.Build(spec, typeof(Row));
        var row = new Row
        {
            TotalValue = 1234.5m,
            NoOfLicences = 7,
            LicenceDate = new DateTime(2026, 2, 1),
            CreatedAt = new DateTime(2026, 2, 1, 8, 30, 0),
        };

        Assert.Equal(1234.5m, Cell(layout, 1, row));
        Assert.Equal(7m, Cell(layout, 2, row));
        Assert.Equal(new DateTime(2026, 2, 1), Cell(layout, 3, row));
        Assert.Equal(new DateTime(2026, 2, 1, 8, 30, 0), Cell(layout, 4, row));

        Assert.Equal(ExcelCellFormat.Money, layout.Columns[1].Format);
        Assert.Equal(ExcelCellFormat.Number, layout.Columns[2].Format);
        Assert.Equal(ExcelCellFormat.Date, layout.Columns[3].Format);
        Assert.Equal(ExcelCellFormat.DateTime, layout.Columns[4].Format);
    }

    [Fact]
    public void A_four_decimal_number_format_selects_the_Money4_cell_style()
    {
        var spec = ExcelSpecFactory.Spec(
            "R",
            ExcelSpecFactory.Column("totalValue", "Total Value", "money", numberFormat: "#,##0.0000"));

        Assert.Equal(ExcelCellFormat.Money4, ExcelLayoutBuilder.Build(spec, typeof(Row)).Columns[1].Format);
    }

    [Fact]
    public void A_boolean_column_exports_Yes_or_No()
    {
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("isActive", "Active", "boolean"));
        var layout = ExcelLayoutBuilder.Build(spec, typeof(Row));

        Assert.Equal("Yes", Cell(layout, 1, new Row { IsActive = true }));
        Assert.Equal("No", Cell(layout, 1, new Row { IsActive = false }));
        Assert.Equal(ExcelLayoutBuilder.NullText, Cell(layout, 1, new Row()));
    }

    [Fact]
    public void transactionAmount_treats_an_integer_string_as_minor_units()
    {
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("transactionAmount", "Amount", "money"));
        var layout = ExcelLayoutBuilder.Build(spec, typeof(Row));

        Assert.Equal(1234.56m, Cell(layout, 1, new Row { TransactionAmount = "123456" }));
        Assert.Equal(1234.56m, Cell(layout, 1, new Row { TransactionAmount = "1,234.56" }));
        Assert.Equal(0m, Cell(layout, 1, new Row { TransactionAmount = "abc" }));
    }

    [Fact]
    public void mpuAmount_and_amountDiff_fall_back_to_the_grids_arithmetic()
    {
        var spec = ExcelSpecFactory.Spec(
            "R",
            ExcelSpecFactory.Column("mpuAmount", "MPU Amount", "money"),
            ExcelSpecFactory.Column("amountDiff", "Difference", "money"));

        var layout = ExcelLayoutBuilder.Build(spec, typeof(Row));
        var row = new Row { TransactionAmount = "100000", MocAmount = 300m, ImAmount = 200m };

        // 1000.00 - 300 - 200, and 1000.00 - 300
        Assert.Equal(500m, Cell(layout, 1, row));
        Assert.Equal(700m, Cell(layout, 2, row));

        // A value that IS present always wins.
        Assert.Equal(42m, Cell(layout, 1, new Row { MpuAmount = 42m, TransactionAmount = "100000" }));
        Assert.Equal(43m, Cell(layout, 2, new Row { AmountDiff = 43m, TransactionAmount = "100000" }));
    }

    [Fact]
    public void An_unbound_dataIndex_becomes_a_blank_column_that_keeps_its_ui_title()
    {
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("remark", "Remark"));
        var layout = ExcelLayoutBuilder.Build(spec, typeof(Row));

        Assert.Equal("Remark", layout.Columns[1].Header);
        Assert.Null(Cell(layout, 1, new Row { CompanyName = "ACME" }));
        Assert.Equal("remark", layout.Columns[1].DataIndex);
    }

    [Fact]
    public void Every_generic_column_is_bound_so_the_footer_can_place_totals()
    {
        var spec = ExcelSpecFactory.Spec(
            "R",
            ExcelSpecFactory.Column("totalValue", "Total Value", "money", "TotalValue"));

        var column = ExcelLayoutBuilder.Build(spec, typeof(Row)).Columns[1];

        Assert.Equal("TotalValue", column.Key);
        Assert.Equal("totalValue", column.DataIndex);
        Assert.True(column.IsNumeric);
    }

    [Fact]
    public void The_header_block_carries_the_title_the_from_to_lines_and_the_export_time()
    {
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("companyName", "Company Name"));
        spec.Title = "Import Licence Report";
        spec.HeaderLines = ["Ministry of Commerce", "Import Licence Report (01/02/2026) To (28/02/2026)"];

        var layout = ExcelLayoutBuilder.WithStandardHeaderBlock(
            ExcelLayoutBuilder.Build(spec, typeof(Row)),
            spec,
            "Fallback",
            new RangeRequest { FromDate = new DateTime(2026, 2, 1), ToDate = new DateTime(2026, 2, 28) },
            ExportedAt);

        Assert.Equal(
            [
                "Ministry of Commerce",
                "Import Licence Report (01/02/2026) To (28/02/2026)",
                "From Date: 01/02/2026",
                "To Date: 28/02/2026",
                "Exported: 02/09/2026 19:40",
            ],
            layout.HeaderBlock.Select(line => line.Text));

        // The subtitle already contains the title, so no redundant title row is added.
        Assert.DoesNotContain(
            layout.HeaderBlock,
            line => line.Kind == ExcelHeaderLineKind.Title && line.Text == "Import Licence Report");
    }

    [Fact]
    public void A_title_that_no_header_line_mentions_gets_its_own_first_row()
    {
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("companyName", "Company Name"));
        spec.Title = "Account Summary Report";
        spec.HeaderLines = [];

        var layout = ExcelLayoutBuilder.WithStandardHeaderBlock(
            ExcelLayoutBuilder.Build(spec, typeof(Row)),
            spec,
            "Fallback",
            new RangeRequest { FromDate = new DateTime(2026, 2, 1), ToDate = new DateTime(2026, 2, 28) },
            ExportedAt);

        Assert.Equal("Account Summary Report", layout.HeaderBlock[0].Text);
        Assert.Equal(ExcelHeaderLineKind.Title, layout.HeaderBlock[0].Kind);
    }

    [Fact]
    public void A_single_date_request_gets_one_date_row_and_a_dateless_one_gets_none()
    {
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("companyName", "Company Name"));

        var single = ExcelLayoutBuilder.WithStandardHeaderBlock(
            ExcelReportLayout.None, spec, "T", new SingleDateRequest { Date = new DateTime(2026, 2, 15) }, ExportedAt);
        var none = ExcelLayoutBuilder.WithStandardHeaderBlock(
            ExcelReportLayout.None, spec, "T", new NoDateRequest(), ExportedAt);

        Assert.Contains(single.HeaderBlock, line => line.Text == "Date: 15/02/2026");
        Assert.DoesNotContain(single.HeaderBlock, line => line.Text.StartsWith("From Date"));
        Assert.DoesNotContain(none.HeaderBlock, line => line.Text.Contains("Date:"));
        Assert.Contains(none.HeaderBlock, line => line.Text.StartsWith("Exported: "));
    }

    [Fact]
    public void A_typed_layouts_own_title_line_is_preserved_and_the_header_block_added_under_it()
    {
        var typed = new ExcelReportLayout
        {
            TitleLines = ["Account Summary Report (01/02/2026) To (28/02/2026)"],
            Columns = [ExcelColumn.RowNumber(), ExcelColumn.Text<Row>("Company Name", row => row.CompanyName)],
        };

        var layout = ExcelLayoutBuilder.WithStandardHeaderBlock(
            typed,
            null,
            "Account Summary Report",
            new RangeRequest { FromDate = new DateTime(2026, 2, 1), ToDate = new DateTime(2026, 2, 28) },
            ExportedAt);

        Assert.Equal(["Account Summary Report (01/02/2026) To (28/02/2026)"], layout.TitleLines);
        Assert.Equal(
            ["From Date: 01/02/2026", "To Date: 28/02/2026", "Exported: 02/09/2026 19:40"],
            layout.HeaderBlock.Select(line => line.Text));
    }

    [Fact]
    public void Dates_use_invariant_separators_whatever_the_server_culture_is()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            var layout = ExcelLayoutBuilder.WithStandardHeaderBlock(
                ExcelReportLayout.None,
                null,
                "Report",
                new RangeRequest { FromDate = new DateTime(2026, 2, 1), ToDate = new DateTime(2026, 2, 28) },
                ExportedAt);

            Assert.Contains(layout.HeaderBlock, line => line.Text == "From Date: 01/02/2026");
            Assert.Contains(layout.HeaderBlock, line => line.Text == "Exported: 02/09/2026 19:40");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Column_widths_follow_the_data_type_heuristic()
    {
        var spec = ExcelSpecFactory.Spec(
            "R",
            ExcelSpecFactory.Column("licenceDate", "Licence Date", "date"),
            ExcelSpecFactory.Column("createdAt", "Created", "dateTime"),
            ExcelSpecFactory.Column("totalValue", "Total Value", "money"),
            ExcelSpecFactory.Column("noOfLicences", "No", "number"),
            ExcelSpecFactory.Column("companyName", "Company Name"));

        var widths = ExcelLayoutBuilder.Build(spec, typeof(Row)).Columns.Select(c => c.Width).ToArray();

        Assert.Equal(12d, widths[1]);
        Assert.Equal(20d, widths[2]);
        Assert.Equal(16d, widths[3]);
        Assert.Equal(12d, widths[4]);
        Assert.Equal("Company Name".Length + 4d, widths[5]);
    }

    [Fact]
    public void An_empty_string_renders_verbatim_on_a_plain_text_column_and_N_A_only_when_missing()
    {
        var spec = ExcelSpecFactory.Spec(
            "R",
            ExcelSpecFactory.Column("companyName", "Company Name"),
            ExcelSpecFactory.Column("isActive", "Active", "boolean"));

        var layout = ExcelLayoutBuilder.Build(spec, typeof(Row));

        // A render-less grid column prints `value?.toString() ?? 'N/A'`, so only a
        // MISSING value is "N/A"; an empty or whitespace string renders as it is.
        Assert.Equal(string.Empty, Cell(layout, 1, new Row { CompanyName = string.Empty }));
        Assert.Equal("   ", Cell(layout, 1, new Row { CompanyName = "   " }));
        Assert.Equal(ExcelLayoutBuilder.NullText, Cell(layout, 1, new Row()));

        // A column the grid gives a render (boolean here) runs the value through
        // hasValue first, so blank and missing both read "N/A".
        Assert.Equal(ExcelLayoutBuilder.NullText, Cell(layout, 2, new Row()));
        Assert.Equal("Yes", Cell(layout, 2, new Row { IsActive = true }));
    }

    [Fact]
    public void A_decimal_in_a_text_column_drops_its_sql_scale_the_way_the_grid_does()
    {
        // The grid stringifies the JSON number (JS has already normalised it), so a
        // decimal(18,4) SUM of 1500 must read "1500", not "1500.0000".
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("totalValue", "Total Value"));
        var layout = ExcelLayoutBuilder.Build(spec, typeof(Row));

        Assert.Equal("1500", Cell(layout, 1, new Row { TotalValue = 1500.0000m }));
        Assert.Equal("1500.25", Cell(layout, 1, new Row { TotalValue = 1500.2500m }));
    }

    [Fact]
    public void With_no_row_type_the_columns_export_blank_instead_of_N_A_on_every_row()
    {
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("companyName", "Company Name"));

        var layout = ExcelLayoutBuilder.Build(spec, rowType: null);

        Assert.Equal("Company Name", layout.Columns[1].Header);
        Assert.Null(Cell(layout, 1, new Row { CompanyName = "ACME" }));
    }

    [Fact]
    public void A_spec_with_no_columns_and_no_sections_is_refused_not_reflected()
    {
        var spec = ExcelSpecFactory.Spec("R");
        spec.ShowRowNumber = false;

        Assert.Throws<InvalidOperationException>(() => ExcelLayoutBuilder.Build(spec, typeof(Row)));
    }

    [Fact]
    public void A_range_request_whose_dates_were_never_set_gets_no_date_rows_at_all()
    {
        var spec = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("companyName", "Company Name"));

        var layout = ExcelLayoutBuilder.WithStandardHeaderBlock(
            ExcelReportLayout.None, spec, "T", new RangeRequest(), ExportedAt);

        // Half a range, or "01/01/0001", would be worse than nothing.
        Assert.DoesNotContain(layout.HeaderBlock, line => line.Text.Contains("Date:"));
        Assert.Contains(layout.HeaderBlock, line => line.Text.StartsWith("Exported: "));
    }
}
