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
    public class WholeSaleDetailReportController : ControllerBase
    {
        private const string FormType = "Whole Sale";

        private readonly TradeNetDbContext _context;

        public WholeSaleDetailReportController(TradeNetDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_WholeSaleRetailReportResult>>> Post(
            [FromBody] WholeSaleDetailReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_WholeSaleRetailReport.Query(_context, reportRequest!);
            var result = await ReportQueryService.CreatePagedResultAsync(query, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] WholeSaleDetailReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_WholeSaleRetailReport.Query(_context, reportRequest!);
            byte[] fileBytes;
            try
            {
                fileBytes = await ExcelGenerator.CreateWorkbookAsync(
                    query,
                    request!,
                    "Whole Sale Detail Report");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            return File(
                fileBytes,
                ExcelGenerator.ContentType,
                "WholeSaleDetailReport.xlsx");
        }

        private bool TryCreateReportRequest(
            WholeSaleDetailReportRequest? request,
            out sp_WholeSaleRetailReportRequest? reportRequest,
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

            reportRequest = new sp_WholeSaleRetailReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                Date = request.ToDate.Date,
                ApplyType = request.ApplyType?.Trim() ?? string.Empty,
                FormType = FormType,
                Type = "Detail"
            };

            return true;
        }
    }

    public sealed class WholeSaleDetailReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? ApplyType { get; set; }
    }
}
