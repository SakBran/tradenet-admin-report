using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Xunit.Abstractions;

namespace Backend.Tests;

/// <summary>
/// Guards that EVERY Border Import Permit report endpoint executes WITHOUT error
/// when called, against the live TradeNetDBTest database (stored procedures as deployed).
///
/// Set TRADENET_REPORT_TEST_CONNECTION_STRING to the TradeNetDBTest connection string
/// (see Backend/appsettings.json) before running, e.g.:
///   TRADENET_REPORT_TEST_CONNECTION_STRING="Server=...,14330;Database=TradeNetDB;User Id=sa;Password=***;TrustServerCertificate=True;MultipleActiveResultSets=true;"
/// </summary>
public sealed class BorderImportPermitEndpointTests(ITestOutputHelper output)
{
    /// <summary>The 12 Border Import Permit report controllers (auto-discovered).</summary>
    public static IEnumerable<object[]> BorderImportPermitControllers() =>
        ReportTestHelper.ControllerTypes
            .Where(type => type.Name.StartsWith("BorderImportPermit", StringComparison.Ordinal))
            .OrderBy(type => type.Name)
            .Select(type => new object[] { type });

    [Fact]
    public void All_twelve_border_import_permit_reports_are_discovered()
    {
        var names = ReportTestHelper.ControllerTypes
            .Where(type => type.Name.StartsWith("BorderImportPermit", StringComparison.Ordinal))
            .Select(type => type.Name)
            .OrderBy(name => name)
            .ToArray();

        output.WriteLine(string.Join(Environment.NewLine, names));
        Assert.Equal(12, names.Length);
    }

    [Theory]
    [MemberData(nameof(BorderImportPermitControllers))]
    public async Task Post_endpoint_executes_without_error(Type controllerType)
    {
        // Throws if the controller/stored-procedure raises any exception (the regression we guard against).
        var actionResult = await ReportTestHelper.InvokePostAsync(
            controllerType,
            ReportTestHelper.CreateTradeNetDbTestDbContext);

        Assert.NotNull(actionResult);

        // A well-formed ApiResult with a non-negative row count == the endpoint ran cleanly.
        var totalCount = ReportTestHelper.GetApiResultTotalCount(actionResult!);
        output.WriteLine($"{controllerType.Name}: totalCount={totalCount}");
        Assert.True(totalCount >= 0, $"{controllerType.Name} returned an invalid row count.");
    }

    [Theory]
    [MemberData(nameof(BorderImportPermitControllers))]
    public async Task Excel_endpoint_executes_without_error(Type controllerType)
    {
        var actionResult = await ReportTestHelper.InvokeExcelAsync(
            controllerType,
            ReportTestHelper.CreateTradeNetDbTestDbContext);

        Assert.NotNull(actionResult);

        // Migrated endpoints enqueue (Ok(EnqueueResult)); legacy ones stream a file (FileContentResult).
        Assert.True(
            actionResult is OkObjectResult or FileContentResult,
            $"{controllerType.Name} Excel endpoint returned an unexpected result: {actionResult.GetType().Name}.");
    }
}
