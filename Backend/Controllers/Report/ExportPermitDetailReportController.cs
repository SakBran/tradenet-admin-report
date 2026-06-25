using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.DBContext;
using API.Model;
using API.Service.ExcelExport;
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
    public class ExportPermitDetailReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "ExportPermitDetailReport";

        private readonly TradeNetDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IExcelExportJobService _excelExportJobs;

        public ExportPermitDetailReportController(TradeNetDbContext context, IMemoryCache cache, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _cache = cache;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_ExportPermitDetailReportResult>>> Post([FromBody] ExportPermitDetailReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var result = await sp_ExportPermitDetailReport_Fast.CreatePagedResultAsync(_context, _cache, procedureRequest!, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ExportPermitDetailReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out _, out var errorResult))
            {
                return errorResult!;
            }

            var result = await _excelExportJobs.EnqueueAsync(
                ReportKey,
                request!,
                request!.ToDate,
                User.FindFirst(ClaimTypes.Name)?.Value);

            return Ok(result);
        }

        // --- Async Excel export streaming (used by the background queue worker) ---
        public string ExcelWorksheetTitle => "Export Permit Detail Report";
        public Type ExcelRequestType => typeof(ExportPermitDetailReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((ExportPermitDetailReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            ExportPermitDetailReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            await foreach (var chunk in sp_ExportPermitDetailReport_Fast.StreamResolvedChunksAsync(
                _context, _cache, procedureRequest!, chunkSize, cancellationToken))
            {
                sink.Append(chunk);
            }
        }

        private bool TryCreateReportRequest(
            ExportPermitDetailReportRequest? request,
            out sp_ExportPermitDetailReportRequest? procedureRequest,
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
            procedureRequest = new sp_ExportPermitDetailReportRequest
            {
                Type = "Oversea",
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                PaThaKaTypeId = request.PaThaKaTypeId,
                ExportImportSectionId = request.ExportImportSectionId,
                BuyerCountryId = request.BuyerCountryId,
                CompanyRegistrationNo = request.CompanyRegistrationNo,
                SakhanId = request.SakhanId,
                HSCode = request.HSCode,
            };

            return true;
        }
    }

    public sealed class ExportPermitDetailReportRequest : ReportQueryRequest
    {
        public string Type { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int PaThaKaTypeId { get; set; }
        public int ExportImportSectionId { get; set; }
        public int BuyerCountryId { get; set; }
        public string CompanyRegistrationNo { get; set; } = string.Empty;
        public int SakhanId { get; set; }
        public string HSCode { get; set; } = string.Empty;
    }
}

