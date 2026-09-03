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
    public class MPUReportController : ControllerBase, IStreamingExcelReport, IExcelFooterTotalsProvider
    {
        private const string ReportKey = "MPUReport";

        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        // Excel worksheets allow 1,048,576 rows including the header.
        private const int MaxExcelDataRows = 1_048_576 - 1;

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public MPUReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_MPUReportResult>>> Post([FromBody] MPUReportRequest? request)
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

            var rows = await sp_MPUReport.ExecuteAsync(
                _context, procedureRequest!, sortColumn, sortOrder, pageIndex, pageSize, includeTotalCount);

            var data = rows.Select(row => row.ToResult()).ToList();

            var result = includeTotalCount
                ? ApiResult<sp_MPUReportResult>.CreatePageFromRows(
                    data, rows.Count > 0 ? (rows[0].TotalCount ?? 0) : 0, pageIndex, pageSize,
                    request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery)
                : ApiResult<sp_MPUReportResult>.CreateFastPageFromRows(
                    data, pageIndex, pageSize,
                    request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery);

            if (includeTotalCount)
            {
                result.ColumnTotals = await sp_MPUReport.ExecuteColumnTotalsAsync(_context, procedureRequest!);
            }

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] MPUReportRequest? request)
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
        public string ExcelWorksheetTitle => "MPU Report";
        public Type ExcelRequestType => typeof(MPUReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((MPUReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            MPUReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            await foreach (var chunk in sp_MPUReport.ExecuteQueryable(_context, procedureRequest!)
                .AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
            {
                sink.Append(chunk.Select(row => row.ToResult()).ToList());
            }
        }

        /// <summary>
        /// The grid's Total row, computed directly instead of by replaying <see cref="Post"/>.
        ///
        /// This calls the SAME helper <c>Post</c> uses for the grid's <c>ColumnTotals</c>
        /// (<c>sp_MPUReport.ExecuteColumnTotalsAsync</c>, keyed by the six money columns'
        /// dataIndexes — <c>transactionAmount</c>, <c>mocAmount</c>, <c>imAmount</c>,
        /// <c>mpuAmount</c>, <c>totalAmount</c>, <c>amountDiff</c> — see
        /// StoredProcedureToLinq/sp_MPUReport.cs:101-109), so the sheet's Total row is the
        /// grid's Total row by construction rather than a second implementation that can drift.
        /// Those sums are cross-page: one aggregate over the whole filtered set, not over the
        /// returned page.
        ///
        /// The default probe is opted out of because it sets <c>IncludeTotalCount = true</c>,
        /// which makes <c>sp_MPUReport_pagination</c> run its <c>COUNT(*)</c> branch — a
        /// second, separate UNION scan of <c>MPUPaymentTransaction</c> on top of the page
        /// query (StoredProcedureMigrations/sp_MPUReport_pagination.sql:42-70) — a count the
        /// export never uses. Under <c>FooterTotalsPolicy.Required</c> a slow probe fails the
        /// whole export job (Contract §9 lists MPU as one of the reports with a cheap totals
        /// helper).
        ///
        /// No per-currency footer on this report: MPU card settlements are all MMK.
        /// </summary>
        [NonAction]
        public async Task<ReportFooterTotals?> GetExcelFooterTotalsAsync(
            object request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryCreateReportRequest((MPUReportRequest)request, out var procedureRequest, out _))
            {
                // Unreachable in practice: the Excel action validates the same filters
                // before enqueueing. Throwing mirrors the default probe, which fails the
                // job when Post rejects the export's own filters, rather than quietly
                // shipping a sheet with no footer.
                throw new InvalidOperationException(
                    "MPU Report footer totals: the export's stored FromDate/ToDate are invalid.");
            }

            var columnTotals = await sp_MPUReport.ExecuteColumnTotalsAsync(_context, procedureRequest!);

            return new ReportFooterTotals(columnTotals, CurrencyTotals: null);
        }

        private bool TryCreateReportRequest(
            MPUReportRequest? request,
            out sp_MPUReportRequest? procedureRequest,
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
            procedureRequest = new sp_MPUReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                FormType = request.FormType,
                PaymentType = NormalizePaymentType(request.PaymentType),
            };

            return true;
        }

        private static string NormalizePaymentType(string? paymentType)
        {
            var normalized = (paymentType ?? string.Empty)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal);

            return normalized.Equals("CitizenPay", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("Citizen", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("CP", StringComparison.OrdinalIgnoreCase)
                    ? "CitizenPay"
                    : paymentType ?? string.Empty;
        }
    }

    public sealed class MPUReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string FormType { get; set; } = string.Empty;
        public string PaymentType { get; set; } = string.Empty;
    }
}

