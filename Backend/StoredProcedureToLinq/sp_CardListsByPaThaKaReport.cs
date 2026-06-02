using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_CardListsByPaThaKaReportRequest
{
    public string CompanyRegistrationNo { get; set; } = string.Empty;
}

public sealed class sp_CardListsByPaThaKaReportResult
{
    public string? MicpermitNo { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public DateTime CompanyRegistrationDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BusinessType { get; set; } = null!;
    public string? LineofBusiness { get; set; }
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public double? Capital { get; set; }
}

public sealed class sp_CardListsByPaThaKaReportRow
{
    public string? MicpermitNo { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public DateTime CompanyRegistrationDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BusinessType { get; set; } = null!;
    public string? LineofBusiness { get; set; }
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public double? Capital { get; set; }
    public int TotalCount { get; set; }

    public sp_CardListsByPaThaKaReportResult ToResult() => new()
    {
        MicpermitNo = MicpermitNo,
        CompanyRegistrationNo = CompanyRegistrationNo,
        CompanyName = CompanyName,
        CompanyRegistrationDate = CompanyRegistrationDate,
        EndDate = EndDate,
        BusinessType = BusinessType,
        LineofBusiness = LineofBusiness,
        UnitLevel = UnitLevel,
        StreetNumberStreetName = StreetNumberStreetName,
        QuarterCityTownship = QuarterCityTownship,
        State = State,
        Country = Country,
        PostalCode = PostalCode,
        Capital = Capital,
    };
}

/// <summary>
/// Executes <c>dbo.sp_CardListsByPaThaKaReport_pagination</c> directly (NOT the
/// untouched original). See StoredProcedureMigrations/sp_CardListsByPaThaKaReport_pagination.sql.
/// </summary>
public static class sp_CardListsByPaThaKaReport
{
    public static async Task<List<sp_CardListsByPaThaKaReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_CardListsByPaThaKaReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null)
    {
        return await ExecuteQueryable(db, request, sortColumn, sortOrder, pageIndex, pageSize)
            .ToListAsync();
    }

    public static IQueryable<sp_CardListsByPaThaKaReportRow> ExecuteQueryable(
        TradeNetDbContext db,
        sp_CardListsByPaThaKaReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new[]
        {
            new SqlParameter("@CompanyRegistrationNo", request.CompanyRegistrationNo ?? string.Empty),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
        };

        const string sql =
            "EXEC dbo.sp_CardListsByPaThaKaReport_pagination @CompanyRegistrationNo, " +
            "@SortColumn, @SortOrder, @PageIndex, @PageSize";

        return db.Database.SqlQueryRaw<sp_CardListsByPaThaKaReportRow>(sql, parameters);
    }
}
