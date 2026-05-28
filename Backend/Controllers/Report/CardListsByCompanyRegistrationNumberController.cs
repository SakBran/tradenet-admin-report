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
    public class CardListsByCompanyRegistrationNumberController : ControllerBase
    {
        private readonly TradeNetDbContext _context;

        public CardListsByCompanyRegistrationNumberController(TradeNetDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_CardListsByPaThaKaReportResult>>> Post([FromBody] CardListsByCompanyRegistrationNumberRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_CardListsByPaThaKaReport.Query(_context, procedureRequest!);
            var result = await ReportQueryService.CreatePagedResultAsync(query, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] CardListsByCompanyRegistrationNumberRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_CardListsByPaThaKaReport.Query(_context, procedureRequest!);
            byte[] fileBytes;
            try
            {
                fileBytes = await ExcelGenerator.CreateWorkbookAsync(
                    query,
                    request!,
                    "CardListsByCompanyRegistrationNumber");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            return File(
                fileBytes,
                ExcelGenerator.ContentType,
                "CardListsByCompanyRegistrationNumber.xlsx");
        }

        private bool TryCreateReportRequest(
            CardListsByCompanyRegistrationNumberRequest? request,
            out sp_CardListsByPaThaKaReportRequest? procedureRequest,
            out ActionResult? errorResult)
        {
            procedureRequest = null;
            errorResult = null;

            if (request == null)
            {
                errorResult = BadRequest("Request body is required.");
                return false;
            }

            procedureRequest = new sp_CardListsByPaThaKaReportRequest
            {
                CompanyRegistrationNo = request.CompanyRegistrationNo,
            };

            return true;
        }
    }

    public sealed class CardListsByCompanyRegistrationNumberRequest : ReportQueryRequest
    {
        public string CompanyRegistrationNo { get; set; } = string.Empty;
    }
}

