using System.Reflection;
using System.Text.RegularExpressions;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;

namespace Backend.Tests;

/// <summary>
/// Pins Border Import Permit By Section to the OLD report, byte for byte (owner decision
/// 2026-09-06: "even if it is wrong in the old code").
///
/// The old screen (legacy ReportsController.cs:14622-14724) fetches the same
/// dbo.sp_ImportPermitDetailReport @Type='Border' rows as the Detail report and renders
/// BorderImportPermitBySectionReport.rdlc: one row per (SectionName, Currency) group in
/// first-appearance order (no SortExpressions), Sr.No. from a Code group counter, No of Licences
/// = CountDistinct(LicenceNo), Total Value = FORMAT(Sum(Amount),"N4"), a TOTAL footer with only the
/// distinct licence count, and a Section hyperlink that opens the Detail report in a new window.
/// </summary>
public sealed class BorderImportPermitBySectionLegacyParityTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Controller_hands_the_detail_query_every_legacy_parameter_unchanged()
    {
        var method = ReportTestHelper.GetTryCreateReportRequest(typeof(BorderImportPermitBySectionReportController));
        var controller = ReportTestHelper.CreateController(
            typeof(BorderImportPermitBySectionReportController),
            ReportTestHelper.CreateInMemoryDbContext(nameof(Controller_hands_the_detail_query_every_legacy_parameter_unchanged)));

        var request = new BorderImportPermitBySectionReportRequest
        {
            Type = "Oversea", // ignored, like the old hidden field: the screen is Border
            FromDate = new DateTime(2025, 1, 1, 0, 0, 0),
            ToDate = new DateTime(2025, 12, 31, 23, 59, 59),
            PaThaKaTypeId = 3,
            ExportImportSectionId = 7,
            SellerCountryId = 11,
            CompanyRegistrationNo = "ABC-123",
            SakhanId = 4,
        };

        object?[] parameters = [request, null, null];
        Assert.True(Assert.IsType<bool>(method.Invoke(controller, parameters)));
        var procedureRequest = Assert.IsType<sp_ImportPermitDetailReportRequest>(parameters[1]);

        Assert.Equal("Border", procedureRequest.Type);
        Assert.Equal(request.FromDate, procedureRequest.FromDate);
        Assert.Equal(request.ToDate, procedureRequest.ToDate);
        Assert.Equal(3, procedureRequest.PaThaKaTypeId);
        Assert.Equal(7, procedureRequest.ExportImportSectionId);
        Assert.Equal(11, procedureRequest.SellerCountryId);
        Assert.Equal("ABC-123", procedureRequest.CompanyRegistrationNo);
        Assert.Equal(4, procedureRequest.SakhanId);
    }

    [Fact]
    public void Grid_and_excel_group_in_the_rdlc_first_appearance_order_with_a_count_only_footer()
    {
        var controller = File.ReadAllText(Path.Combine(
            RepositoryRoot, "Backend", "Controllers", "Report", "BorderImportPermitBySectionReportController.cs"));

        // Both surfaces: Section dimension, no Sakhan split, SourceOrder, CountOnly footer.
        Assert.Equal(2, Regex.Matches(controller, "ReportAggregateDimension\\.Section, includeSakhan: false").Count);
        Assert.Equal(2, Regex.Matches(controller, "ordering: ReportAggregateOrdering\\.SourceOrder").Count);
        Assert.Contains("columnTotalsMode: ReportColumnTotalsMode.CountOnly", controller);
        // The Excel rows must not be re-sorted alphabetically behind the grid's back.
        Assert.DoesNotContain("OrderGroups(", controller);
        Assert.Contains("[ExcelFormatVersion(3)]", controller);
    }

    [Fact]
    public void Ui_matches_BorderImportPermitBySectionReport_rdlc()
    {
        var config = ExtractReportConfig("BorderImportPermitBySectionReport");

        // rdlc:261-481 header cells, in order (Sr.No. is the row-number column).
        Assert.Equal(["Section", "No of Licences", "Total Value", "Currency"], ExtractColumnTitles(config));
        Assert.Contains("rowNumberTitle: 'Sr.No.'", config);

        // rdlc:688 =FORMAT(Sum(Fields!Amount.Value),"N4"); the count is a bare integer.
        Assert.Equal("#,##0.0000", ExtractColumnOption(config, "totalValue", "numberFormat"));
        Assert.Equal("money", ExtractColumnOption(config, "totalValue", "dataType"));
        Assert.Null(ExtractColumnOption(config, "noOfLicences", "numberFormat"));

        // Legacy header1 and the one-page ReportViewer; no per-currency footer block.
        Assert.Contains("importLicenceRangeSubtitle('List of Border Import Permit By Section', true)", config);
        Assert.Contains("defaultPageSize: 1000", config);
        Assert.DoesNotContain("currencyTotalsColumns", config);

        // rdlc:610: the Section cell opens the Detail report in a new window with the section id
        // and the search's dates / card type / Sakhan.
        Assert.Contains("targetReportKey: 'BorderImportPermitDetailReport'", config);
        Assert.Contains("carryFilters: ['FromDate', 'ToDate', 'PaThaKaTypeId', 'SakhanId']", config);
        Assert.Contains("rowParams: { ExportImportSectionId: 'sectionId' }", config);
        Assert.Contains("openInNewTab: true", config);

        // Filter box = Views/Reports/BorderImportPermitBySectionReport.cshtml:25-59 (Type is the
        // old hidden field). Seller Country / Company Registration No stay off the box but on
        // the DTO for drill-downs.
        Assert.Equal(
            ["dateRange", "Type", "SakhanId", "PaThaKaTypeId", "ExportImportSectionId"],
            ExtractFilterNames(config));
        Assert.Contains("lookupName: 'sakhans'", config);
        Assert.Contains("lookupName: 'paThaKaTypes'", config);
        Assert.Contains("lookupName: 'borderImportPermitSections'", config);

        var requestFields = typeof(BorderImportPermitBySectionReportRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("SellerCountryId", requestFields);
        Assert.Contains("CompanyRegistrationNo", requestFields);
    }

    // --- reportConfigs.ts source extraction -------------------------------------------------

    private static string ExtractReportConfig(string key)
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot, "Frontend", "src", "Report", "config", "reportConfigs.ts"));
        var start = source.IndexOf($"\n  {key}: {{", StringComparison.Ordinal);
        Assert.True(start >= 0, $"reportConfigs.ts has no '{key}' entry.");
        var end = source.IndexOf("\n  },\n", start, StringComparison.Ordinal);
        Assert.True(end > start);
        return source.Substring(start, end - start);
    }

    private static string[] ExtractColumnTitles(string config)
    {
        var columns = config[config.IndexOf("columns: [", StringComparison.Ordinal)..];
        return Regex.Matches(columns, @"title: '([^']*)'").Select(match => match.Groups[1].Value).ToArray();
    }

    private static string[] ExtractFilterNames(string config)
    {
        var filtersStart = config.IndexOf("filters: [", StringComparison.Ordinal);
        var filtersEnd = config.IndexOf("columns: [", StringComparison.Ordinal);
        var filters = config.Substring(filtersStart, filtersEnd - filtersStart);
        return Regex.Matches(filters, @"name: '([^']*)'").Select(match => match.Groups[1].Value).ToArray();
    }

    private static string? ExtractColumnOption(string config, string dataIndex, string option)
    {
        var match = Regex.Match(
            config,
            $@"dataIndex: '{Regex.Escape(dataIndex)}',[^}}]*?{option}: '([^']*)'",
            RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
            && !(Directory.Exists(Path.Combine(directory.FullName, "Backend"))
                && Directory.Exists(Path.Combine(directory.FullName, "Frontend"))
                && Directory.Exists(Path.Combine(directory.FullName, "StoredProcedureMigrations"))))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
