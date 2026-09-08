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
    // v2: the summary is no longer split per buyer company -- HSCodeReport.rdlc groups on
    // (HSCodeId, Currency) only (rdlc:1150-1160), so 31/08-01/09/2026 goes from 1058 rows to the
    // old report's 304, and each Total Value is now the whole HS code's sum instead of one buyer's
    // slice. The export cache keys on payload + this version; without the bump a closed-period
    // request keeps serving the company-split workbook.
    [ExcelFormatVersion(2)]
    public class ExportLicenceByHSCodeReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "ExportLicenceByHSCodeReport";

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public ExportLicenceByHSCodeReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<ReportAggregateResult>>> Post([FromBody] ExportLicenceByHSCodeReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var result = await sp_HSCodeReport.CreateAggregateResultAsync(_context, procedureRequest!, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ExportLicenceByHSCodeReportRequest? request)
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
        public string ExcelWorksheetTitle => "Export Licence By HS Code Report";
        public Type ExcelRequestType => typeof(ExportLicenceByHSCodeReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((ExportLicenceByHSCodeReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            ExportLicenceByHSCodeReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);

            // Match the grid's HS Code token exactly. The grid goes through
            // sp_HSCodeReport.CreateAggregateResultAsync, which trims @HSCode before the LIKE
            // (the deployed sp_HSCodeReport_pagination does LTRIM/RTRIM too); this path calls
            // GetAggregateRowsAsync directly, so without the trim a filter typed with a stray
            // space would LIKE ' 1006%' here and '1006%' in the grid.
            procedureRequest!.HSCode = procedureRequest.HSCode?.Trim() ?? string.Empty;

            // Row order already equals the grid's, so no re-sort here. The grid pages through
            // sp_HSCodeReport_pagination ("ORDER BY result.HSCode, result.CompanyName,
            // result.Currency") when no Export Section is chosen, and through AggregateQuery
            // otherwise -- and AggregateQuery, which is also what GetAggregateRowsAsync streams,
            // ends with exactly that ORDER BY server-side. Re-sorting with
            // ReportAggregationService.OrderGroups(..., ReportAggregateDimension.HSCode,
            // includeSakhan: false) would sort on the same keys but with
            // StringComparer.OrdinalIgnoreCase, trading the DB collation for ordinal semantics --
            // it could only move Excel rows AWAY from the grid order.
            var rows = await sp_HSCodeReport.GetAggregateRowsAsync(_context, procedureRequest);
            sink.Append(rows);
        }

        private bool TryCreateReportRequest(
            ExportLicenceByHSCodeReportRequest? request,
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
                FormType = "Export Licence",
                FilterType = request.FilterType ?? string.Empty,
                HSCode = request.HSCode ?? string.Empty,
                SakhanId = request.SakhanId,
                ExportImportSectionId = request.ExportImportSectionId,
                // The HS Code detail drill (ExportLicenceHSCodeDetailReport) posts
                // GroupBy='Company' to get HSCodeDetailReport.rdlc's (HS code, company) rows.
                // The summary posts nothing and keeps the RDLC's (HS code, currency) grain; the
                // two arrive here as otherwise identical parameters, so the config has to say
                // which shape it wants.
                GroupByCompany = string.Equals(request.GroupBy, "Company", StringComparison.OrdinalIgnoreCase),
            };

            return true;
        }
    }

    public sealed class ExportLicenceByHSCodeReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string FormType { get; set; } = string.Empty;
        public string FilterType { get; set; } = string.Empty;
        public string HSCode { get; set; } = string.Empty;
        public int SakhanId { get; set; }
        public int ExportImportSectionId { get; set; }

        /// <summary>
        /// 'Company' from the HS Code detail drill; empty from the summary. A string, not a bool,
        /// because the page posts derived filter values as strings.
        /// </summary>
        public string GroupBy { get; set; } = string.Empty;
    }
}

