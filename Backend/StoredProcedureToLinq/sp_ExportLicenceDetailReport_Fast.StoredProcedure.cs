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

public sealed class sp_ExportLicenceDetailReportRow
{
    public int PaThaKaTypeId { get; set; }
    public string PaThaKaTypeCode { get; set; } = string.Empty;
    public string PaThaKaTypeName { get; set; } = string.Empty;
    public int? SakhanId { get; set; }
    public string? SakhanCode { get; set; }
    public string? SakhanName { get; set; }
    public int ExportImportSectionId { get; set; }
    public int ExportImportMethodId { get; set; }
    public int ExportImportIncotermId { get; set; }
    public int BuyerCountryId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string LicenceNo { get; set; } = string.Empty;
    public DateTime? LicenceDate { get; set; }
    public string CompanyRegistrationNo { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? UnitLevel { get; set; }
    public string? StreetNumberStreetName { get; set; }
    public string? QuarterCityTownship { get; set; }
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerAddress { get; set; } = string.Empty;
    public string? BuyerCountry { get; set; }
    public string? PortofExport { get; set; }
    public string PortofDischarge { get; set; } = string.Empty;
    public DateTime? LastDate { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string? DestinationCountry { get; set; }
    public string? ConsignedCountry { get; set; }
    public string? CountryofOrigin { get; set; }
    public string HSCode { get; set; } = string.Empty;
    public string HSDescription { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Conditions { get; set; }
    public string? ApplicationNo { get; set; }
    public DateTime? ApplicationDate { get; set; }
    public string? CommodityType { get; set; }
    public DateTime? ApproveDate { get; set; }
    public int TotalCount { get; set; }

    public sp_ExportLicenceDetailReportResult ToResult()
    {
        return new sp_ExportLicenceDetailReportResult
        {
            PaThaKaTypeId = PaThaKaTypeId,
            PaThaKaTypeCode = PaThaKaTypeCode,
            PaThaKaTypeName = PaThaKaTypeName,
            SakhanId = SakhanId,
            SakhanCode = SakhanCode,
            SakhanName = SakhanName,
            ExportImportSectionId = ExportImportSectionId,
            ExportImportMethodId = ExportImportMethodId,
            ExportImportIncotermId = ExportImportIncotermId,
            BuyerCountryId = BuyerCountryId,
            SectionCode = SectionCode,
            SectionName = SectionName,
            LicenceNo = LicenceNo,
            LicenceDate = LicenceDate,
            CompanyRegistrationNo = CompanyRegistrationNo,
            CompanyName = CompanyName,
            UnitLevel = UnitLevel,
            StreetNumberStreetName = StreetNumberStreetName,
            QuarterCityTownship = QuarterCityTownship,
            State = State,
            Country = Country,
            PostalCode = PostalCode,
            BuyerName = BuyerName,
            BuyerAddress = BuyerAddress,
            BuyerCountry = BuyerCountry,
            PortofExport = PortofExport,
            PortofDischarge = PortofDischarge,
            LastDate = LastDate,
            MethodName = MethodName,
            DestinationCountry = DestinationCountry,
            ConsignedCountry = ConsignedCountry,
            CountryofOrigin = CountryofOrigin,
            HSCode = HSCode,
            HSDescription = HSDescription,
            Unit = Unit,
            Price = Price,
            Quantity = Quantity,
            Amount = Amount,
            Currency = Currency,
            Conditions = Conditions,
            ApplicationNo = ApplicationNo,
            ApplicationDate = ApplicationDate,
            CommodityType = CommodityType,
            ApproveDate = ApproveDate,
        };
    }
}

public sealed class sp_ExportLicenceTotalValueReportRow
{
    public string? Currency { get; set; }
    public int NoOfLicences { get; set; }
    public decimal TotalValue { get; set; }
    public int? TotalCount { get; set; }

    public ReportAggregateResult ToResult()
    {
        return new ReportAggregateResult
        {
            Currency = Currency,
            NoOfLicences = NoOfLicences,
            TotalValue = TotalValue,
            TotalUSDValue = null,
        };
    }
}

public static partial class sp_ExportLicenceDetailReport_Fast
{
    private const int StoredProcedureDefaultPageSize = 10;
    private const int StoredProcedureMaxPageSize = 1000;

    public static async Task<List<sp_ExportLicenceDetailReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request,
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
            new SqlParameter("@Type", request.Type ?? string.Empty),
            new SqlParameter("@FromDate", request.FromDate),
            new SqlParameter("@ToDate", request.ToDate),
            new SqlParameter("@PaThaKaTypeId", request.PaThaKaTypeId),
            new SqlParameter("@ExportImportSectionId", request.ExportImportSectionId),
            new SqlParameter("@ExportImportMethodId", request.ExportImportMethodId),
            new SqlParameter("@ExportImportIncotermId", request.ExportImportIncotermId),
            new SqlParameter("@BuyerCountryId", request.BuyerCountryId),
            new SqlParameter("@CompanyRegistrationNo", request.CompanyRegistrationNo ?? string.Empty),
            new SqlParameter("@SakhanId", request.SakhanId),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
            new SqlParameter("@IncludeTotalCount", includeTotalCount),
        };

        const string sql =
            "EXEC dbo.sp_ExportLicenceDetailReport_Pagination "
            + "@Type, @FromDate, @ToDate, @PaThaKaTypeId, @ExportImportSectionId, @ExportImportMethodId, "
            + "@ExportImportIncotermId, @BuyerCountryId, @CompanyRegistrationNo, @SakhanId, "
            + "@SortColumn, @SortOrder, @PageIndex, @PageSize, @IncludeTotalCount";

        return await db.Database.SqlQueryRaw<sp_ExportLicenceDetailReportRow>(sql, parameters).ToListAsync();
    }

    public static IQueryable<sp_ExportLicenceDetailReportRow> ExecuteQueryable(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request,
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
            new SqlParameter("@Type", request.Type ?? string.Empty),
            new SqlParameter("@FromDate", request.FromDate),
            new SqlParameter("@ToDate", request.ToDate),
            new SqlParameter("@PaThaKaTypeId", request.PaThaKaTypeId),
            new SqlParameter("@ExportImportSectionId", request.ExportImportSectionId),
            new SqlParameter("@ExportImportMethodId", request.ExportImportMethodId),
            new SqlParameter("@ExportImportIncotermId", request.ExportImportIncotermId),
            new SqlParameter("@BuyerCountryId", request.BuyerCountryId),
            new SqlParameter("@CompanyRegistrationNo", request.CompanyRegistrationNo ?? string.Empty),
            new SqlParameter("@SakhanId", request.SakhanId),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
            new SqlParameter("@IncludeTotalCount", includeTotalCount),
        };

        const string sql =
            "EXEC dbo.sp_ExportLicenceDetailReport_Pagination "
            + "@Type, @FromDate, @ToDate, @PaThaKaTypeId, @ExportImportSectionId, @ExportImportMethodId, "
            + "@ExportImportIncotermId, @BuyerCountryId, @CompanyRegistrationNo, @SakhanId, "
            + "@SortColumn, @SortOrder, @PageIndex, @PageSize, @IncludeTotalCount";

        return db.Database.SqlQueryRaw<sp_ExportLicenceDetailReportRow>(sql, parameters);
    }

