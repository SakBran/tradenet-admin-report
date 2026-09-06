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
    // v2: the report now runs the legacy oversea 'Import Permit' query (see TryCreateReportRequest),
    // so the row set changed for an unchanged request payload. The export cache keys on payload +
    // this version; without the bump a closed-period request would keep serving the border-only
    // workbook for 24h.
    // v3: rows are now in the old report's order (HS code ID, first appearance) and Total Value is
    // a 4-decimal money cell.
    [ExcelFormatVersion(3)]
    public class BorderImportPermitByHSCodeReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "BorderImportPermitByHSCodeReport";

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public BorderImportPermitByHSCodeReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<ReportAggregateResult>>> Post([FromBody] BorderImportPermitByHSCodeReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var result = await sp_HSCodeReport.CreateAggregateResultAsync(_context, procedureRequest!, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] BorderImportPermitByHSCodeReportRequest? request)
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
        public string ExcelWorksheetTitle => "Border Import Permit By HS Code Report";
        public Type ExcelRequestType => typeof(BorderImportPermitByHSCodeReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((BorderImportPermitByHSCodeReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            BorderImportPermitByHSCodeReportRequest request,
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

            // Row order already equals the grid's (LegacyOrder: both surfaces take the same
            // LINQ path -- AggregateQuery for the summary, LegacyCompanyGroupsAsync for the
            // drill), so no re-sort here: ReportAggregationService.OrderGroups would put the
            // rows back into HS code string order, away from the old report's.
            var rows = await sp_HSCodeReport.GetAggregateRowsAsync(_context, procedureRequest);
            sink.Append(rows);
        }

        private bool TryCreateReportRequest(
            BorderImportPermitByHSCodeReportRequest? request,
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
                // DELIBERATELY "Import Permit", not "Border Import Permit" -- bug-for-bug parity
                // with Tradenet 2.0. The old Border Import Permit By HS Code screen sets
                // `model.FormType = AppConfig.ImportPermit` (legacy ReportsController.cs:15465),
                // so it has always run dbo.sp_HSCodeReport's OVERSEA ImportPermit branch:
                // LicenceDate window, @SakhanId ignored, grouped on (HSCodeId, Currency) by
                // BorderHSCodeReport.rdlc. The customer compares this report against that
                // screen and the owner's instruction (2026-09-05) is "same result as the old
                // report". The border-only answer (18 licences over 2025) was rejected because
                // the old report shows 997. Do not "fix" this back to Border without a new
                // decision; BorderImportPermitByHSCodeLegacyParityTests pins it.
                FormType = "Import Permit",
                FilterType = request.FilterType ?? string.Empty,
                HSCode = request.HSCode ?? string.Empty,
                // Passed for filter-box parity only; the Import Permit branch ignores it, exactly
                // as the old screen's Sakhan dropdown did. ExportImportSectionId is likewise never
                // mapped (the old form never sent it to the procedure either).
                SakhanId = request.SakhanId,
                // The HS Code detail drill (BorderImportPermitHSCodeDetailReport) posts
                // GroupBy='Company' to get HSCodeDetailReport.rdlc's (HS code, company) rows.
                GroupByCompany = string.Equals(request.GroupBy, "Company", StringComparison.OrdinalIgnoreCase),
                // Byte-identical to the old report (owner decision 2026-09-06): legacy
                // dbo.sp_HSCodeReport ends with ORDER BY HSCode.Id and neither RDLC sorts, so the
                // groups print in HS code ID order with first-appearance ties -- not the deployed
                // procedure's HS code string / currency order.
                LegacyOrder = true,
            };

            return true;
        }
    }

    public sealed class BorderImportPermitByHSCodeReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string FormType { get; set; } = string.Empty;
        public string FilterType { get; set; } = string.Empty;
        public string HSCode { get; set; } = string.Empty;
        public int SakhanId { get; set; }

        /// <summary>
        /// 'Company' from the HS Code detail drill; empty from the summary. A string, not a bool,
        /// because the page posts derived filter values as strings.
        /// </summary>
        public string GroupBy { get; set; } = string.Empty;
    }
}

