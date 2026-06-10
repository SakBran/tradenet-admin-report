using API.Model.TradeNet;
using API.Service.Reports;
using Backend.Controllers;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Tests;

public sealed class BorderImportLicenceParityTests
{
    [Theory]
    [InlineData("BorderImportLicenceByMethodReportController")]
    [InlineData("BorderImportLicenceBySectionReportController")]
    [InlineData("BorderImportLicenceBySellerCountryReportController")]
    [InlineData("BorderImportLicenceCompanyListReportController")]
    [InlineData("BorderImportLicenceDailyReportNewLicenceReportController")]
    [InlineData("BorderImportLicenceDetailReportController")]
    [InlineData("BorderImportLicenceTotalValueLicencesReportController")]
    public void Border_import_licence_reports_force_border_type(string controllerName)
    {
        var controllerType = ReportTestHelper.ControllerTypes.Single(type => type.Name == controllerName);

        var procedureRequest = ReportTestHelper.CreateProcedureRequest(controllerType);

        var actualType = Assert.IsType<string>(
            procedureRequest.GetType().GetProperty("Type")?.GetValue(procedureRequest));

        Assert.Equal("Border", actualType);
    }

    [Fact]
    public void Border_import_licence_total_value_uses_composite_summary_contract()
    {
        var controllerType = ReportTestHelper.ControllerTypes.Single(
            type => type.Name == "BorderImportLicenceTotalValueLicencesReportController");
        var post = controllerType.GetMethod("Post")
            ?? throw new InvalidOperationException("Post action is missing.");

        var taskResultType = Assert.Single(post.ReturnType.GetGenericArguments());
        var actionResultType = Assert.Single(taskResultType.GetGenericArguments());

        Assert.Equal(typeof(ImportLicenceTotalValueLicencesSummary), actionResultType);
    }

    [Theory]
    [InlineData("borderImportLicenceSections", "BIL-SECTION")]
    [InlineData("borderImportLicenceMethods", "BIL-METHOD")]
    [InlineData("borderImportLicenceIncoterms", "BIL-INCOTERM")]
    public async Task Border_import_licence_lookups_only_return_border_import_values(
        string lookupName,
        string expectedCode)
    {
        await using var db = ReportTestHelper.CreateInMemoryDbContext(
            $"{nameof(BorderImportLicenceParityTests)}_{lookupName}_{Guid.NewGuid():N}");

        SeedLookupRows(db);
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new ReportLookupsController(db, cache);

        var result = await controller.Get(lookupName);
        var options = Assert.IsAssignableFrom<List<ReportLookupOption>>(
            Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result).Value);

        var option = Assert.Single(options);
        Assert.Equal(expectedCode, option.Code);
    }

    private static void SeedLookupRows(API.DBContext.TradeNetDbContext db)
    {
        db.ExportImportSections.AddRange(
            Section(1, "Import Licence", "BIL-SECTION", isBorder: true, isOversea: false),
            Section(2, "Import Licence", "OVERSEA-SECTION", isBorder: false, isOversea: true),
            Section(3, "Import Permit", "PERMIT-SECTION", isBorder: true, isOversea: false),
            Section(4, "Export Licence", "EXPORT-SECTION", isBorder: true, isOversea: false));

        db.ExportImportMethods.AddRange(
            Method(1, "Import", "BIL-METHOD", isBorder: true, isOversea: false),
            Method(2, "Import", "OVERSEA-METHOD", isBorder: false, isOversea: true),
            Method(3, "Export", "EXPORT-METHOD", isBorder: true, isOversea: false));

        db.ExportImportIncoterms.AddRange(
            Incoterm(1, "Import", "BIL-INCOTERM", isBorder: true, isOversea: false),
            Incoterm(2, "Import", "OVERSEA-INCOTERM", isBorder: false, isOversea: true),
            Incoterm(3, "Export", "EXPORT-INCOTERM", isBorder: true, isOversea: false));
    }

    private static ExportImportSection Section(
        int id,
        string type,
        string code,
        bool isBorder,
        bool isOversea) => new()
        {
            Id = id,
            Type = type,
            Code = code,
            Name = code,
            SortOrder = id,
            IsActive = true,
            IsDeleted = false,
            IsBorder = isBorder,
            IsOversea = isOversea,
            CreatedDate = ReportTestHelper.FromDate,
        };

    private static ExportImportMethod Method(
        int id,
        string type,
        string code,
        bool isBorder,
        bool isOversea) => new()
        {
            Id = id,
            Type = type,
            Code = code,
            Name = code,
            HscodeType = string.Empty,
            SortOrder = id,
            IsActive = true,
            IsDeleted = false,
            IsBorder = isBorder,
            IsOversea = isOversea,
            CreatedDate = ReportTestHelper.FromDate,
        };

    private static ExportImportIncoterm Incoterm(
        int id,
        string type,
        string code,
        bool isBorder,
        bool isOversea) => new()
        {
            Id = id,
            Type = type,
            Code = code,
            Name = code,
            SortOrder = id,
            IsActive = true,
            IsDeleted = false,
            IsBorder = isBorder,
            IsOversea = isOversea,
            CreatedDate = ReportTestHelper.FromDate,
        };
}
