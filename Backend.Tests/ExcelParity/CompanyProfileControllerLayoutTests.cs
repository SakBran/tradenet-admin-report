using System.IO.Compression;
using System.Xml.Linq;
using API.Service.ExcelExport;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;

namespace Backend.Tests.ExcelParity;

/// <summary>
/// Pins the Company Profile sheet to the layout the customer sends to the 11 ministries
/// (complaint 2026-09-25, their sample image): No | Company's Name | Address |
/// EIR No. &amp; Date | Type of Organization | လုပ်ငန်းရည်ရွယ်ချက် | Capital |
/// Board of Director (Name | NRC No.) | Title, one merged block per company. The same
/// strings feed the grid (Frontend/src/Report/Page/CompanyProfile.tsx).
/// </summary>
public sealed class CompanyProfileControllerLayoutTests
{
    private static CompanyProfileRequest Request() => new()
    {
        FromDate = new DateTime(2026, 8, 1),
        ToDate = new DateTime(2026, 8, 31, 23, 59, 59),
    };

    /// <summary>The real layout the controller declares, so these tests cannot drift from it.</summary>
    private static ExcelReportLayout Layout()
        => new CompanyProfileController(null!, null!).GetExcelLayout(Request());

    [Fact]
    public void The_columns_are_the_customers_layout_with_the_director_band()
    {
        var layout = Layout();

        Assert.Equal(
            [
                "No", "Company's Name", "Address", "EIR No. & Date", "Type of Organization",
                "လုပ်ငန်းရည်ရွယ်ချက်", "Capital", "Name", "NRC No.", "Title",
            ],
            layout.Columns.Select(column => column.Header).ToArray());

        Assert.Equal(
            [null, null, null, null, null, null, null, "Board of Director", "Board of Director", null],
            layout.Columns.Select(column => column.GroupHeader).ToArray());

        // The seven company columns merge over the company's director rows; the director
        // columns and Title are per row.
        Assert.Equal(
            [true, true, true, true, true, true, true, false, false, false],
            layout.Columns.Select(column => column.MergeWithinRowGroup).ToArray());

        Assert.NotNull(layout.RowGroupKey);
        Assert.Equal(
            "company-1",
            layout.RowGroupKey!(new sp_CompanyProfileReportResult { Id = "company-1" }));

        Assert.Equal(
            ["Ministry of Commerce", "Directorate of Trade", "Company Profile (01/08/2026) To (31/08/2026)"],
            layout.TitleLines.ToArray());
    }

    [Theory]
    [InlineData(10000000d, "MMK", "K-10000000")]
    [InlineData(1000000d, null, "K-1000000")]
    [InlineData(50000d, "USD", "USD-50000")]
    [InlineData(1500.5d, "usd", "USD-1500.5")]
    [InlineData(null, "MMK", "")]
    public void Capital_reads_like_the_sample(double? capital, string? currency, string expected)
        => Assert.Equal(expected, CompanyProfileFormat.Capital(capital, currency));

    [Fact]
    public void Validity_address_and_title_read_like_the_sample()
    {
        Assert.Equal(
            "1-8-2026 to 31-7-2031",
            CompanyProfileFormat.Validity(new DateTime(2026, 8, 1), new DateTime(2031, 7, 31)));

        // A missing UnitLevel / PostalCode leaves no ", ," gap.
        Assert.Equal(
            "NO.9, GROUND FLOOR (RIGHT), PHO MYAY STREET, PHO MYAY QUARTER, MINGALAR TAUNG NYUNT TOWNSHIP, Yangon Region, MYANMAR",
            CompanyProfileFormat.Address(
                null,
                "NO.9, GROUND FLOOR (RIGHT), PHO MYAY STREET",
                "PHO MYAY QUARTER, MINGALAR TAUNG NYUNT TOWNSHIP",
                "Yangon Region",
                "MYANMAR",
                null));

        Assert.Equal("Director", CompanyProfileFormat.DirectorTitle(""));
        Assert.Equal("Director", CompanyProfileFormat.DirectorTitle(null));
        Assert.Equal("Managing Director", CompanyProfileFormat.DirectorTitle(" Managing Director "));
    }

