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
    // The sheet gained the legacy two-block layout (it had no export at all before), so
    // cached files from the previous format version must not be served.
    [ExcelFormatVersion(2)]
    // IExcelReportLayoutProvider: this is a document-style master/detail report, not a grid —
    // the sheet is two tables (company info, then that company's directors), so it is declared
    // here rather than built from the page's flat column spec.
    // IExcelNoFooterReport: no RDLC of this report has a Sum row, and without the marker the
    // footer resolver would replay Post — whose failure, under the default Required policy,
    // would fail the whole export.
    public class ListOfDirectorsByCompanyRegistrationNoController
        : ControllerBase, IStreamingExcelReport, IExcelReportLayoutProvider, IExcelNoFooterReport
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
        // detail. Not paged (document-style report, mirrors CardLists); the Excel export
        // reproduces the same two blocks via GetExcelLayout/WriteRowsAsync below.
        [HttpPost("Detail")]
        public async Task<ActionResult<DirectorsByCompanyDetailResult>> Detail(
            [FromBody] DirectorsByCompanyDetailRequest? request)
        {
            var registrationNo = request?.CompanyRegistrationNo?.Trim();
            if (string.IsNullOrWhiteSpace(registrationNo))
            {
                return BadRequest("Company Registration No is required.");
            }

            return Ok(await LoadDetailAsync(registrationNo));
        }

        /// <summary>
        /// The document this report is: the company block and its directors. Shared by
        /// <see cref="Detail"/> and the Excel export so the page and the sheet cannot disagree.
        /// </summary>
        private async Task<DirectorsByCompanyDetailResult> LoadDetailAsync(
            string registrationNo,
            CancellationToken cancellationToken = default)
        {
            var rows = await sp_DirectorListReport.ExecuteQueryable(
                _context,
                new sp_DirectorListReportRequest
                {
                    FromDate = DateTime.Today,
                    ToDate = DateTime.Today,
                    CompanyRegistrationNo = registrationNo,
                    Type = "By Company Registration No",
                }).ToListAsync(cancellationToken);

            var first = rows.FirstOrDefault();
            if (first == null)
            {
                return new DirectorsByCompanyDetailResult { CompanyRegistrationNo = registrationNo };
            }

            return new DirectorsByCompanyDetailResult
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
            };
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

        /// <summary>
        /// The two stacked blocks of the legacy DirectorListByCompanyRegistrationNoReport.rdlc:
        /// the 7-column company header (rdlc:451-851) and, under a "List of Directors" bar
        /// (rdlc:1352), the director table (rdlc:1419-1639). Headers, order and the composed
        /// Address cells are the page's, verbatim
        /// (Frontend/src/Report/Page/ListOfDirectorsByCompanyRegistrationNo.tsx:195-262).
        /// </summary>
        [NonAction]
        public ExcelReportLayout GetExcelLayout(object request)
        {
            var typedRequest = (ListOfDirectorsByCompanyRegistrationNoRequest)request;

            return new ExcelReportLayout
            {
                // The legacy RDLC banner (rdlc:223-308) and its header1 parameter,
                // "List of Directors (<CompanyRegistrationNo>)" — the same three lines the
                // page prints above the tables.
                TitleLines = new[]
                {
                    "Ministry of Commerce",
                    "Directorate of Trade",
                    $"List of Directors ({typedRequest.CompanyRegistrationNo?.Trim()})",
                },
                Sections = new[]
                {
                    new ExcelReportSection
                    {
                        // The company block has no section bar in the RDLC, just its header row.
                        Title = string.Empty,
                        Columns = new[]
                        {
                            ExcelColumn.Text<DirectorsByCompanyInfo>(
                                    "Company Registration No", row => row.CompanyRegistrationNo)
                                .Bind("CompanyRegistrationNo", "companyRegistrationNo"),
                            ExcelColumn.Text<DirectorsByCompanyInfo>("Company Name", row => row.CompanyName)
                                .Bind("CompanyName", "companyName"),
                            ExcelColumn.Text<DirectorsByCompanyInfo>("Company Address", row => JoinAddress(row))
                                .Bind("CompanyAddress", "companyAddress"),
                            ExcelColumn.Date<DirectorsByCompanyInfo>(
                                    "Company Registration Date", row => row.CompanyRegistrationDate)
                                .Bind("CompanyRegistrationDate", "companyRegistrationDate"),
                            ExcelColumn.Date<DirectorsByCompanyInfo>("Valid Date", row => row.EndDate)
                                .Bind("ValidDate", "endDate"),
                            ExcelColumn.Text<DirectorsByCompanyInfo>("Business Type", row => row.BusinessType)
                                .Bind("BusinessType", "businessType"),
                            ExcelColumn.Text<DirectorsByCompanyInfo>("Line of Business", row => row.LineofBusiness)
                                .Bind("LineOfBusiness", "lineofBusiness"),
                        },
                    },
                    new ExcelReportSection
                    {
                        Title = "List of Directors",
                        Columns = new[]
                        {
                            ExcelColumn.RowNumber("No"),
                            ExcelColumn.Text<DirectorsByCompanyDirector>("Name", row => row.DirectorName)
                                .Bind("Name", "directorName"),
                            ExcelColumn.Text<DirectorsByCompanyDirector>("NRC No", row => row.DirectorNRC)
                                .Bind("nrcNo", "directorNRC"),
                            ExcelColumn.Text<DirectorsByCompanyDirector>("Position", row => row.DirectorPosition)
                                .Bind("Position", "directorPosition"),
                            ExcelColumn.Text<DirectorsByCompanyDirector>("Address", row => JoinAddress(row))
                                .Bind("Address", "directorAddress"),
                        },
                    },
                },
            };
        }

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((ListOfDirectorsByCompanyRegistrationNoRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            ListOfDirectorsByCompanyRegistrationNoRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            // The SAME load Detail does, so the sheet and the page cannot disagree. (The export
            // used to stream the flat grid projection, which is not what this screen shows.)
            var detail = await LoadDetailAsync(
                request.CompanyRegistrationNo?.Trim() ?? string.Empty, cancellationToken);

            if (detail.Company == null)
            {
                return;
            }

            sink.BeginSection(0);
            sink.Append(new[] { detail.Company });

            sink.BeginSection(1);
            sink.Append(detail.Directors);
        }

        /// <summary>
        /// The single combined address cell both blocks print, mirroring the page's
        /// <c>buildAddress</c>: State and Postal Code share one comma-separated part.
        /// </summary>
        private static string JoinAddress(IReportAddressParts parts)
        {
            var stateLine = string.Join(
                ' ',
                new[] { parts.State, parts.PostalCode }
                    .Select(part => part?.Trim())
                    .Where(part => !string.IsNullOrEmpty(part)));

            return string.Join(
                ", ",
                new[]
                {
                    parts.UnitLevel?.Trim(),
                    parts.StreetNumberStreetName?.Trim(),
                    parts.QuarterCityTownship?.Trim(),
                    stateLine,
                    parts.Country?.Trim(),
                }.Where(part => !string.IsNullOrEmpty(part)));
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

    /// <summary>
    /// The address parts both blocks of this report compose into one cell.
    /// </summary>
    public interface IReportAddressParts
    {
        string? UnitLevel { get; }
        string? StreetNumberStreetName { get; }
        string? QuarterCityTownship { get; }
        string? State { get; }
        string? Country { get; }
        string? PostalCode { get; }
    }

    public sealed class DirectorsByCompanyInfo : IReportAddressParts
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

    public sealed class DirectorsByCompanyDirector : IReportAddressParts
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
