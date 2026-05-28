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
    public class ListOfDirectorsByCompanyRegistrationNoController : ControllerBase
    {
        private readonly TradeNetDbContext _context;

        public ListOfDirectorsByCompanyRegistrationNoController(TradeNetDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_DirectorListReportResult>>> Post([FromBody] ListOfDirectorsByCompanyRegistrationNoRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_DirectorListReport.Query(_context, procedureRequest!);
            var result = await ReportQueryService.CreatePagedResultAsync(query, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ListOfDirectorsByCompanyRegistrationNoRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_DirectorListReport.Query(_context, procedureRequest!);
            byte[] fileBytes;
            try
            {
                fileBytes = await ExcelGenerator.CreateWorkbookAsync(
                    query,
                    request!,
                    "List of Directors By Company Registration No");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            return File(
                fileBytes,
                ExcelGenerator.ContentType,
                "ListOfDirectorsByCompanyRegistrationNo.xlsx");
        }

        private bool TryCreateReportRequest(
            ListOfDirectorsByCompanyRegistrationNoRequest? request,
            out sp_DirectorListReportRequest? procedureRequest,
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
            procedureRequest = new sp_DirectorListReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                CompanyRegistrationNo = request.CompanyRegistrationNo,
                Name = request.Name,
                Nationality = request.Nationality,
                NRCType = request.NRCType,
                NRCPrefixId = request.NRCPrefixId,
                NRCPrefixCodeId = request.NRCPrefixCodeId,
                NRCNo = request.NRCNo,
                Type = request.Type,
            };

            return true;
        }
    }

    public sealed class ListOfDirectorsByCompanyRegistrationNoRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string CompanyRegistrationNo { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Nationality { get; set; } = string.Empty;
        public string NRCType { get; set; } = string.Empty;
        public int NRCPrefixId { get; set; }
        public int NRCPrefixCodeId { get; set; }
        public string NRCNo { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}

