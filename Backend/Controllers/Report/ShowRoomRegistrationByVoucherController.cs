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
    public class ShowRoomRegistrationByVoucherController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "ShowRoomRegistrationByVoucher";

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public ShowRoomRegistrationByVoucherController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_ShowRoomRegistrationReportResult>>> Post(
            [FromBody] ShowRoomRegistrationByVoucherRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_ShowRoomRegistrationReport.Query(_context, reportRequest!);
            var result = await ReportQueryService.CreatePagedResultAsync(query, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ShowRoomRegistrationByVoucherRequest? request)
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
        public string ExcelWorksheetTitle => "Show Room Registration By Voucher";
        public Type ExcelRequestType => typeof(ShowRoomRegistrationByVoucherRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((ShowRoomRegistrationByVoucherRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            ShowRoomRegistrationByVoucherRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            var query = sp_ShowRoomRegistrationReport.Query(_context, procedureRequest!);
            await foreach (var chunk in query.AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
            {
                sink.Append(chunk);
            }
        }

        private bool TryCreateReportRequest(
            ShowRoomRegistrationByVoucherRequest? request,
            out sp_ShowRoomRegistrationReportRequest? reportRequest,
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

            reportRequest = new sp_ShowRoomRegistrationReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                PaymentType = request.PaymentType?.Trim() ?? string.Empty,
                ApplyType = request.ApplyType?.Trim() ?? string.Empty,
                AllowedFormTypes = sp_ShowRoomReport.ResolveFormTypes(request.FormType)
            };

            return true;
        }
    }

    public sealed class ShowRoomRegistrationByVoucherRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? ApplyType { get; set; }
        public string? PaymentType { get; set; }
        public string? FormType { get; set; }
    }
}
