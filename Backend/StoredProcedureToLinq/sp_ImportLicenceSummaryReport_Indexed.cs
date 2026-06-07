using API.DBContext;
using API.Service.Reports;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

/// <summary>
/// Row DTO for <c>dbo.sp_ImportLicenceSummaryReport_Indexed</c>. All dimension columns are
/// nullable because only the columns relevant to the requested <c>@Dimension</c> are non-null;
/// the rest are emitted as typed NULLs by the proc.
/// </summary>
public sealed class sp_ImportLicenceSummaryReportIndexedRow
{
    public string? SectionName { get; set; }
    public int? SectionId { get; set; }
    public string? MethodName { get; set; }
    public int? MethodId { get; set; }
    public string? Country { get; set; }
    public int? CountryId { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? PaThaKaType { get; set; }
    public string? GroupDate { get; set; }
    public string? Currency { get; set; }
    public int NoOfLicences { get; set; }
    public decimal TotalValue { get; set; }

    /// <summary>
    /// Maps this SP row to <see cref="ReportAggregateResult"/> for the standard aggregate
    /// report consumers (Section / Method / Country / Company / Daily / TotalValue).
    /// </summary>
    public ReportAggregateResult ToAggregateResult(ReportAggregateDimension dimension) => new()
    {
        SectionName   = dimension == ReportAggregateDimension.Section  ? SectionName  : null,
        SectionId     = dimension == ReportAggregateDimension.Section  ? SectionId    : null,
        MethodName    = dimension == ReportAggregateDimension.Method   ? MethodName   : null,
        MethodId      = dimension == ReportAggregateDimension.Method   ? MethodId     : null,
        Country       = dimension == ReportAggregateDimension.Country  ? Country      : null,
        CountryId     = dimension == ReportAggregateDimension.Country  ? CountryId    : null,
        CompanyName   = dimension == ReportAggregateDimension.Company  ? CompanyName  : null,
        CompanyRegistrationNo = dimension == ReportAggregateDimension.Company ? CompanyRegistrationNo : null,
        Date          = dimension == ReportAggregateDimension.Daily    ? GroupDate    : null,
        Currency      = Currency,
        NoOfLicences  = NoOfLicences,
        TotalValue    = TotalValue,
    };
}

/// <summary>
/// Caller for <c>dbo.sp_ImportLicenceSummaryReport_Indexed</c>. Uses
/// <c>WITH (NOEXPAND)</c> on <c>vw_ImportLicenceItemTotalByCurrency</c> inside the proc,
/// which SQL Server cannot emit from EF LINQ — that is why referencing the view through
/// EF still timed out (185s+) even after adding the view join.
/// </summary>
public static class sp_ImportLicenceSummaryReport_Indexed
{
    /// <summary>
    /// Executes the proc for the given <paramref name="dimension"/> string
    /// ("Section" | "Method" | "Country" | "Company" | "Daily" | "TotalValue" | "PaThaKaType").
    /// An optional <paramref name="currency"/> restricts to a single currency.
    /// </summary>
    public static async Task<List<sp_ImportLicenceSummaryReportIndexedRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_ImportLicenceDetailReportRequest request,
        string dimension,
        string currency = "")
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
            new("@Currency",                currency),
            new("@Dimension",               dimension),
        };

        const string sql =
            "EXEC dbo.sp_ImportLicenceSummaryReport_Indexed " +
            "@FromDate, @ToDate, @PaThaKaTypeId, @ExportImportSectionId, @ExportImportMethodId, " +
            "@ExportImportIncotermId, @SellerCountryId, @CompanyRegistrationNo, @Currency, @Dimension";

        return await db.Database
            .SqlQueryRaw<sp_ImportLicenceSummaryReportIndexedRow>(sql, parameters)
            .ToListAsync();
    }
}
