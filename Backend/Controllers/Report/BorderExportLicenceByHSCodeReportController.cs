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
    // (HSCodeId, Currency) only (rdlc:1158-1168), so 31/08-01/09/2026 goes from 76 rows to the old
    // report's 33, and each Total Value is now the whole HS code's sum. The export cache keys on
    // payload + this version; without the bump a closed-period request keeps serving the
    // company-split workbook.
    [ExcelFormatVersion(2)]
    public class BorderExportLicenceByHSCodeReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "BorderExportLicenceByHSCodeReport";

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public BorderExportLicenceByHSCodeReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<ReportAggregateResult>>> Post([FromBody] BorderExportLicenceByHSCodeReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var result = await sp_HSCodeReport.CreateAggregateResultAsync(_context, procedureRequest!, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] BorderExportLicenceByHSCodeReportRequest? request)
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
        public string ExcelWorksheetTitle => "Border Export Licence By HS Code Report";
        public Type ExcelRequestType => typeof(BorderExportLicenceByHSCodeReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((BorderExportLicenceByHSCodeReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            BorderExportLicenceByHSCodeReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);

            // Row order already equals the grid's. The grid pages through
            // sp_HSCodeReport_pagination, which sorts "ORDER BY result.HSCode,
            // result.CompanyName, result.Currency"; GetAggregateRowsAsync's
            // AggregateQuery applies exactly that ORDER BY server-side, so both come
            // back in the same DB-collation order. Re-sorting here with
            // ReportAggregationService.OrderGroups(..., ReportAggregateDimension.HSCode,
            // includeSakhan: false) would sort on the same three keys but with
            // StringComparer.OrdinalIgnoreCase, trading the DB collation for ordinal
            // semantics -- i.e. it could only move Excel rows AWAY from the grid order.
            var rows = await sp_HSCodeReport.GetAggregateRowsAsync(_context, procedureRequest!);
            sink.Append(rows);
        }

        private bool TryCreateReportRequest(
            BorderExportLicenceByHSCodeReportRequest? request,
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
                FormType = "Border Export Licence",
                FilterType = request.FilterType ?? string.Empty,
                HSCode = request.HSCode ?? string.Empty,
                SakhanId = request.SakhanId,
                // The HS Code detail drill (BorderExportLicenceHSCodeDetailReport) posts
                // GroupBy='Company' to get HSCodeDetailReport.rdlc's (HS code, company) rows --
                // what the old screen's BorderHSCodeDetailReport action rendered. The summary
                // posts nothing and keeps BorderHSCodeReport.rdlc's (HS code, currency) grain.
                GroupByCompany = string.Equals(request.GroupBy, "Company", StringComparison.OrdinalIgnoreCase),
            };

            return true;
        }
    }

    public sealed class BorderExportLicenceByHSCodeReportRequest : ReportQueryRequest
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

