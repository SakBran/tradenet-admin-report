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
    public class SaleCenterSummaryReportController : ControllerBase
    {
        private const string FormType = "Sale Center for Motor Vehicles";

        private readonly TradeNetDbContext _context;

        public SaleCenterSummaryReportController(TradeNetDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_SaleCenterReportResult>>> Post(
            [FromBody] SaleCenterSummaryReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_SaleCenterReport.Query(_context, reportRequest!);
            var result = await ReportQueryService.CreatePagedResultAsync(query, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] SaleCenterSummaryReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_SaleCenterReport.Query(_context, reportRequest!);
            byte[] fileBytes;
            try
            {
                fileBytes = await ExcelGenerator.CreateWorkbookAsync(
                    query,
                    request!,
                    "Sale Center Summary Report");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            return File(
                fileBytes,
                ExcelGenerator.ContentType,
                "SaleCenterSummaryReport.xlsx");
        }

        private bool TryCreateReportRequest(
            SaleCenterSummaryReportRequest? request,
            out sp_SaleCenterReportRequest? reportRequest,
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

            reportRequest = new sp_SaleCenterReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                Date = request.ToDate.Date,
                ApplyType = string.Empty,
                FormType = FormType,
                Type = "Summary"
            };

            return true;
        }
    }

    public sealed class SaleCenterSummaryReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }
}
