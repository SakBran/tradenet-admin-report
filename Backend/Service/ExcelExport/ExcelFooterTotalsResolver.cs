using System;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Service.ExcelExport
{
    /// <summary>Where an export's footer numbers come from.</summary>
    public interface IExcelFooterTotalsResolver
    {
        /// <summary>
        /// The same totals the grid footer shows for these filters, or null when the
        /// report has no footer.
        /// </summary>
        /// <param name="report">The controller instance already created for this export.</param>
        Task<ReportFooterTotals?> ResolveAsync(
            object report,
            Type controllerType,
            object request,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Replays the report's own <c>Post</c> action for one row with
    /// <c>IncludeTotalCount = true</c> — byte for byte the grid's lazy exact-count
    /// request — and lifts <see cref="IReportTotals"/> off the response. That keeps the
    /// footer honest by construction: it is produced by the same SQL the grid footer used,
    /// never by a second implementation that can drift.
    ///
    /// A controller that must not be replayed (page-dependent totals, or a probe path
    /// that times out) implements <see cref="IExcelFooterTotalsProvider"/> instead.
    /// </summary>
    public sealed class DefaultExcelFooterTotalsResolver : IExcelFooterTotalsResolver
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly IServiceProvider _services;
        private readonly ExcelExportOptions _options;
        private readonly ILogger<DefaultExcelFooterTotalsResolver> _logger;

        public DefaultExcelFooterTotalsResolver(
            IServiceProvider services,
            IOptions<ExcelExportOptions> options,
            ILogger<DefaultExcelFooterTotalsResolver> logger)
        {
            _services = services;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<ReportFooterTotals?> ResolveAsync(
            object report,
            Type controllerType,
            object request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(controllerType);
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                if (report is IExcelFooterTotalsProvider provider)
                {
                    return await provider.GetExcelFooterTotalsAsync(request, cancellationToken);
                }

                return await ProbeAsync(controllerType, request);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (_options.FooterTotals == FooterTotalsPolicy.BestEffort)
                {
                    _logger.LogWarning(
                        ex,
                        "Excel export for '{Controller}': could not resolve the grid's footer totals — exporting without a footer.",
                        controllerType.Name);
                    return null;
                }

                throw;
            }
        }

        private async Task<ReportFooterTotals?> ProbeAsync(Type controllerType, object request)
        {
            var requestType = request.GetType();
            var post = ExcelRowTypeResolver.FindBarePost(controllerType, requestType);
            if (post == null)
            {
                _logger.LogDebug(
                    "Excel export for '{Controller}': no bare Post action to read footer totals from.",
                    controllerType.Name);
                return null;
            }

            var probeRequest = CloneForProbe(request, requestType);

            // No HttpContext: no report's Post touches HttpContext or User (only the
            // Excel enqueue action does), so the bare controller instance is enough.
            var controller = ActivatorUtilities.CreateInstance(_services, controllerType);
            var returned = post.Invoke(controller, new[] { probeRequest });

            if (returned is not Task task)
            {
                return null;
            }

            await task;

            var result = task.GetType().GetProperty("Result")?.GetValue(task);
            return ExtractTotals(result, controllerType);
        }

        /// <summary>
        /// A JSON round-trip clone so the export's own request object is never mutated,
        /// asking for exactly one row plus the exact count — the grid's lazy-totals call.
        /// </summary>
        private static object CloneForProbe(object request, Type requestType)
        {
            var json = JsonSerializer.Serialize(request, requestType, JsonOptions);
            var clone = JsonSerializer.Deserialize(json, requestType, JsonOptions)
                ?? throw new InvalidOperationException(
                    $"Could not clone the {requestType.Name} request to read the report's footer totals.");

            if (clone is ReportQueryRequest query)
            {
                query.PageIndex = 0;
                query.PageSize = 1;
                query.IncludeTotalCount = true;
                query.Excel = null;
            }

            return clone;
        }

        private ReportFooterTotals? ExtractTotals(object? result, Type controllerType)
        {
            if (result == null)
            {
                return null;
            }

            var actionResult = result is IConvertToActionResult convertible
                ? convertible.Convert()
                : result as IActionResult;

            switch (actionResult)
            {
                case ObjectResult objectResult:
                    if (objectResult.StatusCode is >= 400)
                    {
                        throw new InvalidOperationException(
                            $"{controllerType.Name}.Post rejected the export's own filters with HTTP {objectResult.StatusCode} " +
                            $"while reading the report's footer totals: {objectResult.Value}");
                    }

                    return FromValue(objectResult.Value);

                case StatusCodeResult statusCodeResult when statusCodeResult.StatusCode >= 400:
                    throw new InvalidOperationException(
                        $"{controllerType.Name}.Post returned HTTP {statusCodeResult.StatusCode} " +
                        "while reading the report's footer totals.");

                case null:
                    // Not an IActionResult at all: either the payload itself (a Post
                    // returning ApiResult<T> directly) or an ActionResult<T> carrying a
                    // bare Value.
                    return FromValue(
                        result is IReportTotals
                            ? result
                            : result.GetType().GetProperty("Value")?.GetValue(result));

                default:
                    return null;
            }
        }

        private static ReportFooterTotals? FromValue(object? value)
            => value is IReportTotals totals
                ? new ReportFooterTotals(totals.ColumnTotals, totals.CurrencyTotals)
                : null;
    }
}
