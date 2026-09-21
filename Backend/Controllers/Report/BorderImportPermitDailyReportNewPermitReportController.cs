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
    // v2: the grand-total footer lost its Total Value cell (legacy parity), so cached
    // closed-period .xlsx files must not be reused.
    // v3: rows are no longer split by Sakhan -- the legacy rdlc groups on
    // (sLicenceDate, Currency) only (BorderImportPermitByDailyReport.rdlc:1269-1270) and this
    // grid has no Sakhan column, so the v2 row shape must not be reused either.
    [ExcelFormatVersion(3)]
    public class BorderImportPermitDailyReportNewPermitReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "BorderImportPermitDailyReportNewPermitReport";

        private readonly TradeNetDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IExcelExportJobService _excelExportJobs;

        public BorderImportPermitDailyReportNewPermitReportController(TradeNetDbContext context, IMemoryCache cache, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _cache = cache;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<ReportAggregateResult>>> Post([FromBody] BorderImportPermitDailyReportNewPermitReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            // includeSakhan: false -- the legacy report groups on (sLicenceDate, Currency) only
            // (BorderImportPermitByDailyReport.rdlc:1269-1270); Sakhan is a *filter* there,
            // never a group key. Keeping it in the key repeated the same (date, currency) pair
            // once per border office, with nothing in the grid to tell them apart.
            var result = await sp_ImportPermitDetailReport_Fast.CreateAggregateResultAsync(
                _context, procedureRequest!, request!, ReportAggregateDimension.Daily, includeSakhan: false,
                // The legacy TOTAL row prints only CountDistinct(LicenceNo) — the Total Value
                // cell is blank, because each row is one (group, currency) pair and summing
                // across currencies is meaningless (BorderImportPermitByDailyReport.rdlc).
                includeColumnTotals: true, columnTotalsMode: ReportColumnTotalsMode.CountOnly);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] BorderImportPermitDailyReportNewPermitReportRequest? request)
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
        public string ExcelWorksheetTitle => "Border Import Permit Daily Report (New Permit Report)";
        public Type ExcelRequestType => typeof(BorderImportPermitDailyReportNewPermitReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((BorderImportPermitDailyReportNewPermitReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            BorderImportPermitDailyReportNewPermitReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            var rows = await sp_ImportPermitDetailReport_Fast.GetAggregateRowsAsync(
                _context, procedureRequest!, ReportAggregateDimension.Daily, includeSakhan: false);

            // Same canonical ordering the JSON grid path applies (CreatePagedResultFromGroups -> Order),
            // so the exported rows appear in the order the user saw on screen.
            sink.Append(ReportAggregationService.OrderGroups(rows, ReportAggregateDimension.Daily, includeSakhan: false));
        }

        private bool TryCreateReportRequest(
            BorderImportPermitDailyReportNewPermitReportRequest? request,
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
                Type = "Border",
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

    public sealed class BorderImportPermitDailyReportNewPermitReportRequest : ReportQueryRequest
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

