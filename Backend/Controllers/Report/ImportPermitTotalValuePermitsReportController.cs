using System;
using System.Globalization;
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
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [ExcelFormatVersion(2)]
    public class ImportPermitTotalValuePermitsReportController
        : ControllerBase, IStreamingExcelReport, IExcelReportLayoutProvider, IExcelNoFooterReport
    {
        private const string ReportKey = "ImportPermitTotalValuePermitsReport";

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public ImportPermitTotalValuePermitsReportController(
            TradeNetDbContext context,
            IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ImportPermitTotalValuePermitsSummary>> Post(
            [FromBody] ImportPermitTotalValuePermitsReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var summary = await sp_ImportPermitDetailReport_Fast.GetTotalValuePermitsSummaryAsync(
                _context,
                procedureRequest!);

            return Ok(summary);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel(
            [FromBody] ImportPermitTotalValuePermitsReportRequest? request)
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

        public string ExcelWorksheetTitle => "Import Permit Total Value & Permits Report";
        public Type ExcelRequestType => typeof(ImportPermitTotalValuePermitsReportRequest);

        [NonAction]
        public ExcelReportLayout GetExcelLayout(object request)
        {
            var typedRequest = (ImportPermitTotalValuePermitsReportRequest)request;

            return new ExcelReportLayout
            {
                TitleLines = new[]
                {
                    ExcelReportTitle.DateRange(
                        "Import Permits Total Value & Permits",
                        typedRequest.FromDate,
                        typedRequest.ToDate),
                },
                Sections = new[]
                {
                    new ExcelReportSection
                    {
                        Title = "Total Value",
                        Columns = new[]
                        {
                            ExcelColumn.RowNumber("Sr.No.", width: 9),
                            ExcelColumn.Money4<TotalValueByCurrencyRow>(
                                    "Total Value",
                                    row => row.TotalValue)
                                .Bind("TotalValue", "totalValue"),
                            ExcelColumn.Text<TotalValueByCurrencyRow>(
                                    "Currency",
                                    row => row.Currency)
                                .Bind("Currency", "currency"),
                        },
                    },
                    new ExcelReportSection
                    {
                        Title = "Total Permits",
                        Columns = new[]
                        {
                            ExcelColumn.RowNumber("Sr.No.", width: 9),
                            ExcelColumn.Number<TotalPermitsByPaThaKaTypeRow>(
                                    "Total Permits",
                                    row => row.NoOfPermits)
                                .Bind("TotalPermits", "noOfPermits"),
                            ExcelColumn.Text<TotalPermitsByPaThaKaTypeRow>(
                                    "Pa Tha Ka Type",
                                    row => row.PaThaKaType)
                                .Bind("PaThaKaType", "paThaKaType"),
                        },
                    },
                },
            };
        }

        [NonAction]
        public Task WriteRowsAsync(
            object request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
            => WriteRowsAsync(
                (ImportPermitTotalValuePermitsReportRequest)request,
                sink,
                cancellationToken);

        private async Task WriteRowsAsync(
            ImportPermitTotalValuePermitsReportRequest request,
            IExcelRowSink sink,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryCreateReportRequest(request, out var procedureRequest, out _);

            var summary = await sp_ImportPermitDetailReport_Fast.GetTotalValuePermitsSummaryAsync(
                _context,
                procedureRequest!);

            sink.BeginSection(0);
            sink.Append(summary.TotalValueByCurrency);

            sink.BeginSection(1);
            sink.Append(summary.TotalPermitsByPaThaKaType);

            sink.AppendNote(
                "Total USD Value: "
                + summary.TotalUsdValue.ToString("N4", CultureInfo.InvariantCulture));
        }

        private bool TryCreateReportRequest(
            ImportPermitTotalValuePermitsReportRequest? request,
            out sp_ImportPermitDetailReportRequest? procedureRequest,
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

            procedureRequest = new sp_ImportPermitDetailReportRequest
            {
                Type = "Oversea",
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                PaThaKaTypeId = request.PaThaKaTypeId,
                ExportImportSectionId = request.ExportImportSectionId,
            };

            return true;
        }
    }

    public sealed class ImportPermitTotalValuePermitsReportRequest : ReportQueryRequest
    {
        public string Type { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int PaThaKaTypeId { get; set; }
        public int ExportImportSectionId { get; set; }
    }
}
