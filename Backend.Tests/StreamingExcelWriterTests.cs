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

        // Entry Date → a real date serial carrying the dd/mm/yyyy style.
        Assert.Null(cells[1].Attribute("t"));
        Assert.Equal(new DateTime(2026, 8, 31).ToOADate().ToString("0.##########"), cells[1].Element(ns + "v")?.Value);
        Assert.Equal("3", cells[1].Attribute("s")?.Value);

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

        // Label sits immediately left of Deducted Fees.
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

        // 4 preamble + header + 2 data + Total + MMK + TOTAL
        Assert.Equal(10, rows.Count);
        // "Total" sits in the first data column with no total of its own (Entry Date),
        // exactly where BasicTable's totalLabelIndex puts it — NOT immediately left of
        // the totalled column, which is where the legacy WriteTotalsRow used to put it.
        Assert.Equal("Total", rows[7].Elements(ns + "c").ElementAt(1).Descendants(ns + "t").First().Value);
        Assert.Equal("3001", rows[7].Elements(ns + "c").ElementAt(6).Element(ns + "v")?.Value);
        Assert.Equal(
            "MMK:2 licence(s)",
            rows[8].Elements(ns + "c").ElementAt(1).Descendants(ns + "t").First().Value);
        Assert.Equal("TOTAL", rows[9].Elements(ns + "c").First().Descendants(ns + "t").First().Value);

        // Exactly one "Total" label — the layout's own summed row must not also appear.
        Assert.Single(doc.Descendants(ns + "t").Where(t => t.Value == "Total"));
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
            ],
        };

        using var ms = new MemoryStream();
        using (var writer = new StreamingExcelWriter(ms, "Formats", layout))
        {
            writer.AppendRows(new[] { new SectionRow { TotalValue = 1.2345m } });
            writer.Finish();
        }

        using var archive = new ZipArchive(new MemoryStream(ms.ToArray()), ZipArchiveMode.Read);
        var styles = XDocument.Load(archive.GetEntry("xl/styles.xml")!.Open());
        var stylesNs = styles.Root!.Name.Namespace;
        var xfCount = styles.Descendants(stylesNs + "cellXfs").Single().Elements().Count();

        var doc = ReadSheet(archive, 1);
        var ns = doc.Root!.Name.Namespace;
        var cells = doc.Descendants(ns + "row").Last().Elements(ns + "c").ToList();

        Assert.Equal("9", cells[0].Attribute("s")?.Value);
        Assert.Equal("10", cells[1].Attribute("s")?.Value);
        Assert.InRange(10, 0, xfCount - 1);

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

    private static XDocument ReadSheet(ZipArchive archive, int index)
    {
        var entry = archive.GetEntry($"xl/worksheets/sheet{index}.xml")!;
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }
}
