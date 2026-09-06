using API.DBContext;
using API.Model;
using API.Service.Reports;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

/// <summary>
/// The Border Import Permit Detail grid and its Excel export, served by
/// <c>dbo.sp_BorderImportPermitDetailReport_pagination</c>: the legacy Tradenet 2.0
/// <c>dbo.sp_ImportPermitDetailReport</c> ('Border' branch) kept verbatim -- same joins, same
/// filters, same <c>CreatedDate &lt;= @ToDate</c> window, same select list including
/// <c>dbo.fn_GetNRCNo</c> and the FOR XML CSV expanders -- with item-grain key paging around it.
/// The owner's instruction (2026-09-06) is a byte-identical result to the old report, so the
/// query is not corrected anywhere; only the paging and the deterministic order were added.
///
/// Stored procedures are deployed by hand while the application auto-deploys, so until the
/// procedure exists on a server both paths fall back to the LINQ twin
/// (<see cref="sp_ImportPermitDetailReport_Fast"/>), which pages in the same order and prints
/// the same <see cref="LegacyCompanyAddress"/>; the only thing it cannot reproduce exactly is
/// the server-side <c>fn_GetNRCNo</c>. Nothing else is caught -- a real SQL error is a 500.
/// </summary>
public static class sp_BorderImportPermitDetailReport
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 1000;

    /// <summary>SQL Server error 2812: "Could not find stored procedure".</summary>
    private const int ProcedureNotFound = 2812;

    public static async Task<ApiResult<sp_ImportPermitDetailReportResult>> CreatePagedResultAsync(
        TradeNetDbContext db,
        IMemoryCache cache,
        sp_ImportPermitDetailReportRequest request,
        ReportQueryRequest pagingRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var pageIndex = Math.Max(0, pagingRequest.PageIndex);
        var pageSize = NormalizePageSize(pagingRequest.PageSize);

        List<sp_BorderImportPermitDetailReportRow> rows;
        try
        {
            rows = await ExecuteAsync(db, request, pageIndex, pageSize, cancellationToken);

            // A page past the end carries no row, hence no TotalCount. Ask the first page for it
            // so the pager can still show the real total and step back to a page with rows.
            if (rows.Count == 0 && pageIndex > 0)
            {
                rows = await ExecuteAsync(db, request, pageIndex: 0, pageSize: 1, cancellationToken);
                var total = rows.Count == 0 ? 0 : rows[0].TotalCount;
                return ApiResult<sp_ImportPermitDetailReportResult>.CreatePageFromRows(
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
            return await sp_ImportPermitDetailReport_Fast.CreatePagedResultAsync(db, cache, request, pagingRequest);
        }

        var results = rows.Select(row => row.ToResult()).ToList();
        var totalCount = rows.Count == 0 ? 0 : rows[0].TotalCount;

        return ApiResult<sp_ImportPermitDetailReportResult>.CreatePageFromRows(
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
    /// Every row, in page order, for the Excel export: the procedure with <c>@PageSize = 0</c>
    /// (all rows) sliced into <paramref name="chunkSize"/> lists, or the LINQ stream where the
    /// procedure is not deployed. Same rows and the same order as the grid either way.
    /// </summary>
    public static async IAsyncEnumerable<List<sp_ImportPermitDetailReportResult>> StreamResolvedChunksAsync(
        TradeNetDbContext db,
        IMemoryCache cache,
        sp_ImportPermitDetailReportRequest request,
        int chunkSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(request);

        var size = Math.Max(1, chunkSize);
        var rows = await TryExecuteAllAsync(db, request, cancellationToken);

        if (rows == null)
        {
            await foreach (var chunk in sp_ImportPermitDetailReport_Fast.StreamResolvedChunksAsync(
                db, cache, request, size, cancellationToken))
            {
                yield return chunk;
            }

            yield break;
        }

        for (var offset = 0; offset < rows.Count; offset += size)
        {
            yield return rows
                .Skip(offset)
                .Take(size)
                .Select(row => row.ToResult())
                .ToList();
        }
    }

    /// <summary>
    /// Runs the procedure as-is. <paramref name="pageSize"/> &lt;= 0 returns every row (the parity
    /// check against the legacy procedure and the Excel export use this); every row carries the
    /// item-grain <c>TotalCount</c>. Throws <see cref="SqlException"/> 2812 where the procedure is
    /// not deployed.
    /// </summary>
    public static async Task<List<sp_BorderImportPermitDetailReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_ImportPermitDetailReportRequest request,
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
            new SqlParameter("@SellerCountryId", SqlDbType.Int) { Value = request.SellerCountryId },
            new SqlParameter("@CompanyRegistrationNo", SqlDbType.NVarChar, 50)
            {
                Value = request.CompanyRegistrationNo ?? string.Empty
            },
            new SqlParameter("@SakhanId", SqlDbType.Int) { Value = request.SakhanId },
            new SqlParameter("@PageIndex", SqlDbType.Int) { Value = Math.Max(0, pageIndex) },
            new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize },
            new SqlParameter("@IncludeTotalCount", SqlDbType.Bit) { Value = true },
        };

        const string sql =
            "EXEC dbo.sp_BorderImportPermitDetailReport_pagination "
            + "@FromDate, @ToDate, @PaThaKaTypeId, @ExportImportSectionId, @SellerCountryId, "
            + "@CompanyRegistrationNo, @SakhanId, @PageIndex, @PageSize, @IncludeTotalCount";

        return await db.Database
            .SqlQueryRaw<sp_BorderImportPermitDetailReportRow>(sql, parameters)
            .ToListAsync(cancellationToken);
    }

    /// <summary>All rows, or null where the procedure is not deployed (error 2812).</summary>
    private static async Task<List<sp_BorderImportPermitDetailReportRow>?> TryExecuteAllAsync(
        TradeNetDbContext db,
        sp_ImportPermitDetailReportRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteAsync(db, request, pageIndex: 0, pageSize: 0, cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == ProcedureNotFound)
        {
            return null;
        }
    }

    private static int NormalizePageSize(int pageSize)
    {
        return pageSize <= 0
            ? DefaultPageSize
            : Math.Min(pageSize, MaxPageSize);
    }
}

