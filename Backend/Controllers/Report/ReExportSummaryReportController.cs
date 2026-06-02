using System;
using System.Threading.Tasks;
using API.DBContext;
using API.Model;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers.Report
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReExportSummaryReportController : ControllerBase
    {
        private readonly TradeNetDbContext _context;

        public ReExportSummaryReportController(TradeNetDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_ReExportReportResult>>> Post(
            [FromBody] ReExportSummaryReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_ReExportReport.Query(_context, reportRequest!);
            var result = await ReportQueryService.CreatePagedResultAsync(query, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ReExportSummaryReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_ReExportReport.Query(_context, reportRequest!);
            byte[] fileBytes;
            try
            {
                fileBytes = await ExcelGenerator.CreateWorkbookAsync(
                    query,
                    request!,
                    "Re-Export Summary Report");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            return File(
                fileBytes,
                ExcelGenerator.ContentType,
                "ReExportSummaryReport.xlsx");
        }

        private bool TryCreateReportRequest(
            ReExportSummaryReportRequest? request,
            out sp_ReExportReportRequest? reportRequest,
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

            reportRequest = new sp_ReExportReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                Date = request.ToDate.Date,
                ApplyType = string.Empty,
                Type = "Summary"
            };

            return true;
        }
    }

    public sealed class ReExportSummaryReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }
}
