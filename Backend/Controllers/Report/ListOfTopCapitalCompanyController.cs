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
    public class ListOfTopCapitalCompanyController : ControllerBase
    {
        private readonly TradeNetDbContext _context;

        public ListOfTopCapitalCompanyController(TradeNetDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_PaThaKaReportResult>>> Post([FromBody] ListOfTopCapitalCompanyRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_PaThaKaReport.Query(_context, procedureRequest!);
            var result = await ReportQueryService.CreatePagedResultAsync(query, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ListOfTopCapitalCompanyRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_PaThaKaReport.Query(_context, procedureRequest!);
            byte[] fileBytes;
            try
            {
                fileBytes = await ExcelGenerator.CreateWorkbookAsync(
                    query,
                    request!,
                    "List of Top Capital Company");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            return File(
                fileBytes,
                ExcelGenerator.ContentType,
                "ListOfTopCapitalCompany.xlsx");
        }

        private bool TryCreateReportRequest(
            ListOfTopCapitalCompanyRequest? request,
            out sp_PaThaKaReportRequest? procedureRequest,
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
            procedureRequest = new sp_PaThaKaReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                BusinessTypeId = request.BusinessTypeId,
                LineofBusinessId = request.LineofBusinessId,
                State = request.State,
                Status = request.Status,
            };

            return true;
        }
    }

    public sealed class ListOfTopCapitalCompanyRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int BusinessTypeId { get; set; }
        public int LineofBusinessId { get; set; }
        public string State { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}

