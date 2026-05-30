using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_ImportLicencePendingDetailReportRequest
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

public sealed class sp_ImportLicencePendingDetailReportResult
{
    public int PaThaKaTypeId { get; set; }
    public string PaThaKaTypeCode { get; set; } = null!;
    public string PaThaKaTypeName { get; set; } = null!;
    public int? SakhanId { get; set; }
    public string? SakhanCode { get; set; }
    public string? SakhanName { get; set; }
    public int ExportImportSectionId { get; set; }
    public int ExportImportMethodId { get; set; }
    public int ExportImportIncotermId { get; set; }
    public int SellerCountryId { get; set; }
    public string SectionCode { get; set; } = null!;
    public string SectionName { get; set; } = null!;
    public string LicenceNo { get; set; } = null!;
    public DateTime? LicenceDate { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string SellerName { get; set; } = null!;
    public string SellerAddress { get; set; } = null!;
    public string? SellerCountry { get; set; }
    public string PortofDischarge { get; set; } = null!;
    public DateTime? LastDate { get; set; }
    public string MethodName { get; set; } = null!;
    public string? ConsignedCountry { get; set; }
    public string? CountryofOrigin { get; set; }
    public string HSCode { get; set; } = null!;
    public string? HSDescription { get; set; }
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Conditions { get; set; }
    public string ApplicationNo { get; set; } = null!;
    public DateTime ApplicationDate { get; set; }
    public string? FESCNo { get; set; }
    public string? CommodityType { get; set; }
}

public sealed class sp_ImportLicencePendingDetailReportRow
{
    public int? PaThaKaTypeId { get; set; }
    public string? PaThaKaTypeCode { get; set; }
    public string? PaThaKaTypeName { get; set; }
    public int? ExportImportSectionId { get; set; }
    public int? ExportImportMethodId { get; set; }
    public int? ExportImportIncotermId { get; set; }
    public int? SellerCountryId { get; set; }
    public string? SectionCode { get; set; }
    public string? SectionName { get; set; }
    public string? LicenceNo { get; set; }
    public DateTime? LicenceDate { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? CompanyName { get; set; }
    public string? UnitLevel { get; set; }
    public string? StreetNumberStreetName { get; set; }
    public string? QuarterCityTownship { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? SellerName { get; set; }
    public string? SellerAddress { get; set; }
    public string? SellerCountry { get; set; }
    public string? PortofDischarge { get; set; }
    public DateTime? LastDate { get; set; }
    public string? MethodName { get; set; }
    public string? ConsignedCountry { get; set; }
    public string? CountryofOrigin { get; set; }
    public string? HSCode { get; set; }
    public string? HSDescription { get; set; }
    public string? Unit { get; set; }
    public decimal? Price { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? Conditions { get; set; }
    public string? ApplicationNo { get; set; }
    public DateTime? ApplicationDate { get; set; }
    public string? FESCNo { get; set; }
    public string? CommodityType { get; set; }
    public int? TotalCount { get; set; }

    public sp_ImportLicencePendingDetailReportResult ToResult() => new()
    {
        PaThaKaTypeId = PaThaKaTypeId ?? 0,
        PaThaKaTypeCode = PaThaKaTypeCode ?? string.Empty,
        PaThaKaTypeName = PaThaKaTypeName ?? string.Empty,
        ExportImportSectionId = ExportImportSectionId ?? 0,
        ExportImportMethodId = ExportImportMethodId ?? 0,
        ExportImportIncotermId = ExportImportIncotermId ?? 0,
        SellerCountryId = SellerCountryId ?? 0,
        SectionCode = SectionCode ?? string.Empty,
        SectionName = SectionName ?? string.Empty,
        LicenceNo = LicenceNo ?? string.Empty,
        LicenceDate = LicenceDate,
        CompanyRegistrationNo = CompanyRegistrationNo ?? string.Empty,
        CompanyName = CompanyName ?? string.Empty,
        UnitLevel = UnitLevel,
        StreetNumberStreetName = StreetNumberStreetName ?? string.Empty,
        QuarterCityTownship = QuarterCityTownship ?? string.Empty,
        State = State ?? string.Empty,
        Country = Country ?? string.Empty,
        PostalCode = PostalCode,
        SellerName = SellerName ?? string.Empty,
        SellerAddress = SellerAddress ?? string.Empty,
        SellerCountry = SellerCountry,
        PortofDischarge = PortofDischarge ?? string.Empty,
        LastDate = LastDate,
        MethodName = MethodName ?? string.Empty,
        ConsignedCountry = ConsignedCountry,
        CountryofOrigin = CountryofOrigin,
        HSCode = HSCode ?? string.Empty,
        HSDescription = HSDescription,
        Unit = Unit,
        Price = Price ?? 0m,
        Quantity = Quantity ?? 0m,
        Amount = Amount ?? 0m,
        Currency = Currency,
        Conditions = Conditions,
        ApplicationNo = ApplicationNo ?? string.Empty,
        ApplicationDate = ApplicationDate ?? default,
        FESCNo = FESCNo,
        CommodityType = CommodityType,
    };
}

public static class sp_ImportLicencePendingDetailReport
{
    /// <summary>
    /// Executes <c>dbo.sp_ImportLicencePendingDetailReport_pagination</c> (DB-side paging via
    /// INSERT-EXEC wrapper over the untouched original).
    /// </summary>
    public static async Task<List<sp_ImportLicencePendingDetailReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_ImportLicencePendingDetailReportRequest request,
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
            "EXEC dbo.sp_ImportLicencePendingDetailReport_pagination @Type, @FromDate, @ToDate, @PaThaKaTypeId, " +
            "@ExportImportSectionId, @ExportImportMethodId, @ExportImportIncotermId, @SellerCountryId, " +
            "@CompanyRegistrationNo, @SakhanId, @SortColumn, @SortOrder, @PageIndex, @PageSize, @IncludeTotalCount";

        return await db.Database
            .SqlQueryRaw<sp_ImportLicencePendingDetailReportRow>(sql, parameters)
            .ToListAsync();
    }
}
