using System.IO.Compression;
using System.Xml.Linq;
using API.Model;
using API.Service.ExcelExport;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;

namespace Backend.Tests;

public sealed class StreamingExcelWriterTests
{
    private sealed class Row
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public DateTime? When { get; init; }
    }

    private static byte[] Write(IEnumerable<IReadOnlyList<Row>> chunks, string title = "Report")
    {
        using var ms = new MemoryStream();
        using (var writer = new StreamingExcelWriter(ms, title))
        {
            foreach (var chunk in chunks)
            {
                writer.AppendRows(chunk);
            }

            writer.Finish();
        }

        return ms.ToArray();
    }

    [Fact]
    public void Produces_a_valid_zip_with_expected_parts()
    {
        var bytes = Write(new[]
        {
            new List<Row> { new() { Id = 1, Name = "A", Amount = 1.5m, When = new DateTime(2026, 1, 1) } },
            new List<Row> { new() { Id = 2, Name = "B<>&", Amount = 2m, When = null } },
        });

        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(bytes, 0, 2));

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
        Assert.NotNull(archive.GetEntry("_rels/.rels"));
        Assert.NotNull(archive.GetEntry("xl/workbook.xml"));
        Assert.NotNull(archive.GetEntry("xl/_rels/workbook.xml.rels"));
        Assert.NotNull(archive.GetEntry("xl/styles.xml"));
        Assert.NotNull(archive.GetEntry("xl/worksheets/sheet1.xml"));
    }

    [Fact]
    public void Writes_header_plus_one_row_per_record()
    {
        var bytes = Write(new[]
        {
            new List<Row>
            {
                new() { Id = 1, Name = "A", Amount = 1m, When = null },
                new() { Id = 2, Name = "B", Amount = 2m, When = null },
                new() { Id = 3, Name = "C", Amount = 3m, When = null },
            },
        });

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;
        var rows = doc.Descendants(ns + "row").ToList();

        // 1 header + 3 data rows.
        Assert.Equal(4, rows.Count);

        // Header carries the property names.
        var headerCells = rows[0].Elements(ns + "c").ToList();
        Assert.Equal(4, headerCells.Count);
        Assert.Contains("Id", doc.ToString());
        Assert.Contains("Amount", doc.ToString());
    }

    [Fact]
    public void Numeric_columns_are_written_as_numbers_and_text_as_inline_strings()
    {
        var bytes = Write(new[]
        {
            new List<Row> { new() { Id = 42, Name = "hello", Amount = 9.25m, When = null } },
        });

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;
        var dataRow = doc.Descendants(ns + "row").ElementAt(1);
        var cells = dataRow.Elements(ns + "c").ToList();

        // Id (numeric) → <v>42</v>, no inlineStr type.
        Assert.Null(cells[0].Attribute("t"));
        Assert.Equal("42", cells[0].Element(ns + "v")?.Value);

        // Name (text) → t="inlineStr"
        Assert.Equal("inlineStr", cells[1].Attribute("t")?.Value);
        Assert.Equal("hello", cells[1].Descendants(ns + "t").First().Value);
    }

    [Fact]
    public void Empty_export_still_produces_one_header_sheet()
    {
        var bytes = Write(System.Array.Empty<IReadOnlyList<Row>>());

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("xl/worksheets/sheet1.xml"));
        Assert.NotNull(archive.GetEntry("xl/workbook.xml"));
    }

    // ---- Layout mode: title row + explicit columns (Account Summary Report) ----

    private const string ExpectedTitle = "Account Summary Report (31/08/2026) To (31/08/2026)";

    private static readonly string[] ExpectedHeaders =
    [
        "No", "Entry Date", "Company Registration No", "Company Name",
        "Voucher No", "Transaction Title", "Deducted Fees", "Remark",
    ];

    /// <summary>The real layout the controller declares, so the tests can't drift from it.</summary>
    private static ExcelReportLayout AccountSummaryLayout()
        => new AccountSummaryReportController(null!, null!).GetExcelLayout(new AccountSummaryReportRequest
        {
            FromDate = new DateTime(2026, 8, 31, 0, 0, 0),
            ToDate = new DateTime(2026, 8, 31, 23, 59, 59),
        });

    private static sp_AccountSummaryReportResult AccountRow(int id, double amount, DateTime? voucherDate = null)
        => new()
        {
            Id = id.ToString(),
            VoucherDate = voucherDate ?? new DateTime(2026, 8, 31),
            PaymentDate = new DateTime(2026, 8, 31),
            CompanyRegistrationNo = $"REG{id}",
            VoucherNo = $"V{id}",
            CompanyName = $"Company {id}",
            TransactionTitle = "Registration Fees",
            Amount = amount,
            AccountTitleCode = "A1",
            SortOrder = 1,
            SakhanId = 0,
            LocationCode = "NPT",
            FormType = "Pa Tha Ka",
        };

    private static byte[] WriteWithLayout(
        IEnumerable<IReadOnlyList<sp_AccountSummaryReportResult>> chunks,
        ExcelReportLayout? layout = null,
        int maxRowsPerSheet = 1_048_576)
    {
        using var ms = new MemoryStream();
        using (var writer = new StreamingExcelWriter(
            ms, "Account Summary Report", layout ?? AccountSummaryLayout(), maxRowsPerSheet))
        {
            foreach (var chunk in chunks)
            {
                writer.AppendRows(chunk);
            }

            writer.Finish();
        }

        return ms.ToArray();
    }

    [Fact]
    public void Layout_writes_the_report_title_then_the_ui_headers()
    {
        var bytes = WriteWithLayout([[AccountRow(1, 1000), AccountRow(2, 2500)]]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;
        var rows = doc.Descendants(ns + "row").ToList();

        // title + header + 2 data + totals
        Assert.Equal(5, rows.Count);

        var titleCells = rows[0].Elements(ns + "c").ToList();
        Assert.Single(titleCells);
        Assert.Equal("A1", titleCells[0].Attribute("r")?.Value);
        Assert.Equal(ExpectedTitle, titleCells[0].Descendants(ns + "t").First().Value);

        var headers = rows[1].Elements(ns + "c")
            .Select(c => c.Descendants(ns + "t").First().Value)
            .ToArray();
        Assert.Equal(ExpectedHeaders, headers);
    }

    [Fact]
    public void Layout_merges_the_title_across_the_columns_after_sheetData()
    {
        var bytes = WriteWithLayout([[AccountRow(1, 1000)]]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;

        var merge = doc.Descendants(ns + "mergeCell").Single();
        Assert.Equal("A1:H1", merge.Attribute("ref")?.Value);

        // mergeCells must follow sheetData in the CT_Worksheet sequence, or Excel
        // reports the file as corrupt.
        var children = doc.Root!.Elements().Select(e => e.Name.LocalName).ToList();
        Assert.True(
            children.IndexOf("mergeCells") > children.IndexOf("sheetData"),
            $"mergeCells must come after sheetData, got: {string.Join(", ", children)}");
        Assert.True(children.IndexOf("cols") < children.IndexOf("sheetData"), "cols must come before sheetData");
    }

    [Fact]
    public void Layout_row_indexes_are_contiguous_and_match_their_cell_references()
    {
        var bytes = WriteWithLayout([[AccountRow(1, 1), AccountRow(2, 2)], [AccountRow(3, 3)]]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        AssertRowIndexesAreSane(ReadSheet(archive, 1));
    }

    [Fact]
    public void Layout_types_cells_so_excel_can_sum_and_sort_them()
    {
        var bytes = WriteWithLayout([[AccountRow(7, 1234.5, new DateTime(2026, 8, 31))]]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;
        var cells = doc.Descendants(ns + "row").ElementAt(2).Elements(ns + "c").ToList();

        // No → a plain number, not text.
        Assert.Null(cells[0].Attribute("t"));
        Assert.Equal("1", cells[0].Element(ns + "v")?.Value);

        // Entry Date → a real date serial carrying the mm/dd/yyyy style (style 13): the
        // payment department reads these into their own books, and this report's DCCA
        // file has always printed MM/dd/yyyy.
        Assert.Null(cells[1].Attribute("t"));
        Assert.Equal(new DateTime(2026, 8, 31).ToOADate().ToString("0.##########"), cells[1].Element(ns + "v")?.Value);
        Assert.Equal("13", cells[1].Attribute("s")?.Value);

        // Company Registration No stays text, so a numeric-looking code keeps its shape.
        Assert.Equal("inlineStr", cells[2].Attribute("t")?.Value);

        // Deducted Fees → numeric, so SUM() works.
        Assert.Null(cells[6].Attribute("t"));
        Assert.Equal("1234.5", cells[6].Element(ns + "v")?.Value);

        // Remark → an empty cell (unbound in the old RDLC too).
        Assert.Empty(cells[7].Elements());
    }

    [Fact]
    public void Layout_appends_a_totals_row_matching_the_grid_footer()
    {
        var bytes = WriteWithLayout([[AccountRow(1, 1000.25), AccountRow(2, 2000.75)]]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;
        var rows = doc.Descendants(ns + "row").ToList();
        var totalCells = rows[^1].Elements(ns + "c").ToList();

        // Label sits immediately left of Deducted Fees. This row comes from the layout's own
        // TotalsRowLabel (AccountSummaryReportController.cs:131 sets it to "Total"), NOT from
        // ExcelFooterBuilder, whose grid-footer label is the RDLC's "TOTAL".
        Assert.Equal("Total", totalCells[5].Descendants(ns + "t").First().Value);
        Assert.Equal("3001", totalCells[6].Element(ns + "v")?.Value);
        Assert.Single(doc.Descendants(ns + "row").Where(r => r.Descendants(ns + "t").Any(t => t.Value == "Total")));
    }

    [Fact]
    public void Totals_row_is_not_counted_as_data_and_is_skipped_when_empty()
    {
        using var ms = new MemoryStream();
        using (var writer = new StreamingExcelWriter(ms, "Account Summary Report", AccountSummaryLayout()))
        {
            writer.AppendRows(new[] { AccountRow(1, 5), AccountRow(2, 5) });
            writer.Finish();
            Assert.Equal(2, writer.TotalDataRows);
        }

        var emptyBytes = WriteWithLayout([]);
        using var archive = new ZipArchive(new MemoryStream(emptyBytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;

        // Title + headers only — no stray "Total 0" row.
        var rows = doc.Descendants(ns + "row").ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(ExpectedTitle, rows[0].Descendants(ns + "t").First().Value);
    }

    [Fact]
    public void Sheet_rollover_repeats_the_preamble_and_keeps_numbering_continuous()
    {
        // 5 rows per sheet = title + header + 3 data rows, so 4 rows spill onto sheet 2.
        var bytes = WriteWithLayout(
            [Enumerable.Range(1, 4).Select(i => AccountRow(i, i)).ToList()],
            maxRowsPerSheet: 5);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("xl/worksheets/sheet2.xml"));

        var sheet2 = ReadSheet(archive, 2);
        var ns = sheet2.Root!.Name.Namespace;
        var rows = sheet2.Descendants(ns + "row").ToList();

        Assert.Equal(ExpectedTitle, rows[0].Descendants(ns + "t").First().Value);
        Assert.Equal(
            ExpectedHeaders,
            rows[1].Elements(ns + "c").Select(c => c.Descendants(ns + "t").First().Value).ToArray());

        // Row positions restart per sheet, but "No" keeps counting: sheet 1 held 1-3.
        AssertRowIndexesAreSane(sheet2);
        Assert.Equal("4", rows[2].Elements(ns + "c").First().Element(ns + "v")?.Value);

        AssertRowIndexesAreSane(ReadSheet(archive, 1));
    }

    [Fact]
    public void Style_counts_match_the_declared_elements()
    {
        var bytes = WriteWithLayout([[AccountRow(1, 1)]]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var entry = archive.GetEntry("xl/styles.xml")!;
        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        var ns = doc.Root!.Name.Namespace;

        foreach (var name in new[] { "numFmts", "fonts", "fills", "borders", "cellStyleXfs", "cellXfs" })
        {
            var element = doc.Descendants(ns + name).Single();
            Assert.Equal(element.Elements().Count().ToString(), element.Attribute("count")?.Value);
        }

        // Every style index the writer emits must exist in cellXfs.
        var xfCount = doc.Descendants(ns + "cellXfs").Single().Elements().Count();
        var sheet = ReadSheet(archive, 1);
        var sheetNs = sheet.Root!.Name.Namespace;
        foreach (var styleAttribute in sheet.Descendants(sheetNs + "c").Select(c => c.Attribute("s")).Where(a => a != null))
        {
            Assert.InRange(int.Parse(styleAttribute!.Value), 0, xfCount - 1);
        }
    }

    [Fact]
    public void Title_uses_invariant_date_separators_regardless_of_server_culture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // de-DE renders "/" in a custom date format as "." unless the format is invariant.
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            Assert.Equal(
                ExpectedTitle,
                ExcelReportTitle.DateRange(
                    "Account Summary Report",
                    new DateTime(2026, 8, 31),
                    new DateTime(2026, 8, 31, 23, 59, 59)));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    // ---- Standard header block, freeze pane, grid footer rows, composite sections ----

    private static ExcelReportLayout WithBlock(ExcelReportLayout layout, object request)
        => ExcelLayoutBuilder.WithStandardHeaderBlock(
            layout,
            null,
            "Account Summary Report",
            request,
            new DateTimeOffset(2026, 9, 2, 19, 40, 0, TimeSpan.Zero));

    private static AccountSummaryReportRequest SampleRequest() => new()
    {
        FromDate = new DateTime(2026, 8, 1),
        ToDate = new DateTime(2026, 8, 31, 23, 59, 59),
    };

    [Fact]
    public void The_header_block_sits_above_the_column_headers_with_meta_rows_unmerged()
    {
        var layout = WithBlock(AccountSummaryLayout(), SampleRequest());
        var bytes = WriteWithLayout([[AccountRow(1, 10)]], layout);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;
        var rows = doc.Descendants(ns + "row").ToList();

        // subtitle, From Date, To Date, Exported, header, 1 data row, and — because this
        // test appends no AppendFooterRows footer — the layout's own summed totals row.
        Assert.Equal(7, rows.Count);
        Assert.Equal(
            "Total",
            rows[6].Elements(ns + "c").ElementAt(5).Descendants(ns + "t").First().Value);
        Assert.Equal(ExpectedTitle, rows[0].Descendants(ns + "t").First().Value);
        Assert.Equal("From Date: 01/08/2026", rows[1].Descendants(ns + "t").First().Value);
        Assert.Equal("To Date: 31/08/2026", rows[2].Descendants(ns + "t").First().Value);
        Assert.Equal("Exported: 02/09/2026 19:40", rows[3].Descendants(ns + "t").First().Value);
        Assert.Equal(
            ExpectedHeaders,
            rows[4].Elements(ns + "c").Select(c => c.Descendants(ns + "t").First().Value).ToArray());

        // Only the title band is merged; the meta rows stay single cells.
        var merge = doc.Descendants(ns + "mergeCell").Single();
        Assert.Equal("A1:H1", merge.Attribute("ref")?.Value);
        AssertRowIndexesAreSane(doc);
    }

    [Fact]
    public void The_header_rows_are_frozen_and_sheetViews_comes_first()
    {
        var layout = WithBlock(AccountSummaryLayout(), SampleRequest());
        var bytes = WriteWithLayout([[AccountRow(1, 10)]], layout);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;

        var pane = doc.Descendants(ns + "pane").Single();
        Assert.Equal("5", pane.Attribute("ySplit")?.Value);
        Assert.Equal("A6", pane.Attribute("topLeftCell")?.Value);
        Assert.Equal("frozen", pane.Attribute("state")?.Value);

        var children = doc.Root!.Elements().Select(e => e.Name.LocalName).ToList();
        Assert.Equal(0, children.IndexOf("sheetViews"));
        Assert.True(children.IndexOf("cols") < children.IndexOf("sheetData"));
    }

    [Fact]
    public void The_legacy_no_layout_sheet_is_untouched_by_the_header_block_work()
    {
        var bytes = Write([[new Row { Id = 1, Name = "A", Amount = 1m, When = null }]]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;

        // No freeze pane, no widths, header still at row 1.
        Assert.Empty(doc.Descendants(ns + "sheetViews"));
        Assert.Empty(doc.Descendants(ns + "cols"));
        Assert.Equal("1", doc.Descendants(ns + "row").First().Attribute("r")?.Value);
    }

    [Fact]
    public void AppendFooterRows_writes_the_grid_footer_and_suppresses_the_summed_totals_row()
    {
        var layout = WithBlock(AccountSummaryLayout(), SampleRequest());
        var totals = new ReportFooterTotals(
            new Dictionary<string, decimal> { ["amount"] = 3001m },
            new ReportCurrencyTotalsSummary
            {
                Currencies = [new ReportCurrencyTotal { Currency = "MMK", NoOfLicences = 2, TotalValue = 3001m }],
                GrandTotalLicences = 2,
            });

        using var ms = new MemoryStream();
        using (var writer = new StreamingExcelWriter(ms, "Account Summary Report", layout))
        {
            writer.AppendRows(new[] { AccountRow(1, 1000.25), AccountRow(2, 2000.75) });
            writer.AppendFooterRows(ExcelFooterBuilder.Build(layout, totals, writer.TotalDataRows));
            writer.Finish();

            Assert.Equal(2, writer.TotalDataRows);
        }

        using var archive = new ZipArchive(new MemoryStream(ms.ToArray()), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;
        var rows = doc.Descendants(ns + "row").ToList();

        // 4 preamble + header + 2 data + column totals + MMK + grand total
        Assert.Equal(10, rows.Count);
        // "TOTAL" sits in the first data column with no total of its own (Entry Date),
        // exactly where BasicTable's totalLabelIndex puts it — NOT immediately left of
        // the totalled column, which is where the legacy WriteTotalsRow used to put it.
        Assert.Equal("TOTAL", rows[7].Elements(ns + "c").ElementAt(1).Descendants(ns + "t").First().Value);
        Assert.Equal("3001", rows[7].Elements(ns + "c").ElementAt(6).Element(ns + "v")?.Value);
        Assert.Equal(
            "MMK:2 licence(s)",
            rows[8].Elements(ns + "c").ElementAt(1).Descendants(ns + "t").First().Value);
        Assert.Equal("TOTAL", rows[9].Elements(ns + "c").First().Descendants(ns + "t").First().Value);

        // Exactly two "TOTAL" labels — the column-totals row and the currency grand row.
        // A third would mean the layout's own summed row was written as well.
        Assert.Equal(2, doc.Descendants(ns + "t").Count(t => t.Value == "TOTAL"));
        AssertRowIndexesAreSane(doc);
    }

    private sealed class SectionRow
    {
        public string Currency { get; init; } = string.Empty;
        public decimal TotalValue { get; init; }
    }

    [Fact]
    public void A_composite_sheet_writes_one_header_per_section_and_restarts_its_numbering()
    {
        var layout = new ExcelReportLayout
        {
            Sections =
            [
                new ExcelReportSection
                {
                    Title = "Total Value",
                    Columns =
                    [
                        ExcelColumn.RowNumber("Sr.No."),
                        ExcelColumn.Money4<SectionRow>("Total Value", row => row.TotalValue),
                        ExcelColumn.Text<SectionRow>("Currency", row => row.Currency),
                    ],
                },
                new ExcelReportSection
                {
                    Title = "Total Licences",
                    Columns =
                    [
                        ExcelColumn.RowNumber("Sr.No."),
                        ExcelColumn.Number<SectionRow>("Total Licences", row => row.TotalValue),
                        ExcelColumn.Text<SectionRow>("Pa Tha Ka Type", row => row.Currency),
                    ],
                },
            ],
        };

        using var ms = new MemoryStream();
        using (var writer = new StreamingExcelWriter(ms, "Total Value", layout))
        {
            writer.BeginSection(0);
            writer.AppendRows(new[]
            {
                new SectionRow { Currency = "USD", TotalValue = 10m },
                new SectionRow { Currency = "EUR", TotalValue = 20m },
            });

            writer.BeginSection(1);
            writer.AppendRows(new[] { new SectionRow { Currency = "Wholesale", TotalValue = 5m } });
            writer.AppendNote("Total USD Value: 1,234.5678");
            writer.Finish();
        }

        using var archive = new ZipArchive(new MemoryStream(ms.ToArray()), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;
        var rows = doc.Descendants(ns + "row").ToList();

        var texts = rows
            .Select(row => row.Elements(ns + "c").FirstOrDefault())
            .Select(cell => cell?.Descendants(ns + "t").FirstOrDefault()?.Value
                ?? cell?.Element(ns + "v")?.Value
                ?? string.Empty)
            .ToList();

        Assert.Equal("Total Value", texts[0]);
        Assert.Equal("Sr.No.", texts[1]);
        Assert.Equal("1", texts[2]);
        Assert.Equal("2", texts[3]);
        Assert.Equal(string.Empty, texts[4]);            // spacer
        Assert.Equal("Total Licences", texts[5]);
        Assert.Equal("Sr.No.", texts[6]);
        Assert.Equal("1", texts[7]);                     // numbering restarts per section
        Assert.Equal(string.Empty, texts[8]);            // spacer
        Assert.Equal("Total USD Value: 1,234.5678", texts[9]);

        // One header row per section, and only per section.
        Assert.Equal(2, doc.Descendants(ns + "row").Count(row =>
            row.Elements(ns + "c").Any(c => c.Descendants(ns + "t").Any(t => t.Value == "Sr.No."))));
        AssertRowIndexesAreSane(doc);
    }

    [Fact]
    public void The_new_cell_formats_use_styles_that_exist()
    {
        var layout = new ExcelReportLayout
        {
            Columns =
            [
                ExcelColumn.Timestamp<SectionRow>("Created", _ => new DateTime(2026, 2, 1, 8, 30, 0)),
                ExcelColumn.Money4<SectionRow>("Total Value", row => row.TotalValue),
                ExcelColumn.Integer<SectionRow>("Total Amount", _ => 18000m),
            ],
        };

        using var ms = new MemoryStream();
        using (var writer = new StreamingExcelWriter(ms, "Formats", layout))
        {
            writer.AppendRows(new[] { new SectionRow { TotalValue = 1.2345m } });
            // The TOTAL row's whole-number cell has its own bold "#,##0" style.
            writer.AppendFooterRows(
            [
                new ExcelFooterRow([new ExcelFooterCell("TOTAL"), null, new ExcelFooterCell(18000m, ExcelCellFormat.Integer)]),
            ]);
            writer.Finish();
        }

        using var archive = new ZipArchive(new MemoryStream(ms.ToArray()), ZipArchiveMode.Read);
        var styles = XDocument.Load(archive.GetEntry("xl/styles.xml")!.Open());
        var stylesNs = styles.Root!.Name.Namespace;
        var xfCount = styles.Descendants(stylesNs + "cellXfs").Single().Elements().Count();

        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;
        var rows = doc.Descendants(ns + "row").ToList();
        var cells = rows[^2].Elements(ns + "c").ToList();
        var footerCells = rows[^1].Elements(ns + "c").ToList();

        Assert.Equal("9", cells[0].Attribute("s")?.Value);
        Assert.Equal("10", cells[1].Attribute("s")?.Value);
        Assert.Equal("11", cells[2].Attribute("s")?.Value);
        Assert.Equal("18000", cells[2].Element(ns + "v")?.Value);
        Assert.Equal("12", footerCells[2].Attribute("s")?.Value);
        Assert.Equal("18000", footerCells[2].Element(ns + "v")?.Value);
        Assert.InRange(12, 0, xfCount - 1);

        // Style 11/12 must be the "#,##0" number format (numFmtId 168), not the general one.
        var xfs = styles.Descendants(stylesNs + "cellXfs").Single().Elements().ToList();
        Assert.Equal("168", xfs[11].Attribute("numFmtId")?.Value);
        Assert.Equal("168", xfs[12].Attribute("numFmtId")?.Value);
        Assert.Contains(
            styles.Descendants(stylesNs + "numFmt"),
            fmt => fmt.Attribute("numFmtId")?.Value == "168" && fmt.Attribute("formatCode")?.Value == "#,##0");

        // Every count attribute still matches its element count.
        foreach (var name in new[] { "numFmts", "fonts", "fills", "borders", "cellStyleXfs", "cellXfs" })
        {
            var element = styles.Descendants(stylesNs + name).Single();
            Assert.Equal(element.Elements().Count().ToString(), element.Attribute("count")?.Value);
        }
    }

    private static void AssertRowIndexesAreSane(XDocument sheet)
    {
        var ns = sheet.Root!.Name.Namespace;
        var expected = 1;

        foreach (var row in sheet.Descendants(ns + "row"))
        {
            var rowNumber = row.Attribute("r")!.Value;
            Assert.Equal(expected.ToString(), rowNumber);

            foreach (var cell in row.Elements(ns + "c"))
            {
                var reference = cell.Attribute("r")!.Value;
                Assert.Equal(rowNumber, new string(reference.SkipWhile(char.IsLetter).ToArray()));
            }

            expected++;
        }
    }

    /// <summary>
    /// 2026-09-17, ငွေစာရင်း: "ဒသမနောက် လေးလုံး မထည့်ပေးပါနှင့်". Deducted Fees now
    /// carries the as-stored format, so the sheet must say 0.#### — which is what makes
    /// Excel print 3000 as "3000" rather than "3000.0000". The bold TOTAL under it has
    /// to use the same number format or the column disagrees with its own total.
    /// </summary>
    [Fact]
    public void Ngwe_sayin_amounts_are_written_with_the_as_stored_number_format()
    {
        var bytes = WriteWithLayout([[AccountRow(1, 3000), AccountRow(2, 3000.5)]]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var styles = ReadStyles(archive);
        var ns = styles.Root!.Name.Namespace;

        var asStored = styles.Descendants(ns + "numFmt")
            .Single(fmt => fmt.Attribute("formatCode")?.Value == "0.####")
            .Attribute("numFmtId")!.Value;

        // Every cellXfs index that renders as-stored, body and bold total alike.
        var asStoredStyles = styles.Descendants(ns + "cellXfs").Single().Elements(ns + "xf")
            .Select((xf, index) => (xf, index))
            .Where(entry => entry.xf.Attribute("numFmtId")?.Value == asStored)
            .Select(entry => entry.index.ToString())
            .ToHashSet();

        Assert.Equal(2, asStoredStyles.Count);

        var doc = ReadSheet(archive, 1);
        var sheetNs = doc.Root!.Name.Namespace;
        var rows = doc.Descendants(sheetNs + "row").ToList();

        // Find Deducted Fees by its header rather than by a hard-coded letter, so
        // inserting a column ahead of it does not silently make this test vacuous.
        var amountColumn = rows[1].Elements(sheetNs + "c")
            .Single(cell => cell.Descendants(sheetNs + "t").FirstOrDefault()?.Value == "Deducted Fees")
            .Attribute("r")!.Value;
        amountColumn = new string(amountColumn.TakeWhile(char.IsLetter).ToArray());

        var written = rows
            .SelectMany(row => row.Elements(sheetNs + "c"))
            .Where(cell =>
                new string(cell.Attribute("r")!.Value.TakeWhile(char.IsLetter).ToArray()) == amountColumn)
            .Where(cell => asStoredStyles.Contains(cell.Attribute("s")?.Value ?? "0"))
            .Select(cell => cell.Element(sheetNs + "v")?.Value)
            .ToList();

        // The two data cells plus the TOTAL, all through the as-stored styles, and
        // written as raw numbers so Excel itself does the formatting.
        Assert.Equal(["3000", "3000.5", "6000.5"], written);
    }

    /// <summary>
    /// Excel calls a workbook corrupt when a count attribute disagrees with the number
    /// of elements it introduces, and the style indexes are hand-numbered constants. A
    /// wrong count only shows up when a customer opens the file, so it is pinned here.
    /// </summary>
    [Fact]
    public void Style_counts_match_the_elements_they_introduce()
    {
        var bytes = WriteWithLayout([[AccountRow(1, 1000)]]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var styles = ReadStyles(archive);
        var ns = styles.Root!.Name.Namespace;

        foreach (var name in new[] { "numFmts", "fonts", "fills", "borders", "cellStyleXfs", "cellXfs" })
        {
            var element = styles.Descendants(ns + name).Single();
            Assert.Equal(
                element.Attribute("count")?.Value,
                element.Elements().Count().ToString());
        }
    }

    // ---- Grouped table (Company Profile, 2026-09-25): banded header, merged groups ----

    private sealed class GroupRow
    {
        public string CompanyId { get; init; } = string.Empty;
        public string Company { get; init; } = string.Empty;
        public string Director { get; init; } = string.Empty;
        public string Nrc { get; init; } = string.Empty;
    }

    private static GroupRow Director(string companyId, string director, string? company = null)
        => new() { CompanyId = companyId, Company = company ?? $"Company {companyId}", Director = director, Nrc = $"NRC-{director}" };

    /// <summary>No | Company | [Board of Director: Name | NRC No.] | Title, grouped by company.</summary>
    private static ExcelReportLayout GroupedLayout() => new()
    {
        RowGroupKey = row => ((GroupRow)row).CompanyId,
        Columns =
        [
            ExcelColumn.RowNumber("No").MergedWithinRowGroup(),
            ExcelColumn.WrappedText<GroupRow>("Company", row => row.Company, 30).MergedWithinRowGroup(),
            ExcelColumn.WrappedText<GroupRow>("Name", row => row.Director, 20).WithGroupHeader("Board of Director"),
            ExcelColumn.WrappedText<GroupRow>("NRC No.", row => row.Nrc, 18).WithGroupHeader("Board of Director"),
            ExcelColumn.WrappedText<GroupRow>("Title", _ => "Director", 12),
        ],
    };

    private static byte[] WriteGrouped(
        IEnumerable<IReadOnlyList<GroupRow>> chunks,
        ExcelReportLayout? layout = null,
        int maxRowsPerSheet = 1_048_576,
        Action<StreamingExcelWriter>? beforeFinish = null)
    {
        using var ms = new MemoryStream();
        using (var writer = new StreamingExcelWriter(ms, "Company Profile", layout ?? GroupedLayout(), maxRowsPerSheet))
        {
            foreach (var chunk in chunks)
            {
                writer.AppendRows(chunk);
            }

            beforeFinish?.Invoke(writer);
            writer.Finish();
        }

        return ms.ToArray();
    }

    private static XElement Cell(XDocument sheet, string reference)
    {
        var ns = sheet.Root!.Name.Namespace;
        return sheet.Descendants(ns + "c").Single(c => c.Attribute("r")?.Value == reference);
    }

    private static string CellText(XDocument sheet, string reference)
    {
        var cell = Cell(sheet, reference);
        var ns = sheet.Root!.Name.Namespace;
        return cell.Descendants(ns + "t").FirstOrDefault()?.Value ?? cell.Element(ns + "v")?.Value ?? string.Empty;
    }

    private static List<string> MergeRefs(XDocument sheet)
    {
        var ns = sheet.Root!.Name.Namespace;
        return sheet.Descendants(ns + "mergeCell").Select(m => m.Attribute("ref")!.Value).ToList();
    }

    [Fact]
    public void A_banded_header_is_two_rows_with_the_band_merged_across_its_columns()
    {
        var bytes = WriteGrouped([[Director("1", "A")]]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);

        Assert.Equal("No", CellText(doc, "A1"));
        Assert.Equal("Company", CellText(doc, "B1"));
        Assert.Equal("Board of Director", CellText(doc, "C1"));
        Assert.Equal(string.Empty, CellText(doc, "D1"));
        Assert.Equal("Title", CellText(doc, "E1"));
        Assert.Equal("Name", CellText(doc, "C2"));
        Assert.Equal("NRC No.", CellText(doc, "D2"));
        Assert.Equal(string.Empty, CellText(doc, "A2"));

        // Every cell under a merge is still written, styled, so the grid lines draw.
        var ns = doc.Root!.Name.Namespace;
        foreach (var cell in doc.Descendants(ns + "row").Take(2).SelectMany(row => row.Elements(ns + "c")))
        {
            Assert.Equal("25", cell.Attribute("s")?.Value);
        }

        Assert.Equal(["A1:A2", "B1:B2", "C1:D1", "E1:E2"], MergeRefs(doc));

        // The pane freezes below BOTH header rows.
        var pane = doc.Descendants(ns + "pane").Single();
        Assert.Equal("2", pane.Attribute("ySplit")?.Value);
        Assert.Equal("A3", pane.Attribute("topLeftCell")?.Value);
        AssertRowIndexesAreSane(doc);
    }

    [Fact]
    public void A_grouped_table_prints_landscape_on_one_page_width_repeating_its_header_rows()
    {
        var layout = GroupedLayout().With(headerBlock: [ExcelHeaderLine.Heading("Company Profile")]);
        var bytes = WriteGrouped([[Director("1", "A")]], layout);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;

        // CT_Worksheet order: sheetPr first, pageMargins + pageSetup after mergeCells.
        var children = doc.Root!.Elements().Select(e => e.Name.LocalName).ToList();
        Assert.Equal(["sheetPr", "sheetViews", "cols", "sheetData", "mergeCells", "pageMargins", "pageSetup"], children);
        Assert.Equal("1", doc.Descendants(ns + "pageSetUpPr").Single().Attribute("fitToPage")?.Value);

        var setup = doc.Descendants(ns + "pageSetup").Single();
        Assert.Equal("landscape", setup.Attribute("orientation")?.Value);
        Assert.Equal("1", setup.Attribute("fitToWidth")?.Value);
        Assert.Equal("0", setup.Attribute("fitToHeight")?.Value);

        // Rows 2-3: the two header rows under the one heading line.
        var workbook = XDocument.Load(archive.GetEntry("xl/workbook.xml")!.Open());
        var printTitles = workbook.Descendants(workbook.Root!.Name.Namespace + "definedName").Single();
        Assert.Equal("_xlnm.Print_Titles", printTitles.Attribute("name")?.Value);
        Assert.Equal("0", printTitles.Attribute("localSheetId")?.Value);
        Assert.Equal("'Company Profile'!$2:$3", printTitles.Value);
    }

    [Fact]
    public void A_plain_layout_gets_no_print_setup_or_defined_names()
    {
        var bytes = WriteWithLayout([[AccountRow(1, 10)]]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;

        Assert.Empty(doc.Descendants(ns + "sheetPr"));
        Assert.Empty(doc.Descendants(ns + "pageSetup"));
        using var reader = new StreamReader(archive.GetEntry("xl/workbook.xml")!.Open());
        Assert.DoesNotContain("definedNames", reader.ReadToEnd());
    }

    [Fact]
    public void Grouped_rows_merge_the_company_cells_across_a_chunk_boundary_and_number_the_groups()
    {
        // Company 1's three directors arrive split over two chunks.
        var bytes = WriteGrouped(
        [
            [Director("1", "A"), Director("1", "B")],
            [Director("1", "C"), Director("2", "D")],
        ]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);

        Assert.Equal("1", CellText(doc, "A3"));
        Assert.Equal("Company 1", CellText(doc, "B3"));
        Assert.Equal("2", CellText(doc, "A6"));
        Assert.Equal("Company 2", CellText(doc, "B6"));

        // Continuation rows: an empty, styled cell in each merged column; directors printed.
        foreach (var reference in new[] { "A4", "A5", "B4", "B5" })
        {
            var cell = Cell(doc, reference);
            Assert.Empty(cell.Elements());
            Assert.NotNull(cell.Attribute("s"));
        }

        Assert.Equal("C", CellText(doc, "C5"));
        Assert.Equal("24", Cell(doc, "A3").Attribute("s")?.Value);
        Assert.Equal("23", Cell(doc, "B3").Attribute("s")?.Value);

        // Company 2 has one director: nothing to merge.
        Assert.Equal(["A1:A2", "B1:B2", "C1:D1", "E1:E2", "A3:A5", "B3:B5"], MergeRefs(doc));
        AssertRowIndexesAreSane(doc);
    }

    [Fact]
    public void A_group_split_by_a_sheet_rollover_reprints_its_values_and_number()
    {
        // 4 rows per sheet = 2 header rows + 2 data rows; company 1 has three directors.
        var bytes = WriteGrouped(
            [[Director("1", "A"), Director("1", "B"), Director("1", "C"), Director("2", "D")]],
            maxRowsPerSheet: 4);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var sheet1 = ReadSheet(archive, 1);
        var sheet2 = ReadSheet(archive, 2);

        Assert.Contains("A3:A4", MergeRefs(sheet1));
        Assert.DoesNotContain(MergeRefs(sheet1), reference => reference.EndsWith("5"));

        // Sheet 2 repeats both header rows, then re-prints company 1 with the same "No".
        Assert.Equal("Board of Director", CellText(sheet2, "C1"));
        Assert.Equal("Name", CellText(sheet2, "C2"));
        Assert.Equal("1", CellText(sheet2, "A3"));
        Assert.Equal("Company 1", CellText(sheet2, "B3"));
        Assert.Equal("C", CellText(sheet2, "C3"));
        Assert.Equal("2", CellText(sheet2, "A4"));

        AssertRowIndexesAreSane(sheet1);
        AssertRowIndexesAreSane(sheet2);
    }

    [Fact]
    public void Wrapped_text_keeps_its_line_breaks_and_stays_a_string()
    {
        var layout = new ExcelReportLayout
        {
            Columns = [ExcelColumn.WrappedText<GroupRow>("EIR No. & Date", row => row.Company, 24)],
        };

        var bytes = WriteGrouped(
            [[Director("1", "A", "138468097\n1-8-2026 to 31-7-2031"), Director("2", "B", "138468097")]],
            layout);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;

        var multiLine = Cell(doc, "A2");
        Assert.Equal("inlineStr", multiLine.Attribute("t")?.Value);
        Assert.Equal("23", multiLine.Attribute("s")?.Value);
        Assert.Equal("138468097\n1-8-2026 to 31-7-2031", multiLine.Descendants(ns + "t").Single().Value);
        Assert.Equal(
            "preserve",
            multiLine.Descendants(ns + "t").Single().Attribute(XNamespace.Xml + "space")?.Value);

        // A number-looking EIR no is text, not a number Excel would reformat.
        Assert.Equal("inlineStr", Cell(doc, "A3").Attribute("t")?.Value);

        // The break is an entity in the file, so no newline handling can drop it.
        using var reader = new StreamReader(archive.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        Assert.Contains("138468097&#xA;1-8-2026", reader.ReadToEnd());
    }

    [Fact]
    public void A_groups_merged_text_sets_the_row_heights_that_excel_will_not_auto_fit()
    {
        // Three lines of company text (45pt) merged over two single-line director rows.
        var bytes = WriteGrouped(
        [
            [Director("1", "A", "NAME\n138468097\n(25/08/2023)"), Director("1", "B"), Director("2", "C", "ONE LINE")],
        ]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;
        var rows = doc.Descendants(ns + "row").ToDictionary(row => row.Attribute("r")!.Value);

        Assert.Equal("22.5", rows["3"].Attribute("ht")?.Value);
        Assert.Equal("1", rows["3"].Attribute("customHeight")?.Value);
        Assert.Equal("22.5", rows["4"].Attribute("ht")?.Value);

        // A group that fits on the default height keeps it.
        Assert.Null(rows["5"].Attribute("ht"));
    }

    [Fact]
    public void Footer_rows_follow_the_buffered_last_group_and_are_never_merged()
    {
        long rowsBeforeFinish = 0;
        var bytes = WriteGrouped(
            [[Director("1", "A"), Director("1", "B")]],
            beforeFinish: writer =>
            {
                // The footer builder reads TotalDataRows before Finish, while the last
                // group is still buffered.
                rowsBeforeFinish = writer.TotalDataRows;
                writer.AppendFooterRows([new ExcelFooterRow([new ExcelFooterCell("TOTAL")])]);
            });

        Assert.Equal(2, rowsBeforeFinish);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var doc = ReadSheet(archive, 1);

        Assert.Equal("Company 1", CellText(doc, "B3"));
        Assert.Equal("TOTAL", CellText(doc, "A5"));
        Assert.Contains("A3:A4", MergeRefs(doc));
        Assert.DoesNotContain(MergeRefs(doc), reference => reference.Contains('5'));
        AssertRowIndexesAreSane(doc);
    }

    [Fact]
    public void Grouping_needs_a_single_grid_with_explicit_columns()
    {
        var sectioned = new ExcelReportLayout
        {
            RowGroupKey = row => row,
            Sections = [new ExcelReportSection { Columns = [ExcelColumn.RowNumber()] }],
        };
        var columnless = new ExcelReportLayout { RowGroupKey = row => row };
        var bandedSection = new ExcelReportLayout
        {
            Sections =
            [
                new ExcelReportSection
                {
                    Columns = [ExcelColumn.Text<GroupRow>("Name", row => row.Director).WithGroupHeader("Board")],
                },
            ],
        };

        foreach (var layout in new[] { sectioned, columnless, bandedSection })
        {
            Assert.Throws<ArgumentException>(() => new StreamingExcelWriter(new MemoryStream(), "X", layout));
        }
    }

    [Fact]
    public void The_grouped_table_styles_are_appended_without_moving_the_existing_ones()
    {
        var bytes = WriteGrouped([[Director("1", "A")]]);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var styles = ReadStyles(archive);
        var ns = styles.Root!.Name.Namespace;
        var xfs = styles.Descendants(ns + "cellXfs").Single().Elements(ns + "xf").ToList();

        Assert.Equal(27, xfs.Count);
        Assert.All(xfs.Take(23), xf => Assert.Equal("0", xf.Attribute("borderId")?.Value));
        Assert.All(xfs.Skip(23), xf => Assert.Equal("1", xf.Attribute("borderId")?.Value));
        Assert.Equal("1", xfs[23].Element(ns + "alignment")?.Attribute("wrapText")?.Value);
        Assert.Equal("top", xfs[23].Element(ns + "alignment")?.Attribute("vertical")?.Value);
        Assert.Equal("top", xfs[24].Element(ns + "alignment")?.Attribute("vertical")?.Value);
        Assert.Equal("2", xfs[25].Attribute("fontId")?.Value);
        Assert.Equal("center", xfs[26].Element(ns + "alignment")?.Attribute("horizontal")?.Value);
        Assert.Equal(2, styles.Descendants(ns + "border").Count());
    }

    private static XDocument ReadStyles(ZipArchive archive)
    {
        using var stream = archive.GetEntry("xl/styles.xml")!.Open();
        return XDocument.Load(stream);
    }

    private static XDocument ReadSheet(ZipArchive archive, int index)
    {
        var entry = archive.GetEntry($"xl/worksheets/sheet{index}.xml")!;
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }
}
