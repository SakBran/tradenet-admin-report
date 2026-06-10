using System;
using System.Collections.Generic;
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
    public class BorderImportLicenceDetailReportPendingController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "BorderImportLicenceDetailReportPending";

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public BorderImportLicenceDetailReportPendingController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_ImportLicencePendingDetailReportResult>>> Post([FromBody] BorderImportLicenceDetailReportPendingRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var result = await sp_BorderImportLicencePendingDetailReport.CreatePagedResultAsync(_context, procedureRequest!, request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] BorderImportLicenceDetailReportPendingRequest? request)
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
        public string ExcelWorksheetTitle => "Border Import Licence Detail Report (Pending)";
        public Type ExcelRequestType => typeof(BorderImportLicenceDetailReportPendingRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((BorderImportLicenceDetailReportPendingRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            BorderImportLicenceDetailReportPendingRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            await foreach (var chunk in sp_BorderImportLicencePendingDetailReport.ExecuteQueryable(_context, procedureRequest!)
                .AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
            {
                sink.Append(chunk.ConvertAll(row => row.ToResult()));
            }
        }

        private bool TryCreateReportRequest(
            BorderImportLicenceDetailReportPendingRequest? request,
            out sp_ImportLicencePendingDetailReportRequest? procedureRequest,
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
            procedureRequest = new sp_ImportLicencePendingDetailReportRequest
            {
                Type = "Border",
                FromDate = request.FromDate,
                ToDate = NormalizeInclusiveToDate(request.ToDate),
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

        private static DateTime NormalizeInclusiveToDate(DateTime toDate)
            => toDate.TimeOfDay == TimeSpan.Zero
                ? toDate.Date.AddDays(1).AddTicks(-1)
                : toDate;
    }

    public sealed class BorderImportLicenceDetailReportPendingRequest : ReportQueryRequest
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
    }
}

