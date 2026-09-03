using API.Model;
using API.Service.ExcelExport;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Tests;

/// <summary>
/// Every generic export binds its columns to whatever this resolves. If it answers null
/// for a report the whole sheet comes out blank, so the resolution rule is pinned
/// against real controllers rather than a stub.
/// </summary>
public sealed class ExcelRowTypeResolverTests
{
    private sealed class Request : ReportQueryRequest;

    private sealed class BareController : ControllerBase
    {
        [HttpPost]
        public Task<ActionResult<ApiResult<string>>> Post([FromBody] Request? request)
            => Task.FromResult<ActionResult<ApiResult<string>>>(NotFound());

        [HttpPost("Excel")]
        public Task<IActionResult> Excel([FromBody] Request? request)
            => Task.FromResult<IActionResult>(NotFound());
    }

    private sealed class ProviderController : ControllerBase, IExcelRowTypeProvider
    {
        public Type ExcelRowType => typeof(ReportLicenceListResult);
    }

    [Fact]
    public void The_row_type_is_the_T_of_the_bare_Post_s_ApiResult()
        => Assert.Equal(typeof(string), ExcelRowTypeResolver.Resolve(typeof(BareController)));

    [Fact]
    public void A_real_report_controller_resolves_to_the_type_its_grid_renders()
        => Assert.Equal(
            typeof(sp_AccountSummaryReportResult),
            ExcelRowTypeResolver.Resolve(typeof(AccountSummaryReportController)));

    [Fact]
    public void A_composite_report_resolves_to_its_summary_payload()
        => Assert.Equal(
            typeof(ImportLicenceTotalValueLicencesSummary),
            ExcelRowTypeResolver.Resolve(typeof(ImportLicenceTotalValueLicencesReportController)));

    [Fact]
    public void A_declared_row_type_is_used_when_there_is_no_bare_Post()
        => Assert.Equal(
            typeof(ReportLicenceListResult),
            ExcelRowTypeResolver.Resolve(typeof(ProviderController)));

    [Fact]
    public void FindBarePost_ignores_the_Excel_action()
    {
        var post = ExcelRowTypeResolver.FindBarePost(typeof(BareController), typeof(Request));

        Assert.NotNull(post);
        Assert.Equal("Post", post!.Name);
        Assert.Equal(typeof(Request), post.GetParameters()[0].ParameterType);
    }

    [Fact]
    public void Every_streaming_report_controller_has_a_resolvable_row_type()
    {
        var unresolved = ReportTestHelper.ControllerTypes
            .Where(type => typeof(IStreamingExcelReport).IsAssignableFrom(type))
            .Where(type => ExcelRowTypeResolver.Resolve(type) == null)
            .Select(type => type.Name)
            .ToList();

        Assert.True(
            unresolved.Count == 0,
            "These reports would export every column blank because their row type cannot be resolved: "
                + string.Join(", ", unresolved));
    }
}
