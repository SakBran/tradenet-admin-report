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
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Controllers.Report
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AlcoholicBeveragesImportationRegistrationByVoucherController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "AlcoholicBeveragesImportationRegistrationByVoucher";

        private readonly TradeNetDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IExcelExportJobService _excelExportJobs;

        public AlcoholicBeveragesImportationRegistrationByVoucherController(
            TradeNetDbContext context,
            IMemoryCache cache,
            IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _cache = cache;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_WineImportationRegistrationReportResult>>> Post(
            [FromBody] AlcoholicBeveragesImportationRegistrationByVoucherRequest? request)
        {
            if (!TryCreateReportRequest(request, out var reportRequest, out var errorResult))
            {
                return errorResult!;
            }

            var result = await sp_WineImportationRegistrationReport_Fast.CreatePagedResultAsync(
                _context,
                _cache,
                reportRequest!,
                request!);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel(
            [FromBody] AlcoholicBeveragesImportationRegistrationByVoucherRequest? request)
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
        public string ExcelWorksheetTitle => "Alcoholic Beverages Importation Registration By Voucher";
        public Type ExcelRequestType => typeof(AlcoholicBeveragesImportationRegistrationByVoucherRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((AlcoholicBeveragesImportationRegistrationByVoucherRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            AlcoholicBeveragesImportationRegistrationByVoucherRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            await foreach (var chunk in sp_WineImportationRegistrationReport_Fast.StreamResolvedChunksAsync(
                _context, _cache, procedureRequest!, chunkSize, cancellationToken))
            {
                sink.Append(chunk);
            }
        }

        private bool TryCreateReportRequest(
            AlcoholicBeveragesImportationRegistrationByVoucherRequest? request,
            out sp_WineImportationRegistrationReportRequest? reportRequest,
            out ActionResult? errorResult)
        {
            reportRequest = null;
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

            reportRequest = new sp_WineImportationRegistrationReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                PaymentType = request.PaymentType?.Trim() ?? string.Empty,
                ApplyType = request.ApplyType?.Trim() ?? string.Empty
            };

            return true;
        }
    }

    public sealed class AlcoholicBeveragesImportationRegistrationByVoucherRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? ApplyType { get; set; }
        public string? PaymentType { get; set; }
    }
}
