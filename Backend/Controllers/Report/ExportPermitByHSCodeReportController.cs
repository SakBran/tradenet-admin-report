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

namespace Backend.Controllers.Report
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    // 2 = the (HS code, currency) grain of HSCodeReport.rdlc:1152-1153. Version 1 was the
    // company-split workbook; without this bump the Excel job queue keeps serving it.
    [ExcelFormatVersion(2)]
    public class ExportPermitByHSCodeReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "ExportPermitByHSCodeReport";

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public ExportPermitByHSCodeReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<ReportAggregateResult>>> Post([FromBody] ExportPermitByHSCodeReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var result = await sp_HSCodeReport.CreateAggregateResultAsync(_context, procedureRequest!, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ExportPermitByHSCodeReportRequest? request)
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
        public string ExcelWorksheetTitle => "Export Permit By HS Code Report";
        public Type ExcelRequestType => typeof(ExportPermitByHSCodeReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((ExportPermitByHSCodeReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            ExportPermitByHSCodeReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);

            // Match the grid's HS Code token exactly. The grid goes through
            // sp_HSCodeReport.CreateAggregateResultAsync, which trims @HSCode before the LIKE
            // (the deployed sp_HSCodeReport_pagination does LTRIM/RTRIM too); this path calls
            // GetAggregateRowsAsync directly, so without the trim a filter typed with a stray
            // space would LIKE ' 1006%' here and '1006%' in the grid -- and the Excel footer
            // (probed through Post) would then count a different row set than the sheet rows.
            procedureRequest!.HSCode = procedureRequest.HSCode?.Trim() ?? string.Empty;

            // Row order already equals the grid's, so no re-sort here. The grid pages through
            // sp_HSCodeReport_pagination ("ORDER BY result.HSCode, result.Currency,
            // result.HSCodeId" since 2026-09-21), and AggregateQuery -- what GetAggregateRowsAsync
            // streams -- ends with the same leading keys server-side (the HSCodeId tie-break only
            // exists inside the procedure, where it makes OFFSET/FETCH a unique-key sort; it can
            // separate rows here only when two HS code IDs share a code string AND a currency).
            // Re-sorting with ReportAggregationService.OrderGroups(...,
            // ReportAggregateDimension.HSCode, includeSakhan: false) would sort on the same keys
            // but with StringComparer.OrdinalIgnoreCase, trading the DB collation for ordinal
            // semantics -- it could only move Excel rows AWAY from the grid order.
            var rows = await sp_HSCodeReport.GetAggregateRowsAsync(_context, procedureRequest);
            sink.Append(rows);
        }

        private bool TryCreateReportRequest(
            ExportPermitByHSCodeReportRequest? request,
            out sp_HSCodeReportRequest? procedureRequest,
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
            procedureRequest = new sp_HSCodeReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                FormType = "Export Permit",
                FilterType = request.FilterType ?? string.Empty,
                HSCode = request.HSCode ?? string.Empty,
                SakhanId = request.SakhanId,
                ExportImportSectionId = request.ExportImportSectionId,
                // The HS Code detail drill (ExportPermitHSCodeDetailReport) posts
                // GroupBy='Company' to get HSCodeDetailReport.rdlc's (HS code, company) rows.
                // The summary posts nothing and keeps the RDLC's (HS code, currency) grain; the
                // two arrive here as otherwise identical parameters, so the config has to say
                // which shape it wants.
                GroupByCompany = string.Equals(request.GroupBy, "Company", StringComparison.OrdinalIgnoreCase),
            };

            return true;
        }
    }

    public sealed class ExportPermitByHSCodeReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string FormType { get; set; } = string.Empty;
        public string FilterType { get; set; } = string.Empty;
        public string HSCode { get; set; } = string.Empty;
        public int SakhanId { get; set; }

        /// <summary>
        /// The old ExportPermitByHSCodeReport.cshtml's "Export Section" dropdown
        /// (ReportsController.cs:7650, exportImportSectionRepository.GetAll(ExportPermit)).
        /// A non-zero value takes the report off sp_HSCodeReport_pagination onto the LINQ twin,
        /// which is the only path that filters on it.
        /// </summary>
        public int ExportImportSectionId { get; set; }

        /// <summary>
        /// 'Company' from the HS Code detail drill; empty from the summary. A string, not a bool,
        /// because the page posts derived filter values as strings.
        /// </summary>
        public string GroupBy { get; set; } = string.Empty;
    }
}

