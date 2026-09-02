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
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers.Report
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    // v2 = the exported sheet gained the RDLC title row and the grid's 8 columns.
    [ExcelFormatVersion(2)]
    public class AccountSummaryReportController : ControllerBase, IStreamingExcelReport, IExcelReportLayoutProvider
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
        /// The exported sheet mirrors the grid and the old Tradenet 2.0 RDLC: a title
        /// banner, then No / Entry Date / Company Registration No / Company Name /
        /// Voucher No / Transaction Title / Deducted Fees / Remark.
        /// </summary>
        [NonAction]
        public ExcelReportLayout GetExcelLayout(object request)
        {
            var typedRequest = (AccountSummaryReportRequest)request;

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
                    ExcelColumn.Money<sp_AccountSummaryReportResult>("Deducted Fees", row => row.Amount, includeInTotals: true),
                    // Unbound in the old RDLC too — a header with a deliberately empty body.
                    ExcelColumn.Blank("Remark", width: 18),
                },
            };
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

    public sealed class AccountSummaryReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string FormType { get; set; } = string.Empty;
        public int SakhanId { get; set; }
    }
}
