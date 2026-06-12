using API.DBContext;
using API.Service.Reports;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_ExportLicenceSummaryReportRow
{
    public string? SectionName { get; set; }
    public int? SectionId { get; set; }
    public string? MethodName { get; set; }
    public int? MethodId { get; set; }
    public string? Country { get; set; }
    public int? CountryId { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? GroupDate { get; set; }
    public string? Currency { get; set; }
    public int NoOfLicences { get; set; }
    public decimal TotalValue { get; set; }

    public ReportAggregateResult ToAggregateResult(ReportAggregateDimension dimension) => new()
    {
        SectionName = dimension == ReportAggregateDimension.Section ? SectionName : null,
        SectionId = dimension == ReportAggregateDimension.Section ? SectionId : null,
        MethodName = dimension == ReportAggregateDimension.Method ? MethodName : null,
        MethodId = dimension == ReportAggregateDimension.Method ? MethodId : null,
        Country = dimension == ReportAggregateDimension.Country ? Country : null,
        CountryId = dimension == ReportAggregateDimension.Country ? CountryId : null,
        CompanyName = dimension == ReportAggregateDimension.Company ? CompanyName : null,
        CompanyRegistrationNo = dimension == ReportAggregateDimension.Company ? CompanyRegistrationNo : null,
        Date = dimension == ReportAggregateDimension.Daily ? GroupDate : null,
        Currency = Currency,
        NoOfLicences = NoOfLicences,
        TotalValue = TotalValue,
    };
}

public static class sp_ExportLicenceSummaryReport
{
    public static async Task<List<sp_ExportLicenceSummaryReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request,
        string dimension)
    {
        var parameters = new SqlParameter[]
        {
            new("@FromDate", request.FromDate),
            new("@ToDate", request.ToDate),
            new("@PaThaKaTypeId", request.PaThaKaTypeId),
            new("@ExportImportSectionId", request.ExportImportSectionId),
            new("@ExportImportMethodId", request.ExportImportMethodId),
            new("@ExportImportIncotermId", request.ExportImportIncotermId),
            new("@BuyerCountryId", request.BuyerCountryId),
            new("@CompanyRegistrationNo", request.CompanyRegistrationNo ?? string.Empty),
            new("@Dimension", dimension),
        };

        const string sql =
            "EXEC dbo.sp_ExportLicenceSummaryReport " +
            "@FromDate, @ToDate, @PaThaKaTypeId, @ExportImportSectionId, @ExportImportMethodId, " +
            "@ExportImportIncotermId, @BuyerCountryId, @CompanyRegistrationNo, @Dimension";

        return await db.Database
            .SqlQueryRaw<sp_ExportLicenceSummaryReportRow>(sql, parameters)
            .ToListAsync();
    }
}
