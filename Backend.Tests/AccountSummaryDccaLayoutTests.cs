using System.IO.Compression;
using System.Xml.Linq;
using API.Service.ExcelExport;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;

namespace Backend.Tests;

/// <summary>
/// The Account Summary report ships two sheets. The default one mirrors the grid and the
/// RDLC; this one is the file DCCA imports, and it is pinned to the structure of the old
/// Tradenet 2.0 template (Content/excel-template/TransactionFees.xlsx) because DCCA's
/// importer is keyed to that exact shape: headers on row 1, row 2 blank, data from row 3,
/// no title banner and no Total row.
///
/// Everything here is built from the controller's real <see cref="IExcelReportLayoutProvider"/>,
/// so the assertions cannot drift away from what the export actually produces.
/// </summary>
public sealed class AccountSummaryDccaLayoutTests
{
    private static readonly string[] ExpectedDccaHeaders =
    [
        "No", "Entry Date", "HtaThaKa No", "Company Name", "Transation Title",
        "Deducted Fees", "Remark", "Account Code", "Location Code",
    ];

    private static ExcelReportLayout Layout(string? exportFormat)
        => new AccountSummaryReportController(null!, null!).GetExcelLayout(
            new AccountSummaryReportRequest
            {
                FromDate = new DateTime(2026, 8, 31, 0, 0, 0),
                ToDate = new DateTime(2026, 8, 31, 23, 59, 59),
                ExportFormat = exportFormat,
            });

    private static ExcelReportLayout DccaLayout() => Layout("Dcca");

    /// <summary>
    /// Rows 1-2 mirror the customer's sample: one company with two fee lines. Row 3 is a
    /// Member-branch row, where the stored procedure blanks BOTH company fields.
    /// </summary>
    private static sp_AccountSummaryReportResult Row(
        int id,
        double amount,
        string? companyRegistrationNo,
        string? companyName,
        string accountTitleCode)
        => new()
        {
            Id = id.ToString(),
            VoucherDate = new DateTime(2026, 3, 4),
            PaymentDate = new DateTime(2026, 3, 4),
            CompanyRegistrationNo = companyRegistrationNo,
            VoucherNo = $"U0320260000{id}",
            CompanyName = companyName,
            TransactionTitle = "Application Form Fees",
            Amount = amount,
            AccountTitleCode = accountTitleCode,
            SortOrder = 1,
            SakhanId = 0,
            LocationCode = "NPT",
            FormType = "Pa Tha Ka",
        };

    private static readonly sp_AccountSummaryReportResult[] SampleRows =
    [
        Row(1, 10000, "000000005", "SD02Co., Ltd", "002"),
        Row(2, 250000, "000000005", "SD02Co., Ltd", "007"),
        Row(3, 50000, string.Empty, string.Empty, "007"),
    ];

    private static XDocument WriteSheet(ExcelReportLayout layout)
    {
        using var ms = new MemoryStream();
        using (var writer = new StreamingExcelWriter(
            ms, layout.WorksheetTitle ?? "Account Summary Report", layout))
        {
            writer.AppendRows(SampleRows);
            writer.Finish();
        }

        var archive = new ZipArchive(new MemoryStream(ms.ToArray()), ZipArchiveMode.Read);
        using var stream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        return XDocument.Load(stream);
    }

    private static List<XElement> Rows(XDocument sheet)
        => sheet.Descendants(sheet.Root!.Name.Namespace + "row").ToList();

    private static List<string> CellText(XElement row)
    {
        var ns = row.Name.Namespace;
        return row.Elements(ns + "c")
            .Select(cell => cell.Descendants(ns + "t").FirstOrDefault()?.Value
                ?? cell.Element(ns + "v")?.Value
                ?? string.Empty)
            .ToList();
    }

    [Fact]
    public void The_default_export_is_unchanged_when_no_format_is_requested()
    {
        // The customer asked for an ADDITIONAL format; the existing sheet must not move.
        var layout = Layout(null);

        Assert.Equal(
            ["No", "Entry Date", "Company Registration No", "Company Name",
             "Voucher No", "Transaction Title", "Deducted Fees", "Remark"],
            layout.Columns.Select(column => column.Header));
        Assert.Equal("Total", layout.TotalsRowLabel);
        Assert.False(layout.SuppressStandardHeaderBlock);
        Assert.Equal(0, layout.BlankRowsAfterHeader);
        Assert.Null(layout.WorksheetTitle);
    }

    [Fact]
    public void Dcca_declares_the_nine_template_headers_in_order()
    {
        // "Transation Title" is the template's own typo. It is load-bearing: the importer
        // matches on the header text, so correcting it would be a breaking change.
        Assert.Equal(ExpectedDccaHeaders, DccaLayout().Columns.Select(column => column.Header));
    }

