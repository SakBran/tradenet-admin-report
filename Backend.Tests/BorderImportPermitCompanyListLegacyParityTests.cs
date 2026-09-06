using System.Reflection;
using System.Text.RegularExpressions;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;

namespace Backend.Tests;

/// <summary>
/// Pins Border Import Permit Company List to the OLD report, byte for byte (owner decision
/// 2026-09-06: "even if it is wrong in the old code").
///
/// The old screen (legacy ReportsController.cs:15347-15430) fetches the same
/// dbo.sp_ImportPermitDetailReport @Type='Border' rows as the Detail report and renders
/// BorderImportPermitByCompanyReport.rdlc: one row per (CompanyRegistrationNo, Currency) group in
/// first-appearance order (no SortExpressions) showing the group's first Company Name, Sr.No. from
/// a Code group counter, No of Licences = CountDistinct(LicenceNo), Total Value =
/// FORMAT(Sum(Amount),"N4"), a TOTAL footer with only the distinct licence count, a Company Name
/// hyperlink into the Detail report -- and a header that reads "List of Import Permit By Company
/// (from) To (to)" on this BORDER screen (ReportsController.cs:15425). All of it kept.
/// </summary>
public sealed class BorderImportPermitCompanyListLegacyParityTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Controller_hands_the_detail_query_every_legacy_parameter_unchanged()
    {
        var method = ReportTestHelper.GetTryCreateReportRequest(typeof(BorderImportPermitCompanyListReportController));
        var controller = ReportTestHelper.CreateController(
            typeof(BorderImportPermitCompanyListReportController),
            ReportTestHelper.CreateInMemoryDbContext(nameof(Controller_hands_the_detail_query_every_legacy_parameter_unchanged)));

        var request = new BorderImportPermitCompanyListReportRequest
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
            RepositoryRoot, "Backend", "Controllers", "Report", "BorderImportPermitCompanyListReportController.cs"));

        Assert.Equal(2, Regex.Matches(controller, "ReportAggregateDimension\\.Company, includeSakhan: false").Count);
        Assert.Equal(2, Regex.Matches(controller, "ordering: ReportAggregateOrdering\\.SourceOrder").Count);
        Assert.Contains("columnTotalsMode: ReportColumnTotalsMode.CountOnly", controller);
        Assert.DoesNotContain("OrderGroups(", controller);
        Assert.Contains("[ExcelFormatVersion(3)]", controller);
    }

    [Fact]
    public void Ui_matches_BorderImportPermitByCompanyReport_rdlc()
    {
        var config = ExtractReportConfig("BorderImportPermitCompanyListReport");

        // rdlc:261-481 header cells, in order (Sr.No. is the row-number column).
        Assert.Equal(["Company Name", "No of Licences", "Total Value", "Currency"], ExtractColumnTitles(config));
        Assert.Contains("rowNumberTitle: 'Sr.No.'", config);

        // rdlc:686 =FORMAT(Sum(Fields!Amount.Value),"N4"); the count is a bare integer.
        Assert.Equal("#,##0.0000", ExtractColumnOption(config, "totalValue", "numberFormat"));
        Assert.Equal("money", ExtractColumnOption(config, "totalValue", "dataType"));
        Assert.Null(ExtractColumnOption(config, "noOfLicences", "numberFormat"));

        // The legacy header1, wrong wording included ("Import Permit", no "Border", no "From").
        Assert.Contains("reportDateRangeSubtitle('List of Import Permit By Company')", config);
        Assert.Contains("defaultPageSize: 1000", config);
        Assert.DoesNotContain("currencyTotalsColumns", config);

        // rdlc:608: the Company Name cell opens the Detail report in a new window for that
        // registration number with the search's dates, card type, section and Sakhan.
        Assert.Contains("targetReportKey: 'BorderImportPermitDetailReport'", config);
        Assert.Contains("carryFilters: ['FromDate', 'ToDate', 'PaThaKaTypeId', 'ExportImportSectionId', 'SakhanId']", config);
        Assert.Contains("rowParams: { CompanyRegistrationNo: 'companyRegistrationNo' }", config);
        Assert.Contains("openInNewTab: true", config);

        // Filter box = Views/Reports/BorderImportPermitByCompanyReport.cshtml:25-72 (Type is the
        // old hidden field; Company Name is the readonly auto-filled box). No Seller Country box.
        Assert.Equal(
            ["dateRange", "Type", "SakhanId", "PaThaKaTypeId", "ExportImportSectionId", "CompanyRegistrationNo"],
            ExtractFilterNames(config));
        Assert.Contains("...importLicenceCompanyNameFilter", config);
        Assert.Contains("lookupName: 'sakhans'", config);
        Assert.Contains("lookupName: 'paThaKaTypes'", config);
        Assert.Contains("lookupName: 'borderImportPermitSections'", config);

        var requestFields = typeof(BorderImportPermitCompanyListReportRequest)
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
