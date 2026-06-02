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
    public class OGARecommendationReportController : ControllerBase
    {
        private readonly TradeNetDbContext _context;

        public OGARecommendationReportController(TradeNetDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_OGARecommendationListReportResult>>> Post(
            [FromBody] OGARecommendationReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_OGARecommendationListReport.Query(_context, reportRequest!);
            var result = await ReportQueryService.CreatePagedResultAsync(query, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] OGARecommendationReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_OGARecommendationListReport.Query(_context, reportRequest!);
            byte[] fileBytes;
            try
            {
                fileBytes = await ExcelGenerator.CreateWorkbookAsync(
                    query,
                    request!,
                    "OGA Recommendation Report");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            return File(
                fileBytes,
                ExcelGenerator.ContentType,
                "OGARecommendationReport.xlsx");
        }

        private bool TryCreateReportRequest(
            OGARecommendationReportRequest? request,
            out sp_OGARecommendationListReportRequest? reportRequest,
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

            reportRequest = new sp_OGARecommendationListReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                OGADepartmentId = request.OGADepartmentId,
                OGASectionId = request.OGASectionId,
                CompanyRegistrationNo = request.CompanyRegistrationNo?.Trim() ?? string.Empty,
                ReferenceNo = request.ReferenceNo?.Trim() ?? string.Empty
            };

            return true;
        }
    }

    public sealed class OGARecommendationReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int OGADepartmentId { get; set; }
        public int OGASectionId { get; set; }
        public string? CompanyRegistrationNo { get; set; }
        public string? ReferenceNo { get; set; }
    }
}