    public static async Task<ApiResult<ReportAggregateResult>> CreateTotalValueAggregateResultAsync(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request,
        ReportQueryRequest pagingRequest)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var pageIndex = Math.Max(0, pagingRequest.PageIndex);
        var pageSize = pagingRequest.PageSize <= 0
            ? StoredProcedureDefaultPageSize
            : Math.Min(pagingRequest.PageSize, StoredProcedureMaxPageSize);

        var rows = await ExecuteTotalValueAggregateAsync(
            db,
            request,
            pagingRequest.SortColumn,
            pagingRequest.SortOrder,
            pageIndex,
            pagingRequest.IncludeTotalCount ? pageSize : pageSize + 1,
            pagingRequest.IncludeTotalCount);

        var results = rows.Select(row => row.ToResult()).ToList();

        if (pagingRequest.IncludeTotalCount)
        {
            var totalCount = rows.Count == 0 ? 0 : rows[0].TotalCount ?? 0;
            return ApiResult<ReportAggregateResult>.CreatePageFromRows(
                results,
                totalCount,
                pageIndex,
                pageSize,
                null,
                null,
                pagingRequest.FilterColumn,
                pagingRequest.FilterQuery);
        }

        return ApiResult<ReportAggregateResult>.CreateFastPageFromRows(
            results,
            pageIndex,
            pageSize,
            null,
            null,
            pagingRequest.FilterColumn,
            pagingRequest.FilterQuery);
    }

    public static async Task<List<ReportAggregateResult>> GetTotalValueAggregateRowsAsync(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var rows = await ExecuteTotalValueAggregateAsync(
            db,
            request,
            sortColumn: null,
            sortOrder: null,
            pageIndex: null,
            pageSize: null,
            includeTotalCount: false);

        return rows.Select(row => row.ToResult()).ToList();
    }

    private static async Task<List<sp_ExportLicenceTotalValueReportRow>> ExecuteTotalValueAggregateAsync(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null,
        bool includeTotalCount = true)
    {
        var parameters = new[]
        {
            new SqlParameter("@Type", request.Type ?? string.Empty),
            new SqlParameter("@FromDate", request.FromDate),
            new SqlParameter("@ToDate", request.ToDate),
            new SqlParameter("@PaThaKaTypeId", request.PaThaKaTypeId),
            new SqlParameter("@ExportImportSectionId", request.ExportImportSectionId),
            new SqlParameter("@ExportImportMethodId", request.ExportImportMethodId),
            new SqlParameter("@ExportImportIncotermId", request.ExportImportIncotermId),
            new SqlParameter("@BuyerCountryId", request.BuyerCountryId),
            new SqlParameter("@CompanyRegistrationNo", request.CompanyRegistrationNo ?? string.Empty),
            new SqlParameter("@SakhanId", request.SakhanId),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
            new SqlParameter("@IncludeTotalCount", includeTotalCount),
        };

        const string sql =
            "EXEC dbo.sp_ExportLicenceTotalValueReport_Fast_pagination "
            + "@Type, @FromDate, @ToDate, @PaThaKaTypeId, @ExportImportSectionId, @ExportImportMethodId, "
            + "@ExportImportIncotermId, @BuyerCountryId, @CompanyRegistrationNo, @SakhanId, "
            + "@SortColumn, @SortOrder, @PageIndex, @PageSize, @IncludeTotalCount";

        return await db.Database.SqlQueryRaw<sp_ExportLicenceTotalValueReportRow>(sql, parameters).ToListAsync();
    }
}
