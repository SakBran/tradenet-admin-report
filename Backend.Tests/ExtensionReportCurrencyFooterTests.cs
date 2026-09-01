using API.Model;
using API.Service.ExcelExport;
using API.StoredProcedureToLinq;
using System.IO.Compression;
using System.Xml.Linq;

namespace Backend.Tests;

public sealed class ExtensionReportCurrencyFooterTests
{
    [Fact]
    public void Currency_footer_rows_keep_total_values_separate_by_currency()
    {
        var footerRows = sp_ExtensionReport.CreateCurrencyFooterRows(new ReportCurrencyTotalsSummary
        {
            Currencies = new[]
            {
                new ReportCurrencyTotal { Currency = "USD", NoOfLicences = 2, TotalValue = 125.50m },
                new ReportCurrencyTotal { Currency = "MMK", NoOfLicences = 1, TotalValue = 300000m },
            },
            GrandTotalLicences = 3,
        });

        Assert.Collection(footerRows,
            usd =>
            {
                Assert.Equal("USD:2 licence(s)", usd.LicenceNo);
                Assert.Equal("USD", usd.Currency);
                Assert.Equal(125.50m, usd.Amount);
            },
            mmk =>
            {
                Assert.Equal("MMK:1 licence(s)", mmk.LicenceNo);
                Assert.Equal("MMK", mmk.Currency);
                Assert.Equal(300000m, mmk.Amount);
            },
            total =>
            {
                Assert.Equal("Total:3 licence(s)", total.LicenceNo);
                Assert.Null(total.Currency);
                Assert.Null(total.Amount);
            });
    }

    [Fact]
    public void Currency_footer_rows_write_total_value_cells_to_excel()
    {
        var footerRows = sp_ExtensionReport.CreateCurrencyFooterRows(new ReportCurrencyTotalsSummary
        {
            Currencies = new[]
            {
                new ReportCurrencyTotal { Currency = "USD", NoOfLicences = 2, TotalValue = 125.50m },
            },
            GrandTotalLicences = 2,
        });

        using var stream = new MemoryStream();
        using (var writer = new StreamingExcelWriter(stream, "Extension"))
        {
            writer.AppendRows(new[] { new sp_ExtensionReportResult { LicenceNo = "IL-1", Currency = "USD", Amount = 25m } });
            writer.AppendRows(footerRows);
            writer.Finish();
        }

        using var archive = new ZipArchive(new MemoryStream(stream.ToArray()), ZipArchiveMode.Read);
        using var worksheet = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var document = XDocument.Load(worksheet);
        var ns = document.Root!.Name.Namespace;
        Assert.Contains(document.Descendants(ns + "v"), value => value.Value == "125.50");
    }
}
