using System.Reflection;
using API.Service.ExcelExport;
using API.Service.Reports;
using Backend.Controllers.Report;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Tests.ExcelParity;

public sealed class ImportPermitTotalValuePermitsReportControllerLayoutTests
{
    private static readonly Type ControllerType =
        typeof(ImportPermitTotalValuePermitsReportController);

    private static ImportPermitTotalValuePermitsReportRequest Request() => new()
    {
        FromDate = new DateTime(2026, 4, 1),
        ToDate = new DateTime(2026, 4, 28, 23, 59, 59),
    };

    private static ExcelReportLayout Layout()
        => new ImportPermitTotalValuePermitsReportController(null!, null!)
            .GetExcelLayout(Request());

    [Fact]
    public void The_controller_exposes_the_composite_summary_contract()
    {
        Assert.True(typeof(IExcelReportLayoutProvider).IsAssignableFrom(ControllerType));
        Assert.Equal(
            typeof(ImportPermitTotalValuePermitsSummary),
            ExcelRowTypeResolver.Resolve(ControllerType));
    }

    [Fact]
    public void The_layout_matches_the_requested_two_tables()
    {
        var layout = Layout();

        Assert.Equal(2, layout.Sections.Count);
        Assert.Equal("Total Value", layout.Sections[0].Title);
        Assert.Equal("Total Permits", layout.Sections[1].Title);
        Assert.Equal(
            new[] { "Sr.No.", "Total Value", "Currency" },
            layout.Sections[0].Columns.Select(column => column.Header).ToArray());
        Assert.Equal(
            new[] { "Sr.No.", "Total Permits", "Pa Tha Ka Type" },
            layout.Sections[1].Columns.Select(column => column.Header).ToArray());
    }

    [Fact]
    public void The_title_uses_the_selected_date_range()
    {
        Assert.Equal(
            "Import Permits Total Value & Permits (01/04/2026) To (28/04/2026)",
            Assert.Single(Layout().TitleLines));
    }

    [Fact]
    public void The_excel_hooks_are_not_mvc_actions_and_use_format_version_two()
    {
        foreach (var name in new[]
                 {
                     nameof(IExcelReportLayoutProvider.GetExcelLayout),
                     nameof(IStreamingExcelReport.WriteRowsAsync),
                 })
        {
            var method = ControllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate => candidate.Name == name);
            Assert.NotNull(method.GetCustomAttribute<NonActionAttribute>());
        }

        Assert.Equal(2, ControllerType.GetCustomAttribute<ExcelFormatVersionAttribute>()?.Version);
    }
}
