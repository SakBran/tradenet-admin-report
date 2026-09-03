using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Model;
using API.Model.ExcelExport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// Rejects an Excel enqueue that carries no presentation spec, at the edge, with a
    /// message the user can act on — instead of queueing a job that can only fail (or,
    /// worse, silently produce the old reflection dump) minutes later in the background
    /// worker. Also sanitizes and bounds a spec that IS present, since it drives file
    /// names, worksheet names and cell text.
    ///
    /// Only the <c>Excel</c> action of an <see cref="IStreamingExcelReport"/> controller
    /// is touched; the grid's own <c>Post</c> never is.
    /// </summary>
    public sealed class RequireExcelPresentationSpecFilter : IAsyncActionFilter
    {
        internal const string MissingSpecMessage =
            "Excel presentation spec missing — refresh the page and try again.";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!Applies(context, out var request))
            {
                await RunActionAsync(context, next);
                return;
            }

            // A null body is the controller's own BadRequest to report.
            if (request == null)
            {
                await RunActionAsync(context, next);
                return;
            }

            var controllerType = ((ControllerActionDescriptor)context.ActionDescriptor).ControllerTypeInfo.AsType();
            var hasTypedLayout = typeof(IExcelReportLayoutProvider).IsAssignableFrom(controllerType);

            if (request.Excel == null)
            {
                if (!hasTypedLayout)
                {
                    context.Result = Reject(MissingSpecMessage, Array.Empty<string>());
                    return;
                }

                await RunActionAsync(context, next);
                return;
            }

            try
            {
                // A typed layout supplies its own columns, so a spec may legitimately
                // carry only the title and header lines.
                ExcelPresentationSpecValidator.ValidateAndSanitize(request.Excel, requireColumns: !hasTypedLayout);
            }
            catch (ExcelPresentationSpecException ex)
            {
                context.Result = Reject("The Excel presentation spec is not valid.", ex.Errors);
                return;
            }

            // The spec drives the file name, the sheet title and the column set, so a
            // spec belonging to a DIFFERENT report must never be accepted here: it would
            // export this report's rows under the other report's name and columns.
            var reportKey = ReportKeyOf(controllerType);
            if (!string.Equals(request.Excel.ControllerName, reportKey, StringComparison.Ordinal))
            {
                context.Result = Reject(
                    "The Excel presentation spec is not valid.",
                    new[]
                    {
                        $"excel.controllerName: '{request.Excel.ControllerName}' is not this report ('{reportKey}').",
                    });
                return;
            }

            await RunActionAsync(context, next);
        }

        /// <summary>
        /// Runs the action and maps an <see cref="ExcelPresentationSpecException"/> the
        /// action itself raised (<c>ExcelExportJobService.EnqueueAsync</c> re-validates
        /// defensively) to the same 400 body — it must never surface as a 500.
        /// </summary>
        private static async Task RunActionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executed = await next();

            var specException = Unwrap(executed.Exception);
            if (specException == null)
            {
                return;
            }

            executed.Result = Reject("The Excel presentation spec is not valid.", specException.Errors);
            executed.ExceptionHandled = true;
        }

        private static ExcelPresentationSpecException? Unwrap(Exception? exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is ExcelPresentationSpecException specException)
                {
                    return specException;
                }
            }

            return null;
        }

        /// <summary>The registry's report key: the controller's class name minus "Controller".</summary>
        private static string ReportKeyOf(Type controllerType)
            => controllerType.Name.EndsWith("Controller", StringComparison.Ordinal)
                ? controllerType.Name[..^"Controller".Length]
                : controllerType.Name;

        private static bool Applies(ActionExecutingContext context, out ReportQueryRequest? request)
        {
            request = null;

            if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            {
                return false;
            }

            if (!string.Equals(descriptor.ActionName, "Excel", StringComparison.Ordinal))
            {
                return false;
            }

            if (!typeof(IStreamingExcelReport).IsAssignableFrom(descriptor.ControllerTypeInfo.AsType()))
            {
                return false;
            }

            var bound = context.ActionArguments.Values.OfType<ReportQueryRequest>().FirstOrDefault();
            if (bound != null)
            {
                request = bound;
                return true;
            }

            // The parameter exists but bound to null (empty body) — still our action.
            var declaresRequest = descriptor.MethodInfo
                .GetParameters()
                .Any(parameter => typeof(ReportQueryRequest).IsAssignableFrom(parameter.ParameterType));

            return declaresRequest;
        }

        private static BadRequestObjectResult Reject(string message, IReadOnlyList<string> errors)
            => new(new ExcelSpecRejection { Message = message, Errors = errors });

        /// <summary>The 400 body the frontend surfaces to the user.</summary>
        internal sealed class ExcelSpecRejection
        {
            public string Message { get; init; } = string.Empty;
            public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
        }
    }
}
