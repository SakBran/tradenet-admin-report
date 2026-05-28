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
    public class RegistrationByBusinessTypeController : ControllerBase
    {
        private readonly TradeNetDbContext _context;

        public RegistrationByBusinessTypeController(TradeNetDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_PaThaKaByBusinessTypeReportResult>>> Post([FromBody] RegistrationByBusinessTypeRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_PaThaKaByBusinessTypeReport.Query(_context, procedureRequest!);
            var result = await ReportQueryService.CreatePagedResultAsync(query, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] RegistrationByBusinessTypeRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_PaThaKaByBusinessTypeReport.Query(_context, procedureRequest!);
            byte[] fileBytes;
            try
            {
                fileBytes = await ExcelGenerator.CreateWorkbookAsync(
                    query,
                    request!,
                    "Registration By Business Type");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            return File(
                fileBytes,
                ExcelGenerator.ContentType,
                "RegistrationByBusinessType.xlsx");
        }

        private bool TryCreateReportRequest(
            RegistrationByBusinessTypeRequest? request,
            out sp_PaThaKaByBusinessTypeReportRequest? procedureRequest,
            out ActionResult? errorResult)
        {
            procedureRequest = null;
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
            procedureRequest = new sp_PaThaKaByBusinessTypeReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                BusinessTypeId = request.BusinessTypeId,
            };

            return true;
        }
    }

    public sealed class RegistrationByBusinessTypeRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int BusinessTypeId { get; set; }
    }
}

