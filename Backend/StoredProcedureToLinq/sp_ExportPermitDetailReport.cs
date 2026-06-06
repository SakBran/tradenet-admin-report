using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_ExportPermitDetailReportRequest
{
    public string Type { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int PaThaKaTypeId { get; set; }
    public int ExportImportSectionId { get; set; }
    public int BuyerCountryId { get; set; }
    public string CompanyRegistrationNo { get; set; } = string.Empty;
    public int SakhanId { get; set; }
}

public class sp_ExportPermitDetailReportResult
{
    public int PaThaKaTypeId { get; set; }
    public string PaThaKaTypeCode { get; set; } = null!;
    public string PaThaKaTypeName { get; set; } = null!;
    public int? SakhanId { get; set; }
    public string? SakhanCode { get; set; }
    public string? SakhanName { get; set; }
    public int ExportImportSectionId { get; set; }
    public int BuyerCountryId { get; set; }
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
    public string ConsigneeName { get; set; } = null!;
    public string ConsigneeAddress { get; set; } = null!;
    public string? BuyerCountry { get; set; }
    public string PortofExport { get; set; } = null!;
    public string PortofDischarge { get; set; } = null!;
    public string DestinationCountry { get; set; } = null!;
    public DateTime? LastDate { get; set; }
    public string? ConsignedCountry { get; set; }
    public string CountryofOrigin { get; set; } = null!;
    public string HSCode { get; set; } = null!;
    public string HSDescription { get; set; } = null!;
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string NRCNo { get; set; } = null!;
    public string PermitType { get; set; } = null!;
    public string? Conditions { get; set; }
    public DateTime? ApproveDate { get; set; }
}

public sealed class sp_ExportPermitDetailReportRow : sp_ExportPermitDetailReportResult
{
    public int TotalCount { get; set; }
}

public static class sp_ExportPermitDetailReport
{
    public static async Task<List<sp_ExportPermitDetailReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_ExportPermitDetailReportRequest request,
        int pageIndex,
        int pageSize,
        bool includeTotalCount)
    {
        var sql = "EXEC dbo.sp_ExportPermitDetailReport_Fast_pagination "
            + "@Type, @FromDate, @ToDate, @PaThaKaTypeId, @ExportImportSectionId, @BuyerCountryId, @CompanyRegistrationNo, @SakhanId, "
            + "@SortColumn, @SortOrder, @PageIndex, @PageSize, @IncludeTotalCount";

        var parameters = new[]
        {
            new SqlParameter("@Type", request.Type),
            new SqlParameter("@FromDate", request.FromDate),
            new SqlParameter("@ToDate", request.ToDate),
            new SqlParameter("@PaThaKaTypeId", request.PaThaKaTypeId),
            new SqlParameter("@ExportImportSectionId", request.ExportImportSectionId),
            new SqlParameter("@BuyerCountryId", request.BuyerCountryId),
            new SqlParameter("@CompanyRegistrationNo", request.CompanyRegistrationNo),
            new SqlParameter("@SakhanId", request.SakhanId),
            new SqlParameter("@SortColumn", DBNull.Value),
            new SqlParameter("@SortOrder", DBNull.Value),
            new SqlParameter("@PageIndex", pageIndex),
            new SqlParameter("@PageSize", pageSize),
            new SqlParameter("@IncludeTotalCount", includeTotalCount),
        };

        return await db.Database.SqlQueryRaw<sp_ExportPermitDetailReportRow>(sql, parameters)
            .ToListAsync();
    }
}
