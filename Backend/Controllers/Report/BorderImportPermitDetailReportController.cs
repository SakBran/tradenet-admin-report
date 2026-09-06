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
    // v2: the rows now come from the legacy dbo.sp_ImportPermitDetailReport query (order,
    // Company Address text, fn_GetNRCNo), so an unchanged request payload maps to a different
    // sheet. The export cache keys on payload + this version; without the bump a closed-period
    // request would keep serving the previous workbook for 24h.
    [ExcelFormatVersion(2)]
    // IExcelNoFooterReport: BorderImportPermitDetailReport.rdlc has no total row (only the
    // "Details" group, rdlc:3009), so the sheet must not synthesise one either.
    public class BorderImportPermitDetailReportController : ControllerBase, IStreamingExcelReport, IExcelNoFooterReport
    {
        private const string ReportKey = "BorderImportPermitDetailReport";

        private readonly TradeNetDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IExcelExportJobService _excelExportJobs;

        public BorderImportPermitDetailReportController(
            TradeNetDbContext context,
            IMemoryCache cache,
            IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _cache = cache;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_ImportPermitDetailReportResult>>> Post([FromBody] BorderImportPermitDetailReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            // Byte-identical to the old report (owner decision 2026-09-06): the legacy
            // dbo.sp_ImportPermitDetailReport 'Border' query verbatim, paged
            // (dbo.sp_BorderImportPermitDetailReport_pagination). Falls back to the LINQ twin
            // where the procedure is not deployed yet (SQL error 2812).
            var result = await sp_BorderImportPermitDetailReport.CreatePagedResultAsync(
                _context, _cache, procedureRequest!, request!, HttpContext.RequestAborted);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] BorderImportPermitDetailReportRequest? request)
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
        public string ExcelWorksheetTitle => "Border Import Permit Detail Report";
        public Type ExcelRequestType => typeof(BorderImportPermitDetailReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((BorderImportPermitDetailReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            BorderImportPermitDetailReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            // Same procedure as the grid (every row, same order), so the sheet is the grid.
            await foreach (var chunk in sp_BorderImportPermitDetailReport.StreamResolvedChunksAsync(
                _context, _cache, procedureRequest!, chunkSize, cancellationToken))
            {
                sink.Append(chunk);
            }
        }

        private bool TryCreateReportRequest(
            BorderImportPermitDetailReportRequest? request,
            out sp_ImportPermitDetailReportRequest? procedureRequest,
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
            procedureRequest = new sp_ImportPermitDetailReportRequest
            {
                // Legacy ReportsController.cs:14460 (model.Type = AppConfig.Border); the request's
                // own Type is ignored, as the old hidden field was.
                Type = "Border",
                // Passed through unchanged: the page posts <day>T00:00:00 / <day>T23:59:59, which
                // is exactly what the old Reports.GetImportPermitDetailReport appended
                // (" 00:00:00" / " 23:59:59") before calling the procedure.
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                PaThaKaTypeId = request.PaThaKaTypeId,
                ExportImportSectionId = request.ExportImportSectionId,
                SellerCountryId = request.SellerCountryId,
                CompanyRegistrationNo = request.CompanyRegistrationNo,
                SakhanId = request.SakhanId,
            };

            return true;
        }
    }

    public sealed class BorderImportPermitDetailReportRequest : ReportQueryRequest
    {
        public string Type { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int PaThaKaTypeId { get; set; }
        public int ExportImportSectionId { get; set; }
        public int SellerCountryId { get; set; }
        public string CompanyRegistrationNo { get; set; } = string.Empty;
        public int SakhanId { get; set; }
    }
}

