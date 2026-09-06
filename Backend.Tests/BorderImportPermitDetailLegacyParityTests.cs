using System.Reflection;
using System.Text.RegularExpressions;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;

namespace Backend.Tests;

/// <summary>
/// Pins Border Import Permit Detail to the OLD report, byte for byte (owner decision 2026-09-06:
/// "even if it is wrong in the old code").
///
/// The old screen (legacy ReportsController.cs:14455-14620) calls dbo.sp_ImportPermitDetailReport
/// with @Type = 'Border', FromDate " 00:00:00" / ToDate " 23:59:59", and renders
/// BorderImportPermitDetailReport.rdlc: 23 columns behind a "Sr.No." row number, no group, no
/// sort, no footer; dates pre-formatted "dd/MM/yyyy", Price/Value FORMAT "N4", Qty "N2", and
/// Company Address built by CommonRepository.GetAddress. The new report runs that procedure's
/// 'Border' query verbatim (sp_BorderImportPermitDetailReport_pagination) and these tests make
/// sure nobody "improves" any of it without a new decision.
/// </summary>
public sealed class BorderImportPermitDetailLegacyParityTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    private static string ProcedureSql => File.ReadAllText(Path.Combine(
        RepositoryRoot, "StoredProcedureMigrations", "sp_BorderImportPermitDetailReport_pagination.sql"));

    [Fact]
    public void Controller_hands_the_procedure_every_legacy_parameter_unchanged()
    {
        var method = ReportTestHelper.GetTryCreateReportRequest(typeof(BorderImportPermitDetailReportController));
        var controller = ReportTestHelper.CreateController(
            typeof(BorderImportPermitDetailReportController),
            ReportTestHelper.CreateInMemoryDbContext(nameof(Controller_hands_the_procedure_every_legacy_parameter_unchanged)));

        var request = new BorderImportPermitDetailReportRequest
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
        // The 23:59:59 upper bound is passed through as-is: the legacy predicate is
        // CreatedDate <= @ToDate, not the calendar-day window the listing reports use.
        Assert.Equal(request.FromDate, procedureRequest.FromDate);
        Assert.Equal(request.ToDate, procedureRequest.ToDate);
        Assert.Equal(3, procedureRequest.PaThaKaTypeId);
        Assert.Equal(7, procedureRequest.ExportImportSectionId);
        Assert.Equal(11, procedureRequest.SellerCountryId);
        Assert.Equal("ABC-123", procedureRequest.CompanyRegistrationNo);
        Assert.Equal(4, procedureRequest.SakhanId);
    }

    [Fact]
    public void Procedure_is_the_legacy_border_query_verbatim()
    {
        var sql = ProcedureSql;

        // Every FROM / JOIN / WHERE line of dbo.sp_ImportPermitDetailReport's 'Border' branch
        // (docs/StoredProcedureDefinitions.sql), as written there.
        string[] legacyLines =
        [
            "FROM BorderImportPermit",
            "INNER JOIN PaThaKa ON PaThaKa.Id = BorderImportPermit.PaThaKaId",
            "INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id",
            "INNER JOIN BorderImportPermitItem ON BorderImportPermit.Id = BorderImportPermitItem.BorderImportPermitId",
            "INNER JOIN Unit unit ON BorderImportPermitItem.UnitId = unit.Id",
            "INNER JOIN Currency currency ON BorderImportPermitItem.CurrencyId = currency.Id",
            "INNER JOIN HSCode ON BorderImportPermitItem.HSCodeId = HSCode.Id",
            "INNER JOIN ExportImportSection section ON section.Id  = BorderImportPermit.ExportImportSectionId",
            "INNER JOIN Countries sellerCountry ON sellerCountry.Id  = BorderImportPermit.SellerCountryId",
            "INNER JOIN Sakhan sakhan ON sakhan.Id = BorderImportPermit.SakhanId",
            "WHERE ApplyType='New'",
            "AND BorderImportPermit.Status='Approved'",
            "AND (BorderImportPermit.CreatedDate>=@FromDate AND BorderImportPermit.CreatedDate<=@ToDate)",
            "AND PaThaKa.CompanyRegistrationNo=(CASE WHEN @CompanyRegistrationNo='' then PaThaKa.CompanyRegistrationNo ELSE @CompanyRegistrationNo END)",
            "AND paThaKaType.Id=(CASE WHEN @PaThaKaTypeId=0 then paThaKaType.Id ELSE @PaThaKaTypeId END)",
            "AND BorderImportPermit.ExportImportSectionId=(CASE WHEN @ExportImportSectionId=0 then BorderImportPermit.ExportImportSectionId ELSE @ExportImportSectionId END)",
            "AND BorderImportPermit.SellerCountryId=(CASE WHEN @SellerCountryId=0 then BorderImportPermit.SellerCountryId ELSE @SellerCountryId END)",
            "AND BorderImportPermit.SakhanId=(CASE WHEN @SakhanId=0 then BorderImportPermit.SakhanId ELSE @SakhanId END)",
        ];

        foreach (var line in legacyLines)
        {
            Assert.Contains(line, sql);
        }

        // The legacy select list: the scalar NRC function and both FOR XML CSV expanders, so the
        // cell text is produced by the same server code the old report used.
        Assert.Contains(
            "dbo.fn_GetNRCNo(BorderImportPermit.NRCType,BorderImportPermit.NRCPrefixId,BorderImportPermit.NRCPrefixCodeId,BorderImportPermit.NRCNo) NRCNo",
            sql);
        Assert.Contains("WHERE ','+BorderImportPermit.PortofShipmentId+',' LIKE '%,'+CAST(portofShipment.Id as nvarchar(20)) +',%'", sql);
        Assert.Contains("WHERE ','+BorderImportPermit.CountryofOriginId+',' LIKE '%,'+CAST(countries.Id as nvarchar(20)) +',%'", sql);
        Assert.Contains(".value('substring(text()[1], 2)', 'varchar(max)') as PortofShipment", sql);
        Assert.Contains(".value('substring(text()[1], 2)', 'varchar(max)') as CountryofOrigin", sql);
        Assert.Contains("ImportPermitNo LicenceNo,BorderImportPermit.IssuedDate LicenceDate", sql);
        Assert.Contains("HSCode.Code HSCode,BorderImportPermitItem.Description HSDescription", sql);
        Assert.Contains("unit.Code Unit,Price,Quantity,Amount,currency.Code Currency", sql);
        Assert.Contains("PermitType,BorderImportPermit.Remark Conditions,BorderImportPermit.ApproveDate", sql);

        // Nothing "fixed": no calendar-day window, no DISTINCT, no Oversea branch.
        Assert.DoesNotContain("DATEADD", sql);
        Assert.DoesNotContain("DISTINCT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ImportPermitItem ON", sql.Replace("BorderImportPermitItem ON", string.Empty));

        // Paging shape and the deterministic order (the legacy has none), plus what the CSV
        // expanders need at CREATE time.
        Assert.Contains("CREATE OR ALTER PROCEDURE [dbo].[sp_BorderImportPermitDetailReport_pagination]", sql);
        Assert.Contains("SET QUOTED_IDENTIFIER ON;", sql);
        Assert.Contains("INTO #K", sql);
        Assert.Contains("INTO #P", sql);
        Assert.Contains("ROW_NUMBER() OVER (ORDER BY PermitCreatedDate, PermitId, ItemNo, ItemUniqueId) AS PageOrder", sql);
        Assert.Contains("OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY", sql);
        Assert.Contains("ORDER BY k.PageOrder", sql);
        Assert.Contains("@Total AS TotalCount", sql);
        Assert.Equal(2, Regex.Matches(sql, "OPTION \\(RECOMPILE\\)").Count);
    }

    [Fact]
    public void Wrapper_passes_the_procedure_the_ten_parameters_in_order()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot, "Backend", "StoredProcedureToLinq", "sp_BorderImportPermitDetailReport.cs"));

        Assert.Contains(
            "\"EXEC dbo.sp_BorderImportPermitDetailReport_pagination \"\n"
            + "            + \"@FromDate, @ToDate, @PaThaKaTypeId, @ExportImportSectionId, @SellerCountryId, \"\n"
            + "            + \"@CompanyRegistrationNo, @SakhanId, @PageIndex, @PageSize, @IncludeTotalCount\"",
            source.Replace("\r\n", "\n"));

        // Both the grid and the Excel export go through the wrapper, and the row type names every
        // result-set column so EF can materialise it.
        var controller = File.ReadAllText(Path.Combine(
            RepositoryRoot, "Backend", "Controllers", "Report", "BorderImportPermitDetailReportController.cs"));
        Assert.Contains("sp_BorderImportPermitDetailReport.CreatePagedResultAsync(", controller);
        Assert.Contains("sp_BorderImportPermitDetailReport.StreamResolvedChunksAsync(", controller);
        Assert.Contains("[ExcelFormatVersion(2)]", controller);
        Assert.Contains("IExcelNoFooterReport", controller);

        string[] resultColumns =
        [
            "PaThaKaTypeId", "PaThaKaTypeCode", "PaThaKaTypeName", "SakhanId", "SakhanCode", "SakhanName",
            "ExportImportSectionId", "SellerCountryId", "SectionCode", "SectionName", "LicenceNo", "LicenceDate",
            "CompanyRegistrationNo", "CompanyName", "UnitLevel", "StreetNumberStreetName", "QuarterCityTownship",
            "State", "Country", "PostalCode", "AuthorisedAgentName", "AuthorisedAgentAddress", "SellerCountry",
            "PortofShipment", "PortofDischarge", "CountryofOrigin", "LastDate", "HSCode", "HSDescription", "Unit",
            "Price", "Quantity", "Amount", "Currency", "NRCNo", "PermitType", "Conditions", "ApproveDate", "TotalCount",
        ];
        var rowProperties = typeof(sp_BorderImportPermitDetailReportRow)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();
        Assert.Equal(resultColumns, rowProperties);
    }

    [Theory]
    // unitLevel, street, quarter, state, country, postal -> CommonRepository.GetAddress
    [InlineData("No.235, Block-5", "Simsa Road", "Dukahtawng Quarter, Myitkyinar", "Kachin State", "MYANMAR", "1011",
        "No.235, Block-5, Simsa Road, Dukahtawng Quarter, Myitkyinar, Kachin State 1011, MYANMAR")]
    // no postal code: legacy writes "State," with NO space
    [InlineData("", "Simsa Road", "Myitkyinar", "Kachin State", "MYANMAR", "",
        "Simsa Road, Myitkyinar, Kachin State,MYANMAR")]
    // no country: legacy leaves the trailing ", "
    [InlineData("Unit 1", "", "Yangon", "Yangon Region", "", "11011",
        "Unit 1, Yangon, Yangon Region 11011, ")]
    // NULLs came through row["X"].ToString() as "" in the old code
    [InlineData(null, null, null, null, "MYANMAR", null, "MYANMAR")]
    [InlineData(null, null, null, null, null, null, "")]
    public void Company_address_is_the_legacy_GetAddress_string(
        string? unitLevel, string? street, string? quarter, string? state, string? country, string? postal, string expected)
    {
        Assert.Equal(expected, LegacyCompanyAddress.Compose(unitLevel, street, quarter, state, country, postal));
    }

    [Fact]
    public void Api_result_carries_the_company_address_the_grid_prints()
    {
        Assert.NotNull(typeof(sp_ImportPermitDetailReportResult).GetProperty("CompanyAddress"));

        var config = ExtractReportConfig("BorderImportPermitDetailReport");
        Assert.Contains("dataIndex: 'companyAddress'", config);
    }

    [Fact]
    public void Ui_matches_BorderImportPermitDetailReport_rdlc()
    {
        var config = ExtractReportConfig("BorderImportPermitDetailReport");

        // rdlc:341-1530 header cells, in order (Sr.No. is the row number column).
        Assert.Equal(
            [
                "Section", "Permit No", "Permit Date", "Company Registration No", "Company Name",
                "Company Address", "Union Citizenship No", "Agent Name", "Agent Address", "Seller Country",
                "Port of Shipment", "Place/Port of Discharge", "Last Date", "Country of Orign", "Type of Permit",
                "HSCode", "Decription", "A/U", "Price", "Qty", "Value", "Currency", "Conditions",
            ],
            ExtractColumnTitles(config));
        Assert.Contains("rowNumberTitle: 'Sr.No.'", config);

        // rdlc:2705-2864: FORMAT(Price,"N4"), FORMAT(Quantity,"N2"), FORMAT(Amount,"N4"); the
        // model's sLicenceDate / LastDate are .ToString("dd/MM/yyyy").
        Assert.Equal("#,##0.0000", ExtractColumnOption(config, "price", "numberFormat"));
        Assert.Equal("#,##0.00", ExtractColumnOption(config, "quantity", "numberFormat"));
        Assert.Equal("#,##0.0000", ExtractColumnOption(config, "amount", "numberFormat"));
        Assert.Equal("DD/MM/YYYY", ExtractColumnOption(config, "licenceDate", "dateFormat"));
        Assert.Equal("DD/MM/YYYY", ExtractColumnOption(config, "lastDate", "dateFormat"));

        // Legacy header1: "List of Border Import Permit By Detail From (<from>) To (<to>)"; the old
        // ReportViewer printed every row on one page; the RDLC has no footer (no TOTAL anywhere).
        Assert.Contains("importLicenceRangeSubtitle('List of Border Import Permit By Detail', true)", config);
        Assert.Contains("defaultPageSize: 1000", config);
        Assert.DoesNotContain("includeColumnTotals", config);
        Assert.DoesNotContain("currencyTotalsColumns", config);

        // Filter box = Views/Reports/BorderImportPermitDetailReport.cshtml:28-62 (Type is the old
        // hidden field). Seller Country / Company Registration No stay off the box but on the DTO
        // for the By-X drill-downs.
        Assert.Equal(
            ["dateRange", "Type", "SakhanId", "PaThaKaTypeId", "ExportImportSectionId"],
            ExtractFilterNames(config));
        Assert.Contains("lookupName: 'sakhans'", config);
        Assert.Contains("lookupName: 'paThaKaTypes'", config);
        Assert.Contains("lookupName: 'borderImportPermitSections'", config);

        var requestFields = typeof(BorderImportPermitDetailReportRequest)
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
