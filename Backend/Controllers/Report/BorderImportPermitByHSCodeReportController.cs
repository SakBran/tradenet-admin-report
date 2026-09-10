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
    // v4: the report reads the BORDER tables (2026-09-10) -- a different row set for an unchanged
    // payload, and the Sakhan/Import Section filters now apply. Mandatory: the export cache is keyed
    // on payload + this version only, so without the bump a closed-period request would keep serving
    // the oversea workbook for 24h.
    [ExcelFormatVersion(4)]
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
                // Border, NOT the oversea "Import Permit" -- reversed on 2026-09-10.
                //
                // Until then this screen ran dbo.sp_HSCodeReport's OVERSEA ImportPermit branch
                // bug-for-bug, because the old Tradenet 2.0 screen does: legacy
                // ReportsController.cs:15465 sets `model.FormType = AppConfig.ImportPermit`, so the
                // old report has always listed oversea permits under a Border title, and its Sakhan
                // dropdown could not work -- the oversea ImportPermit table has no SakhanId column.
                // The owner asked for that same result on 2026-09-05.
                //
                // The customer then asked for the Sakhan filter to work, saying the old report is
                // the thing that is wrong ("Old Reportမှာမှားနေလို့ပါ", 2026-09-10). This now reads
                // the real border permits, so Sakhan filters. Measured cost on the customer's
                // window (2024-05-01..2026-09-06): 1,014 rows / 1,328 permits -> 31 / 112.
                // Doc: docs/BorderPermitByHSCodeSakhanSwitch_2026-09-10.md.
                FormType = "Border Import Permit",
                FilterType = request.FilterType ?? string.Empty,
                HSCode = request.HSCode ?? string.Empty,
                // Live since the switch: sp_HSCodeReport.BorderImportPermitRows filters on both.
                // The section predicate is an addition of ours -- the legacy Border branch of
                // dbo.sp_HSCodeReport has no section parameter at all -- kept so the Import Section
                // box does something rather than sitting dead next to a working Sakhan box.
                SakhanId = request.SakhanId,
                ExportImportSectionId = request.ExportImportSectionId,
                // The HS Code detail drill (BorderImportPermitHSCodeDetailReport) posts
                // GroupBy='Company' to get HSCodeDetailReport.rdlc's (HS code, company) rows.
                GroupByCompany = string.Equals(request.GroupBy, "Company", StringComparison.OrdinalIgnoreCase),
                // Keep this true even though the report no longer chases the old row SET. It is
                // load-bearing for two other reasons: it forces the LINQ path
                // (UsesAggregateStoredProcedure returns false for it), and it makes AggregateQuery
                // take the (HSCodeId, Currency) branch BEFORE GroupsByCompany -- which would
                // otherwise split the summary per buyer company, the defect fixed on 2026-09-08.
                // It also keeps BorderHSCodeReport.rdlc's row order: HS code ID, first-appearance
                // ties (hence PermitCreatedDate/PermitId on the Border projection).
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
        /// The Import Section box. Added 2026-09-10 with the switch to the Border tables -- the box
        /// had always been rendered and posted, but this DTO had no property to bind it to, so the
        /// value was dropped before it reached the query.
        /// </summary>
        public int ExportImportSectionId { get; set; }

        /// <summary>
        /// 'Company' from the HS Code detail drill; empty from the summary. A string, not a bool,
        /// because the page posts derived filter values as strings.
        /// </summary>
        public string GroupBy { get; set; } = string.Empty;
    }
}

