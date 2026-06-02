using API.Service.ExcelExport;
using API.Service.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Tests;

public sealed class ReportEndpointSmokeTests(ReportSqlServerFixture sqlServerFixture)
    : IClassFixture<ReportSqlServerFixture>
{
    [Theory]
    [MemberData(nameof(Controllers))]
    public void Report_controller_is_authorized_and_exposes_post_and_excel_routes(Type controllerType)
    {
        Assert.Contains(
            controllerType.GetCustomAttributes(inherit: true),
            attribute => attribute is AuthorizeAttribute);

        var post = controllerType.GetMethod("Post");
        Assert.NotNull(post);
        Assert.Contains(
            post!.GetCustomAttributes(inherit: true),
            attribute => attribute is HttpPostAttribute httpPost
                && string.IsNullOrEmpty(httpPost.Template));

        var excel = controllerType.GetMethod("Excel");
        Assert.NotNull(excel);
        Assert.Contains(
            excel!.GetCustomAttributes(inherit: true),
            attribute => attribute is HttpPostAttribute httpPost
                && httpPost.Template == "Excel");
    }

    [Theory]
    [MemberData(nameof(Controllers))]
    public async Task Report_post_endpoint_returns_empty_page_for_empty_authenticated_fixture(Type controllerType)
    {
        var actionResult = await ReportTestHelper.InvokePostAsync(
            controllerType,
            sqlServerFixture.CreateDbContext);
        Assert.NotNull(actionResult);

        var totalCount = ReportTestHelper.GetApiResultTotalCount(actionResult!);
        Assert.Equal(0, totalCount);
    }

    [Theory]
    [MemberData(nameof(Controllers))]
    public async Task Report_excel_endpoint_returns_xlsx_or_queues_job_for_empty_authenticated_fixture(Type controllerType)
    {
        var actionResult = await ReportTestHelper.InvokeExcelAsync(
            controllerType,
            sqlServerFixture.CreateDbContext);

        // Two valid shapes during the queue rollout:
        //  - legacy: the endpoint still generates an .xlsx synchronously (FileContentResult);
        //  - migrated: the endpoint enqueues a job and returns an EnqueueResult (Ok(...)).
        if (actionResult is OkObjectResult ok)
        {
            var enqueue = Assert.IsType<EnqueueResult>(ok.Value);
            Assert.NotEqual(Guid.Empty, enqueue.JobId);
            return;
        }

        var fileResult = Assert.IsType<FileContentResult>(actionResult);
        Assert.Equal(ExcelGenerator.ContentType, fileResult.ContentType);
        Assert.EndsWith(".xlsx", fileResult.FileDownloadName, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("PK", System.Text.Encoding.ASCII.GetString(fileResult.FileContents, 0, 2), StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> Controllers() => ReportTestHelper.ControllerCases();
}
