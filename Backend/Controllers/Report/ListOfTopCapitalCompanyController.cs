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
    public class ListOfTopCapitalCompanyController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "ListOfTopCapitalCompany";

        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        // The legacy screen's "No of List" box (model.TotalRecords) defaulted to 10 and was
        // required; a blank box bound to 0 and made Take(0) return nothing, so 0 is treated
        // as "unset" here rather than reproducing that silent-empty behaviour.
        private const int DefaultTopCount = 10;
        private const int MaxTopCount = MaxPageSize;

        // This report is "the top N companies by capital", exactly as the legacy
        // PaThaKaReports.GetTopCapitalCompanyReport was
        // (.OrderByDescending(x => x.Capital).Take(model.TotalRecords)). The ranking IS the
        // report, so the grid's SortColumn/SortOrder are deliberately ignored: clicking a
        // header re-requests and gets the same order back.
        private const string RankColumn = "Capital";
        private const string RankOrder = "DESC";

        // Excel worksheets allow 1,048,576 rows including the header.
        private const int MaxExcelDataRows = 1_048_576 - 1;

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public ListOfTopCapitalCompanyController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_PaThaKaReportResult>>> Post([FromBody] ListOfTopCapitalCompanyRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var pageIndex = Math.Max(0, request!.PageIndex);
            var pageSize = request.PageSize <= 0
                ? DefaultPageSize
                : Math.Min(request.PageSize, MaxPageSize);

            // SQL ranks and truncates (ORDER BY Capital DESC + FETCH NEXT @TopCount), so the
            // window is at most "No of List" rows and the grid pages within it in memory.
            var window = await TopCompaniesAsync(procedureRequest!, request);

            var data = window
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .Select(row => row.ToResult())
                .ToList();

            var result = ApiResult<sp_PaThaKaReportResult>.CreatePageFromRows(
                data,
                // The report IS the top-N window, so that — not the count of everything
                // matching the filters (rows[0].TotalCount) — is the total the pager sees.
                window.Count,
                pageIndex,
                pageSize,
                request.SortColumn,
                request.SortOrder,
                request.FilterColumn,
                request.FilterQuery);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ListOfTopCapitalCompanyRequest? request)
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
        public string ExcelWorksheetTitle => "List of Top Capital Company";
        public Type ExcelRequestType => typeof(ListOfTopCapitalCompanyRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((ListOfTopCapitalCompanyRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            ListOfTopCapitalCompanyRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);

            // The same window Post serves, so the sheet cannot show rows the grid never had.
            // (It used to stream EVERY matching company, ignoring the top-N entirely.)
            var window = await TopCompaniesAsync(procedureRequest!, request, cancellationToken);

            sink.Append(window.Select(row => row.ToResult()).ToList());
        }

        /// <summary>
        /// The report's rows: the <c>No of List</c> highest-capital companies matching the
        /// filters, capital descending. Ranking and truncation happen in SQL via the
        /// procedure's sort/paging parameters.
        /// </summary>
        private Task<List<sp_PaThaKaReportRow>> TopCompaniesAsync(
            sp_PaThaKaReportRequest procedureRequest,
            ListOfTopCapitalCompanyRequest request,
            CancellationToken cancellationToken = default)
            => sp_PaThaKaReport.ExecuteQueryable(
                    _context,
                    procedureRequest,
                    RankColumn,
                    RankOrder,
                    pageIndex: 0,
                    pageSize: ResolveTopCount(request))
                .ToListAsync(cancellationToken);

        private static int ResolveTopCount(ListOfTopCapitalCompanyRequest request) =>
            request.TotalRecords <= 0
                ? DefaultTopCount
                : Math.Min(request.TotalRecords, MaxTopCount);

        private bool TryCreateReportRequest(
            ListOfTopCapitalCompanyRequest? request,
            out sp_PaThaKaReportRequest? procedureRequest,
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
            procedureRequest = new sp_PaThaKaReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                BusinessTypeId = request.BusinessTypeId,
                LineofBusinessId = request.LineofBusinessId,
                State = request.State,
                Status = request.Status,
            };

            return true;
        }
    }

    public sealed class ListOfTopCapitalCompanyRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int BusinessTypeId { get; set; }
        public int LineofBusinessId { get; set; }
        public string State { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// How many top-capital companies to return (the "No of List" filter). Defaults to
        /// the legacy screen's 10 for a caller that omits it; the controller clamps it.
        /// </summary>
        public int TotalRecords { get; set; } = 10;
    }
}

