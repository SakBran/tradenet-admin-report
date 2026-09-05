using API.DBContext;
using API.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

/// <summary>
/// The oversea Export Licence Detail grid, served by <c>dbo.sp_ExportLicenceDetailReportV3_pagination</c>:
/// the legacy Tradenet 2.0 <c>dbo.sp_ExportLicenceDetailReport</c> ('Oversea' branch) kept verbatim,
/// with item-grain key-first paging wrapped around it. One row per <c>ExportLicenceItem</c>, exactly
/// the rows the old report prints, and <c>TotalCount</c> at the same grain -- the previous inline
/// path paged licences but counted items, so every page past licences/PageSize came back empty.
///
/// Stored procedures are deployed by hand while the application auto-deploys, so until the
/// procedure exists on a server the grid falls back to the item-grain LINQ path the Excel export
/// already uses (<see cref="sp_ExportLicenceDetailReport_Fast.CreatePagedResultAsync"/>): the same
/// rows, just slower. Nothing else is caught -- a real SQL error must still surface as a 500.
/// </summary>
public static class sp_ExportLicenceDetailReportV3
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 1000;

    /// <summary>SQL Server error 2812: "Could not find stored procedure".</summary>
    private const int ProcedureNotFound = 2812;

    public static async Task<ApiResult<sp_ExportLicenceDetailReportResult>> CreatePagedResultAsync(
        TradeNetDbContext db,
        IMemoryCache cache,
        sp_ExportLicenceDetailReportRequest request,
        ReportQueryRequest pagingRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var pageIndex = Math.Max(0, pagingRequest.PageIndex);
        var pageSize = NormalizePageSize(pagingRequest.PageSize);

        List<sp_ExportLicenceDetailReportRow> rows;
        try
        {
            rows = await ExecuteAsync(db, request, pageIndex, pageSize, cancellationToken);

            // A page past the end carries no row, hence no TotalCount. Ask the first page for it
            // so the pager can still show the real total and step back to a page with rows.
            if (rows.Count == 0 && pageIndex > 0)
            {
                rows = await ExecuteAsync(db, request, pageIndex: 0, pageSize: 1, cancellationToken);
                var total = rows.Count == 0 ? 0 : rows[0].TotalCount;
                return ApiResult<sp_ExportLicenceDetailReportResult>.CreatePageFromRows(
                    [],
                    total,
                    pageIndex,
                    pageSize,
                    pagingRequest.SortColumn,
                    pagingRequest.SortOrder,
                    pagingRequest.FilterColumn,
                    pagingRequest.FilterQuery);
            }
        }
        catch (SqlException ex) when (ex.Number == ProcedureNotFound)
        {
            return await sp_ExportLicenceDetailReport_Fast.CreatePagedResultAsync(db, cache, request, pagingRequest);
        }

        var results = rows.Select(row => row.ToResult()).ToList();
        var totalCount = rows.Count == 0 ? 0 : rows[0].TotalCount;

        return ApiResult<sp_ExportLicenceDetailReportResult>.CreatePageFromRows(
            results,
            totalCount,
            pageIndex,
            pageSize,
            pagingRequest.SortColumn,
            pagingRequest.SortOrder,
            pagingRequest.FilterColumn,
            pagingRequest.FilterQuery);
    }

    /// <summary>
    /// Runs the procedure as-is. <paramref name="pageSize"/> &lt;= 0 returns every row (the parity
    /// check against the legacy procedure uses this); every row carries the item-grain
    /// <c>TotalCount</c>. Throws <see cref="SqlException"/> 2812 where the procedure is not deployed.
    /// </summary>
    public static async Task<List<sp_ExportLicenceDetailReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new[]
        {
            new SqlParameter("@FromDate", SqlDbType.DateTime) { Value = request.FromDate },
            new SqlParameter("@ToDate", SqlDbType.DateTime) { Value = request.ToDate },
            new SqlParameter("@PaThaKaTypeId", SqlDbType.Int) { Value = request.PaThaKaTypeId },
            new SqlParameter("@ExportImportSectionId", SqlDbType.Int) { Value = request.ExportImportSectionId },
            new SqlParameter("@ExportImportMethodId", SqlDbType.Int) { Value = request.ExportImportMethodId },
            new SqlParameter("@ExportImportIncotermId", SqlDbType.Int) { Value = request.ExportImportIncotermId },
            new SqlParameter("@BuyerCountryId", SqlDbType.Int) { Value = request.BuyerCountryId },
            new SqlParameter("@CompanyRegistrationNo", SqlDbType.NVarChar, 50)
            {
                Value = request.CompanyRegistrationNo ?? string.Empty
            },
            new SqlParameter("@PageIndex", SqlDbType.Int) { Value = Math.Max(0, pageIndex) },
            new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize },
            new SqlParameter("@IncludeTotalCount", SqlDbType.Bit) { Value = true },
            new SqlParameter("@Auto", SqlDbType.NVarChar, 20) { Value = request.Auto ?? string.Empty },
        };

        const string sql =
            "EXEC dbo.sp_ExportLicenceDetailReportV3_pagination "
            + "@FromDate, @ToDate, @PaThaKaTypeId, @ExportImportSectionId, @ExportImportMethodId, "
            + "@ExportImportIncotermId, @BuyerCountryId, @CompanyRegistrationNo, "
            + "@PageIndex, @PageSize, @IncludeTotalCount, @Auto";

        return await db.Database
            .SqlQueryRaw<sp_ExportLicenceDetailReportRow>(sql, parameters)
            .ToListAsync(cancellationToken);
    }

    private static int NormalizePageSize(int pageSize)
    {
        return pageSize <= 0
            ? DefaultPageSize
            : Math.Min(pageSize, MaxPageSize);
    }
}