    /// <summary>The customer's three sample companies, as PROD returned them on 2026-09-25.</summary>
    internal static IReadOnlyList<sp_CompanyProfileReportResult> SampleRows()
    {
        sp_CompanyProfileReportResult Row(
            string id, string regNo, string name, DateTime regDate,
            string? unit, string street, string quarter, double capital,
            string director, string nrc)
            => new sp_CompanyProfileReportRow
            {
                Id = id,
                CompanyRegistrationNo = regNo,
                CompanyName = name,
                CompanyRegistrationDate = regDate,
                StartDate = new DateTime(2026, 8, 1),
                EndDate = new DateTime(2031, 7, 31),
                BusinessType = "Trading",
                LineofBusiness = "Myanmar Company",
                UnitLevel = unit,
                StreetNumberStreetName = street,
                QuarterCityTownship = quarter,
                State = "Yangon Region",
                Country = "MYANMAR",
                Capital = capital,
                CapitalCurrency = "MMK",
                DirectorName = director,
                DirectorNRC = nrc,
                DirectorPosition = "",
                PermitBusiness = "",
            }.ToResult();

        const string baby = "BABY VISION COMPANY LIMITED";
        const string htwe = "MA HTWE YI & MA SAN SAN HTWE CO., LTD.";
        const string wai = "WAI PON LAR GROUP OF COMPANY LIMITED";

        return
        [
            Row("c1", "138468097", baby, new DateTime(2023, 8, 25), "NO. 6, ROOM-006, PAZUNDAUNG GARDEN HOUSING",
                "UPPER PAZUNDAUNG ROAD", "PAZUNDAUNG TOWNSHIP", 10000000, "U ZAW ZAW", "12/KATATA(N)026913"),
            Row("c1", "138468097", baby, new DateTime(2023, 8, 25), "NO. 6, ROOM-006, PAZUNDAUNG GARDEN HOUSING",
                "UPPER PAZUNDAUNG ROAD", "PAZUNDAUNG TOWNSHIP", 10000000, "DAW HNIN PHYU EI", "12/OUKAMA(N)179024"),
            Row("c2", "144076370", htwe, new DateTime(2025, 8, 21), null,
                "NO.9, GROUND FLOOR (RIGHT), PHO MYAY STREET", "PHO MYAY QUARTER, MINGALAR TAUNG NYUNT TOWNSHIP",
                10000000, "DAW TIN TIN HTWE", "12/MAGATA(N)069223"),
            Row("c2", "144076370", htwe, new DateTime(2025, 8, 21), null,
                "NO.9, GROUND FLOOR (RIGHT), PHO MYAY STREET", "PHO MYAY QUARTER, MINGALAR TAUNG NYUNT TOWNSHIP",
                10000000, "DAW SAN SAN HTWE", "12/MAGATA(N)075888"),
            Row("c3", "145327946", wai, new DateTime(2026, 1, 13), "NO.3",
                "YANGON CITY VILLAS, 8 MILES", "MAYANGONE TOWNSHIP", 1000000, "WANG XINGANG", "EN6429826"),
            Row("c3", "145327946", wai, new DateTime(2026, 1, 13), "NO.3",
                "YANGON CITY VILLAS, 8 MILES", "MAYANGONE TOWNSHIP", 1000000, "LI ZHEN", "EK8104719"),
            Row("c3", "145327946", wai, new DateTime(2026, 1, 13), "NO.3",
                "YANGON CITY VILLAS, 8 MILES", "MAYANGONE TOWNSHIP", 1000000, "DAW YIN THIRI HLAING", "9/MAHAMA(N)083479"),
        ];
    }

