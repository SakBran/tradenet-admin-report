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
    // (sLicenceDate, Currency) only (BorderExportPermitByDailyReport.rdlc:1277-1278), and
    // this grid has no Sakhan column -- and the TOTAL row no longer sums Total Value, so
    // cached .xlsx files from the pre-fix shape must not be reused.
    [ExcelFormatVersion(2)]
    public class BorderExportPermitDailyReportNewPermitReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "BorderExportPermitDailyReportNewPermitReport";

        private readonly TradeNetDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IExcelExportJobService _excelExportJobs;

        public BorderExportPermitDailyReportNewPermitReportController(TradeNetDbContext context, IMemoryCache cache, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _cache = cache;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<ReportAggregateResult>>> Post([FromBody] BorderExportPermitDailyReportNewPermitReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            // includeSakhan: false -- the legacy report groups on (sLicenceDate, Currency) only
            // (BorderExportPermitByDailyReport.rdlc:1277-1278); Sakhan is a *filter* there,
            // never a group key. Keeping it in the key repeated the same (date, currency) pair
            // once per border office, with nothing in the grid to tell them apart.
            //
            // CountOnly matches the legacy TOTAL row, which prints CountDistinct(LicenceNo)
            // under "No of Permits" (rdlc:1047) and leaves the Total Value cell blank -- each
            // grid row is one (date, currency) pair, so summing the value column adds
            // THB + USD + CNY into a meaningless number. BuildColumnTotals still emits
            // totalUSDValue for the Daily dimension, as the legacy footer does (rdlc:1208).
            var result = await sp_ExportPermitDetailReport_Fast.CreateAggregateResultAsync(
                _context, procedureRequest!, request!, ReportAggregateDimension.Daily, includeSakhan: false,
                includeColumnTotals: true, columnTotalsMode: ReportColumnTotalsMode.CountOnly);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] BorderExportPermitDailyReportNewPermitReportRequest? request)
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
        public string ExcelWorksheetTitle => "Border Export Permit Daily Report (New Permit Report)";
        public Type ExcelRequestType => typeof(BorderExportPermitDailyReportNewPermitReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((BorderExportPermitDailyReportNewPermitReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            BorderExportPermitDailyReportNewPermitReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            var rows = await sp_ExportPermitDetailReport_Fast.GetAggregateRowsAsync(
                _context, procedureRequest!, ReportAggregateDimension.Daily, includeSakhan: false);

            // Same ordering the JSON grid path applies (CreatePagedResultFromGroups -> Order): Date,
            // then Currency (includeSakhan: false, matching this report's Post -- no Sakhan tie-break).
            // Stated here at the append site because that guarantee must not depend on the helper
            // keeping its own internal OrderGroups call.
            sink.Append(ReportAggregationService.OrderGroups(rows, ReportAggregateDimension.Daily, includeSakhan: false));
        }

        private bool TryCreateReportRequest(
            BorderExportPermitDailyReportNewPermitReportRequest? request,
            out sp_ExportPermitDetailReportRequest? procedureRequest,
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
            procedureRequest = new sp_ExportPermitDetailReportRequest
            {
                Type = "Border",
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                PaThaKaTypeId = request.PaThaKaTypeId,
                ExportImportSectionId = request.ExportImportSectionId,
                BuyerCountryId = request.BuyerCountryId,
                CompanyRegistrationNo = request.CompanyRegistrationNo,
                SakhanId = request.SakhanId,
            };

            return true;
        }
    }

    public sealed class BorderExportPermitDailyReportNewPermitReportRequest : ReportQueryRequest
    {
        public string Type { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int PaThaKaTypeId { get; set; }
        public int ExportImportSectionId { get; set; }
        public int BuyerCountryId { get; set; }
        public string CompanyRegistrationNo { get; set; } = string.Empty;
        public int SakhanId { get; set; }
    }
}

