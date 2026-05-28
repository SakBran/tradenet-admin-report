using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;
using Backend.Controllers.Report;

namespace Backend.Tests;

public sealed class ReportSeededDatabaseSmokeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task TradeNetDBTest_database_is_available()
    {
        await using var db = ReportTestHelper.CreateTradeNetDbTestDbContext();

        Assert.True(
            await db.Database.CanConnectAsync(),
            "TradeNetDBTest must be available for seeded report smoke tests.");
    }

    [Theory]
    [MemberData(nameof(Controllers))]
    public async Task Report_post_endpoint_generates_seeded_database_page(Type controllerType)
    {
        var actionResult = await ReportTestHelper.InvokePostAsync(
            controllerType,
            ReportTestHelper.CreateTradeNetDbTestDbContext);

        Assert.NotNull(actionResult);

        var totalCount = ReportTestHelper.GetApiResultTotalCount(actionResult!);
        var status = totalCount > 0 ? "data-returning" : "empty-by-fixture";

        output.WriteLine($"{controllerType.Name}: {status}, totalCount={totalCount}");
        Assert.True(totalCount >= 0, $"{controllerType.Name} returned an invalid row count.");
    }

    [Theory]
    [MemberData(nameof(RepresentativeCounts))]
    public async Task Representative_report_modules_match_seeded_database_row_counts(
        Type controllerType,
        int expectedTotalCount)
    {
        var actionResult = await ReportTestHelper.InvokePostAsync(
            controllerType,
            ReportTestHelper.CreateTradeNetDbTestDbContext);

        Assert.NotNull(actionResult);
        Assert.Equal(expectedTotalCount, ReportTestHelper.GetApiResultTotalCount(actionResult!));
    }

    public static IEnumerable<object[]> Controllers() => ReportTestHelper.ControllerCases();

    public static IEnumerable<object[]> RepresentativeCounts()
    {
        yield return [typeof(AccountSummaryReportController), 0];
        yield return [typeof(CompanyProfileController), 3];
        yield return [typeof(ListOfCompanyController), 1];
        yield return [typeof(ListOfDirectorsController), 3];
        yield return [typeof(ListOfDirectorsByCompanyRegistrationNoController), 3];
        yield return [typeof(ListOfTopCapitalCompanyController), 1];
        yield return [typeof(ListOfValidAndInvalidCompanyController), 1];
        yield return [typeof(MemberRegistrationReportController), 52];
        yield return [typeof(PaThaKaRegisteredBusinessOrganizationReportController), 1];
        yield return [typeof(RegistrationByBusinessTypeController), 1];
        yield return [typeof(ImportLicenceDetailReportController), 0];
        yield return [typeof(ExportLicenceDetailReportController), 0];
        yield return [typeof(ImportPermitDetailReportController), 0];
        yield return [typeof(ExportPermitDetailReportController), 0];
        yield return [typeof(BorderImportLicenceDetailReportController), 0];
        yield return [typeof(BorderExportLicenceDetailReportController), 0];
        yield return [typeof(BorderImportPermitDetailReportController), 0];
        yield return [typeof(BorderExportPermitDetailReportController), 0];
        yield return [typeof(MPUReportController), 0];
        yield return [typeof(OnlineFeesReportController), 0];
        yield return [typeof(ChequeNoReportController), 0];
    }
}
