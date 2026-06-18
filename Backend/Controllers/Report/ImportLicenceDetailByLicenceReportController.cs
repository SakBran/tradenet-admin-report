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

namespace Backend.Controllers.Report
{
    /// <summary>
    /// Licence-level drill report (one row per licence + currency). Reached from the
    /// By Section / Method / Seller Country / Company summaries by clicking a cell; the
    /// drill carries the clicked dimension id AND its currency so the record count here
    /// equals the "No of Licences" shown in that summary cell (the legacy "click the
    /// count → detail" behaviour). The per-item Detail report is left unchanged.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ImportLicenceDetailByLicenceReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "ImportLicenceDetailByLicenceReport";

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public ImportLicenceDetailByLicenceReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<ReportLicenceListResult>>> Post([FromBody] ImportLicenceDetailByLicenceReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var currency = string.IsNullOrWhiteSpace(request!.Currency) ? null : request.Currency;

            var result = await sp_ImportLicenceDetailReport_Fast.CreateLicenceListPagedResultAsync(
                _context, procedureRequest!, currency, request);

            // Currency-grouped footer ("<CUR>: N licence(s)" + "Total: N licence(s)") so the
            // drilled list reconciles with the per-currency count clicked in the summary.
            // It is a heavy COUNT(DISTINCT)+SUM aggregate over the whole section (no
            // seek-and-stop), so it is computed ONLY on the exact-count request — the same
            // lazy request the grid uses for the total — never on the initial fast page.
            // Otherwise a large section (e.g. Section 4 / USD ~32k licences) would block the
            // first paint for ~60s and the grid would look empty ("data not appearing").
            // BasicTable merges this footer from the lazy response.
            if (request.IncludeTotalCount && result.Data.Count > 0)
            {
                result.CurrencyTotals = await sp_ImportLicenceDetailReport_Fast.CreateLicenceCurrencyTotalsAsync(
                    _context, procedureRequest!, currency);
            }

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ImportLicenceDetailByLicenceReportRequest? request)
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
        public string ExcelWorksheetTitle => "Import Licence Detail (By Licence) Report";
        public Type ExcelRequestType => typeof(ImportLicenceDetailByLicenceReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((ImportLicenceDetailByLicenceReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            ImportLicenceDetailByLicenceReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            var currency = string.IsNullOrWhiteSpace(request.Currency) ? null : request.Currency;
            var rows = await sp_ImportLicenceDetailReport_Fast.GetLicenceListRowsAsync(
                _context, procedureRequest!, currency);
            sink.Append(rows);
        }

        private bool TryCreateReportRequest(
            ImportLicenceDetailByLicenceReportRequest? request,
            out sp_ImportLicenceDetailReportRequest? procedureRequest,
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

            procedureRequest = new sp_ImportLicenceDetailReportRequest
            {
                Type = "Oversea",
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                PaThaKaTypeId = request.PaThaKaTypeId,
                ExportImportSectionId = request.ExportImportSectionId,
                ExportImportMethodId = request.ExportImportMethodId,
                ExportImportIncotermId = request.ExportImportIncotermId,
                SellerCountryId = request.SellerCountryId,
                CompanyRegistrationNo = request.CompanyRegistrationNo,
                SakhanId = request.SakhanId,
            };

            return true;
        }
    }

    public sealed class ImportLicenceDetailByLicenceReportRequest : ReportQueryRequest
    {
        public string Type { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int PaThaKaTypeId { get; set; }
        public int ExportImportSectionId { get; set; }
        public int ExportImportMethodId { get; set; }
        public int ExportImportIncotermId { get; set; }
        public int SellerCountryId { get; set; }
        public string CompanyRegistrationNo { get; set; } = string.Empty;
        public int SakhanId { get; set; }

        /// <summary>Optional currency filter — the drill carries the clicked cell's currency.</summary>
        public string Currency { get; set; } = string.Empty;
    }
}