/// <summary>
/// One result-set row of <c>dbo.sp_BorderImportPermitDetailReport_pagination</c>: the legacy
/// select list of <c>dbo.sp_ImportPermitDetailReport</c> ('Border') plus <c>TotalCount</c>.
/// Property names equal the result-set column names (EF maps <c>SqlQueryRaw</c> by name).
/// </summary>
public sealed class sp_BorderImportPermitDetailReportRow
{
    public int PaThaKaTypeId { get; set; }
    public string PaThaKaTypeCode { get; set; } = string.Empty;
    public string PaThaKaTypeName { get; set; } = string.Empty;
    public int? SakhanId { get; set; }
    public string? SakhanCode { get; set; }
    public string? SakhanName { get; set; }
    public int ExportImportSectionId { get; set; }
    public int SellerCountryId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string LicenceNo { get; set; } = string.Empty;
    public DateTime? LicenceDate { get; set; }
    public string CompanyRegistrationNo { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? UnitLevel { get; set; }
    public string? StreetNumberStreetName { get; set; }
    public string? QuarterCityTownship { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? AuthorisedAgentName { get; set; }
    public string? AuthorisedAgentAddress { get; set; }
    public string? SellerCountry { get; set; }
    public string? PortofShipment { get; set; }
    public string? PortofDischarge { get; set; }
    public string? CountryofOrigin { get; set; }
    public DateTime? LastDate { get; set; }
    public string HSCode { get; set; } = string.Empty;
    public string? HSDescription { get; set; }
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? NRCNo { get; set; }
    public string? PermitType { get; set; }
    public string? Conditions { get; set; }
    public DateTime? ApproveDate { get; set; }
    public int TotalCount { get; set; }

    public sp_ImportPermitDetailReportResult ToResult()
    {
        return new sp_ImportPermitDetailReportResult
        {
            PaThaKaTypeId = PaThaKaTypeId,
            PaThaKaTypeCode = PaThaKaTypeCode,
            PaThaKaTypeName = PaThaKaTypeName,
            SakhanId = SakhanId,
            SakhanCode = SakhanCode,
            SakhanName = SakhanName,
            ExportImportSectionId = ExportImportSectionId,
            SellerCountryId = SellerCountryId,
            SectionCode = SectionCode,
            SectionName = SectionName,
            LicenceNo = LicenceNo,
            LicenceDate = LicenceDate,
            CompanyRegistrationNo = CompanyRegistrationNo,
            CompanyName = CompanyName,
            // The legacy model built this in C# from the six columns (CommonRepository.GetAddress);
            // same algorithm, same bytes.
            CompanyAddress = LegacyCompanyAddress.Compose(
                UnitLevel, StreetNumberStreetName, QuarterCityTownship, State, Country, PostalCode),
            UnitLevel = UnitLevel,
            StreetNumberStreetName = StreetNumberStreetName ?? string.Empty,
            QuarterCityTownship = QuarterCityTownship ?? string.Empty,
            State = State ?? string.Empty,
            Country = Country ?? string.Empty,
            PostalCode = PostalCode,
            AuthorisedAgentName = AuthorisedAgentName ?? string.Empty,
            AuthorisedAgentAddress = AuthorisedAgentAddress ?? string.Empty,
            SellerCountry = SellerCountry,
            // Legacy: row["PortofShipment"].ToString() -- a NULL expander result prints as "".
            PortofShipment = PortofShipment ?? string.Empty,
            PortofDischarge = PortofDischarge ?? string.Empty,
            CountryofOrigin = CountryofOrigin ?? string.Empty,
            LastDate = LastDate,
            HSCode = HSCode,
            HSDescription = HSDescription,
            Unit = Unit,
            Price = Price,
            Quantity = Quantity,
            Amount = Amount,
            Currency = Currency,
            NRCNo = NRCNo ?? string.Empty,
            PermitType = PermitType ?? string.Empty,
            Conditions = Conditions,
            ApproveDate = ApproveDate,
        };
    }
}
