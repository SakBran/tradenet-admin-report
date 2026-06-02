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
    public class EVCycleShowRoomRegistrationByVoucherController : ControllerBase
    {
        private const string RegistrationType = "Show Room for Electric Cycles";

        private readonly TradeNetDbContext _context;

        public EVCycleShowRoomRegistrationByVoucherController(TradeNetDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_EVCycleShowRoomRegistrationReportResult>>> Post(
            [FromBody] EVCycleShowRoomRegistrationByVoucherRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_EVCycleShowRoomRegistrationReport.Query(_context, reportRequest!);
            var result = await ReportQueryService.CreatePagedResultAsync(query, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] EVCycleShowRoomRegistrationByVoucherRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_EVCycleShowRoomRegistrationReport.Query(_context, reportRequest!);
            byte[] fileBytes;
            try
            {
                fileBytes = await ExcelGenerator.CreateWorkbookAsync(
                    query,
                    request!,
                    "EVCycle Show Room Registration By Voucher");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            return File(
                fileBytes,
                ExcelGenerator.ContentType,
                "EVCycleShowRoomRegistrationByVoucher.xlsx");
        }

        private bool TryCreateReportRequest(
            EVCycleShowRoomRegistrationByVoucherRequest? request,
            out sp_EVCycleShowRoomRegistrationReportRequest? reportRequest,
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

            reportRequest = new sp_EVCycleShowRoomRegistrationReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                PaymentType = request.PaymentType?.Trim() ?? string.Empty,
                ApplyType = request.ApplyType?.Trim() ?? string.Empty,
                RegistrationType = RegistrationType
            };

            return true;
        }
    }

    public sealed class EVCycleShowRoomRegistrationByVoucherRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? ApplyType { get; set; }
        public string? PaymentType { get; set; }
    }
}
