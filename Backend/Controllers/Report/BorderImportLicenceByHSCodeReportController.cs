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
    // v2: the summary is no longer split per buyer company -- BorderHSCodeReport.rdlc groups on
    // (HSCodeId, Currency) only (rdlc:1159-1162), so 2025 goes from 9,213 rows to the old report's
    // 2,881 (2026 to 14/09: 7,420 -> 2,690), and each Total Value is now the whole HS code's sum
    // (customer complaint 2026-09-14). The export cache keys on payload + this version; without
    // the bump a closed-period request keeps serving the company-split workbook.
    [ExcelFormatVersion(2)]
    public class BorderImportLicenceByHSCodeReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "BorderImportLicenceByHSCodeReport";

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public BorderImportLicenceByHSCodeReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<ReportAggregateResult>>> Post([FromBody] BorderImportLicenceByHSCodeReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var result = await sp_HSCodeReport.CreateAggregateResultAsync(_context, procedureRequest!, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] BorderImportLicenceByHSCodeReportRequest? request)
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
        public string ExcelWorksheetTitle => "Border Import Licence By HS Code Report";
        public Type ExcelRequestType => typeof(BorderImportLicenceByHSCodeReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((BorderImportLicenceByHSCodeReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            BorderImportLicenceByHSCodeReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);

            // Match the grid's HS Code token exactly. The grid goes through
            // sp_HSCodeReport.CreateAggregateResultAsync, which trims @HSCode before the
            // LIKE (the deployed sp_HSCodeReport_pagination does LTRIM/RTRIM too); this
            // path calls GetAggregateRowsAsync directly, so without the trim a filter
            // typed with a stray space would LIKE ' 1006%' here and '1006%' in the grid.
            procedureRequest!.HSCode = procedureRequest.HSCode?.Trim() ?? string.Empty;

            // Row order already equals the grid's, so no re-sort here. The grid pages
            // through sp_HSCodeReport_pagination's Border Import Licence branch ("ORDER BY
            // result.HSCode,result.Currency,result.HSCodeId" -- BorderHSCodeReport.rdlc's
            // (HS code, currency) grain, HSCodeId only as the unique page-window tie-break)
            // when no Import Section is chosen, and through AggregateQuery otherwise -- and
            // AggregateQuery, which is also what GetAggregateRowsAsync streams, ends with
            // ORDER BY (HSCode, Currency) server-side: the same key in the same DB
            // collation. Re-sorting with ReportAggregationService.OrderGroups(...,
            // ReportAggregateDimension.HSCode, includeSakhan: false) would sort on
            // (HSCode, CompanyName, Currency) -- CompanyName is always null at this grain --
            // with StringComparer.OrdinalIgnoreCase, trading the DB collation for ordinal
            // semantics -- it could only move Excel rows AWAY from the grid order.
            var rows = await sp_HSCodeReport.GetAggregateRowsAsync(_context, procedureRequest);
            sink.Append(rows);
        }

        private bool TryCreateReportRequest(
            BorderImportLicenceByHSCodeReportRequest? request,
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
                FormType = "Border Import Licence",
                FilterType = request.FilterType ?? string.Empty,
                HSCode = request.HSCode ?? string.Empty,
                ExportImportSectionId = request.ExportImportSectionId,
                SakhanId = request.SakhanId,
                // The HS Code detail drill (BorderImportLicenceHSCodeDetailReport) posts
                // GroupBy='Company' to get HSCodeDetailReport.rdlc's (HS code, company) rows --
                // what the old screen's BorderHSCodeDetailReport action (ReportsController.cs:10526 on origin/master)
                // rendered. The summary posts nothing and keeps BorderHSCodeReport.rdlc's
                // (HS code, currency) grain.
                GroupByCompany = string.Equals(request.GroupBy, "Company", StringComparison.OrdinalIgnoreCase),
            };

            return true;
        }
    }

    public sealed class BorderImportLicenceByHSCodeReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string FormType { get; set; } = string.Empty;
        public string FilterType { get; set; } = string.Empty;
        public string HSCode { get; set; } = string.Empty;
        public int ExportImportSectionId { get; set; }
        public int SakhanId { get; set; }

        /// <summary>
        /// 'Company' from the HS Code detail drill; empty from the summary. A string, not a bool,
        /// because the page posts derived filter values as strings.
        /// </summary>
        public string GroupBy { get; set; } = string.Empty;
    }
}

