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
    public class ListOfDirectorsByCompanyRegistrationNoController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "ListOfDirectorsByCompanyRegistrationNo";

        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        // Excel worksheets allow 1,048,576 rows including the header.
        private const int MaxExcelDataRows = 1_048_576 - 1;

        private readonly TradeNetDbContext _context;
        private readonly IExcelExportJobService _excelExportJobs;

        public ListOfDirectorsByCompanyRegistrationNoController(TradeNetDbContext context, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _excelExportJobs = excelExportJobs;
        }

        // Master-detail report keyed by a single Company Registration No, restoring the
        // legacy PaThaKaDirectorListByCompanyRegistrationNoReport layout: one company-info
        // header + that company's directors (No / Name / NRC No / Position / Address).
        // Uses the @Type='By Company Registration No' branch of sp_DirectorListReport, which
        // ignores the date range and returns one row per director with full company + director
        // detail. No paging / no Excel (document-style report, mirrors CardLists).
        [HttpPost("Detail")]
        public async Task<ActionResult<DirectorsByCompanyDetailResult>> Detail(
            [FromBody] DirectorsByCompanyDetailRequest? request)
        {
            var registrationNo = request?.CompanyRegistrationNo?.Trim();
            if (string.IsNullOrWhiteSpace(registrationNo))
            {
                return BadRequest("Company Registration No is required.");
            }

            var rows = await sp_DirectorListReport.ExecuteAsync(
                _context,
                new sp_DirectorListReportRequest
                {
                    FromDate = DateTime.Today,
                    ToDate = DateTime.Today,
                    CompanyRegistrationNo = registrationNo,
                    Type = "By Company Registration No",
                });

            var first = rows.FirstOrDefault();
            if (first == null)
            {
                return Ok(new DirectorsByCompanyDetailResult { CompanyRegistrationNo = registrationNo });
            }

            return Ok(new DirectorsByCompanyDetailResult
            {
                CompanyRegistrationNo = registrationNo,
                Company = new DirectorsByCompanyInfo
                {
                    CompanyRegistrationNo = first.CompanyRegistrationNo,
                    CompanyName = first.CompanyName,
                    CompanyRegistrationDate = first.CompanyRegistrationDate,
                    EndDate = first.EndDate,
                    BusinessType = first.BusinessType,
                    LineofBusiness = first.LineofBusiness,
                    UnitLevel = first.UnitLevel,
                    StreetNumberStreetName = first.StreetNumberStreetName,
                    QuarterCityTownship = first.QuarterCityTownship,
                    State = first.State,
                    Country = first.Country,
                    PostalCode = first.PostalCode,
                },
                Directors = rows.Select(row => new DirectorsByCompanyDirector
                {
                    DirectorName = row.DirectorName,
                    DirectorNRC = row.DirectorNRC,
                    DirectorPosition = row.DirectorPosition,
                    UnitLevel = row.DirectorUnitLevel,
                    StreetNumberStreetName = row.DirectorStreetNumberStreetName,
                    QuarterCityTownship = row.DirectorQuarterCityTownship,
                    State = row.DirectorState,
                    Country = row.DirectorCountry,
                    PostalCode = row.DirectorPostalCode,
                }).ToList(),
            });
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_DirectorListReportResult>>> Post([FromBody] ListOfDirectorsByCompanyRegistrationNoRequest? request)
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

            var rows = await sp_DirectorListReport.ExecuteAsync(
                _context, procedureRequest!, sortColumn, sortOrder, pageIndex, pageSize);

            var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;
            var data = rows.Select(row => row.ToResult()).ToList();

            var result = ApiResult<sp_DirectorListReportResult>.CreatePageFromRows(
                data, totalCount, pageIndex, pageSize,
                request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ListOfDirectorsByCompanyRegistrationNoRequest? request)
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
        public string ExcelWorksheetTitle => "List of Directors By Company Registration No";
        public Type ExcelRequestType => typeof(ListOfDirectorsByCompanyRegistrationNoRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((ListOfDirectorsByCompanyRegistrationNoRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            ListOfDirectorsByCompanyRegistrationNoRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            await foreach (var chunk in sp_DirectorListReport.ExecuteQueryable(_context, procedureRequest!)
                .AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
            {
                sink.Append(chunk.Select(row => row.ToResult()).ToList());
            }
        }

        private bool TryCreateReportRequest(
            ListOfDirectorsByCompanyRegistrationNoRequest? request,
            out sp_DirectorListReportRequest? procedureRequest,
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
            procedureRequest = new sp_DirectorListReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                CompanyRegistrationNo = request.CompanyRegistrationNo,
                Name = request.Name,
                Nationality = request.Nationality,
                NRCType = request.NRCType,
                NRCPrefixId = request.NRCPrefixId,
                NRCPrefixCodeId = request.NRCPrefixCodeId,
                NRCNo = request.NRCNo,
                Type = request.Type,
            };

            return true;
        }
    }

    public sealed class ListOfDirectorsByCompanyRegistrationNoRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string CompanyRegistrationNo { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Nationality { get; set; } = string.Empty;
        public string NRCType { get; set; } = string.Empty;
        public int NRCPrefixId { get; set; }
        public int NRCPrefixCodeId { get; set; }
        public string NRCNo { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    public sealed class DirectorsByCompanyDetailRequest
    {
        public string CompanyRegistrationNo { get; set; } = string.Empty;
    }

    public sealed class DirectorsByCompanyDetailResult
    {
        public string CompanyRegistrationNo { get; set; } = string.Empty;
        public DirectorsByCompanyInfo? Company { get; set; }
        public List<DirectorsByCompanyDirector> Directors { get; set; } = new();
    }

    public sealed class DirectorsByCompanyInfo
    {
        public string CompanyRegistrationNo { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public DateTime CompanyRegistrationDate { get; set; }
        public DateTime EndDate { get; set; }
        public string BusinessType { get; set; } = string.Empty;
        public string? LineofBusiness { get; set; }
        public string? UnitLevel { get; set; }
        public string? StreetNumberStreetName { get; set; }
        public string? QuarterCityTownship { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
    }

    public sealed class DirectorsByCompanyDirector
    {
        public string? DirectorName { get; set; }
        public string? DirectorNRC { get; set; }
        public string? DirectorPosition { get; set; }
        public string? UnitLevel { get; set; }
        public string? StreetNumberStreetName { get; set; }
        public string? QuarterCityTownship { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
    }
}
