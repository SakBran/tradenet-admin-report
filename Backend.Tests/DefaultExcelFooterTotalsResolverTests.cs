using API.Model;
using API.Service.ExcelExport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Backend.Tests;

/// <summary>
/// The footer must be the grid's own numbers, so the resolver replays the report's Post
/// rather than re-deriving anything. These facts pin what it sends, what it reads back,
/// and what it does when the probe fails.
/// </summary>
public sealed class DefaultExcelFooterTotalsResolverTests
{
    private sealed class Request : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    private sealed class Row
    {
        public decimal Amount { get; init; }
    }

    /// <summary>Records the request the resolver actually sent.</summary>
    private sealed class ProbeController : ControllerBase
    {
        public static Request? LastRequest;

        [HttpPost]
        public Task<ActionResult<ApiResult<Row>>> Post([FromBody] Request? request)
        {
            LastRequest = request;
            var result = ApiResult<Row>.CreateFastPageFromRows([new Row { Amount = 1m }], 0, 1, null, null, null, null);
            result.ColumnTotals = new Dictionary<string, decimal> { ["amount"] = 42m };
            result.CurrencyTotals = new ReportCurrencyTotalsSummary
            {
                Currencies = [new ReportCurrencyTotal { Currency = "USD", NoOfLicences = 2, TotalValue = 5m }],
                GrandTotalLicences = 2,
            };

            return Task.FromResult<ActionResult<ApiResult<Row>>>(Ok(result));
        }
    }

    private sealed class RejectingController : ControllerBase
    {
        [HttpPost]
        public Task<ActionResult<ApiResult<Row>>> Post([FromBody] Request? request)
            => Task.FromResult<ActionResult<ApiResult<Row>>>(BadRequest("FromDate is required."));
    }

    private sealed class NoTotalsController : ControllerBase
    {
        [HttpPost]
        public Task<ActionResult<string>> Post([FromBody] Request? request)
            => Task.FromResult<ActionResult<string>>("no totals here");
    }

    private sealed class OverridingController : ControllerBase, IExcelFooterTotalsProvider
    {
        [HttpPost]
        public Task<ActionResult<ApiResult<Row>>> Post([FromBody] Request? request)
            => throw new InvalidOperationException("the probe must not be used");

        [NonAction]
        public Task<ReportFooterTotals?> GetExcelFooterTotalsAsync(object request, CancellationToken cancellationToken)
            => Task.FromResult<ReportFooterTotals?>(
                new ReportFooterTotals(new Dictionary<string, decimal> { ["amount"] = 7m }, null));
    }

    private static DefaultExcelFooterTotalsResolver Resolver(
        FooterTotalsPolicy policy = FooterTotalsPolicy.Required)
        => new(
            new ServiceCollection().BuildServiceProvider(),
            Options.Create(new ExcelExportOptions { FooterTotals = policy }),
            NullLogger<DefaultExcelFooterTotalsResolver>.Instance);

    [Fact]
    public async Task The_probe_asks_for_one_row_with_the_exact_count_and_no_spec()
    {
        var request = new Request
        {
            FromDate = new DateTime(2025, 2, 1),
            ToDate = new DateTime(2025, 2, 28),
            PageIndex = 3,
            PageSize = 50,
            Excel = ExcelSpecFactory.Spec("R", ExcelSpecFactory.Column("amount", "Amount", "money")),
        };

        var totals = await Resolver().ResolveAsync(
            new ProbeController(), typeof(ProbeController), request, CancellationToken.None);

        Assert.NotNull(ProbeController.LastRequest);
        Assert.Equal(0, ProbeController.LastRequest!.PageIndex);
        Assert.Equal(1, ProbeController.LastRequest.PageSize);
        Assert.True(ProbeController.LastRequest.IncludeTotalCount);
        Assert.Null(ProbeController.LastRequest.Excel);
        Assert.Equal(new DateTime(2025, 2, 1), ProbeController.LastRequest.FromDate);

        // The caller's own request object is never mutated.
        Assert.Equal(3, request.PageIndex);
        Assert.NotNull(request.Excel);

        Assert.Equal(42m, totals!.ColumnTotals!["amount"]);
        Assert.Equal(2, totals.CurrencyTotals!.GrandTotalLicences);
    }

    [Fact]
    public async Task A_controller_that_overrides_the_probe_is_asked_directly()
    {
        var totals = await Resolver().ResolveAsync(
            new OverridingController(), typeof(OverridingController), new Request(), CancellationToken.None);

        Assert.Equal(7m, totals!.ColumnTotals!["amount"]);
    }

    [Fact]
    public async Task A_Post_that_carries_no_totals_means_no_footer_rather_than_a_failure()
    {
        var totals = await Resolver().ResolveAsync(
            new NoTotalsController(), typeof(NoTotalsController), new Request(), CancellationToken.None);

        Assert.Null(totals);
    }

    [Fact]
    public async Task A_rejected_probe_fails_the_job_under_the_default_policy()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => Resolver().ResolveAsync(
            new RejectingController(), typeof(RejectingController), new Request(), CancellationToken.None));

    [Fact]
    public async Task BestEffort_logs_and_drops_the_footer_instead()
    {
        var totals = await Resolver(FooterTotalsPolicy.BestEffort).ResolveAsync(
            new RejectingController(), typeof(RejectingController), new Request(), CancellationToken.None);

        Assert.Null(totals);
    }

    [Fact]
    public void ApiResult_exposes_its_totals_through_the_shared_interface()
    {
        var result = ApiResult<Row>.CreateFastPageFromRows([], 0, 10, null, null, null, null);
        result.ColumnTotals = new Dictionary<string, decimal> { ["amount"] = 1m };

        var totals = Assert.IsAssignableFrom<IReportTotals>(result);
        Assert.Equal(1m, totals.ColumnTotals!["amount"]);
    }
}
