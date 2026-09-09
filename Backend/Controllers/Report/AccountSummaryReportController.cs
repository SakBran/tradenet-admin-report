using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using API.DBContext;
using API.Model;
using API.Service.ExcelExport;
using API.Service.ExcelExport.Dcca;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers.Report
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    // v2 = the exported sheet gained the RDLC title row and the grid's 8 columns.
    // v3 = the DCCA variant is now written byte-for-byte from the old export's own parts
    //      (DccaWorkbookWriter) instead of through StreamingExcelWriter, so its bytes changed.
    [ExcelFormatVersion(3)]
    public class AccountSummaryReportController
        : ControllerBase,
          IStreamingExcelReport,
          IExcelReportLayoutProvider,
          IExcelFooterTotalsProvider,
          ICustomExcelWriter
    {
        private const string ReportKey = "AccountSummaryReport";

        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public AccountSummaryReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_AccountSummaryReportResult>>> Post([FromBody] AccountSummaryReportRequest? request)
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

            try
            {
                var rows = await sp_AccountSummaryReport.ExecuteAsync(
                    _context, procedureRequest!, sortColumn, sortOrder, pageIndex, pageSize, includeTotalCount);

                var data = rows.Select(row => row.ToResult()).ToList();

                var result = includeTotalCount
                    ? ApiResult<sp_AccountSummaryReportResult>.CreatePageFromRows(
                        data, rows.Count > 0 ? (rows[0].TotalCount ?? 0) : 0, pageIndex, pageSize,
                        request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery)
                    : ApiResult<sp_AccountSummaryReportResult>.CreateFastPageFromRows(
                        data, pageIndex, pageSize,
                        request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery);

                if (includeTotalCount)
                {
                    result.ColumnTotals = await sp_AccountSummaryReport.ExecuteColumnTotalsAsync(_context, procedureRequest!);
                }

                return Ok(result);
            }
            catch (SqlException ex) when (IsMissingPaginationProcedure(ex))
            {
                var query = sp_AccountSummaryReport.Query(_context, procedureRequest!);
                var result = await ReportQueryService.CreatePagedResultAsync(query, request);

                if (request.IncludeTotalCount)
                {
                    result.ColumnTotals = await sp_AccountSummaryReport.ExecuteColumnTotalsAsync(_context, procedureRequest!);
                }

                return Ok(result);
            }
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] AccountSummaryReportRequest? request)
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
        public string ExcelWorksheetTitle => "Account Summary Report";
        public Type ExcelRequestType => typeof(AccountSummaryReportRequest);

        /// <summary>
        /// The RDLC-shaped export the grid mirrors.
        ///
        /// The DCCA variant never reaches here: it is written by
        /// <see cref="WriteCustomExcelAsync"/>, which the job handler dispatches to before it
        /// asks for a layout. This returns the standard layout unconditionally rather than
        /// throwing for DCCA, so a regression in that dispatch produces the wrong file — which a
        /// test catches — instead of a failed job with an opaque message.
        /// </summary>
        [NonAction]
        public ExcelReportLayout GetExcelLayout(object request)
            => StandardLayout((AccountSummaryReportRequest)request);

        /// <summary>
        /// The exported sheet mirrors the grid and the old Tradenet 2.0 RDLC: a title
        /// banner, then No / Entry Date / Company Registration No / Company Name /
        /// Voucher No / Transaction Title / Deducted Fees / Remark.
        /// </summary>
        private static ExcelReportLayout StandardLayout(AccountSummaryReportRequest typedRequest)
        {
            return new ExcelReportLayout
            {
                TitleLines = new[]
                {
                    ExcelReportTitle.DateRange("Account Summary Report", typedRequest.FromDate, typedRequest.ToDate),
                },
                TotalsRowLabel = "Total",
                Columns = new[]
                {
                    ExcelColumn.RowNumber(),
                    ExcelColumn.Date<sp_AccountSummaryReportResult>("Entry Date", row => row.VoucherDate),
                    ExcelColumn.Text<sp_AccountSummaryReportResult>("Company Registration No", row => row.CompanyRegistrationNo, width: 24),
                    ExcelColumn.Text<sp_AccountSummaryReportResult>("Company Name", row => row.CompanyName, width: 34),
                    ExcelColumn.Text<sp_AccountSummaryReportResult>("Voucher No", row => row.VoucherNo, width: 16),
                    ExcelColumn.Text<sp_AccountSummaryReportResult>("Transaction Title", row => row.TransactionTitle, width: 30),
                    // Bound to "amount" so the footer builder places Post's
                    // ColumnTotals["amount"] under this column instead of re-summing.
                    ExcelColumn.Money<sp_AccountSummaryReportResult>("Deducted Fees", row => row.Amount, includeInTotals: true)
                        .Bind("DeductedFees", "amount"),
                    // Unbound in the old RDLC too — a header with a deliberately empty body.
                    ExcelColumn.Blank("Remark", width: 18),
                },
            };
        }

        // --- The DCCA import file (written byte-for-byte, not through the shared writer) ---

        /// <summary>
        /// True only for the DCCA variant, so the report's normal export keeps going through
        /// <see cref="ExcelReportLayout"/> and <see cref="StreamingExcelWriter"/> untouched.
        ///
        /// Note the request still carries a full presentation spec (the frontend posts one for
        /// both buttons). Its <c>title</c> and <c>fileName</c> still name the job and the
        /// download, but its <c>columns</c> have NO effect on the DCCA file — editing them in
        /// reportConfigs.ts will change nothing here.
        /// </summary>
        [NonAction]
        public bool CanWriteCustomExcel(object request)
            => AccountSummaryExportFormat.IsDcca(((AccountSummaryReportRequest)request).ExportFormat);

        /// <summary>
        /// The file DCCA imports. This is NOT the RDLC shape: the old Tradenet 2.0 screen
        /// produced it separately, from its black "Export" button, by loading the template
        /// workbook <c>Content/excel-template/TransactionFees.xlsx</c> with EPPlus 4.1, filling
        /// cells and saving (<c>ReportsController.AccountSummaryReport</c>, POST).
        ///
        /// DCCA's importer is keyed to that exact file, so it is reproduced rather than
        /// approximated: eight of its ten OOXML parts are copied byte-for-byte out of the
        /// embedded skeleton and only the sheet and the string table are generated. See
        /// <see cref="DccaWorkbookWriter"/> and <see cref="DccaWorkbookSkeleton"/>.
        ///
        /// The header text lives in the skeleton, including its "Transation Title" typo —
        /// correcting it would risk the importer failing to find the column.
        /// </summary>
        [NonAction]
        public async Task WriteCustomExcelAsync(object request, ExcelExportContext context)
        {
            var typedRequest = (AccountSummaryReportRequest)request;
            TryCreateReportRequest(typedRequest, out var procedureRequest, out _);

            await using var writer = new DccaWorkbookWriter();

            // The SAME row stream the normal export uses, with the same ordering and the same
            // includeTotalCount: false — so the two exports can never disagree about the data,
            // and the COUNT(*) that times this report out is still skipped.
            await foreach (var chunk in sp_AccountSummaryReport
                .ExecuteQueryable(_context, procedureRequest!, includeTotalCount: false)
                .AsAsyncEnumerable().ChunkAsync(context.ChunkSize, context.CancellationToken))
            {
                foreach (var row in chunk)
                {
                    await writer.AppendRowAsync(ToDccaRow(row.ToResult()), context.CancellationToken);
                }
            }

            await writer.FinishAsync(context.Output, context.CancellationToken);

            context.RowCount = (int)Math.Min(writer.DataRows, int.MaxValue);
            context.SheetCount = 1;
        }

        /// <summary>
        /// The nine values the old EPPlus loop wrote, in its order. Every string is coerced to
        /// "" rather than left null: the old code read each column as
        /// <c>DataRow[...].ToString()</c>, which yields "" for DBNull and never null.
        /// </summary>
        private static DccaRow ToDccaRow(sp_AccountSummaryReportResult row) => new(
            // The old code called .ToString("MM/dd/yyyy") on a non-nullable DateTime. VoucherDate
            // can never be null here anyway — the procedure's date predicate excludes NULLs.
            EntryDate: row.VoucherDate?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) ?? string.Empty,
            HtaThaKaNo: Concat(row.CompanyRegistrationNo, row.VoucherNo),
            CompanyName: row.CompanyName,
            TransactionTitle: row.TransactionTitle,
            Amount: row.Amount,
            AccountCode: row.AccountTitleCode,
            LocationCode: row.LocationCode);

        /// <summary>
        /// The old export's <c>item.CompanyRegistrationNo + "@" + item.VoucherNo</c>. The
        /// separator is always written, so a row whose company did not resolve (the Member
        /// branch blanks both company fields) reads "@U03202600003" — as it does today.
        /// </summary>
        private static string Concat(string? companyRegistrationNo, string? voucherNo)
            => companyRegistrationNo + "@" + voucherNo;

        /// <summary>
        /// The grid's footer number, without the default probe.
        ///
        /// Replaying <c>Post</c> with <c>IncludeTotalCount = true</c> would run the
        /// pagination procedure's exact <c>COUNT(*)</c> over #rows — the very thing
        /// <c>WriteRowsAsync</c> passes <c>includeTotalCount: false</c> to avoid, because
        /// it is what times this report out.
        ///
        /// This calls the SAME helper <c>Post</c> uses for the grid's
        /// <c>ColumnTotals</c> (<c>sp_AccountSummaryReport.ExecuteColumnTotalsAsync</c>, a
        /// single cross-page SUM(Amount) over the filtered set), so the sheet's Total row is
        /// the grid's Total row by construction. The layout binds the Deducted Fees column to
        /// "amount", which is the key this dictionary carries.
        /// </summary>
        [NonAction]
        public async Task<ReportFooterTotals?> GetExcelFooterTotalsAsync(
            object request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The DCCA import file has no Total row, so there is nothing to resolve — and
            // no reason to spend a cross-page SUM on it. (DccaLayout also leaves
            // TotalsRowLabel null, which blocks the writer's own summed fallback.)
            if (AccountSummaryExportFormat.IsDcca(((AccountSummaryReportRequest)request).ExportFormat))
            {
                return null;
            }

            if (!TryCreateReportRequest((AccountSummaryReportRequest)request, out var procedureRequest, out _))
            {
                // Unreachable in practice: the Excel action validates the same filters
                // before enqueueing. Throwing mirrors the default probe, which fails the
                // job when Post rejects the export's own filters, rather than quietly
                // shipping a sheet with no footer.
                throw new InvalidOperationException(
                    "Account Summary Report footer totals: the export's stored FromDate/ToDate are invalid.");
            }

            var columnTotals = await sp_AccountSummaryReport.ExecuteColumnTotalsAsync(_context, procedureRequest!);

            // No per-currency footer on this report (fees are all MMK).
            return new ReportFooterTotals(columnTotals, CurrencyTotals: null);
        }

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((AccountSummaryReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            AccountSummaryReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);

            // includeTotalCount: false — the export needs every row, never the count, and
            // the extra COUNT(*) over #rows is what makes this proc time out.
            await foreach (var chunk in sp_AccountSummaryReport
                .ExecuteQueryable(_context, procedureRequest!, includeTotalCount: false)
                .AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
            {
                sink.Append(chunk.Select(row => row.ToResult()).ToList());
            }
        }

        private bool TryCreateReportRequest(
            AccountSummaryReportRequest? request,
            out sp_AccountSummaryReportRequest? procedureRequest,
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
            procedureRequest = new sp_AccountSummaryReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                FormType = request.FormType,
                SakhanId = request.SakhanId,
            };

            return true;
        }

        private static bool IsMissingPaginationProcedure(SqlException ex)
        {
            return ex.Number == 2812
                && ex.Message.Contains("sp_AccountSummaryReport_pagination", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The values <see cref="AccountSummaryReportRequest.ExportFormat"/> accepts. The
    /// frontend sends <see cref="Dcca"/> from the report's second export button
    /// (<c>secondaryExcel.requestOverrides</c> in reportConfigs.ts).
    /// </summary>
    public static class AccountSummaryExportFormat
    {
        /// <summary>The file DCCA imports — see the DCCA layout on the controller.</summary>
        public const string Dcca = "Dcca";

        /// <summary>
        /// Case-insensitive so a hand-built request ("dcca", "DCCA") still selects the
        /// import file rather than silently falling back to the RDLC sheet.
        /// </summary>
        public static bool IsDcca(string? exportFormat)
            => string.Equals(exportFormat, Dcca, StringComparison.OrdinalIgnoreCase);
    }

    public sealed class AccountSummaryReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string FormType { get; set; } = string.Empty;
        public int SakhanId { get; set; }

        /// <summary>
        /// Which Excel layout the export uses: null (the default) is the RDLC-shaped sheet
        /// the grid mirrors, <see cref="AccountSummaryExportFormat.Dcca"/> the DCCA import
        /// file. It rides in the request, so the two variants hash to different cache keys
        /// (<c>ExcelExportHasher</c>) instead of one being served the other's file.
        ///
        /// Ignored when null, exactly like <see cref="ReportQueryRequest.Excel"/>, so the
        /// normal export's request JSON — and therefore its warm cache — is unchanged.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ExportFormat { get; set; }
    }
}
