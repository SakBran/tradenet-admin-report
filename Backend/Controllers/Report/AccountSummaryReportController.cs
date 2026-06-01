using System;
using System.Linq;
using System.Threading.Tasks;
using API.DBContext;
using API.Model;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Backend.Controllers.Report
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AccountSummaryReportController : ControllerBase
    {
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        // Excel worksheets allow 1,048,576 rows including the header.
        private const int MaxExcelDataRows = 1_048_576 - 1;

        private readonly TradeNetDbContext _context;

        public AccountSummaryReportController(TradeNetDbContext context)
        {
            _context = context;
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

            try
            {
                var rows = await sp_AccountSummaryReport.ExecuteAsync(
                    _context, procedureRequest!, sortColumn, sortOrder, pageIndex, pageSize, includeTotalCount: true);

                var data = rows.Select(row => row.ToResult()).ToList();

                var result = ApiResult<sp_AccountSummaryReportResult>.CreatePageFromRows(
                    data, rows.Count > 0 ? (rows[0].TotalCount ?? 0) : 0, pageIndex, pageSize,
                    request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery);

                return Ok(result);
            }
            catch (SqlException ex) when (IsMissingPaginationProcedure(ex))
            {
                var query = sp_AccountSummaryReport.Query(_context, procedureRequest!);
                var result = await ReportQueryService.CreatePagedResultAsync(query, request);

                return Ok(result);
            }
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] AccountSummaryReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var sortColumn = string.IsNullOrWhiteSpace(request!.SortColumn) ? null : request.SortColumn;
            var sortOrder = string.IsNullOrWhiteSpace(request.SortOrder) ? null : request.SortOrder;

            try
            {
                var rows = await sp_AccountSummaryReport.ExecuteAsync(
                    _context, procedureRequest!, sortColumn, sortOrder, pageIndex: null, pageSize: null);

                if (rows.Count > MaxExcelDataRows)
                {
                    return BadRequest($"Excel export supports up to {MaxExcelDataRows} data rows.");
                }

                var data = rows.Select(row => row.ToResult()).ToList();
                var fileBytes = ExcelGenerator.CreateWorkbook(data, "Account Summary Report");

                return File(
                    fileBytes,
                    ExcelGenerator.ContentType,
                    "AccountSummaryReport.xlsx");
            }
            catch (SqlException ex) when (IsMissingPaginationProcedure(ex))
            {
                var query = sp_AccountSummaryReport.Query(_context, procedureRequest!);
                byte[] fileBytes;
                try
                {
                    fileBytes = await ExcelGenerator.CreateWorkbookAsync(
                        query,
                        request!,
                        "Account Summary Report");
                }
                catch (InvalidOperationException invalidOperationException)
                {
                    return BadRequest(invalidOperationException.Message);
                }

                return File(
                    fileBytes,
                    ExcelGenerator.ContentType,
                    "AccountSummaryReport.xlsx");
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
