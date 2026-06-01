using System;
using System.Linq;
using System.Threading.Tasks;
using API.DBContext;
using API.Model;
using API.Service.Reports;
using API.StoredProcedureToLinq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers.Report
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ListOfTopCapitalCompanyController : ControllerBase
    {
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        // Excel worksheets allow 1,048,576 rows including the header.
        private const int MaxExcelDataRows = 1_048_576 - 1;

        private readonly TradeNetDbContext _context;

        public ListOfTopCapitalCompanyController(TradeNetDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_PaThaKaReportResult>>> Post([FromBody] ListOfTopCapitalCompanyRequest? request)
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

            // Pagination is performed inside the stored procedure (OFFSET/FETCH);
            // every row carries the total matching count via TotalCount.
            var rows = await sp_PaThaKaReport.ExecuteAsync(
                _context,
                procedureRequest!,
                sortColumn,
                sortOrder,
                pageIndex,
                pageSize);

            var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;
            var data = rows.Select(row => row.ToResult()).ToList();

            var result = ApiResult<sp_PaThaKaReportResult>.CreatePageFromRows(
                data,
                totalCount,
                pageIndex,
                pageSize,
                request.SortColumn,
                request.SortOrder,
                request.FilterColumn,
                request.FilterQuery);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] ListOfTopCapitalCompanyRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var sortColumn = string.IsNullOrWhiteSpace(request!.SortColumn) ? null : request.SortColumn;
            var sortOrder = string.IsNullOrWhiteSpace(request.SortOrder) ? null : request.SortOrder;

            // Null page size returns every matching row from the stored procedure.
            var rows = await sp_PaThaKaReport.ExecuteAsync(
                _context,
                procedureRequest!,
                sortColumn,
                sortOrder,
                pageIndex: null,
                pageSize: null);

            if (rows.Count > MaxExcelDataRows)
            {
                return BadRequest($"Excel export supports up to {MaxExcelDataRows} data rows.");
            }

            var data = rows.Select(row => row.ToResult()).ToList();
            var fileBytes = ExcelGenerator.CreateWorkbook(
                data,
                "List of Top Capital Company");

            return File(
                fileBytes,
                ExcelGenerator.ContentType,
                "ListOfTopCapitalCompany.xlsx");
        }

        private bool TryCreateReportRequest(
            ListOfTopCapitalCompanyRequest? request,
            out sp_PaThaKaReportRequest? procedureRequest,
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
            procedureRequest = new sp_PaThaKaReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                BusinessTypeId = request.BusinessTypeId,
                LineofBusinessId = request.LineofBusinessId,
                State = request.State,
                Status = request.Status,
            };

            return true;
        }
    }

    public sealed class ListOfTopCapitalCompanyRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int BusinessTypeId { get; set; }
        public int LineofBusinessId { get; set; }
        public string State { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}

