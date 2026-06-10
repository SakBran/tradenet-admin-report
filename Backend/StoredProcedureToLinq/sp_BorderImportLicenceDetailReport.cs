using API.DBContext;
using API.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public static class sp_BorderImportLicenceDetailReport
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 1000;

    public static async Task<ApiResult<sp_ImportLicenceDetailReportResult>> CreatePagedResultAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request,
        ReportQueryRequest pagingRequest)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var pageIndex = Math.Max(0, pagingRequest.PageIndex);
        var pageSize = pagingRequest.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(pagingRequest.PageSize, MaxPageSize);

        var rows = await ExecuteAsync(
            db,
            request,
            pagingRequest.SortColumn,
            pagingRequest.SortOrder,
            pageIndex,
            pageSize,
            pagingRequest.IncludeTotalCount);

        var results = rows.Select(row => row.ToResult()).ToList();

        if (pagingRequest.IncludeTotalCount)
        {
            var totalCount = rows.FirstOrDefault()?.TotalCount ?? 0;
            return ApiResult<sp_ImportLicenceDetailReportResult>.CreatePageFromRows(
                results,
                totalCount,
                pageIndex,
                pageSize,
                pagingRequest.SortColumn,
                pagingRequest.SortOrder,
                pagingRequest.FilterColumn,
                pagingRequest.FilterQuery);
        }

        return ApiResult<sp_ImportLicenceDetailReportResult>.CreateFastPageFromRows(
            results,
            pageIndex,
            pageSize,
            pagingRequest.SortColumn,
            pagingRequest.SortOrder,
            pagingRequest.FilterColumn,
            pagingRequest.FilterQuery);
    }

    public static Task<List<sp_ImportLicenceDetailReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null,
        bool includeTotalCount = true)
    {
        return ExecuteQueryable(db, request, sortColumn, sortOrder, pageIndex, pageSize, includeTotalCount)
            .ToListAsync();
    }

    public static IQueryable<sp_ImportLicenceDetailReportRow> ExecuteQueryable(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null,
        bool includeTotalCount = true)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new[]
        {
            new SqlParameter("@FromDate", request.FromDate),
            new SqlParameter("@ToDate", request.ToDate),
            new SqlParameter("@PaThaKaTypeId", request.PaThaKaTypeId),
            new SqlParameter("@ExportImportSectionId", request.ExportImportSectionId),
            new SqlParameter("@ExportImportMethodId", request.ExportImportMethodId),
            new SqlParameter("@ExportImportIncotermId", request.ExportImportIncotermId),
            new SqlParameter("@SellerCountryId", request.SellerCountryId),
            new SqlParameter("@CompanyRegistrationNo", request.CompanyRegistrationNo ?? string.Empty),
            new SqlParameter("@SakhanId", request.SakhanId),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
            new SqlParameter("@IncludeTotalCount", includeTotalCount),
        };

        const string sql =
            "EXEC dbo.sp_BorderImportLicenceDetailReport_pagination @FromDate, @ToDate, @PaThaKaTypeId, " +
            "@ExportImportSectionId, @ExportImportMethodId, @ExportImportIncotermId, @SellerCountryId, " +
            "@CompanyRegistrationNo, @SakhanId, @SortColumn, @SortOrder, @PageIndex, @PageSize, @IncludeTotalCount";

        return db.Database.SqlQueryRaw<sp_ImportLicenceDetailReportRow>(sql, parameters);
    }
}
