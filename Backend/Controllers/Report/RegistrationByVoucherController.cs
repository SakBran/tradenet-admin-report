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
    public class RegistrationByVoucherController : ControllerBase
    {
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        // Excel worksheets allow 1,048,576 rows including the header.
        private const int MaxExcelDataRows = 1_048_576 - 1;

        private readonly TradeNetDbContext _context;

        public RegistrationByVoucherController(TradeNetDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<sp_PaThaKaRegistrationReportResult>>> Post([FromBody] RegistrationByVoucherRequest? request)
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

            var rows = await sp_PaThaKaRegistrationReport.ExecuteAsync(
                _context, procedureRequest!, sortColumn, sortOrder, pageIndex, pageSize);

            var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;
            var data = rows.Select(row => row.ToResult()).ToList();

            var result = ApiResult<sp_PaThaKaRegistrationReportResult>.CreatePageFromRows(
                data, totalCount, pageIndex, pageSize,
                request.SortColumn, request.SortOrder, request.FilterColumn, request.FilterQuery);

            return Ok(result);
        }

        [HttpPost("Excel")]
        public async Task<IActionResult> Excel([FromBody] RegistrationByVoucherRequest? request)
        {
            if (!TryCreateReportRequest(request, out var procedureRequest, out var errorResult))
            {
                return errorResult!;
            }

            var sortColumn = string.IsNullOrWhiteSpace(request!.SortColumn) ? null : request.SortColumn;
            var sortOrder = string.IsNullOrWhiteSpace(request.SortOrder) ? null : request.SortOrder;

            var rows = await sp_PaThaKaRegistrationReport.ExecuteAsync(
                _context, procedureRequest!, sortColumn, sortOrder, pageIndex: null, pageSize: null);

            if (rows.Count > MaxExcelDataRows)
            {
                return BadRequest($"Excel export supports up to {MaxExcelDataRows} data rows.");
            }

            var data = rows.Select(row => row.ToResult()).ToList();
            var fileBytes = ExcelGenerator.CreateWorkbook(data, "Registration By Voucher");

            return File(fileBytes, ExcelGenerator.ContentType, "RegistrationByVoucher.xlsx");
        }

        private bool TryCreateReportRequest(
            RegistrationByVoucherRequest? request,
            out sp_PaThaKaRegistrationReportRequest? procedureRequest,
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
            procedureRequest = new sp_PaThaKaRegistrationReportRequest
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                PaymentType = request.PaymentType,
                ApplyType = request.ApplyType,
            };

            return true;
        }
    }

    public sealed class RegistrationByVoucherRequest : ReportQueryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string PaymentType { get; set; } = string.Empty;
        public string ApplyType { get; set; } = string.Empty;
    }
}
