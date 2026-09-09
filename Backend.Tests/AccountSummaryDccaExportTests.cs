using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using API.Service.ExcelExport;
using API.Service.ExcelExport.Dcca;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Tests;

/// <summary>
/// The DCCA export must reproduce the old Tradenet 2.0 file, because DCCA's importer is keyed
/// to it. These tests pin real bytes, not a layout description.
///
/// The headline test (<see cref="Replaying_the_legacy_rows_reproduces_every_part_byte_for_byte"/>)
/// parses the 543 data rows out of the real 2021 export, feeds them back through the new writer,
/// and requires all ten parts to be SHA-256-identical. It is skipped unless the legacy file is
/// available, because that file holds real company names and voucher numbers and is deliberately
/// NOT committed — point <c>TRADENET_DCCA_LEGACY_XLSX</c> at it to run the test. The always-on
/// tests below pin the same guarantee from the other side: the embedded skeleton's eight static
/// parts against a committed hash manifest.
/// </summary>
public sealed class AccountSummaryDccaExportTests
{
    private const string LegacyPathVariable = "TRADENET_DCCA_LEGACY_XLSX";

    private static readonly string[] ExpectedEntryOrder =
    [
        "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels",
        "xl/theme/theme1.xml", "xl/styles.xml", "xl/worksheets/sheet1.xml",
        "docProps/core.xml", "docProps/app.xml", "xl/sharedStrings.xml",
    ];

    // ---- helpers ----

    private static async Task<byte[]> WriteAsync(IEnumerable<DccaRow> rows)
    {
        await using var writer = new DccaWorkbookWriter();
        foreach (var row in rows)
        {
            await writer.AppendRowAsync(row, CancellationToken.None);
        }

        using var output = new MemoryStream();
        await writer.FinishAsync(output, CancellationToken.None);
        return output.ToArray();
    }

    private static Dictionary<string, byte[]> Parts(byte[] xlsx)
    {
        using var archive = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        var parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            parts[entry.FullName] = buffer.ToArray();
        }

