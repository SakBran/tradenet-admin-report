using API.DBContext;
using API.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public string HSCode { get; set; } = string.Empty;
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

public sealed class ExportPermitDetailCurrencyTotalRow
{
    public string? Currency { get; set; }
    public int NoOfLicences { get; set; }
    public decimal TotalValue { get; set; }
}

public static class sp_ExportPermitDetailReport
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 1000;

    public static async Task<ApiResult<sp_ExportPermitDetailReportResult>> CreatePagedResultAsync(
        TradeNetDbContext db,
        sp_ExportPermitDetailReportRequest request,
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
            pageIndex,
            pageSize,
            pagingRequest.IncludeTotalCount,
            pagingRequest.SortColumn,
            pagingRequest.SortOrder);

        var data = rows.Cast<sp_ExportPermitDetailReportResult>().ToList();

        if (pagingRequest.IncludeTotalCount)
        {
            return ApiResult<sp_ExportPermitDetailReportResult>.CreatePageFromRows(
                data,
                rows.Count > 0 ? rows[0].TotalCount : 0,
                pageIndex,
                pageSize,
                pagingRequest.SortColumn,
                pagingRequest.SortOrder,
                pagingRequest.FilterColumn,
                pagingRequest.FilterQuery);
        }

        return ApiResult<sp_ExportPermitDetailReportResult>.CreateFastPageFromRows(
            data,
            pageIndex,
            pageSize,
            pagingRequest.SortColumn,
            pagingRequest.SortOrder,
            pagingRequest.FilterColumn,
            pagingRequest.FilterQuery);
    }

    public static async Task<List<sp_ExportPermitDetailReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_ExportPermitDetailReportRequest request,
        int pageIndex,
        int pageSize,
        bool includeTotalCount,
        string? sortColumn = null,
        string? sortOrder = null)
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
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", pageIndex),
            new SqlParameter("@PageSize", pageSize),
            new SqlParameter("@IncludeTotalCount", includeTotalCount),
        };

        return await db.Database.SqlQueryRaw<sp_ExportPermitDetailReportRow>(sql, parameters)
            .ToListAsync();
    }

    public static async Task<ReportCurrencyTotalsSummary?> CreateBorderCurrencyTotalsAsync(
        TradeNetDbContext db,
        sp_ExportPermitDetailReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.Type, "Border", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        const string sql = """
            SELECT
                currency.Code AS Currency,
                COUNT(DISTINCT BorderExportPermit.ExportPermitNo) AS NoOfLicences,
                SUM(BorderExportPermitItem.Amount) AS TotalValue
            FROM BorderExportPermit
            INNER JOIN PaThaKa ON PaThaKa.Id = BorderExportPermit.PaThaKaId
            INNER JOIN PaThaKaType paThaKaType ON PaThaKa.PaThaKaTypeId = paThaKaType.Id
            INNER JOIN BorderExportPermitItem ON BorderExportPermit.Id = BorderExportPermitItem.BorderExportPermitId
            INNER JOIN Currency currency ON BorderExportPermitItem.CurrencyId = currency.Id
            WHERE BorderExportPermit.ApplyType = 'New'
              AND BorderExportPermit.Status = 'Approved'
              AND ((@FromDate IS NULL) OR BorderExportPermit.CreatedDate >= @FromDate)
              AND ((@ToDate IS NULL) OR BorderExportPermit.CreatedDate < DATEADD(day, 1, @ToDate))
              AND (@CompanyRegistrationNo = '' OR PaThaKa.CompanyRegistrationNo = @CompanyRegistrationNo)
              AND (@PaThaKaTypeId = 0 OR PaThaKa.PaThaKaTypeId = @PaThaKaTypeId)
              AND (@ExportImportSectionId = 0 OR BorderExportPermit.ExportImportSectionId = @ExportImportSectionId)
              AND (@BuyerCountryId = 0 OR BorderExportPermit.BuyerCountryId = @BuyerCountryId)
              AND (@SakhanId = 0 OR BorderExportPermit.SakhanId = @SakhanId)
            GROUP BY currency.Code
            """;

        var parameters = new[]
        {
            new SqlParameter("@FromDate", request.FromDate),
            new SqlParameter("@ToDate", request.ToDate),
            new SqlParameter("@PaThaKaTypeId", request.PaThaKaTypeId),
            new SqlParameter("@ExportImportSectionId", request.ExportImportSectionId),
            new SqlParameter("@BuyerCountryId", request.BuyerCountryId),
            new SqlParameter("@CompanyRegistrationNo", request.CompanyRegistrationNo ?? string.Empty),
            new SqlParameter("@SakhanId", request.SakhanId),
        };

        var rows = await db.Database
            .SqlQueryRaw<ExportPermitDetailCurrencyTotalRow>(sql, parameters)
            .ToListAsync();

        if (rows.Count == 0)
        {
            return null;
        }

        var currencies = rows
            .OrderByDescending(row => row.NoOfLicences)
            .ThenBy(row => row.Currency, StringComparer.OrdinalIgnoreCase)
            .Select(row => new ReportCurrencyTotal
            {
                Currency = row.Currency ?? string.Empty,
                NoOfLicences = row.NoOfLicences,
                TotalValue = row.TotalValue,
            })
            .ToList();

        return new ReportCurrencyTotalsSummary
        {
            Currencies = currencies,
            GrandTotalLicences = currencies.Sum(currency => currency.NoOfLicences),
        };
    }
}
