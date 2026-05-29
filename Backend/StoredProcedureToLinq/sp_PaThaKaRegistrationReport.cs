using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_PaThaKaRegistrationReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string ApplyType { get; set; } = string.Empty;
}

public sealed class sp_PaThaKaRegistrationReportResult
{
    public DateTime? Date { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string BusinessType { get; set; } = null!;
    public string? LineofBusiness { get; set; }
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string PaymentType { get; set; } = null!;
    public string? VoucherNo { get; set; }
    public DateTime? VoucherDate { get; set; }
    public double TotalAmount { get; set; }
}

public sealed class sp_PaThaKaRegistrationReportRow
{
    public DateTime? Date { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string BusinessType { get; set; } = null!;
    public string? LineofBusiness { get; set; }
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string PaymentType { get; set; } = null!;
    public string? VoucherNo { get; set; }
    public DateTime? VoucherDate { get; set; }
    public double TotalAmount { get; set; }
    public int TotalCount { get; set; }

    public sp_PaThaKaRegistrationReportResult ToResult() => new()
    {
        Date = Date,
        CompanyRegistrationNo = CompanyRegistrationNo,
        CompanyName = CompanyName,
        BusinessType = BusinessType,
        LineofBusiness = LineofBusiness,
        UnitLevel = UnitLevel,
        StreetNumberStreetName = StreetNumberStreetName,
        QuarterCityTownship = QuarterCityTownship,
        State = State,
        Country = Country,
        PostalCode = PostalCode,
        PaymentType = PaymentType,
        VoucherNo = VoucherNo,
        VoucherDate = VoucherDate,
        TotalAmount = TotalAmount,
    };
}

/// <summary>
/// Executes <c>dbo.sp_PaThaKaRegistrationReport_pagination</c> directly (NOT the
/// untouched original). See StoredProcedureMigrations/sp_PaThaKaRegistrationReport_pagination.sql.
/// </summary>
public static class sp_PaThaKaRegistrationReport
{
    public static async Task<List<sp_PaThaKaRegistrationReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_PaThaKaRegistrationReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new[]
        {
            new SqlParameter("@FromDate", request.FromDate),
            new SqlParameter("@ToDate", request.ToDate),
            new SqlParameter("@PaymentType", request.PaymentType ?? string.Empty),
            new SqlParameter("@ApplyType", request.ApplyType ?? string.Empty),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
        };

        const string sql =
            "EXEC dbo.sp_PaThaKaRegistrationReport_pagination @FromDate, @ToDate, @PaymentType, @ApplyType, " +
            "@SortColumn, @SortOrder, @PageIndex, @PageSize";

        return await db.Database
            .SqlQueryRaw<sp_PaThaKaRegistrationReportRow>(sql, parameters)
            .ToListAsync();
    }
}