        return parts;
    }

    private static string[] EntryNames(byte[] xlsx)
    {
        using var archive = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read);
        return archive.Entries.Select(entry => entry.FullName).ToArray();
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>
    /// Loads from a stream, not a string: sheet1.xml carries a UTF-8 BOM (as the legacy file
    /// does), and <c>XDocument.Parse</c> rejects a leading U+FEFF.
    /// </summary>
    private static XDocument Xml(byte[] bytes) => XDocument.Load(new MemoryStream(bytes));

    // ---- the headline byte-for-byte test ----

    [Fact]
    public async Task Replaying_the_legacy_rows_reproduces_every_part_byte_for_byte()
    {
        var legacyPath = Environment.GetEnvironmentVariable(LegacyPathVariable);
        if (string.IsNullOrWhiteSpace(legacyPath) || !File.Exists(legacyPath))
        {
            // Not committed: real company names and voucher numbers.
            return;
        }

        var legacy = Parts(await File.ReadAllBytesAsync(legacyPath));
        var rows = LegacyRows(legacy);

        // Sanity: the file we were pointed at really is the 543-row export.
        Assert.Equal(543, rows.Count);

        var generated = Parts(await WriteAsync(rows));

        Assert.Equal(ExpectedEntryOrder, EntryNames(await WriteAsync(rows)));

        foreach (var name in ExpectedEntryOrder)
        {
            Assert.True(generated.ContainsKey(name), $"The generated workbook has no '{name}' part.");
            Assert.Equal(Sha256(legacy[name]), Sha256(generated[name]));
        }
    }

    /// <summary>
    /// Reads the nine cell values back out of the legacy sheet. Feeding the old file's own values
    /// through the writer makes this a true round trip: any difference is the writer's.
    /// </summary>
    private static List<DccaRow> LegacyRows(Dictionary<string, byte[]> legacy)
    {
        var sst = Xml(legacy["xl/sharedStrings.xml"]);
        var ns = sst.Root!.Name.Namespace;
        var strings = sst.Root.Elements(ns + "si")
            .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value)))
            .ToList();

        var sheet = Xml(legacy["xl/worksheets/sheet1.xml"]);
        var sheetNs = sheet.Root!.Name.Namespace;

        var rows = new List<DccaRow>();
        foreach (var row in sheet.Descendants(sheetNs + "row").Skip(1))
        {
            var cells = row.Elements(sheetNs + "c").ToList();

            string Value(int index)
            {
                var cell = cells[index];
                var raw = cell.Element(sheetNs + "v")?.Value ?? string.Empty;
                return cell.Attribute("t")?.Value == "s" ? strings[int.Parse(raw, CultureInfo.InvariantCulture)] : raw;
            }

            rows.Add(new DccaRow(
                EntryDate: Value(1),
                HtaThaKaNo: Value(2),
                CompanyName: Value(3),
                TransactionTitle: Value(4),
                Amount: double.Parse(Value(5), CultureInfo.InvariantCulture),
                AccountCode: Value(7),
                LocationCode: Value(8)));
        }

        return rows;
    }

    // ---- the skeleton ----

    [Fact]
    public void The_embedded_skeleton_is_valid()
        => Assert.Empty(DccaWorkbookSkeleton.Validate());

    [Fact]
    public async Task The_static_parts_match_the_legacy_export_hashes()
    {
        // The other half of the byte-for-byte guarantee, and this half always runs: whatever
        // rows are exported, these eight parts are copied through untouched.
        var manifest = await File.ReadAllLinesAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dcca", "legacy-static-parts.sha256"));

        var expected = manifest
            .Where(line => !line.StartsWith('#') && line.Trim().Length > 0)
            .Select(line => line.Split("  ", 2))
            .ToDictionary(parts => parts[1].Trim(), parts => parts[0].Trim(), StringComparer.Ordinal);

        Assert.Equal(8, expected.Count);

        var generated = Parts(await WriteAsync([]));
        foreach (var (name, hash) in expected)
        {
            Assert.Equal(hash, Sha256(generated[name]));
        }
    }

    [Fact]
    public async Task A_zero_row_export_still_emits_the_string_table()
    {
        // workbook.xml.rels points an rId at sharedStrings.xml and [Content_Types].xml has an
        // Override for it, so omitting the part when there is no data would leave a dangling
        // relationship and Excel would offer to repair the file.
        var parts = Parts(await WriteAsync([]));

        Assert.True(parts.ContainsKey("xl/sharedStrings.xml"));

        var sst = Text(parts["xl/sharedStrings.xml"]);
        Assert.Contains("count=\"9\" uniqueCount=\"9\"", sst);
        Assert.Contains("<si><t>Transation Title</t></si>", sst);
        Assert.Contains("<dimension ref=\"A1:I1\" />", Text(parts["xl/worksheets/sheet1.xml"]));
    }

    [Fact]
    public async Task The_dropped_parts_stay_dropped_and_no_relationship_dangles()
    {
        var generated = await WriteAsync([SampleRow(1)]);

        Assert.Equal(ExpectedEntryOrder, EntryNames(generated));

        var parts = Parts(generated);
        Assert.DoesNotContain("xl/printerSettings/printerSettings1.bin", parts.Keys);
        Assert.DoesNotContain("xl/worksheets/_rels/sheet1.xml.rels", parts.Keys);

        // Dropping them is only legal because <pageSetup> lost its r:id.
        Assert.DoesNotContain("r:id", Text(parts["xl/worksheets/sheet1.xml"]));
    }

    // ---- row geometry and cell values ----

    private static DccaRow SampleRow(int n) => new(
        EntryDate: "09/01/2021",
        HtaThaKaNo: $"14929075{n}@U0920210000{n}",
        CompanyName: $"Company {n}",
        TransactionTitle: "Application Form Fees",
        Amount: 3000,
        AccountCode: "002",
        LocationCode: "NPT");

    [Fact]
    public async Task Row_two_is_absent_and_data_starts_at_row_three()
    {
        var parts = Parts(await WriteAsync([SampleRow(1), SampleRow(2)]));
        var sheet = Xml(parts["xl/worksheets/sheet1.xml"]);
        var ns = sheet.Root!.Name.Namespace;

        var rowNumbers = sheet.Descendants(ns + "row")
            .Select(row => row.Attribute("r")!.Value)
            .ToArray();

        // The old loop is `int row = 2; foreach (item) { row++; … }`, so there is no row 2 at
        // all — not an empty one.
        Assert.Equal(["1", "3", "4"], rowNumbers);
        Assert.Contains("<dimension ref=\"A1:I4\" />", Text(parts["xl/worksheets/sheet1.xml"]));
    }

    [Fact]
    public async Task Every_cell_carries_the_legacy_style_and_type()
    {
        var parts = Parts(await WriteAsync([SampleRow(1)]));
        var sheet = Xml(parts["xl/worksheets/sheet1.xml"]);
        var ns = sheet.Root!.Name.Namespace;
        var cells = sheet.Descendants(ns + "row").Last().Elements(ns + "c").ToList();

        Assert.Equal(9, cells.Count);

        // A and F are numeric and unstyled; B carries the column's own Text style; the rest are
        // shared strings at style 0.
        var expected = new[] { "0", "3", "0", "0", "0", "0", "0", "0", "0" };
        var expectedTypes = new[] { null, "s", "s", "s", "s", null, "s", "s", "s" };

        for (var i = 0; i < 9; i++)
        {
            Assert.Equal(expected[i], cells[i].Attribute("s")?.Value);
            Assert.Equal(expectedTypes[i], cells[i].Attribute("t")?.Value);
        }
    }

    [Fact]
    public async Task The_string_table_is_built_in_first_use_order_and_interns_the_empty_remark()
    {
        var parts = Parts(await WriteAsync([SampleRow(1)]));
        var sst = Xml(parts["xl/sharedStrings.xml"]);
        var ns = sst.Root!.Name.Namespace;
        var strings = sst.Root.Elements(ns + "si")
            .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value)))
            .ToList();

        // 0-8 are the headers in column order; then each new value as the row is scanned
        // left to right. The empty Remark becomes a real entry, as it did in the old file.
        Assert.Equal(
            ["No", "Entry Date", "HtaThaKa No", "Company Name", "Transation Title",
             "Deducted Fees", "Remark", "Account Code", "Location Code",
             "09/01/2021", "149290751@U09202100001", "Company 1", "Application Form Fees",
             "", "002", "NPT"],
            strings);

        // count == uniqueCount, as EPPlus wrote both.
        Assert.Contains("count=\"16\" uniqueCount=\"16\"", Text(parts["xl/sharedStrings.xml"]));
    }

    [Fact]
    public async Task Repeated_values_collapse_to_one_string_entry()
    {
        var parts = Parts(await WriteAsync([SampleRow(1), SampleRow(1), SampleRow(1)]));
        var sst = Xml(parts["xl/sharedStrings.xml"]);
        var ns = sst.Root!.Name.Namespace;

        // Three identical rows add nothing beyond the first row's six new strings.
        Assert.Equal(16, sst.Root!.Elements(ns + "si").Count());
    }

    [Fact]
    public async Task Two_exports_of_the_same_data_are_byte_identical()
    {
        // Entry timestamps are pinned, so the file is reproducible — which is what "byte
        // identical" means in practice for the customer's QA.
        var first = await WriteAsync([SampleRow(1), SampleRow(2)]);
        var second = await WriteAsync([SampleRow(1), SampleRow(2)]);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task The_row_cap_fails_loudly_instead_of_writing_a_corrupt_sheet()
    {
        await using var writer = new DccaWorkbookWriter();

        var field = typeof(DccaWorkbookWriter)
            .GetField("_dataRows", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(writer, (long)DccaWorkbookWriter.MaxDataRows);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await writer.AppendRowAsync(SampleRow(1), CancellationToken.None));

        Assert.Contains("row limit", error.Message);
    }

    // ---- the encoder, against the rules recovered from EPPlus.dll ----

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("A & B", "A &amp; B")]
    [InlineData("a<b>c", "a&lt;b&gt;c")]
    // EPPlus escapes neither the apostrophe nor the quote — the real export has one raw ' and
    // zero &apos;.
    [InlineData("O'Brien \"x\"", "O'Brien \"x\"")]
    // A literal that already looks like an escape is doubled, so it round-trips.
    [InlineData("_x0041_", "_x005F_x0041_")]
    // Control characters become _x00NN_; tab and newline are legal text and stay.
    [InlineData("ab", "a_x0001_b")]
    [InlineData("a\tb", "a\tb")]
    public void The_encoder_matches_EPPlus(string input, string expected)
        => Assert.Equal(expected, DccaXmlText.Encode(input));

    [Theory]
    [InlineData("plain", false)]
    [InlineData(" leading", true)]
    [InlineData("trailing ", true)]
    [InlineData("Shwe Htut Khaung Co., Ltd.        ", true)]
    [InlineData("HONEYS  GARMENT  INDUSTRY  LIMITED", true)]   // interior double space
    [InlineData("single space inside", false)]
    [InlineData("a\tb", true)]
    [InlineData("", false)]
    public void The_preserve_predicate_matches_EPPlus(string input, bool expected)
        => Assert.Equal(expected, DccaXmlText.NeedsSpacePreserved(input));

    [Theory]
    [InlineData(3000d, "3000")]
    [InlineData(10000d, "10000")]
    [InlineData(250000.5d, "250000.5")]
    // The old chain was float -> .NET Framework double.ToString() (G15) -> decimal.Parse, so a
    // float artefact collapsed. .NET Core's default shortest-round-trip would emit the artefact.
    [InlineData(1234.5600000000001d, "1234.56")]
    [InlineData(0d, "0")]
    public void The_amount_reproduces_the_legacy_G15_round_trip(double input, string expected)
        => Assert.Equal(expected, DccaXmlText.Number(input));

    // ---- the seam ----

    [Fact]
    public void The_controller_claims_only_the_dcca_variant()
    {
        var controller = new AccountSummaryReportController(null!, null!);

        Assert.True(controller.CanWriteCustomExcel(Request("Dcca")));
        Assert.True(controller.CanWriteCustomExcel(Request("dcca")));
        Assert.True(controller.CanWriteCustomExcel(Request("DCCA")));

        Assert.False(controller.CanWriteCustomExcel(Request(null)));
        Assert.False(controller.CanWriteCustomExcel(Request("")));
        Assert.False(controller.CanWriteCustomExcel(Request("Standard")));
    }

    [Fact]
    public void The_custom_writer_members_are_NonAction()
    {
        // ApiController routing rejects a public method that is not an action, exactly as for
        // WriteRowsAsync and GetExcelLayout.
        foreach (var name in new[]
                 {
                     nameof(ICustomExcelWriter.CanWriteCustomExcel),
                     nameof(ICustomExcelWriter.WriteCustomExcelAsync),
                 })
        {
            var method = typeof(AccountSummaryReportController).GetMethod(name, [typeof(object), .. name.EndsWith("Async") ? new[] { typeof(ExcelExportContext) } : Type.EmptyTypes]);
            Assert.NotNull(method);
            Assert.NotNull(method!.GetCustomAttributes(typeof(NonActionAttribute), false).SingleOrDefault());
        }
    }

    [Fact]
    public void The_standard_layout_is_unchanged_and_serves_every_non_dcca_request()
    {
        // The DCCA variant no longer has a layout at all — it is written by the custom writer.
        // GetExcelLayout returns the standard sheet for ANY request, deliberately, so a seam
        // regression is a wrong file a test catches rather than an opaque failed job.
        var controller = new AccountSummaryReportController(null!, null!);

        foreach (var format in new[] { null, "Dcca" })
        {
            var layout = controller.GetExcelLayout(Request(format));

            Assert.Equal(
                ["No", "Entry Date", "Company Registration No", "Company Name",
                 "Voucher No", "Transaction Title", "Deducted Fees", "Remark"],
                layout.Columns.Select(column => column.Header));
            Assert.Equal("Total", layout.TotalsRowLabel);
        }
    }

    [Fact]
    public async Task The_dcca_variant_asks_for_no_footer_totals()
    {
        // Returning null keeps ExcelFooterBuilder from appending the grid's Total row and skips
        // a cross-page SUM the DCCA file has no use for. A null DbContext proves the database is
        // never touched on this path.
        var controller = new AccountSummaryReportController(null!, null!);

        Assert.Null(await controller.GetExcelFooterTotalsAsync(Request("Dcca"), CancellationToken.None));
    }

    private static AccountSummaryReportRequest Request(string? exportFormat) => new()
    {
        FromDate = new DateTime(2021, 9, 1),
        ToDate = new DateTime(2021, 9, 1, 23, 59, 59),
        ExportFormat = exportFormat,
    };
}
