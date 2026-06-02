using System.IO.Compression;
using System.Xml.Linq;
using API.Service.ExcelExport;

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

    private static XDocument ReadSheet(ZipArchive archive, int index)
    {
        var entry = archive.GetEntry($"xl/worksheets/sheet{index}.xml")!;
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }
}
