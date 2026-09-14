using API.Service.Reports;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Tests;

/// <summary>
/// The three EICC report endpoints against the old admin's one EICC screen
/// (Views/EICC/EICCReport.cshtml + EICCController.EICCReport on origin/master). No database:
/// these pin the request mapping and which branch of dbo.sp_EICCReport each endpoint asks for.
/// </summary>
public sealed class EICCReportContractTests
{
    public static TheoryData<Type, string> TypePerController() => new()
    {
        { typeof(EICCCertificateReportController), "Certificate" },
        { typeof(EICCLicencePermitReportController), "LicencePermit" },
        { typeof(EICCBorderLicencePermitReportController), "BorderLicencePermit" },
    };

    [Theory]
    [MemberData(nameof(TypePerController))]
    public void Each_endpoint_pins_its_own_legacy_type(Type controllerType, string expectedType)
    {
        var procedureRequest = Assert.IsType<sp_EICCReportRequest>(
            ReportTestHelper.CreateProcedureRequest(controllerType));

        Assert.Equal(expectedType, procedureRequest.Type);
    }

    [Theory]
    [MemberData(nameof(TypePerController))]
    public void A_blank_status_falls_back_to_Pending(Type controllerType, string expectedType)
    {
        _ = expectedType;

        // ReportTestHelper fills every string with "". The old screen's dropdown has no "all"
        // option and defaults to Pending, and the procedure compares Status with `=`, so a
        // blank would silently return nothing.
        var procedureRequest = Assert.IsType<sp_EICCReportRequest>(
            ReportTestHelper.CreateProcedureRequest(controllerType));

        Assert.Equal("Pending", procedureRequest.EICCStatus);
    }

    [Theory]
    [MemberData(nameof(TypePerController))]
    public void The_eicc_date_is_truncated_to_midnight(Type controllerType, string expectedType)
    {
        _ = expectedType;

        // The procedure matches EICCDate exactly (>= @EICCDate AND <= @EICCDate), so a request
        // carrying a time of day would match nothing at all.
        var procedureRequest = Assert.IsType<sp_EICCReportRequest>(
            ReportTestHelper.CreateProcedureRequest(controllerType));

        Assert.Equal(procedureRequest.EICCDate.Date, procedureRequest.EICCDate);
    }

    [Fact]
    public void The_certificate_endpoint_has_no_product_filters()
    {
        // eicc-reports.js:26-33 hides both product boxes for Certificates, and the procedure's
        // Certificate branch never looks at them.
        var requestType = ReportTestHelper.GetRequestType(typeof(EICCCertificateReportController));

        Assert.Null(requestType.GetProperty("ProductGroupId"));
        Assert.Null(requestType.GetProperty("ProductItemId"));
    }

    [Theory]
    [InlineData(typeof(EICCLicencePermitReportController))]
    [InlineData(typeof(EICCBorderLicencePermitReportController))]
    public void The_licence_permit_endpoints_carry_the_product_filters(Type controllerType)
    {
        var requestType = ReportTestHelper.GetRequestType(controllerType);

        Assert.NotNull(requestType.GetProperty("ProductGroupId"));
        Assert.NotNull(requestType.GetProperty("ProductItemId"));
    }

    [Fact]
    public void A_request_with_no_date_is_rejected()
    {
        using var db = ReportTestHelper.CreateInMemoryDbContext(nameof(A_request_with_no_date_is_rejected));
        var controller = (EICCCertificateReportController)ReportTestHelper.CreateController(
            typeof(EICCCertificateReportController), db);

        var result = controller.Post(new EICCCertificateReportRequest()).GetAwaiter().GetResult();

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [MemberData(nameof(TypePerController))]
    public void Only_the_requested_type_branch_is_queried(Type controllerType, string expectedType)
    {
        _ = controllerType;

        using var db = ReportTestHelper.CreateInMemoryDbContext(
            nameof(Only_the_requested_type_branch_is_queried) + expectedType);

        var query = sp_EICCReport.Query(db, new sp_EICCReportRequest
        {
            Type = expectedType,
            EICCStatus = "Pending",
            EICCDate = new DateTime(2026, 2, 15),
        });

        // The legacy procedure UNIONed all three branches every run, so every run paid for
        // all of them. Each branch has a shape the others do not: only the Certificate
        // branches project RegistrationAddressRow, only the Border ones project
        // LicencePermitAddressRow, and the product-group filter exists on the two
        // Licence/Permit branches only.
        var expression = query.Expression.ToString();

        Assert.Equal(
            expectedType == "Certificate",
            expression.Contains("RegistrationAddressRow()", StringComparison.Ordinal));
        Assert.Equal(
            expectedType == "BorderLicencePermit",
            expression.Contains("LicencePermitAddressRow()", StringComparison.Ordinal));
        Assert.Equal(
            expectedType != "Certificate",
            expression.Contains("strictProductFilter", StringComparison.Ordinal));
    }

    [Fact]
    public void The_company_address_cell_is_the_legacy_GetAddress_string()
    {
        // The old model built this in C# from the six address columns; the row exposes it as one
        // computed property so the grid and the Excel sheet print the same bytes.
        var row = new sp_EICCReportResult
        {
            UnitLevel = "No.5",
            StreetNumberStreetName = "Bogyoke Road",
            QuarterCityTownship = "Latha",
            State = "Yangon Region",
            Country = "Myanmar",
            PostalCode = "11131",
        };

        Assert.Equal(
            LegacyCompanyAddress.Compose(
                "No.5", "Bogyoke Road", "Latha", "Yangon Region", "Myanmar", "11131"),
            row.CompanyAddress);
        Assert.Equal("No.5, Bogyoke Road, Latha, Yangon Region 11131, Myanmar", row.CompanyAddress);
    }
}
