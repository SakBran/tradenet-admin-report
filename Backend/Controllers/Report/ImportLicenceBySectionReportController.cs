using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.DBContext;
using API.Model;
using API.Model.TradeNet;
using API.Service.ExcelExport;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Controllers.Report
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ImportLicenceBySectionReportController : ControllerBase, IStreamingExcelReport
    {
        private const string ReportKey = "ImportLicenceBySectionReport";

        private readonly TradeNetDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IExcelExportJobService _excelExportJobs;
        private const string New = "New";
        private const string Approved = "Approved";
        private static readonly TimeSpan RowsCacheDuration = TimeSpan.FromMinutes(5);

        public ImportLicenceBySectionReportController(TradeNetDbContext context, IMemoryCache cache, IExcelExportJobService excelExportJobs)
        {
            _context = context;
            _cache = cache;
            _excelExportJobs = excelExportJobs;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<ReportAggregateResult>>> Post([FromBody] ImportLicenceBySectionReportRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }
            var startdate = request.FromDate;
            var endDate = request.ToDate;

            var cacheKey = string.Join(
                "|",
                "ImportLicenceBySection",
                request.Type,
                request.FromDate.Ticks,
                request.ToDate.Ticks,
                request.PaThaKaTypeId,
                request.ExportImportSectionId,
                request.ExportImportMethodId,
                request.ExportImportIncotermId,
                request.SellerCountryId,
                request.CompanyRegistrationNo);

            var rows = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = RowsCacheDuration;
                return await BuildRowsAsync(request);
            }) ?? new List<ReportAggregateResult>();

            var pageIndex = Math.Max(0, request.PageIndex);
            var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
            var pageRows = rows
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToList();

            var result = ApiResult<ReportAggregateResult>.CreatePageFromRows(
                pageRows,
                rows.Count,
                pageIndex,
                pageSize,
                null,
                null,
                request.FilterColumn,
                request.FilterQuery);

            return Ok(result);
        }

        private async Task<List<ReportAggregateResult>> BuildRowsAsync(ImportLicenceBySectionReportRequest request)
        {
            var LicenceListQry = (from licence in _context.ImportLicences.AsNoTracking()
                                  join paThaKa in _context.PaThaKas.AsNoTracking() on licence.PaThaKaId equals paThaKa.Id
                                  join paThaKaType in _context.PaThaKaTypes.AsNoTracking() on paThaKa.PaThaKaTypeId equals paThaKaType.Id
                                  join item in _context.VwImportLicenceItemTotalByCurrencies on licence.Id equals item.ImportLicenceId
                                  //   join currency in _context.Currencies.AsNoTracking() on item.CurrencyId equals currency.Id
                                  join section in _context.ExportImportSections.AsNoTracking() on licence.ExportImportSectionId equals section.Id
                                  where request.Type == "Oversea"
                                      && licence.ApplyType == New
                                      && licence.Status == Approved
                                      && licence.ImportLicenceNo != string.Empty
                                      && licence.CreatedDate >= request.FromDate
                                      && licence.CreatedDate <= request.ToDate
                                      && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                                      && (request.PaThaKaTypeId == 0 || paThaKaType.Id == request.PaThaKaTypeId)
                                      && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                                      && (request.ExportImportMethodId == 0 || licence.ExportImportMethodId == request.ExportImportMethodId)
                                      && (request.ExportImportIncotermId == 0 || licence.ExportImportIncotermId == request.ExportImportIncotermId)
                                      && (request.SellerCountryId == 0 || licence.SellerCountryId == request.SellerCountryId)
                                  select new
                                  {
                                      id = licence.Id,
                                      SectionCode = section.Code,
                                      Amount = item.TotalAmount,
                                      Currency = item.CurrencyId
                                  }).ToList();
            var CurrencyList = await _context.Currencies.AsNoTracking().Where(x => x.Code != null).ToListAsync();
            var SectionList = await _context.ExportImportSections.Where(x => x.Type == "Import Licence").AsNoTracking().ToListAsync();
            var cells = new List<ImportLicenceBySection>();



            foreach (var section in SectionList)
            {
                foreach (var currency in CurrencyList)
                {
                    var cell = new ImportLicenceBySection();


                    cell.Currency = currency.Code != null ? currency.Code : "";
                    cell.Section = section.Code;
                    cell.NoOfLicences = LicenceListQry.Where(x => x.Currency == currency.Id && x.SectionCode == section.Code).Count();
                    cell.TotalValue = LicenceListQry.Where(x => x.Currency == currency.Id && x.SectionCode == section.Code).Sum(x => x.Amount);


                    if (cell.NoOfLicences > 0 || cell.TotalValue > 0)
                    {
                        cells.Add(cell);
                    }

                }
            }


            return cells
                .Select(cell => new ReportAggregateResult
                {
                    SectionName = cell.Section,
                    Currency = cell.Currency,
                    NoOfLicences = (int)cell.NoOfLicences,
                    TotalValue = cell.TotalValue,
                })
                .ToList();
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ImportLicenceBySectionReportRequest? request)
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
        public string ExcelWorksheetTitle => "Import Licence By Section Report";
        public Type ExcelRequestType => typeof(ImportLicenceBySectionReportRequest);

        [NonAction]
        public Task WriteRowsAsync(object request, IExcelRowSink sink, int chunkSize, CancellationToken cancellationToken)
            => WriteRowsAsync((ImportLicenceBySectionReportRequest)request, sink, chunkSize, cancellationToken);

        private async Task WriteRowsAsync(
            ImportLicenceBySectionReportRequest request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            TryCreateReportRequest(request, out var procedureRequest, out _);
            var rows = await sp_ImportLicenceDetailReport_Fast.GetSectionRowsAsync(_context, procedureRequest!);
            sink.Append(rows);
        }

        private bool TryCreateReportRequest(
            ImportLicenceBySectionReportRequest? request,
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

    public sealed class ImportLicenceBySectionReportRequest : ReportQueryRequest
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
