using System;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers.Report
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MPUReportV3Controller : ControllerBase, IStreamingExcelReport, IExcelFooterTotalsProvider
    {
        private const string ReportKey = "MPUReportV3";
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public MPUReportV3Controller(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_MPUReport_V3Result>>> Post([FromBody] MPUReportV3Request? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var pageIndex = Math.Max(0, request!.PageIndex);
            var pageSize = request.PageSize <= 0
                ? DefaultPageSize
                : Math.Min(request.PageSize, MaxPageSize);

            var sortColumn = string.IsNullOrWhiteSpace(request.SortColumn) ? null : request.SortColumn;
            var sortOrder = string.IsNullOrWhiteSpace(request.SortOrder) ? null : request.SortOrder;
            var includeTotalCount = request.IncludeTotalCount;

            var rows = await sp_MPUReport_V3.ExecuteAsync(
                _context, procedureRequest!, sortColumn, sortOrder, pageIndex, pageSize, includeTotalCount);

            var data = rows.Select(row => row.ToResult()).ToList();

            var result = includeTotalCount
                ? ApiResult<sp_MPUReport_V3Result>.CreatePageFromRows(
                    data, rows.Count > 0 ? (rows[0].TotalCount ?? 0) : 0, pageIndex, pageSize,
                    request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery)
                : ApiResult<sp_MPUReport_V3Result>.CreateFastPageFromRows(
                    data, pageIndex, pageSize,
                    request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery);

            if (includeTotalCount)
            {
                result.ColumnTotals = await sp_MPUReport_V3.ExecuteColumnTotalsAsync(_context, procedureRequest!);
            }

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] MPUReportV3Request? request)
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
        public string ExcelWorksheetTitle => "MPU Report V3";
        public Type ExcelRequestType => typeof(MPUReportV3Request);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((MPUReportV3Request)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            MPUReportV3Request request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            await foreach (var chunk in sp_MPUReport_V3.ExecuteQueryable(_context, procedureRequest!)
                .AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
            {
                sink.Append(chunk.Select(row => row.ToResult()).ToList());
            }
        }

        /// <summary>
        /// The grid's Total row, computed directly instead of by replaying <see cref="Post"/>.
        ///
        /// This calls the SAME helper <c>Post</c> uses for the grid's <c>ColumnTotals</c>
        /// (<c>sp_MPUReport_V3.ExecuteColumnTotalsAsync</c>, keyed by the six money columns'
        /// dataIndexes — <c>transactionAmount</c>, <c>mocAmount</c>, <c>imAmount</c>,
        /// <c>mpuAmount</c>, <c>totalAmount</c>, <c>amountDiff</c> — see
        /// StoredProcedureToLinq/sp_MPUReport_V3.cs:104-112), so the sheet's Total row is the
        /// grid's Total row by construction rather than a second implementation that can drift.
        /// Those sums are cross-page: one aggregate over the whole filtered set, not over the
        /// returned page. The helper sets its own 180s command timeout.
        ///
        /// The default probe is opted out of because replaying <c>Post</c> runs the whole of
        /// <c>sp_MPUReport_V3_pagination</c> — it materialises #mpu/#acc/#rows before it can
        /// return even one row, then counts them
        /// (StoredProcedureMigrations/sp_MPUReport_V3_pagination.sql:131) — so a PageSize 1
        /// probe costs a second full run of the same proc the export is already paying for,
        /// for a count the export never uses. Under <c>FooterTotalsPolicy.Required</c> a slow
        /// probe fails the whole export job (Contract §9 lists MPUV3 as one of the reports
        /// with a cheap totals helper).
        ///
        /// No per-currency footer on this report: MPU card settlements are all MMK.
        /// </summary>
        [NonAction]
        public async Task<ReportFooterTotals?> GetExcelFooterTotalsAsync(
            object request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryCreateReportRequest((MPUReportV3Request)request, out var procedureRequest, out _))
            {
                // Unreachable in practice: the Excel action validates the same filters
                // before enqueueing. Throwing mirrors the default probe, which fails the
                // job when Post rejects the export's own filters, rather than quietly
                // shipping a sheet with no footer.
                throw new InvalidOperationException(
                    "MPU Report V3 footer totals: the export's stored FromDate/ToDate are invalid.");
            }

            var columnTotals = await sp_MPUReport_V3.ExecuteColumnTotalsAsync(_context, procedureRequest!);

            return new ReportFooterTotals(columnTotals, CurrencyTotals: null);
        }

        private bool TryCreateReportRequest(
            MPUReportV3Request? request,
            out sp_MPUReport_V3Request? procedureRequest,
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

            procedureRequest = new sp_MPUReport_V3Request
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                FormType = request.FormType,
                PaymentType = request.PaymentType,
            };

            return true;
        }
    }

    public sealed class MPUReportV3Request : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string FormType { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
    }
}
