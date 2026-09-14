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
    /// Advance Search for Border Import Permit -- the old admin's Advance Search screen scoped to <c>Border Import Permit</c>
    /// (<c>Views/Reports/AdvanceSearch.cshtml</c>, reached from the tile of that name on its
    /// <c>?type=menu</c> landing page).
    ///
    /// One report per tile, because a report key is what names the route, the sidebar entry and
    /// the Excel export job. The query is the legacy Web API's, ported in
    /// <see cref="sp_AdvanceSearch"/>.
    ///
    /// This type carries no Mode of Transport, Method of Import and Incoterms or Consigned Country columns, so the legacy branch
    /// had those predicates commented out and the filter box does not offer them.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AdvanceSearchBorderImportPermitController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "AdvanceSearchBorderImportPermit";
        private const string LicenceOrPermitType = sp_AdvanceSearch.AdvanceSearchType.BorderImportPermit;

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public AdvanceSearchBorderImportPermitController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_AdvanceSearchResult>>> Post(
            [FromBody] AdvanceSearchBorderImportPermitRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            return Ok(await AdvanceSearchReport.PageAsync(_context, reportRequest!, request!));
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] AdvanceSearchBorderImportPermitRequest? request)
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
        // Must equal the report title the grid shows: the worksheet tab is checked
        // against it (ExcelSpecContractTests).
        public string ExcelWorksheetTitle => "Advance Search for Border Import Permit";
        public Type ExcelRequestType => typeof(AdvanceSearchBorderImportPermitRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((AdvanceSearchBorderImportPermitRequest)request, sink, chunkSize, cancellationToken);

        private Task WriteRowsAsync(
            AdvanceSearchBorderImportPermitRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            return AdvanceSearchReport.StreamAsync(_context, procedureRequest!, sink, chunkSize, cancellationToken);
        }

        private bool TryCreateReportRequest(
            AdvanceSearchBorderImportPermitRequest? request,
            out sp_AdvanceSearchRequest? reportRequest,
            out ActionResult? errorResult)
        {
            reportRequest = null;
            errorResult = null;

            if (request == null)
            {
                errorResult = BadRequest("Request body is required.");
                return false;
            }

            if (request.FromDate == default || request.ToDate == default)
            {
                errorResult = BadRequest("From Date and To Date are required.");
                return false;
            }

            reportRequest = new sp_AdvanceSearchRequest
            {
                TypeOfLicenceOrPermit = LicenceOrPermitType,
                StartDate = request.FromDate,
                EndDate = request.ToDate,
                Pathaka = request.Pathaka?.Trim() ?? string.Empty,
                // "" is this box's "all", not 0 -- see sp_AdvanceSearchRequest.Section.
                Section = request.Section?.Trim() ?? string.Empty,
                SellerCountry = request.SellerCountry,
                PortOfDischarge = request.PortOfDischarge?.Trim() ?? string.Empty,
                // Comma-joined country ids.
                CountryOfOrigin = request.CountryOfOrigin?.Trim() ?? string.Empty,
                Description = request.Description?.Trim() ?? string.Empty,
                StatementCode = request.StatementCode,
                ApplyType = request.ApplyType?.Trim() ?? string.Empty,
            };

            return true;
        }
    }

    public sealed class AdvanceSearchBorderImportPermitRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? Pathaka { get; set; }

        /// <summary>Section id as a string: this box's "all" is <c>""</c>, not <c>0</c>.</summary>
        public string? Section { get; set; }

        public int SellerCountry { get; set; }
        public string? PortOfDischarge { get; set; }
        /// <summary>Comma-joined country ids.</summary>
        public string? CountryOfOrigin { get; set; }

        public string? Description { get; set; }
        public int StatementCode { get; set; }

        /// <summary>
        /// Accepted and ignored, exactly as in the legacy Web API: <c>data.Office</c> has no
        /// references in <c>AdvanceSearchRepository.cs</c>, so the Office (Sakhan) box on this
        /// screen has never filtered anything -- the Sakhan table is joined for display only.
        /// Making it filter is one predicate (<c>x.SakhanId == Office</c>) but it would move row
        /// counts away from the old screen, so it needs its own customer decision.
        /// </summary>
        public int Office { get; set; }

        public string? ApplyType { get; set; }
    }
}
