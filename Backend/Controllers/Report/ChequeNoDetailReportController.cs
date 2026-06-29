using System;
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
    public class ChequeNoDetailReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "ChequeNoDetailReport";
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public ChequeNoDetailReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_ChequeNoDetailReportResult>>> Post(
            [FromBody] ChequeNoDetailReportRequest? request)
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
                var rows = await sp_ChequeNoDetailReport.ExecuteAsync(
                    _context,
                    procedureRequest!,
                    sortColumn,
                    sortOrder,
                    pageIndex,
                    pageSize,
                    request.IncludeTotalCount);

                var data = rows.Select(row => row.ToResult()).ToList();

                var result = request.IncludeTotalCount
                    ? ApiResult<sp_ChequeNoDetailReportResult>.CreatePageFromRows(
                        data, rows.Count > 0 ? (rows[0].TotalCount ?? 0) : 0, pageIndex, pageSize,
                        request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery)
                    : ApiResult<sp_ChequeNoDetailReportResult>.CreateFastPageFromRows(
                        data, pageIndex, pageSize,
                        request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery);

                return Ok(result);
            }
            catch (SqlException ex) when (IsMissingPaginationProcedure(ex))
            {
                var query = sp_ChequeNoDetailReport.Query(_context, procedureRequest!);
                var result = await ReportQueryService.CreatePagedResultAsync(query, request);
                return Ok(result);
            }
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ChequeNoDetailReportRequest? request)
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

        public string ExcelWorksheetTitle => "Cheque No Detail Report";
        public Type ExcelRequestType => typeof(ChequeNoDetailReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((ChequeNoDetailReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            ChequeNoDetailReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            await foreach (var chunk in sp_ChequeNoDetailReport.Query(_context, procedureRequest!)
                .AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
            {
                sink.Append(chunk);
            }
        }

        private bool TryCreateReportRequest(
            ChequeNoDetailReportRequest? request,
            out sp_ChequeNoDetailReportRequest? procedureRequest,
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

            if (request.ChequeNoId <= 0)
            {
                errorResult = BadRequest("ChequeNoId is required.");
                return false;
            }

            procedureRequest = new sp_ChequeNoDetailReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                ChequeNoId = request.ChequeNoId
            };

            return true;
        }

        private static bool IsMissingPaginationProcedure(SqlException ex)
        {
            return ex.Number == 2812
                && ex.Message.Contains("sp_ChequeNoDetailReport", StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ChequeNoDetailReportRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int ChequeNoId { get; set; }
    }
}
