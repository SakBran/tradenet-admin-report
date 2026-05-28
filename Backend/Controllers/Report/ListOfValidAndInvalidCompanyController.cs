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
    public class ListOfValidAndInvalidCompanyController : ControllerBase
    {
        private readonly TradeNetDbContext _context;

        public ListOfValidAndInvalidCompanyController(TradeNetDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_PaThaKaValidInvalidReportResult>>> Post([FromBody] ListOfValidAndInvalidCompanyRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_PaThaKaValidInvalidReport.Query(_context, procedureRequest!);
            var result = await ReportQueryService.CreatePagedResultAsync(query, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ListOfValidAndInvalidCompanyRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_PaThaKaValidInvalidReport.Query(_context, procedureRequest!);
            byte[] fileBytes;
            try
            {
                fileBytes = await ExcelGenerator.CreateWorkbookAsync(
                    query,
                    request!,
                    "List of Valid and Invalid Company");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            return File(
                fileBytes,
                ExcelGenerator.ContentType,
                "ListOfValidAndInvalidCompany.xlsx");
        }

        private bool TryCreateReportRequest(
            ListOfValidAndInvalidCompanyRequest? request,
            out sp_PaThaKaValidInvalidReportRequest? procedureRequest,
            out ActionResult? errorResult)
        {
            procedureRequest = null;
            errorResult = null;

            if (request == null)
            {
                errorResult = BadRequest("Request body is required.");
                return false;
            }

            if (request.Date == default)
            {
                errorResult = BadRequest("Date is required.");
                return false;
            }
            procedureRequest = new sp_PaThaKaValidInvalidReportRequest
            {
                Date = request.Date,
                BusinessTypeId = request.BusinessTypeId,
                LineofBusinessId = request.LineofBusinessId,
                State = request.State,
                Status = request.Status,
                Type = request.Type,
            };

            return true;
        }
    }

    public sealed class ListOfValidAndInvalidCompanyRequest : ReportQueryRequest
    {
        public DateTime Date { get; set; }
        public int BusinessTypeId { get; set; }
        public int LineofBusinessId { get; set; }
        public string State { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}
