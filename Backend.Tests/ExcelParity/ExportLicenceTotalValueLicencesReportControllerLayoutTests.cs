using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;
using API.Model;
using API.Service.ExcelExport;
using API.Service.Reports;
using Backend.Controllers.Report;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Tests.ExcelParity;

/// <summary>
/// Pins the sheet this composite exports against its page
/// (Frontend/src/Report/Page/ExportLicenceTotalValueLicencesReport.tsx). The page is
/// NOT a grid: it renders two tables (valueColumns → Sr.No. / Total Value / Currency,
/// licenceColumns → Sr.No. / Total Licences / Pa Tha Ka Type) plus a right-aligned
/// "Total USD Value" line, so the export is a typed two-section layout rather than the
/// generic spec-built one. Every assertion here is a line of that page.
/// </summary>
public sealed class ExportLicenceTotalValueLicencesReportControllerLayoutTests
{
    private static readonly Type ControllerType =
        typeof(ExportLicenceTotalValueLicencesReportController);

    private static ExportLicenceTotalValueLicencesReportRequest Request() => new()
    {
        Type = "Oversea",
        FromDate = new DateTime(2025, 2, 1),
        ToDate = new DateTime(2025, 2, 28, 23, 59, 59),
    };

    /// <summary>The real layout the controller declares, so these tests cannot drift from it.</summary>
    private static ExcelReportLayout Layout()
        => new ExportLicenceTotalValueLicencesReportController(null!, null!)
            .GetExcelLayout(Request());

    [Fact]
    public void The_controller_declares_its_own_layout_so_the_generic_spec_columns_are_ignored()
    {
        Assert.True(typeof(IExcelReportLayoutProvider).IsAssignableFrom(ControllerType));

        var layout = Layout();

        // No flat column list: the sheet is sections only, which is also what keeps the
        // job handler's row-type guard (bound to Post's summary type) out of the way.
        Assert.Empty(layout.Columns);
        Assert.Equal(
            typeof(ImportLicenceTotalValueLicencesSummary),
            ExcelRowTypeResolver.Resolve(ControllerType));
    }

    [Fact]
    public void The_two_sections_are_the_pages_two_tables_in_page_order()
    {
        var layout = Layout();

        Assert.Equal(2, layout.Sections.Count);
        Assert.Equal("Total Value", layout.Sections[0].Title);
        Assert.Equal("Total Licences", layout.Sections[1].Title);

        Assert.Equal(
            new[] { "Sr.No.", "Total Value", "Currency" },
            layout.Sections[0].Columns.Select(column => column.Header).ToArray());
        Assert.Equal(
            new[] { "Sr.No.", "Total Licences", "Pa Tha Ka Type" },
            layout.Sections[1].Columns.Select(column => column.Header).ToArray());
    }

    [Fact]
    public void The_cells_are_typed_the_way_the_page_renders_them()
    {
        var layout = Layout();

        var value = layout.Sections[0].Columns;
        Assert.True(value[0].IsRowNumber);
        // The page's formatValue() prints 4 decimals.
        Assert.Equal(ExcelCellFormat.Money4, value[1].Format);
        Assert.Equal(ExcelCellFormat.Text, value[2].Format);

        var licences = layout.Sections[1].Columns;
        Assert.True(licences[0].IsRowNumber);
        Assert.Equal(ExcelCellFormat.Number, licences[1].Format);
        Assert.Equal(ExcelCellFormat.Text, licences[2].Format);
    }

    [Fact]
    public void The_title_line_is_the_pages_heading_with_the_requests_date_range()
    {
        var layout = Layout();

        Assert.Equal(
            "Export Licences Total Value & Licences (01/02/2025) To (28/02/2025)",
            Assert.Single(layout.TitleLines));
    }

    [Fact]
    public void The_layout_hook_and_the_row_writer_are_not_mvc_actions()
    {
        foreach (var name in new[] { nameof(IExcelReportLayoutProvider.GetExcelLayout), nameof(IStreamingExcelReport.WriteRowsAsync) })
        {
            var method = ControllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate => candidate.Name == name);

            Assert.NotNull(method.GetCustomAttribute<NonActionAttribute>());
        }
    }

    [Fact]
    public void The_excel_format_version_is_bumped_so_cached_pre_parity_files_are_invalidated()
    {
        var attribute = ControllerType.GetCustomAttribute<ExcelFormatVersionAttribute>();

        Assert.NotNull(attribute);
        Assert.True(
            attribute!.Version > 1,
            $"Expected a bumped ExcelFormatVersion, got {attribute.Version}.");
    }

    [Fact]
    public void The_page_has_no_footer_so_the_sheet_has_none_either()
    {
        // No override: the default resolver replays Post, whose payload
        // (ImportLicenceTotalValueLicencesSummary) is not IReportTotals → no totals.
        Assert.False(typeof(IExcelFooterTotalsProvider).IsAssignableFrom(ControllerType));
        Assert.False(typeof(IReportTotals).IsAssignableFrom(typeof(ImportLicenceTotalValueLicencesSummary)));

        // Even handed totals, a sections-only layout has no column to place them under.
        var totals = new ReportFooterTotals(
            new Dictionary<string, decimal> { ["totalValue"] = 10m },
            new ReportCurrencyTotalsSummary
            {
                Currencies = [new ReportCurrencyTotal { Currency = "USD", NoOfLicences = 1, TotalValue = 10m }],
            });

        Assert.Empty(ExcelFooterBuilder.Build(Layout(), totals, dataRowCount: 3));
    }

    [Fact]
    public void The_sheet_writes_both_tables_then_the_total_usd_value_line()
    {
        var layout = Layout();

        using var ms = new MemoryStream();
        using (var writer = new StreamingExcelWriter(ms, "Export Licence Total Value & Licences Report", layout))
        {
            writer.BeginSection(0);
            writer.AppendRows(new List<TotalValueByCurrencyRow>
            {
                new() { Currency = "MMK", TotalValue = 1500m },
                new() { Currency = "USD", TotalValue = 1234.5678m },
            });

            writer.BeginSection(1);
            writer.AppendRows(new List<TotalLicencesByPaThaKaTypeRow>
            {
                new() { PaThaKaType = "Pa Tha Ka", NoOfLicences = 7 },
            });

            writer.AppendNote("Total USD Value: 1,234.5678");
            writer.Finish();

            Assert.Equal(3, writer.TotalDataRows);
        }

        var rows = NonEmptyRows(ms.ToArray());

        Assert.Equal(
            new[]
            {
                "Export Licences Total Value & Licences (01/02/2025) To (28/02/2025)",
                "Total Value",
                "Sr.No.|Total Value|Currency",
                "1|1500|MMK",
                "2|1234.5678|USD",
                "Total Licences",
                "Sr.No.|Total Licences|Pa Tha Ka Type",
                "1|7|Pa Tha Ka",
                "Total USD Value: 1,234.5678",
            },
            rows);
    }

    /// <summary>Each written row as "cell|cell|…", spacer rows dropped.</summary>
    private static string[] NonEmptyRows(byte[] bytes)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var stream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var doc = XDocument.Load(stream);
        var ns = doc.Root!.Name.Namespace;

        return doc.Descendants(ns + "row")
            .Select(row => string.Join(
                "|",
                row.Elements(ns + "c").Select(cell =>
                    cell.Descendants(ns + "t").FirstOrDefault()?.Value
                        ?? cell.Element(ns + "v")?.Value
                        ?? string.Empty)))
            .Where(text => text.Trim('|').Length > 0)
            .ToArray();
    }
}
