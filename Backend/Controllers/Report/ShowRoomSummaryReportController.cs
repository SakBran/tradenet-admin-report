using System;
using System.Collections.Generic;
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
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ShowRoomSummaryReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "ShowRoomSummaryReport";

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public ShowRoomSummaryReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<RegistrationSummaryRow>>> Post(
            [FromBody] ShowRoomSummaryReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var row = await sp_ShowRoomReport.SummaryRowAsync(_context, reportRequest!);
            var result = ApiResult<RegistrationSummaryRow>.CreatePageFromRows(
                new List<RegistrationSummaryRow> { row }, 1, 0, Math.Max(request!.PageSize, 1));

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ShowRoomSummaryReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out _, out var errorResult))
            {
                return errorResult!;
            }

            var result = await _excelExportJobs.EnqueueAsync(
                ReportKey,
                request!,
                request!.ToDate,
                User.FindFirst(ClaimTypes.Name)?.Value);

            return Ok(result);
        }

        // --- Async Excel export streaming (used by the background queue worker) ---
        public string ExcelWorksheetTitle => "Show Room Summary Report";
        public Type ExcelRequestType => typeof(ShowRoomSummaryReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((ShowRoomSummaryReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            ShowRoomSummaryReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            var row = await sp_ShowRoomReport.SummaryRowAsync(_context, procedureRequest!);
            sink.Append(new[] { row });
        }

        private bool TryCreateReportRequest(
            ShowRoomSummaryReportRequest? request,
            out sp_ShowRoomReportRequest? reportRequest,
            out ActionResult? errorResult)
        {
            reportRequest = null;
            errorResult = null;

            if (request == null)
            {
                errorResult = BadRequest("Request body is required.");
                return false;
            }

            if (request.FromDate == default)
            {
                errorResult = BadRequest("FromDate is required.");
                return false;
            }

            if (request.ToDate == default)
            {
                errorResult = BadRequest("ToDate is required.");
                return false;
            }

            if (request.ToDate < request.FromDate)
            {
                errorResult = BadRequest("ToDate must be greater than or equal to FromDate.");
                return false;
            }

            reportRequest = new sp_ShowRoomReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                Date = request.ToDate.Date,
                ApplyType = string.Empty,
                AllowedFormTypes = sp_ShowRoomReport.ResolveFormTypes(request.FormType),
                Type = "Summary"
            };

            return true;
        }
    }

    public sealed class ShowRoomSummaryReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? FormType { get; set; }
    }
}