    [Theory]
    [InlineData("Dcca")]
    [InlineData("dcca")]
    [InlineData("DCCA")]
    public void The_format_selector_is_case_insensitive(string exportFormat)
        => Assert.Equal(ExpectedDccaHeaders, Layout(exportFormat).Columns.Select(c => c.Header));

    [Fact]
    public void Dcca_writes_headers_on_row_one_then_leaves_row_two_blank()
    {
        var rows = Rows(WriteSheet(DccaLayout()));

        // 1 header + 1 spacer + 3 data rows, and nothing else: no title banner above the
        // header, no Total row below the data.
        Assert.Equal(5, rows.Count);
        Assert.Equal(ExpectedDccaHeaders, CellText(rows[0]));

        // The old EPPlus loop starts at `row = 2` and increments before its first write,
        // so row 2 is empty and the data begins on row 3.
        Assert.Empty(rows[1].Elements());
        Assert.Equal("2", rows[1].Attribute("r")?.Value);
        Assert.Equal("3", rows[2].Attribute("r")?.Value);
    }

    [Fact]
    public void Dcca_joins_the_registration_number_and_voucher_number_with_an_at_sign()
    {
        var rows = Rows(WriteSheet(DccaLayout()));

        Assert.Equal("000000005@U03202600001", CellText(rows[2])[2]);

        // A Member-branch row has no company at all, so the separator leads — exactly what
        // the old export produced ("@U03202600003").
        Assert.Equal("@U03202600003", CellText(rows[4])[2]);
        Assert.Equal(string.Empty, CellText(rows[4])[3]);
    }

    [Fact]
    public void Dcca_writes_the_entry_date_as_month_first_text_not_a_date_serial()
    {
        var sheet = WriteSheet(DccaLayout());
        var ns = sheet.Root!.Name.Namespace;
        var cell = Rows(sheet)[2].Elements(ns + "c").ElementAt(1);

        // The template's column B is numFmt 49 (Text) and the old code wrote
        // VoucherDate.ToString("MM/dd/yyyy"). A date serial here would reach DCCA as a
        // number, and 4 March would arrive as 3 April if the order flipped.
        Assert.Equal("inlineStr", cell.Attribute("t")?.Value);
        Assert.Equal("03/04/2026", cell.Descendants(ns + "t").First().Value);
    }

    [Fact]
    public void Dcca_writes_the_fee_as_a_plain_number_and_leaves_remark_empty()
    {
        var sheet = WriteSheet(DccaLayout());
        var ns = sheet.Root!.Name.Namespace;
        var cells = Rows(sheet)[2].Elements(ns + "c").ToList();

        // General format, no thousands separator: the old export wrote the raw Amount.
        Assert.Null(cells[5].Attribute("t"));
        Assert.Equal("10000", cells[5].Element(ns + "v")?.Value);

        Assert.Equal(string.Empty, CellText(Rows(sheet)[2])[6]);
        Assert.Equal("002", CellText(Rows(sheet)[2])[7]);
        Assert.Equal("NPT", CellText(Rows(sheet)[2])[8]);
    }

    [Fact]
    public void Dcca_suppresses_the_standard_header_block_and_the_totals_row()
    {
        var layout = DccaLayout();

        Assert.True(layout.SuppressStandardHeaderBlock);
        Assert.Equal(1, layout.BlankRowsAfterHeader);
        Assert.Equal("Sheet1", layout.WorksheetTitle);
        Assert.False(layout.FreezeHeader);
        Assert.Null(layout.TotalsRowLabel);
        Assert.DoesNotContain(layout.Columns, column => column.IncludeInTotals);
        Assert.Empty(layout.TitleLines);

        // The shared preamble would push every data row down by four and break the import.
        var withBlock = ExcelLayoutBuilder.WithStandardHeaderBlock(
            layout,
            spec: null,
            fallbackTitle: "Account Summary Report",
            request: new AccountSummaryReportRequest
            {
                FromDate = new DateTime(2026, 8, 31),
                ToDate = new DateTime(2026, 8, 31, 23, 59, 59),
            },
            exportedAt: new DateTimeOffset(2026, 9, 2, 19, 40, 0, TimeSpan.Zero));

        Assert.Empty(withBlock.HeaderBlock);
        Assert.Empty(withBlock.TitleLines);
    }

    [Fact]
    public async Task Dcca_asks_for_no_footer_totals()
    {
        // Returning null is what keeps ExcelFooterBuilder from appending the grid's Total
        // row, and it also skips a cross-page SUM the DCCA file has no use for. Passing a
        // null DbContext proves the database is never touched on this path.
        var controller = new AccountSummaryReportController(null!, null!);

        var totals = await controller.GetExcelFooterTotalsAsync(
            new AccountSummaryReportRequest
            {
                FromDate = new DateTime(2026, 8, 31),
                ToDate = new DateTime(2026, 8, 31, 23, 59, 59),
                ExportFormat = "Dcca",
            },
            CancellationToken.None);

        Assert.Null(totals);
    }
}
