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
    // v3: the report reads the BORDER tables (2026-09-10) -- a different row set for an unchanged
    // payload, and the Sakhan/Export Section filters now apply. Mandatory: the export cache is keyed
    // on payload + this version only, so without the bump a closed-period request would keep serving
    // the oversea workbook for 24h.
    [ExcelFormatVersion(3)]
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
                // Border, NOT the oversea "Export Permit" -- reversed on 2026-09-10, same decision
                // as the Border Import Permit twin.
                //
                // Until then this screen ran dbo.sp_HSCodeReport's OVERSEA ExportPermit branch
                // bug-for-bug, because the old Tradenet 2.0 screen does: legacy
                // ReportsController.cs:14120 sets `model.FormType = AppConfig.ExportPermit` and
                // posts it back through a hidden field (Views/Reports/
                // BorderExportPermitByHSCodeReport.cshtml:21). So the old report has always listed
                // oversea permits under a Border title, and its Sakhan dropdown could not work --
                // the oversea ExportPermit table has no SakhanId column.
                //
                // The customer then asked for the Sakhan filter to work, saying the old report is
                // the thing that is wrong ("Old Reportမှာမှားနေလို့ပါ", 2026-09-10). Measured cost on
                // their window (2024-05-01..2026-09-06): 578 rows / 2,637 permits -> 12 / 8.
                // Doc: docs/BorderPermitByHSCodeSakhanSwitch_2026-09-10.md.
                FormType = "Border Export Permit",
                FilterType = request.FilterType ?? string.Empty,
                HSCode = request.HSCode ?? string.Empty,
                // Live since the switch: sp_HSCodeReport.BorderExportPermitRows filters on both.
                // The section predicate is an addition of ours -- the legacy Border branch of
                // dbo.sp_HSCodeReport has no section parameter at all -- kept so the Export Section
                // box does something rather than sitting dead next to a working Sakhan box.
                SakhanId = request.SakhanId,
                ExportImportSectionId = request.ExportImportSectionId,
                // The HS Code detail drill (BorderExportPermitHSCodeDetailReport) posts
                // GroupBy='Company' to get HSCodeDetailReport.rdlc's (HS code, company) rows.
                GroupByCompany = string.Equals(request.GroupBy, "Company", StringComparison.OrdinalIgnoreCase),
                // Keep this true even though the report no longer chases the old row SET. It forces
                // the LINQ path (UsesAggregateStoredProcedure returns false for it) and makes
                // AggregateQuery take the (HSCodeId, Currency) branch BEFORE GroupsByCompany --
                // which returns true unconditionally for "Border Export Permit", so dropping the
                // flag would split the summary per buyer company (the defect fixed 2026-09-08).
                // It also keeps BorderHSCodeReport.rdlc's row order: HS code ID, first-appearance
                // ties (hence PermitCreatedDate/PermitId on the Border projection).
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
        /// The Export Section box. Mapped onto the query since 2026-09-10; before that it was
        /// posted and dropped. Note this is an addition of ours, not legacy behaviour: legacy
        /// dbo.sp_HSCodeReport has no section parameter on any branch.
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

