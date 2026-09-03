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
    public class ChequeNoReportController : ControllerBase, IStreamingExcelReport, IExcelFooterTotalsProvider
    {
        private const string ReportKey = "ChequeNoReport";

        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public ChequeNoReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_ChequeNoReportResult>>> Post([FromBody] ChequeNoReportRequest? request)
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

            var rows = await sp_ChequeNoReport.ExecuteAsync(
                _context, procedureRequest!, sortColumn, sortOrder, pageIndex, pageSize, request.IncludeTotalCount);

            var data = rows.Select(row => row.ToResult()).ToList();

            var result = request.IncludeTotalCount
                ? ApiResult<sp_ChequeNoReportResult>.CreatePageFromRows(
                    data, rows.Count > 0 ? (rows[0].TotalCount ?? 0) : 0, pageIndex, pageSize,
                    request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery)
                : ApiResult<sp_ChequeNoReportResult>.CreateFastPageFromRows(
                    data, pageIndex, pageSize,
                    request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery);

            if (request.IncludeTotalCount)
            {
                result.ColumnTotals = await sp_ChequeNoReport.ExecuteColumnTotalsAsync(_context, procedureRequest!);
            }

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ChequeNoReportRequest? request)
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
        public string ExcelWorksheetTitle => "Cheque No Report";
        public Type ExcelRequestType => typeof(ChequeNoReportRequest);

        /// <summary>
        /// The grid's Total row, computed directly instead of by replaying <see cref="Post"/>.
        ///
        /// This calls the SAME helper <c>Post</c> uses for the grid's <c>ColumnTotals</c>
        /// (<c>sp_ChequeNoReport.ExecuteColumnTotalsAsync</c> — one cross-page
        /// <c>SUM(AccountTransactionDetail.Amount)</c> over the filtered set, keyed
        /// "amount", which is the Amount column's dataIndex), so the sheet's Total row is
        /// the grid's Total row by construction rather than a second implementation that
        /// can drift.
        ///
        /// The default probe is opted out of because it sets <c>IncludeTotalCount = true</c>,
        /// which makes <c>sp_ChequeNoReport_pagination</c> add a <c>COUNT(*) OVER()</c> on
        /// top of the full AccountTransaction/AccountTransactionDetail/AccountTitle/ChequeNo
        /// join and GROUP BY (StoredProcedureMigrations/sp_ChequeNoReport_pagination.sql:37-59)
        /// — a second scan of the same 2M-row transaction join the export itself is already
        /// paying for, and a count the export never uses. Under
        /// <c>FooterTotalsPolicy.Required</c> a slow probe fails the whole export job
        /// (Contract §9: ChequeNo is one of the reports with a cheap totals helper).
        /// </summary>
        [NonAction]
        public async Task<ReportFooterTotals?> GetExcelFooterTotalsAsync(
            object request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryCreateReportRequest((ChequeNoReportRequest)request, out var procedureRequest, out _))
            {
                // Unreachable in practice: the Excel action validates the same filters
                // before enqueueing. Throwing mirrors the default probe, which fails the
                // job when Post rejects the export's own filters, rather than quietly
                // shipping a sheet with no footer.
                throw new InvalidOperationException(
                    "Cheque No Report footer totals: the export's stored FromDate/ToDate are invalid.");
            }

            var columnTotals = await sp_ChequeNoReport.ExecuteColumnTotalsAsync(_context, procedureRequest!);

            // No per-currency footer on this report: cheque payments are all MMK.
            return new ReportFooterTotals(columnTotals, CurrencyTotals: null);
        }

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((ChequeNoReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            ChequeNoReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            await foreach (var chunk in sp_ChequeNoReport.ExecuteQueryable(_context, procedureRequest!)
                .AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
            {
                sink.Append(chunk.Select(row => row.ToResult()).ToList());
            }
        }

        private bool TryCreateReportRequest(
            ChequeNoReportRequest? request,
            out sp_ChequeNoReportRequest? procedureRequest,
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
            procedureRequest = new sp_ChequeNoReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                ChequeNoId = request.ChequeNoId,
            };

            return true;
        }
    }

    public sealed class ChequeNoReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int ChequeNoId { get; set; }
    }
}