    /// <summary>The whole export path the worker runs: typed layout, header block, writer.</summary>
    internal static byte[] WriteSample(IReadOnlyList<sp_CompanyProfileReportResult> rows)
    {
        var layout = ExcelLayoutBuilder.WithStandardHeaderBlock(
            Layout(), null, "Company Profile", Request(), new DateTimeOffset(2026, 9, 25, 9, 0, 0, TimeSpan.Zero));

        using var ms = new MemoryStream();
        using (var writer = new StreamingExcelWriter(ms, "Company Profile", layout))
        {
            // Two chunks, splitting company 3's directors, like the worker's ChunkAsync can.
            writer.AppendRows(rows.Take(5).ToList());
            writer.AppendRows(rows.Skip(5).ToList());
            writer.Finish();
        }

        return ms.ToArray();
    }

    [Fact]
    public void The_customers_sample_exports_as_merged_company_blocks()
    {
        using var archive = new ZipArchive(new MemoryStream(WriteSample(SampleRows())), ZipArchiveMode.Read);
        XDocument doc;
        using (var stream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open())
        {
            doc = XDocument.Load(stream);
        }

        var ns = doc.Root!.Name.Namespace;
        string Text(string reference)
        {
            var cell = doc.Descendants(ns + "c").SingleOrDefault(c => c.Attribute("r")?.Value == reference);
            return cell?.Descendants(ns + "t").FirstOrDefault()?.Value ?? cell?.Element(ns + "v")?.Value ?? string.Empty;
        }

        // The column header sits under the title lines and the From/To/Exported block.
        var header = doc.Descendants(ns + "row")
            .First(row => row.Elements(ns + "c").FirstOrDefault()?.Descendants(ns + "t").FirstOrDefault()?.Value == "No");
        var h = int.Parse(header.Attribute("r")!.Value);
        var first = h + 2;

        Assert.Equal("Company Profile (01/08/2026) To (31/08/2026)", Text("A3"));
        Assert.Equal("Board of Director", Text($"H{h}"));
        Assert.Equal("Title", Text($"J{h}"));
        Assert.Equal("NRC No.", Text($"I{h + 1}"));

        Assert.Equal("1", Text($"A{first}"));
        Assert.Equal("BABY VISION COMPANY LIMITED\n138468097\n(25/08/2023)", Text($"B{first}"));
        Assert.Equal(
            "NO. 6, ROOM-006, PAZUNDAUNG GARDEN HOUSING, UPPER PAZUNDAUNG ROAD, PAZUNDAUNG TOWNSHIP, Yangon Region, MYANMAR",
            Text($"C{first}"));
        Assert.Equal("138468097\n1-8-2026 to 31-7-2031", Text($"D{first}"));
        Assert.Equal("Trading", Text($"E{first}"));
        Assert.Equal("K-10000000", Text($"G{first}"));
        Assert.Equal("U ZAW ZAW", Text($"H{first}"));
        Assert.Equal("12/KATATA(N)026913", Text($"I{first}"));
        Assert.Equal("Director", Text($"J{first}"));
        Assert.Equal("DAW HNIN PHYU EI", Text($"H{first + 1}"));

        // Company 3 spans the chunk boundary and still numbers 3, merged over its 3 rows.
        Assert.Equal("3", Text($"A{first + 4}"));
        Assert.Equal("K-1000000", Text($"G{first + 4}"));
        Assert.Equal("DAW YIN THIRI HLAING", Text($"H{first + 6}"));

        var merges = doc.Descendants(ns + "mergeCell").Select(m => m.Attribute("ref")!.Value).ToList();
        foreach (var column in "ABCDEFG")
        {
            Assert.Contains($"{column}{first}:{column}{first + 1}", merges);
            Assert.Contains($"{column}{first + 2}:{column}{first + 3}", merges);
            Assert.Contains($"{column}{first + 4}:{column}{first + 6}", merges);
        }

        Assert.Contains($"H{h}:I{h}", merges);
        Assert.DoesNotContain(merges, reference => reference.StartsWith("H") && reference != $"H{h}:I{h}");
    }
}
