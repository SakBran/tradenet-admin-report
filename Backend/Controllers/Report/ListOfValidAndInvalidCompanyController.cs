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
    public class ListOfValidAndInvalidCompanyController : ControllerBase
    {
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        // Excel worksheets allow 1,048,576 rows including the header.
        private const int MaxExcelDataRows = 1_048_576 - 1;

        private readonly TradeNetDbContext _context;

        public ListOfValidAndInvalidCompanyController(TradeNetDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_PaThaKaValidInvalidReportResult>>> Post([FromBody] ListOfValidAndInvalidCompanyRequest? request)
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
            var rows = await sp_PaThaKaValidInvalidReport.ExecuteAsync(
                _context,
                procedureRequest!,
                sortColumn,
                sortOrder,
                pageIndex,
                pageSize);

            var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;
            var data = rows.Select(row => row.ToResult()).ToList();

            var result = ApiResult<sp_PaThaKaValidInvalidReportResult>.CreatePageFromRows(
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
        public async Task<IActionResult> Excel([FromBody] ListOfValidAndInvalidCompanyRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var sortColumn = string.IsNullOrWhiteSpace(request!.SortColumn) ? null : request.SortColumn;
            var sortOrder = string.IsNullOrWhiteSpace(request.SortOrder) ? null : request.SortOrder;

            // Null page size returns every matching row from the stored procedure.
            var rows = await sp_PaThaKaValidInvalidReport.ExecuteAsync(
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
                "List of Valid and Invalid Company");

            return File(
                fileBytes,
                ExcelGenerator.ContentType,
                "ListOfValidAndInvalidCompany.xlsx");
        }

        private bool TryCreateReportRequest(
            ListOfValidAndInvalidCompanyRequest? request,
            out sp_PaThaKaValidInvalidReportRequest? procedureRequest,
            out ActionResult? errorResult)
        {
            procedureRequest = null;
            errorResult = null;

            if (request == null)
            {
                errorResult = BadRequest("Request body is required.");
                return false;
            }

            if (request.Date == default)
            {
                errorResult = BadRequest("Date is required.");
                return false;
            }
            procedureRequest = new sp_PaThaKaValidInvalidReportRequest
            {
                Date = request.Date,
                BusinessTypeId = request.BusinessTypeId,
                LineofBusinessId = request.LineofBusinessId,
                State = request.State,
                Status = request.Status,
                Type = request.Type,
            };

            return true;
        }
    }

    public sealed class ListOfValidAndInvalidCompanyRequest : ReportQueryRequest
    {
        public DateTime Date { get; set; }
        public int BusinessTypeId { get; set; }
        public int LineofBusinessId { get; set; }
        public string State { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}

