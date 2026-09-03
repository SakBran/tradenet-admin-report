using System;
using System.Collections.Generic;
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
    public class ImportLicenceTotalValueLicencesReportController
        : ControllerBase, IStreamingExcelReport, IExcelReportLayoutProvider
    {
        private const string ReportKey = "ImportLicenceTotalValueLicencesReport";

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public ImportLicenceTotalValueLicencesReportController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ImportLicenceTotalValueLicencesSummary>> Post([FromBody] ImportLicenceTotalValueLicencesReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            // Two summary sections (Total Value per Currency + Total Licences per Pa Tha Ka
            // Type) and the FX-converted grand "Total USD Value", mirroring the legacy
            // ImportLicenceByTotalValueLicenceReport.rdlc layout.
            var summary = await sp_ImportLicenceDetailReport_Fast.GetTotalValueLicencesSummaryAsync(
                _context, procedureRequest!);

            return Ok(summary);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ImportLicenceTotalValueLicencesReportRequest? request)
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
        public string ExcelWorksheetTitle => "Import Licence Total Value & Licences Report";
        public Type ExcelRequestType => typeof(ImportLicenceTotalValueLicencesReportRequest);

        /// <summary>
        /// This report's page is not a grid: it renders TWO tables (Total Value per
        /// currency, Total Licences per Pa Tha Ka type) plus a "Total USD Value" line
        /// (Frontend/src/Report/Page/ImportLicenceTotalValueLicencesReport.tsx —
        /// valueColumns/licenceColumns and the heading), so the sheet is declared here
        /// as two sections instead of being built from the grid's flat column spec.
        /// Headers, order and number formats are the page's, verbatim.
        /// </summary>
        [NonAction]
        public ExcelReportLayout GetExcelLayout(object request)
        {
            var typedRequest = (ImportLicenceTotalValueLicencesReportRequest)request;

            return new ExcelReportLayout
            {
                TitleLines = new[]
                {
                    // Same wording as the page's heading (`heading` in the .tsx).
                    ExcelReportTitle.DateRange(
                        "Import Licences Total Value & Licences",
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
                            // The page prints 4 decimals (formatValue → N4).
                            ExcelColumn.Money4<TotalValueByCurrencyRow>("Total Value", row => row.TotalValue)
                                .Bind("TotalValue", "totalValue"),
                            ExcelColumn.Text<TotalValueByCurrencyRow>("Currency", row => row.Currency)
                                .Bind("Currency", "currency"),
                        },
                    },
                    new ExcelReportSection
                    {
                        Title = "Total Licences",
                        Columns = new[]
                        {
                            ExcelColumn.RowNumber("Sr.No.", width: 9),
                            ExcelColumn.Number<TotalLicencesByPaThaKaTypeRow>("Total Licences", row => row.NoOfLicences)
                                .Bind("TotalLicences", "noOfLicences"),
                            ExcelColumn.Text<TotalLicencesByPaThaKaTypeRow>("Pa Tha Ka Type", row => row.PaThaKaType)
                                .Bind("PaThaKaType", "paThaKaType"),
                        },
                    },
                },
            };
        }

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((ImportLicenceTotalValueLicencesReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            ImportLicenceTotalValueLicencesReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);

            // The SAME call Post makes, so the sheet and the page cannot disagree.
            var summary = await sp_ImportLicenceDetailReport_Fast.GetTotalValueLicencesSummaryAsync(
                _context, procedureRequest!);

            sink.BeginSection(0);
            sink.Append(summary.TotalValueByCurrency);

            sink.BeginSection(1);
            sink.Append(summary.TotalLicencesByPaThaKaType);

            sink.AppendNote(
                "Total USD Value: " + summary.TotalUsdValue.ToString("N4", CultureInfo.InvariantCulture));
        }

        private bool TryCreateReportRequest(
            ImportLicenceTotalValueLicencesReportRequest? request,
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

    public sealed class ImportLicenceTotalValueLicencesReportRequest : ReportQueryRequest
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

