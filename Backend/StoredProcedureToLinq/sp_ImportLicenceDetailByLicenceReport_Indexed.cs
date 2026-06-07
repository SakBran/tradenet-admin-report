using API.DBContext;
using API.Model;
using API.Service.Reports;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

/// <summary>
/// Row DTO for <c>dbo.sp_ImportLicenceDetailByLicenceReport_Indexed</c>.
/// One row per licence + currency. <c>TotalCount</c> is non-null only when
/// <c>@IncludeTotalCount = 1</c>; it carries the exact COUNT from the proc.
/// </summary>
public sealed class sp_ImportLicenceDetailByLicenceReportIndexedRow
{
    public string? SectionName { get; set; }
    public string? LicenceNo { get; set; }
    public DateTime? LicenceDate { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? CompanyName { get; set; }
    public string? MethodName { get; set; }
    public string? SellerCountry { get; set; }
    public string? Currency { get; set; }
    public decimal? TotalValue { get; set; }
    public int? TotalCount { get; set; }

    public ReportLicenceListResult ToResult() => new()
    {
        SectionName           = SectionName,
        LicenceNo             = LicenceNo,
        LicenceDate           = LicenceDate,
        CompanyRegistrationNo = CompanyRegistrationNo,
        CompanyName           = CompanyName,
        MethodName            = MethodName,
        SellerCountry         = SellerCountry,
        Currency              = Currency,
        TotalValue            = TotalValue,
    };
}

/// <summary>
/// Caller for <c>dbo.sp_ImportLicenceDetailByLicenceReport_Indexed</c>.
/// Used by the licence-level drill list (one row per licence + currency) reached
/// from the By Section / Method / Seller Country / Company summaries.
/// </summary>
public static class sp_ImportLicenceDetailByLicenceReport_Indexed
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 1000;

    /// <summary>
    /// Returns a paged <see cref="ApiResult{T}"/> of <see cref="ReportLicenceListResult"/>.
    /// When <c>pagingRequest.IncludeTotalCount</c> is false, uses the fast-page sentinel
    /// (fetch pageSize+1) and <see cref="ApiResult{T}.CreateFastPageFromRows"/>; otherwise
    /// fetches the exact COUNT from the proc and uses <see cref="ApiResult{T}.CreatePageFromRows"/>.
    /// </summary>
    public static async Task<ApiResult<ReportLicenceListResult>> ExecutePagedAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request,
        string? currency,
        ReportQueryRequest pagingRequest)
    {
        var pageIndex = Math.Max(0, pagingRequest.PageIndex);
        var pageSize  = pagingRequest.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(pagingRequest.PageSize, MaxPageSize);
        var includeCount = pagingRequest.IncludeTotalCount;

        var rows = await ExecuteRawAsync(
            db, request, currency, pageIndex, pageSize, includeCount);

        // When IncludeTotalCount=1 the proc sets TotalCount on every row.
        var totalCount = includeCount ? rows.FirstOrDefault()?.TotalCount : null;

        var pageRows = includeCount
            ? rows.Select(r => r.ToResult()).ToList()
            : rows.Take(pageSize).Select(r => r.ToResult()).ToList();

        return totalCount.HasValue
            ? ApiResult<ReportLicenceListResult>.CreatePageFromRows(
                pageRows, totalCount.Value, pageIndex, pageSize, null, null,
                pagingRequest.FilterColumn, pagingRequest.FilterQuery)
            : ApiResult<ReportLicenceListResult>.CreateFastPageFromRows(
                rows.Select(r => r.ToResult()).ToList(), pageIndex, pageSize, null, null,
                pagingRequest.FilterColumn, pagingRequest.FilterQuery);
    }

    /// <summary>All rows (no paging) for the Excel export path.</summary>
    public static async Task<List<ReportLicenceListResult>> ExecuteAllAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request,
        string? currency)
    {
        var rows = await ExecuteRawAsync(db, request, currency, pageIndex: 0, pageSize: 0, includeCount: false);
        return rows.Select(r => r.ToResult()).ToList();
    }

    /// <summary>
    /// Currency-grouped footer (per-currency licence count + total value, grand total
    /// licence count). Returns null when no rows match the filters.
    /// </summary>
    public static async Task<ReportCurrencyTotalsSummary?> ExecuteCurrencyTotalsAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request,
        string? currency)
    {
        // The "TotalValue" dimension of the summary SP returns one row per currency —
        // the same aggregation the currency-totals footer needs.
        var rows = await sp_ImportLicenceSummaryReport_Indexed.ExecuteAsync(
            db, request, "TotalValue", currency ?? string.Empty);

        if (rows.Count == 0) return null;

        var currencies = rows
            .OrderByDescending(r => r.NoOfLicences)
            .ThenBy(r => r.Currency, StringComparer.OrdinalIgnoreCase)
            .Select(r => new ReportCurrencyTotal
            {
                Currency     = r.Currency ?? string.Empty,
                NoOfLicences = r.NoOfLicences,
                TotalValue   = r.TotalValue,
            })
            .ToList();

        return new ReportCurrencyTotalsSummary
        {
            Currencies          = currencies,
            GrandTotalLicences  = currencies.Sum(c => c.NoOfLicences),
        };
    }

    private static async Task<List<sp_ImportLicenceDetailByLicenceReportIndexedRow>> ExecuteRawAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request,
        string? currency,
        int pageIndex,
        int pageSize,
        bool includeCount)
    {
        var parameters = new SqlParameter[]
        {
            new("@FromDate",                request.FromDate),
            new("@ToDate",                  request.ToDate),
            new("@PaThaKaTypeId",           request.PaThaKaTypeId),
            new("@ExportImportSectionId",   request.ExportImportSectionId),
            new("@ExportImportMethodId",    request.ExportImportMethodId),
            new("@ExportImportIncotermId",  request.ExportImportIncotermId),
            new("@SellerCountryId",         request.SellerCountryId),
            new("@CompanyRegistrationNo",   request.CompanyRegistrationNo ?? string.Empty),
            new("@Currency",                currency ?? string.Empty),
            new("@PageIndex",               pageIndex),
            new("@PageSize",                pageSize),
            new("@IncludeTotalCount",       includeCount),
        };

        const string sql =
            "EXEC dbo.sp_ImportLicenceDetailByLicenceReport_Indexed " +
            "@FromDate, @ToDate, @PaThaKaTypeId, @ExportImportSectionId, @ExportImportMethodId, " +
            "@ExportImportIncotermId, @SellerCountryId, @CompanyRegistrationNo, @Currency, " +
            "@PageIndex, @PageSize, @IncludeTotalCount";

        return await db.Database
            .SqlQueryRaw<sp_ImportLicenceDetailByLicenceReportIndexedRow>(sql, parameters)
            .ToListAsync();
    }
}
