using System;
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
    /// <summary>
    /// EICC Licence/Permit Report -- the old admin's EICC &gt; Licence &amp; Permit &gt; Reports screen (<c>@Type = 'LicencePermit'</c>).
    ///
    /// The legacy screen was one view (<c>Views/EICC/EICCReport.cshtml</c>) reached from three
    /// sidebar entries that differ only by <c>type</c>; each one is its own report here, because
    /// a report key is what names the route, the menu entry and the Excel export job.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class EICCLicencePermitReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "EICCLicencePermitReport";
        private const string EICCType = sp_EICCReport.LicencePermitType;

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public EICCLicencePermitReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_EICCReportResult>>> Post(
            [FromBody] EICCLicencePermitReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var query = sp_EICCReport.Query(_context, reportRequest!);
            var result = await ReportQueryService.CreatePagedResultAsync(query, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] EICCLicencePermitReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out _, out var errorResult))
            {
                return errorResult!;
            }

            var result = await _excelExportJobs.EnqueueAsync(
                ReportKey,
                request!,
                request!.Date,
                User.FindFirst(ClaimTypes.Name)?.Value);

            return Ok(result);
        }

        // --- Async Excel export streaming (used by the background queue worker) ---
        // Must equal the report title the grid shows: the worksheet tab is checked
        // against it (ExcelSpecContractTests).
        public string ExcelWorksheetTitle => "EICC Licence and Permit Report";
        public Type ExcelRequestType => typeof(EICCLicencePermitReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((EICCLicencePermitReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            EICCLicencePermitReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            var query = sp_EICCReport.Query(_context, procedureRequest!);
            await foreach (var chunk in query.AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
            {
                sink.Append(chunk);
            }
        }

        private bool TryCreateReportRequest(
            EICCLicencePermitReportRequest? request,
            out sp_EICCReportRequest? reportRequest,
            out ActionResult? errorResult)
        {
            reportRequest = null;
            errorResult = null;

            if (request == null)
            {
                errorResult = BadRequest("Request body is required.");
                return false;
            }

            if (request.Date == default)
            {
                errorResult = BadRequest("Date is required.");
                return false;
            }

            reportRequest = new sp_EICCReportRequest
            {
                Type = EICCType,
                // The legacy filter box defaults to Pending and offers only Pending/Approved;
                // it has no "all" option, and the procedure compares Status with `=`.
                EICCStatus = string.IsNullOrWhiteSpace(request.EICCStatus)
                    ? "Pending"
                    : request.EICCStatus.Trim(),
                // Date only: the old screen posted dd/MM/yyyy and the procedure matches the
                // stored EICCDate exactly, so any time component would match nothing.
                EICCDate = request.Date.Date,
                FormType = request.FormType?.Trim() ?? string.Empty,
                ProductGroupId = request.ProductGroupId,
                ProductItemId = request.ProductItemId
            };

            return true;
        }
    }

    public sealed class EICCLicencePermitReportRequest : ReportQueryRequest
    {
        /// <summary>
        /// The single EICC date the report is run for. Named `Date` -- not `EICCDate` --
        /// because that is the name the Excel header block discovers a one-date report by
        /// (<c>ExcelRequestDates.Describe</c>), so the sheet gets its "Date: dd/MM/yyyy" line.
        /// </summary>
        public DateTime Date { get; set; }
        public string? EICCStatus { get; set; }
        public string? FormType { get; set; }
        public int ProductGroupId { get; set; }
        public int ProductItemId { get; set; }
    }
}
