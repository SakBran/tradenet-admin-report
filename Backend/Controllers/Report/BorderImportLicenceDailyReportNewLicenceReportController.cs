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
    // v2: rows are no longer split by Sakhan -- the legacy rdlc groups on
    // (sLicenceDate, Currency) only (BorderImportLicenceByDailyReport.rdlc:1269-1272), and
    // this grid has no Sakhan column -- and the TOTAL row no longer sums Total Value, so
    // cached .xlsx files from the pre-fix shape must not be reused.
    [ExcelFormatVersion(2)]
    public class BorderImportLicenceDailyReportNewLicenceReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "BorderImportLicenceDailyReportNewLicenceReport";

        private readonly TradeNetDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IExcelExportJobService _excelExportJobs;

        public BorderImportLicenceDailyReportNewLicenceReportController(TradeNetDbContext context, IMemoryCache cache, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _cache = cache;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<ReportAggregateResult>>> Post([FromBody] BorderImportLicenceDailyReportNewLicenceReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            // includeSakhan: false -- the legacy report groups on (sLicenceDate, Currency) only
            // (BorderImportLicenceByDailyReport.rdlc:1269-1272); Sakhan is a *filter* there,
            // never a group key. Keeping it in the key split one "2026-01-01 / THB" row into
            // one row per border office, with nothing in the grid to tell them apart.
            //
            // CountOnly matches the legacy TOTAL row, which prints CountDistinct(LicenceNo)
            // under "No of Licences" (rdlc:1039) and leaves the Total Value cell blank
            // (Textbox7) -- each grid row is one (date, currency) pair, so summing the value
            // column adds THB + USD + CNY into a meaningless number. BuildColumnTotals still
            // emits totalUSDValue for the Daily dimension, which is what the legacy footer
            // prints in the last cell (rdlc:1200).
            var result = await sp_ImportLicenceDetailReport_Fast.CreateAggregateResultAsync(
                _context, procedureRequest!, request!, ReportAggregateDimension.Daily, includeSakhan: false,
                includeColumnTotals: true, columnTotalsMode: ReportColumnTotalsMode.CountOnly);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] BorderImportLicenceDailyReportNewLicenceReportRequest? request)
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
        public string ExcelWorksheetTitle => "Border Import Licence Daily Report (New Licence Report)";
        public Type ExcelRequestType => typeof(BorderImportLicenceDailyReportNewLicenceReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((BorderImportLicenceDailyReportNewLicenceReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            BorderImportLicenceDailyReportNewLicenceReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            var rows = await sp_ImportLicenceDetailReport_Fast.GetAggregateRowsAsync(
                _context, procedureRequest!, ReportAggregateDimension.Daily, includeSakhan: false);
            // Same canonical ordering the JSON grid path applies (CreateAggregateResultAsync ->
            // CreatePagedResultFromGroups -> Order), so the exported rows appear in the grid's
            // order. GetAggregateRowsAsync/AggregateInSqlAsync only GROUP BY -- it returns the
            // groups unordered.
            sink.Append(ReportAggregationService.OrderGroups(rows, ReportAggregateDimension.Daily, includeSakhan: false));
        }

        private bool TryCreateReportRequest(
            BorderImportLicenceDailyReportNewLicenceReportRequest? request,
            out sp_ImportLicenceDetailReportRequest? procedureRequest,
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
            procedureRequest = new sp_ImportLicenceDetailReportRequest
            {
                Type = "Border",
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                PaThaKaTypeId = request.PaThaKaTypeId,
                ExportImportSectionId = request.ExportImportSectionId,
                ExportImportMethodId = request.ExportImportMethodId,
                ExportImportIncotermId = request.ExportImportIncotermId,
                SellerCountryId = request.SellerCountryId,
                CompanyRegistrationNo = request.CompanyRegistrationNo,
                SakhanId = request.SakhanId,
            };

            return true;
        }
    }

    public sealed class BorderImportLicenceDailyReportNewLicenceReportRequest : ReportQueryRequest
    {
        public string Type { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int PaThaKaTypeId { get; set; }
        public int ExportImportSectionId { get; set; }
        public int ExportImportMethodId { get; set; }
        public int ExportImportIncotermId { get; set; }
        public int SellerCountryId { get; set; }
        public string CompanyRegistrationNo { get; set; } = string.Empty;
        public int SakhanId { get; set; }
    }
}

