using System;
using System.Threading.Tasks;
using API.DBContext;
using API.Model;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Controllers.Report
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AlcoholicBeveragesImportationDetailReportController : ControllerBase
    {
        private readonly TradeNetDbContext _context;
        private readonly IMemoryCache _cache;

        public AlcoholicBeveragesImportationDetailReportController(
            TradeNetDbContext context,
            IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_WineImportationReportResult>>> Post(
            [FromBody] AlcoholicBeveragesImportationDetailReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var result = await sp_WineImportationReport_Fast.CreatePagedResultAsync(
                _context,
                _cache,
                reportRequest!,
                request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel(
            [FromBody] AlcoholicBeveragesImportationDetailReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            byte[] fileBytes;
            try
            {
                fileBytes = await sp_WineImportationReport_Fast.CreateExcelWorkbookAsync(
                    _context,
                    _cache,
                    reportRequest!,
                    request!,
                    "Alcoholic Beverages Importation Detail Report");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            return File(
                fileBytes,
                ExcelGenerator.ContentType,
                "AlcoholicBeveragesImportationDetailReport.xlsx");
        }

        private bool TryCreateReportRequest(
            AlcoholicBeveragesImportationDetailReportRequest? request,
            out sp_WineImportationReportRequest? reportRequest,
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

            reportRequest = new sp_WineImportationReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                Date = request.ToDate.Date,
                ApplyType = request.ApplyType?.Trim() ?? string.Empty,
                Type = "Detail"
            };

            return true;
        }
    }

    public sealed class AlcoholicBeveragesImportationDetailReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? ApplyType { get; set; }
    }
}
