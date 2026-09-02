using System.Reflection;
using API.Service.ExcelExport;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Tests;

/// <summary>
/// Guards the two ways the opt-in Excel layout can silently go wrong: a report that
/// changes its file shape without bumping its format version (users keep receiving the
/// cached old file), and a layout method that MVC mistakes for an endpoint.
/// </summary>
public sealed class ExcelExportLayoutContractTests
{
    private const string RequestJson = """{"fromDate":"2026-08-31T00:00:00","toDate":"2026-08-31T23:59:59"}""";

    [Fact]
    public void Unversioned_reports_keep_their_existing_hash()
    {
        // Reports that did not change shape must keep reusing their cached exports, so
        // version 1 has to hash exactly like the pre-change formula did.
        var expected = ExcelExportHasher.ComputeHash("SomeReport", RequestJson);

        Assert.Equal(expected, ExcelExportHasher.ComputeHash("SomeReport", RequestJson, 1));
        Assert.Equal(expected, ExcelExportHasher.ComputeHash("SomeReport", RequestJson, 0));
    }

    [Fact]
    public void Bumping_the_format_version_invalidates_the_cached_export()
    {
        var v1 = ExcelExportHasher.ComputeHash("AccountSummaryReport", RequestJson, 1);
        var v2 = ExcelExportHasher.ComputeHash("AccountSummaryReport", RequestJson, 2);
        var v3 = ExcelExportHasher.ComputeHash("AccountSummaryReport", RequestJson, 3);

        Assert.NotEqual(v1, v2);
        Assert.NotEqual(v2, v3);
    }

    [Fact]
    public void Account_summary_declares_a_format_version_above_one()
    {
        var version = typeof(Backend.Controllers.Report.AccountSummaryReportController)
            .GetCustomAttribute<ExcelFormatVersionAttribute>()?.Version;

        Assert.NotNull(version);
        Assert.True(version > 1, "The layout changed the file shape, so the cache key must move with it.");
    }

    [Fact]
    public void Every_layout_provider_marks_its_layout_method_NonAction()
    {
        var providers = ReportTestHelper.ControllerTypes
            .Where(type => typeof(IExcelReportLayoutProvider).IsAssignableFrom(type))
            .ToList();

        Assert.NotEmpty(providers);

        foreach (var type in providers)
        {
            var method = type.GetMethod(nameof(IExcelReportLayoutProvider.GetExcelLayout), [typeof(object)]);

            Assert.NotNull(method);
            Assert.True(
                method!.GetCustomAttribute<NonActionAttribute>() != null,
                $"{type.Name}.GetExcelLayout must be [NonAction] or ApiController routing rejects it at startup.");
        }
    }

    [Fact]
    public void Every_layout_provider_also_streams_rows()
    {
        foreach (var type in ReportTestHelper.ControllerTypes
                     .Where(type => typeof(IExcelReportLayoutProvider).IsAssignableFrom(type)))
        {
            Assert.True(
                typeof(IStreamingExcelReport).IsAssignableFrom(type),
                $"{type.Name} declares an Excel layout but never reaches the export pipeline.");
        }
    }
}
