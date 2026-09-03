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
    public class BorderExportLicenceVoucherReportController
        : ControllerBase, IStreamingExcelReport, IExcelFooterTotalsProvider
    {
        private const string ReportKey = "BorderExportLicenceVoucherReport";

        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        // Excel worksheets allow 1,048,576 rows including the header.
        private const int MaxExcelDataRows = 1_048_576 - 1;

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public BorderExportLicenceVoucherReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_VoucherReportResult>>> Post([FromBody] BorderExportLicenceVoucherReportRequest? request)
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

            var rows = await sp_VoucherReport.ExecuteAsync(
                _context, procedureRequest!, sortColumn, sortOrder, pageIndex, pageSize, request.IncludeTotalCount);

            var data = rows.Select(row => row.ToResult()).ToList();

            var result = request.IncludeTotalCount
                ? ApiResult<sp_VoucherReportResult>.CreatePageFromRows(
                    data, rows.Count > 0 ? (rows[0].TotalCount ?? 0) : 0, pageIndex, pageSize,
                    request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery)
                : ApiResult<sp_VoucherReportResult>.CreateFastPageFromRows(
                    data, pageIndex, pageSize,
                    request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery);

            if (data.Count > 0)
            {
                result.CurrencyTotals = await ExportLicenceListingCurrencyTotals.ExecuteVoucherAsync(
                    _context, procedureRequest!.FormType, procedureRequest.FromDate, procedureRequest.ToDate,
                    procedureRequest.ExportImportSectionId, procedureRequest.PaymentType, procedureRequest.ApplyType,
                    procedureRequest.CompanyRegistrationNo, procedureRequest.SakhanId);
            }

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] BorderExportLicenceVoucherReportRequest? request)
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
        public string ExcelWorksheetTitle => "Border Export Licence Voucher Report";
        public Type ExcelRequestType => typeof(BorderExportLicenceVoucherReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((BorderExportLicenceVoucherReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            BorderExportLicenceVoucherReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            await foreach (var chunk in sp_VoucherReport.ExecuteQueryable(_context, procedureRequest!)
                .AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
            {
                sink.Append(chunk.Select(row => row.ToResult()).ToList());
            }
        }

        /// <summary>
        /// The grid's per-currency footer, computed directly instead of by replaying
        /// <see cref="Post"/>.
        ///
        /// This calls the SAME helper <c>Post</c> uses for the grid's
        /// <c>CurrencyTotals</c> — <c>ExportLicenceListingCurrencyTotals.ExecuteVoucherAsync</c>
        /// (<c>dbo.sp_ExportLicenceVoucherCurrencyTotals</c>) — with the same arguments, so the
        /// sheet's footer is the grid's footer by construction, not a second implementation
        /// that can drift.
        ///
        /// The default probe is opted out of because it sets <c>IncludeTotalCount = true</c>,
        /// which runs <c>sp_VoucherReport_pagination</c>'s Border Export Licence
        /// <c>COUNT(*)</c> branch: a UNION over two AccountTransaction joins that carry no
        /// <c>TransactionFormType</c> discriminator and no LOOP JOIN hint
        /// (StoredProcedureMigrations/sp_VoucherReport_pagination.sql:277-307) — the same cold
        /// columnstore scan that made the sibling Permit voucher reports take 30s+. The export
        /// never uses that count, and under <c>FooterTotalsPolicy.Required</c> a slow probe
        /// would fail the whole export job.
        ///
        /// No <c>ColumnTotals</c> on this report: the grid shows only the per-currency rows
        /// plus the grand TOTAL row.
        /// </summary>
        [NonAction]
        public async Task<ReportFooterTotals?> GetExcelFooterTotalsAsync(
            object request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryCreateReportRequest((BorderExportLicenceVoucherReportRequest)request, out var procedureRequest, out _))
            {
                // Unreachable in practice: the Excel action validates the same filters
                // before enqueueing. Throwing mirrors the default probe, which fails the
                // job when Post rejects the export's own filters, rather than quietly
                // shipping a sheet with no footer.
                throw new InvalidOperationException(
                    "Border Export Licence Voucher Report footer totals: the export's stored FromDate/ToDate are invalid.");
            }

            var currencyTotals = await ExportLicenceListingCurrencyTotals.ExecuteVoucherAsync(
                _context, procedureRequest!.FormType, procedureRequest.FromDate, procedureRequest.ToDate,
                procedureRequest.ExportImportSectionId, procedureRequest.PaymentType, procedureRequest.ApplyType,
                procedureRequest.CompanyRegistrationNo, procedureRequest.SakhanId);

            return new ReportFooterTotals(ColumnTotals: null, CurrencyTotals: currencyTotals);
        }

        private bool TryCreateReportRequest(
            BorderExportLicenceVoucherReportRequest? request,
            out sp_VoucherReportRequest? procedureRequest,
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
            procedureRequest = new sp_VoucherReportRequest
            {
                FormType = "Border Export Licence",
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                ExportImportSectionId = request.ExportImportSectionId,
                PaymentType = request.PaymentType,
                ApplyType = request.ApplyType,
                CompanyRegistrationNo = request.CompanyRegistrationNo,
                SakhanId = request.SakhanId,
            };

            return true;
        }
    }

    public sealed class BorderExportLicenceVoucherReportRequest : ReportQueryRequest
    {
        public string FormType { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int ExportImportSectionId { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public string ApplyType { get; set; } = string.Empty;
        public string CompanyRegistrationNo { get; set; } = string.Empty;
        public int SakhanId { get; set; }
    }
}

