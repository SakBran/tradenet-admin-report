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
    // v2: the report now runs the legacy oversea 'Export Permit' query in the old report's row
    // order (see TryCreateReportRequest), so the row set changed for an unchanged request payload.
    // The export cache keys on payload + this version; without the bump a closed-period request
    // would keep serving the border-only workbook for 24h.
    [ExcelFormatVersion(2)]
    public class BorderExportPermitByHSCodeReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "BorderExportPermitByHSCodeReport";

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public BorderExportPermitByHSCodeReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<ReportAggregateResult>>> Post([FromBody] BorderExportPermitByHSCodeReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var result = await sp_HSCodeReport.CreateAggregateResultAsync(_context, procedureRequest!, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] BorderExportPermitByHSCodeReportRequest? request)
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
        public string ExcelWorksheetTitle => "Border Export Permit By HS Code Report";
        public Type ExcelRequestType => typeof(BorderExportPermitByHSCodeReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((BorderExportPermitByHSCodeReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            BorderExportPermitByHSCodeReportRequest request,
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
            BorderExportPermitByHSCodeReportRequest? request,
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
                // DELIBERATELY "Export Permit", not "Border Export Permit" -- bug-for-bug parity
                // with Tradenet 2.0. The old Border Export Permit By HS Code screen sets
                // `model.FormType = AppConfig.ExportPermit` and posts it back through a hidden
                // field (legacy ReportsController.cs:14120 / Views/Reports/
                // BorderExportPermitByHSCodeReport.cshtml:21), so it has always run
                // dbo.sp_HSCodeReport's OVERSEA ExportPermit branch: LicenceDate window,
                // @SakhanId ignored, no section parameter at all, grouped on (HSCodeId, Currency)
                // by BorderHSCodeReport.rdlc. The customer compares this report against that
                // screen ("record မကိုက်ပါ", 2026-09-07) and the owner's standing instruction for
                // the Border Import Permit twin (2026-09-05) is "same result as the old report".
                // Do not "fix" this back to Border without a new decision;
                // BorderExportPermitByHSCodeLegacyParityTests pins it.
                FormType = "Export Permit",
                FilterType = request.FilterType ?? string.Empty,
                HSCode = request.HSCode ?? string.Empty,
                // Passed for filter-box parity only; the Export Permit branch ignores it, exactly
                // as the old screen's Sakhan dropdown did. ExportImportSectionId is likewise never
                // mapped (the old form never sent it to the procedure either).
                SakhanId = request.SakhanId,
                // The HS Code detail drill (BorderExportPermitHSCodeDetailReport) posts
                // GroupBy='Company' to get HSCodeDetailReport.rdlc's (HS code, company) rows.
                GroupByCompany = string.Equals(request.GroupBy, "Company", StringComparison.OrdinalIgnoreCase),
                // Byte-identical to the old report: legacy dbo.sp_HSCodeReport ends with
                // ORDER BY HSCode.Id and neither RDLC sorts, so the groups print in HS code ID order
                // with first-appearance ties -- not the deployed procedure's HS code string /
                // currency order.
                LegacyOrder = true,
            };

            return true;
        }
    }

    public sealed class BorderExportPermitByHSCodeReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string FormType { get; set; } = string.Empty;
        public string FilterType { get; set; } = string.Empty;
        public string HSCode { get; set; } = string.Empty;

        /// <summary>
        /// Still posted by the Export Section box (kept for filter-box parity with the old form),
        /// but never mapped: legacy dbo.sp_HSCodeReport has no section parameter.
        /// </summary>
        public int ExportImportSectionId { get; set; }
        public int SakhanId { get; set; }

        /// <summary>
        /// 'Company' from the HS Code detail drill; empty from the summary. A string, not a bool,
        /// because the page posts derived filter values as strings.
        /// </summary>
        public string GroupBy { get; set; } = string.Empty;
    }
}

