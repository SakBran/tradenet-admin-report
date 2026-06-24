using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.DBContext;
using API.Model;
using API.Service.ExcelExport;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers.Report
{
    /// <summary>
    /// The legacy "View Detail" drill target of the OGA Recommendation list: the
    /// usage history of a single recommendation (keyed by OGARecommendationId).
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class OGARecommendationHistoryReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "OGARecommendationHistoryReport";

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public OGARecommendationHistoryReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_OGARecommendationHistoryReportResult>>> Post(
            [FromBody] OGARecommendationHistoryReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_OGARecommendationHistoryReport.Query(_context, reportRequest!);
            var result = await ReportQueryService.CreatePagedResultAsync(query, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] OGARecommendationHistoryReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out _, out var errorResult))
            {
                return errorResult!;
            }

            var result = await _excelExportJobs.EnqueueAsync(
                ReportKey,
                request!,
                DateTime.UtcNow,
                User.FindFirst(ClaimTypes.Name)?.Value);

            return Ok(result);
        }

        // --- Async Excel export streaming (used by the background queue worker) ---
        public string ExcelWorksheetTitle => "OGA Recommendation History Report";
        public Type ExcelRequestType => typeof(OGARecommendationHistoryReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((OGARecommendationHistoryReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            OGARecommendationHistoryReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            var query = sp_OGARecommendationHistoryReport.Query(_context, procedureRequest!);
            await foreach (var chunk in query.AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
            {
                sink.Append(chunk);
            }
        }

        private bool TryCreateReportRequest(
            OGARecommendationHistoryReportRequest? request,
            out sp_OGARecommendationHistoryReportRequest? reportRequest,
            out ActionResult? errorResult)
        {
            reportRequest = null;
            errorResult = null;

            if (request == null)
            {
                errorResult = BadRequest("Request body is required.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.OGARecommendationId))
            {
                errorResult = BadRequest("OGARecommendationId is required.");
                return false;
            }

            reportRequest = new sp_OGARecommendationHistoryReportRequest
            {
                OGARecommendationId = request.OGARecommendationId.Trim()
            };

            return true;
        }
    }

    public sealed class OGARecommendationHistoryReportRequest : ReportQueryRequest
    {
        public string? OGARecommendationId { get; set; }
    }
}
