using System;
using System.Threading.Tasks;
using API.DBContext;
using API.Model;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers.Report
{
    [ApiController]
    [Route("api/[controller]")]
    public class MemberRegistrationReportController : ControllerBase
    {
        private readonly TradeNetDbContext _context;

        public MemberRegistrationReportController(TradeNetDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_MemberRegistrationReportResult>>> Post(
            [FromBody] MemberRegistrationReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_MemberRegistrationReport.Query(_context, reportRequest!);
            var result = await ReportQueryService.CreatePagedResultAsync(query, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] MemberRegistrationReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_MemberRegistrationReport.Query(_context, reportRequest!);
            byte[] fileBytes;
            try
            {
                fileBytes = await ExcelGenerator.CreateWorkbookAsync(
                    query,
                    request!,
                    "Member Registration Report");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            return File(
                fileBytes,
                ExcelGenerator.ContentType,
                "MemberRegistrationReport.xlsx");
        }

        private static string? NormalizeApplyType(string? applyType)
        {
            var value = string.IsNullOrWhiteSpace(applyType)
                ? "All"
                : applyType.Trim();

            return value.ToUpperInvariant() switch
            {
                "ALL" => "All",
                "NEW" => "New",
                "EXTENSION" => "Extension",
                _ => null
            };
        }

        private bool TryCreateReportRequest(
            MemberRegistrationReportRequest? request,
            out sp_MemberRegistrationReportRequest? reportRequest,
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

            var applyType = NormalizeApplyType(request.ApplyType);
            if (applyType == null)
            {
                errorResult = BadRequest("ApplyType must be All, New, or Extension.");
                return false;
            }

            reportRequest = new sp_MemberRegistrationReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                ApplyType = applyType
            };

            return true;
        }
    }

    public sealed class MemberRegistrationReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? ApplyType { get; set; } = "All";
    }
}
