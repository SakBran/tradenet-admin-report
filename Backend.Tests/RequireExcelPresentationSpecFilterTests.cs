using System.Reflection;
using API.Model;
using API.Model.ExcelExport;
using API.Service.ExcelExport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Backend.Tests;

/// <summary>
/// A spec-less enqueue used to succeed and then quietly produce a sheet of C# property
/// names half an hour later. It must now fail at the edge with a message the user can
/// act on.
/// </summary>
public sealed class RequireExcelPresentationSpecFilterTests
{
    private sealed class Request : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    private class GenericReportController : ControllerBase, IStreamingExcelReport
    {
        public string ExcelWorksheetTitle => "Generic";
        public Type ExcelRequestType => typeof(Request);

        [HttpPost]
        public Task<ActionResult<ApiResult<string>>> Post([FromBody] Request? request)
            => Task.FromResult<ActionResult<ApiResult<string>>>(NotFound());

        [HttpPost("Excel")]
        public Task<IActionResult> Excel([FromBody] Request? request)
            => Task.FromResult<IActionResult>(Ok());

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class TypedLayoutReportController : GenericReportController, IExcelReportLayoutProvider
    {
        [NonAction]
        public ExcelReportLayout GetExcelLayout(object request) => ExcelReportLayout.None;
    }

    private static async Task<IActionResult?> RunAsync(
        Type controllerType,
        string actionName,
        object? request)
    {
        var method = controllerType.GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public)!;
        var descriptor = new ControllerActionDescriptor
        {
            ActionName = actionName,
            MethodInfo = method,
            ControllerTypeInfo = controllerType.GetTypeInfo(),
            Parameters = method
                .GetParameters()
                .Select(parameter => (ParameterDescriptor)new ControllerParameterDescriptor
                {
                    Name = parameter.Name!,
                    ParameterType = parameter.ParameterType,
                    ParameterInfo = parameter,
                })
                .ToList(),
        };

        var arguments = new Dictionary<string, object?>();
        if (request != null)
        {
            arguments["request"] = request;
        }

        var context = new ActionExecutingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor),
            [],
            arguments,
            controller: null!);

        var nextCalled = false;
        await new RequireExcelPresentationSpecFilter().OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], controller: null!));
        });

        if (context.Result == null)
        {
            Assert.True(nextCalled, "The filter neither rejected the request nor let it through.");
        }

        return context.Result;
    }

    private static ExcelPresentationSpec ValidSpec()
        => ExcelSpecFactory.Spec("GenericReport", ExcelSpecFactory.Column("companyName", "Company Name"));

    [Fact]
    public async Task An_Excel_enqueue_without_a_spec_is_a_400_the_user_can_act_on()
    {
        var result = await RunAsync(typeof(GenericReportController), "Excel", new Request());

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var body = Assert.IsType<RequireExcelPresentationSpecFilter.ExcelSpecRejection>(bad.Value);
        Assert.Equal(RequireExcelPresentationSpecFilter.MissingSpecMessage, body.Message);
    }

    [Fact]
    public async Task A_valid_spec_is_let_through_and_sanitized_in_place()
    {
        var spec = ValidSpec();
        spec.Title = "  Generic   Report  ";

        Assert.Null(await RunAsync(typeof(GenericReportController), "Excel", new Request { Excel = spec }));
        Assert.Equal("Generic Report", spec.Title);
    }

    [Fact]
    public async Task An_invalid_spec_is_a_400_listing_every_problem()
    {
        var spec = ExcelSpecFactory.Spec("GenericReport", ExcelSpecFactory.Column("not a path", "Col", "nope"));

        var result = await RunAsync(typeof(GenericReportController), "Excel", new Request { Excel = spec });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var body = Assert.IsType<RequireExcelPresentationSpecFilter.ExcelSpecRejection>(bad.Value);
        Assert.NotEmpty(body.Errors);
    }

    [Fact]
    public async Task A_report_with_its_own_typed_layout_needs_no_spec()
        => Assert.Null(await RunAsync(typeof(TypedLayoutReportController), "Excel", new Request()));

    [Fact]
    public async Task The_grids_own_Post_is_never_touched()
        => Assert.Null(await RunAsync(typeof(GenericReportController), "Post", new Request()));

    [Fact]
    public async Task An_empty_body_is_left_for_the_controllers_own_validation()
        => Assert.Null(await RunAsync(typeof(GenericReportController), "Excel", request: null));

    [Fact]
    public async Task A_spec_belonging_to_another_report_is_a_400()
    {
        // The spec drives the file name, the sheet title and the column set, so report
        // A's endpoint must not accept report B's spec.
        var spec = ExcelSpecFactory.Spec("SomeOtherReport", ExcelSpecFactory.Column("companyName", "Company Name"));

        var result = await RunAsync(typeof(GenericReportController), "Excel", new Request { Excel = spec });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var body = Assert.IsType<RequireExcelPresentationSpecFilter.ExcelSpecRejection>(bad.Value);
        Assert.Contains(body.Errors, error => error.Contains("SomeOtherReport", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_spec_exception_the_action_itself_raises_is_a_400_and_never_a_500()
    {
        // ExcelExportJobService.EnqueueAsync re-validates defensively; that throw has to
        // reach the user as the same 400 body, not as an unhandled 500.
        var executed = await RunToCompletionAsync(
            typeof(GenericReportController),
            "Excel",
            new Request { Excel = ValidSpec() },
            new ExcelPresentationSpecException(["excel.columns: too many columns."]));

        Assert.True(executed.ExceptionHandled);
        var bad = Assert.IsType<BadRequestObjectResult>(executed.Result);
        var body = Assert.IsType<RequireExcelPresentationSpecFilter.ExcelSpecRejection>(bad.Value);
        Assert.Contains("excel.columns: too many columns.", body.Errors);
    }

    /// <summary>Runs the filter with an action that fails the way the queue can fail.</summary>
    private static async Task<ActionExecutedContext> RunToCompletionAsync(
        Type controllerType,
        string actionName,
        object request,
        Exception actionException)
    {
        var method = controllerType.GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public)!;
        var descriptor = new ControllerActionDescriptor
        {
            ActionName = actionName,
            MethodInfo = method,
            ControllerTypeInfo = controllerType.GetTypeInfo(),
            Parameters = method
                .GetParameters()
                .Select(parameter => (ParameterDescriptor)new ControllerParameterDescriptor
                {
                    Name = parameter.Name!,
                    ParameterType = parameter.ParameterType,
                    ParameterInfo = parameter,
                })
                .ToList(),
        };

        var context = new ActionExecutingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor),
            [],
            new Dictionary<string, object?> { ["request"] = request },
            controller: null!);

        // MVC hands an action's exception back on the ActionExecutedContext rather than
        // throwing out of `next()`.
        var executed = new ActionExecutedContext(context, [], controller: null!)
        {
            Exception = actionException,
        };

        await new RequireExcelPresentationSpecFilter()
            .OnActionExecutionAsync(context, () => Task.FromResult(executed));

        Assert.Null(context.Result);
        return executed;
    }
}
