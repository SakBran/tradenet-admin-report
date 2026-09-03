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
    public class OnlineFeesReportController : ControllerBase, IStreamingExcelReport, IExcelFooterTotalsProvider
    {
        private const string ReportKey = "OnlineFeesReport";

        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public OnlineFeesReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_OnlineFeesReportResult>>> Post([FromBody] OnlineFeesReportRequest? request)
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

            var rows = await sp_OnlineFeesReport.ExecuteAsync(
                _context, procedureRequest!, sortColumn, sortOrder, pageIndex, pageSize, request.IncludeTotalCount);

            var data = rows.Select(row => row.ToResult()).ToList();

            var result = request.IncludeTotalCount
                ? ApiResult<sp_OnlineFeesReportResult>.CreatePageFromRows(
                    data, rows.Count > 0 ? (rows[0].TotalCount ?? 0) : 0, pageIndex, pageSize,
                    request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery)
                : ApiResult<sp_OnlineFeesReportResult>.CreateFastPageFromRows(
                    data, pageIndex, pageSize,
                    request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery);

            if (request.IncludeTotalCount)
            {
                result.ColumnTotals = await sp_OnlineFeesReport.ExecuteColumnTotalsAsync(_context, procedureRequest!);
            }

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] OnlineFeesReportRequest? request)
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
        public string ExcelWorksheetTitle => "Online Fees Report";
        public Type ExcelRequestType => typeof(OnlineFeesReportRequest);

        /// <summary>
        /// The grid's Total row, computed directly instead of by replaying <see cref="Post"/>.
        ///
        /// This calls the SAME helper <c>Post</c> uses for the grid's <c>ColumnTotals</c>
        /// (<c>sp_OnlineFeesReport.ExecuteColumnTotalsAsync</c> — one cross-page
        /// <c>SUM(Amount)</c> over the whole filtered set, keyed "amount", which is the
        /// Deducted Fees column's dataIndex), so the sheet's Total row is the grid's Total
        /// row by construction rather than a second implementation that can drift.
        ///
        /// The default probe is opted out of because it sets <c>IncludeTotalCount = true</c>,
        /// which makes <c>sp_OnlineFeesReport_pagination</c> add a <c>COUNT(*) OVER()</c>
        /// (StoredProcedureMigrations/sp_OnlineFeesReport_pagination.sql:70) on top of the
        /// AccountTransaction/AccountTransactionDetail/AccountTitle join and the eight
        /// per-registration UNION branches — a second pass over the same rows the export is
        /// already streaming, for a count the export never uses. Under
        /// <c>FooterTotalsPolicy.Required</c> a slow probe fails the whole export job
        /// (Contract §9: Online Fees is one of the reports with a cheap totals helper).
        /// </summary>
        [NonAction]
        public async Task<ReportFooterTotals?> GetExcelFooterTotalsAsync(
            object request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryCreateReportRequest((OnlineFeesReportRequest)request, out var procedureRequest, out _))
            {
                // Unreachable in practice: the Excel action validates the same filters
                // before enqueueing. Throwing mirrors the default probe, which fails the
                // job when Post rejects the export's own filters, rather than quietly
                // shipping a sheet with no footer.
                throw new InvalidOperationException(
                    "Online Fees Report footer totals: the export's stored FromDate/ToDate are invalid.");
            }

            var columnTotals = await sp_OnlineFeesReport.ExecuteColumnTotalsAsync(_context, procedureRequest!);

            // No per-currency footer on this report: online fees are all MMK.
            return new ReportFooterTotals(columnTotals, CurrencyTotals: null);
        }

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((OnlineFeesReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            OnlineFeesReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            await foreach (var chunk in sp_OnlineFeesReport.ExecuteQueryable(_context, procedureRequest!)
                .AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
            {
                sink.Append(chunk.Select(row => row.ToResult()).ToList());
            }
        }

        private bool TryCreateReportRequest(
            OnlineFeesReportRequest? request,
            out sp_OnlineFeesReportRequest? procedureRequest,
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
            procedureRequest = new sp_OnlineFeesReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                FormType = request.FormType,
                SakhanId = request.SakhanId,
            };

            return true;
        }
    }

    public sealed class OnlineFeesReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string FormType { get; set; } = string.Empty;
        public int SakhanId { get; set; }
    }
}

