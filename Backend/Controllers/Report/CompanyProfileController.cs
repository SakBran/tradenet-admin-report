using System;
using System.Collections.Generic;
using System.Globalization;
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
    // 2 = the "11 ministries" layout (complaint 2026-09-25); cached files of the old
    // Myanmar-header sheet must not be served.
    [ExcelFormatVersion(2)]
    // IExcelReportLayoutProvider: the sheet is a grouped table — company cells merged over
    // their director rows under a banded "Board of Director" header — which the page's
    // flat column spec cannot describe. IExcelNoFooterReport: the report has no totals row.
    public class CompanyProfileController
        : ControllerBase, IStreamingExcelReport, IExcelReportLayoutProvider, IExcelNoFooterReport
    {
        private const string ReportKey = "CompanyProfile";

        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        // Excel worksheets allow 1,048,576 rows including the header.
        private const int MaxExcelDataRows = 1_048_576 - 1;

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public CompanyProfileController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_CompanyProfileReportResult>>> Post([FromBody] CompanyProfileRequest? request)
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

            var rows = await sp_CompanyProfileReport.ExecuteAsync(
                _context, procedureRequest!, sortColumn, sortOrder, pageIndex, pageSize);

            var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;
            var data = rows.Select(row => row.ToResult()).ToList();

            var result = ApiResult<sp_CompanyProfileReportResult>.CreatePageFromRows(
                data, totalCount, pageIndex, pageSize,
                request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] CompanyProfileRequest? request)
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
        public string ExcelWorksheetTitle => "Company Profile";
        public Type ExcelRequestType => typeof(CompanyProfileRequest);

        private const string DirectorBand = "Board of Director";

        /// <summary>
        /// The layout the customer sends to the 11 ministries (complaint 2026-09-25), exactly
        /// as <c>Frontend/src/Report/Page/CompanyProfile.tsx</c> renders it: one merged block
        /// per company over its director rows, "No" counting companies, and Name / NRC No.
        /// under a "Board of Director" band. The leaf headers match the page's bespoke spec
        /// (<c>Frontend/src/Report/excel/bespoke/companyProfile.ts</c>).
        /// </summary>
        [NonAction]
        public ExcelReportLayout GetExcelLayout(object request)
        {
            var typedRequest = (CompanyProfileRequest)request;

            return new ExcelReportLayout
            {
                TitleLines = new[]
                {
                    "Ministry of Commerce",
                    "Directorate of Trade",
                    ExcelReportTitle.DateRange("Company Profile", typedRequest.FromDate, typedRequest.ToDate),
                },
                RowGroupKey = row => ((sp_CompanyProfileReportResult)row).Id,
                Columns = new[]
                {
                    ExcelColumn.RowNumber("No").MergedWithinRowGroup(),
                    ExcelColumn.WrappedText<sp_CompanyProfileReportResult>("Company's Name", CompanyNameCell, 30)
                        .Bind("CompanyName", "companyName").MergedWithinRowGroup(),
                    ExcelColumn.WrappedText<sp_CompanyProfileReportResult>("Address", row => row.CompanyAddress, 32)
                        .Bind("CompanyAddress", "companyAddress").MergedWithinRowGroup(),
                    ExcelColumn.WrappedText<sp_CompanyProfileReportResult>("EIR No. & Date", EirCell, 24, centered: true)
                        .Bind("EirValidity", "eirValidity").MergedWithinRowGroup(),
                    ExcelColumn.WrappedText<sp_CompanyProfileReportResult>(
                            "Type of Organization", row => row.BusinessType, 14, centered: true)
                        .Bind("BusinessType", "businessType").MergedWithinRowGroup(),
                    ExcelColumn.WrappedText<sp_CompanyProfileReportResult>("လုပ်ငန်းရည်ရွယ်ချက်", PermitBusinessCell, 26)
                        .Bind("PermitBusiness", "permitBusiness").MergedWithinRowGroup(),
                    ExcelColumn.WrappedText<sp_CompanyProfileReportResult>("Capital", row => row.CapitalText, 14, centered: true)
                        .Bind("CapitalText", "capitalText").MergedWithinRowGroup(),
                    ExcelColumn.WrappedText<sp_CompanyProfileReportResult>("Name", row => row.DirectorName, 22)
                        .Bind("DirectorName", "directorName").WithGroupHeader(DirectorBand),
                    ExcelColumn.WrappedText<sp_CompanyProfileReportResult>("NRC No.", row => row.DirectorNrc, 20)
                        .Bind("DirectorNrc", "directorNrc").WithGroupHeader(DirectorBand),
                    ExcelColumn.WrappedText<sp_CompanyProfileReportResult>("Title", row => row.DirectorTitle, 12)
                        .Bind("DirectorTitle", "directorTitle"),
                },
            };
        }

        // "BABY VISION COMPANY LIMITED" / "138468097" / "(25/08/2023)" — the grid's three lines.
        private static string CompanyNameCell(sp_CompanyProfileReportResult row)
            => string.Format(
                CultureInfo.InvariantCulture,
                "{0}\n{1}\n({2:dd/MM/yyyy})",
                row.CompanyName,
                row.CompanyRegistrationNo,
                row.CompanyRegistrationDate);

        // The EIR No. is the company registration no, over its validity period.
        private static string EirCell(sp_CompanyProfileReportResult row)
            => row.CompanyRegistrationNo + "\n" + row.EirValidity;

        // The legacy report put each permitted business on its own line
        // (RDLC: Replace(PermitBusiness, ",", NewLine)); the grid does the same.
        private static string PermitBusinessCell(sp_CompanyProfileReportResult row)
            => string.Join(
                "\n",
                (row.PermitBusiness ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((CompanyProfileRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            CompanyProfileRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            await foreach (var chunk in sp_CompanyProfileReport.ExecuteQueryable(_context, procedureRequest!)
                .AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
            {
                sink.Append(chunk.Select(row => row.ToResult()).ToList());
            }
        }

        private bool TryCreateReportRequest(
            CompanyProfileRequest? request,
            out sp_CompanyProfileReportRequest? procedureRequest,
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
            procedureRequest = new sp_CompanyProfileReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                CompanyRegistrationNo = request.CompanyRegistrationNo,
            };

            return true;
        }
    }

    public sealed class CompanyProfileRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string CompanyRegistrationNo { get; set; } = string.Empty;
    }
}
