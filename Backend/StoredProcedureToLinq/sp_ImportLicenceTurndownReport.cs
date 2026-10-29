using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using API.DBContext;
using API.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace API.StoredProcedureToLinq;

public sealed class ImportLicenceTurndownReportRequest : ReportQueryRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    [Range(0, int.MaxValue)]
    public int ExportImportSectionId { get; set; }
    [StringLength(50)]
    public string CompanyRegistrationNo { get; set; } = string.Empty;
    [StringLength(200)]
    public string CompanyName { get; set; } = string.Empty;
    [StringLength(20)]
    public string ApplyType { get; set; } = string.Empty;
}

public sealed class ImportLicenceTurndownReportRow
{
    public string Id { get; set; } = null!;
    public string? SectionCode { get; set; }
    public string Status { get; set; } = null!;
    public string ApplyType { get; set; } = null!;
    public string ApplicationNo { get; set; } = null!;
    public DateTime ApplicationDate { get; set; }
    public string? LicenceNo { get; set; }
    public DateTime? LicenceDate { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? CompanyName { get; set; }
    public string? CommodityType { get; set; }
    public string? HSCode { get; set; }
    public string? AdditionalDescription { get; set; }
    public string? Currency { get; set; }
    public decimal Amount { get; set; }
    public int TurndownCount { get; set; }
    public DateTime? TurndownDate { get; set; }
    public string? TurndownRemark { get; set; }
    public int? TotalCount { get; set; }
}

public sealed class ImportLicenceTurndownHistoryRow
{
    public string Id { get; set; } = null!;
    public DateTime? CreatedDate { get; set; }
    public string? Remark { get; set; }
}

public sealed class ImportLicenceTurndownApplyType
{
    public string Value { get; set; } = null!;
}

public static class sp_ImportLicenceTurndownReport
{
    public static IQueryable<ImportLicenceTurndownReportRow> Query(
        TradeNetDbContext db, string formType, ImportLicenceTurndownReportRequest request, bool export = false)
    {
        return db.Database.SqlQueryRaw<ImportLicenceTurndownReportRow>("""
            EXEC dbo.sp_ImportLicenceTurndownReport_pagination
                @FormType, @FromDate, @ToDate, @ExportImportSectionId, @CompanyRegistrationNo,
                @CompanyName, @ApplyType, @SortColumn, @SortOrder, @PageIndex, @PageSize,
                @IncludeTotalCount, @FilterColumn, @FilterQuery
            """,
            new SqlParameter("@FormType", formType),
            new SqlParameter("@FromDate", request.FromDate.Date),
            new SqlParameter("@ToDate", request.ToDate.Date),
            new SqlParameter("@ExportImportSectionId", request.ExportImportSectionId),
            new SqlParameter("@CompanyRegistrationNo", request.CompanyRegistrationNo?.Trim() ?? string.Empty),
            new SqlParameter("@CompanyName", request.CompanyName?.Trim() ?? string.Empty),
            new SqlParameter("@ApplyType", request.ApplyType ?? string.Empty),
            new SqlParameter("@SortColumn", (object?)request.SortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)request.SortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", Math.Max(0, request.PageIndex)),
            new SqlParameter("@PageSize", export ? DBNull.Value : (object)(request.PageSize <= 0 ? 10 : Math.Min(1000, request.PageSize))),
            new SqlParameter("@IncludeTotalCount", !export && request.IncludeTotalCount),
            new SqlParameter("@FilterColumn", (object?)request.FilterColumn ?? DBNull.Value),
            new SqlParameter("@FilterQuery", (object?)request.FilterQuery ?? DBNull.Value));
    }

    public static IQueryable<ImportLicenceTurndownHistoryRow> History(TradeNetDbContext db, string formType, string id)
        => db.Database.SqlQueryRaw<ImportLicenceTurndownHistoryRow>(
            "EXEC dbo.sp_ImportLicenceTurndownHistory @FormType, @TransactionId",
            new SqlParameter("@FormType", formType), new SqlParameter("@TransactionId", id));

    public static IQueryable<ImportLicenceTurndownApplyType> ApplyTypes(TradeNetDbContext db, string formType)
        => db.Database.SqlQueryRaw<ImportLicenceTurndownApplyType>(
            "EXEC dbo.sp_ImportLicenceTurndownApplyTypes @FormType", new SqlParameter("@FormType", formType));
}
