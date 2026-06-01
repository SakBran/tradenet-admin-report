using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_CompanyProfileReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string CompanyRegistrationNo { get; set; } = string.Empty;
}

public sealed class sp_CompanyProfileReportResult
{
    public string Id { get; set; } = null!;
    public string CompanyRegistrationNo { get; set; } = null!;
    public DateTime EndDate { get; set; }
    public string CompanyName { get; set; } = null!;
    public DateTime CompanyRegistrationDate { get; set; }
    public string BusinessType { get; set; } = null!;
    public string? LineofBusiness { get; set; }
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public double? Capital { get; set; }
    public string? DirectorName { get; set; }
    public string? DirectorNrc { get; set; }
    public string? DirectorPosition { get; set; }
    public string PermitBusiness { get; set; } = string.Empty;
    public int ExtensionCount { get; set; }
}

public sealed class sp_CompanyProfileReportRow
{
    public string Id { get; set; } = null!;
    public string CompanyRegistrationNo { get; set; } = null!;
    public DateTime EndDate { get; set; }
    public string CompanyName { get; set; } = null!;
    public DateTime CompanyRegistrationDate { get; set; }
    public string BusinessType { get; set; } = null!;
    public string? LineofBusiness { get; set; }
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public double? Capital { get; set; }
    public string? DirectorName { get; set; }
    public string? DirectorNRC { get; set; }
    public string? DirectorPosition { get; set; }
    public string PermitBusiness { get; set; } = string.Empty;
    public int ExtensionCount { get; set; }
    public int TotalCount { get; set; }

    public sp_CompanyProfileReportResult ToResult() => new()
    {
        Id = Id,
        CompanyRegistrationNo = CompanyRegistrationNo,
        EndDate = EndDate,
        CompanyName = CompanyName,
        CompanyRegistrationDate = CompanyRegistrationDate,
        BusinessType = BusinessType,
        LineofBusiness = LineofBusiness,
        UnitLevel = UnitLevel,
        StreetNumberStreetName = StreetNumberStreetName,
        QuarterCityTownship = QuarterCityTownship,
        State = State,
        Country = Country,
        PostalCode = PostalCode,
        Capital = Capital,
        DirectorName = DirectorName,
        DirectorNrc = DirectorNRC,
        DirectorPosition = DirectorPosition,
        PermitBusiness = PermitBusiness,
        ExtensionCount = ExtensionCount,
    };
}

/// <summary>
/// Executes <c>dbo.sp_CompanyProfileReport_pagination</c> directly (NOT the
/// untouched original). See StoredProcedureMigrations/sp_CompanyProfileReport_pagination.sql.
/// </summary>
public static class sp_CompanyProfileReport
{
    public static async Task<List<sp_CompanyProfileReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_CompanyProfileReportRequest request,
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
            new SqlParameter("@CompanyRegistrationNo", request.CompanyRegistrationNo ?? string.Empty),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
        };

        const string sql =
            "EXEC dbo.sp_CompanyProfileReport_pagination @FromDate, @ToDate, @CompanyRegistrationNo, " +
            "@SortColumn, @SortOrder, @PageIndex, @PageSize";

        return await db.Database
            .SqlQueryRaw<sp_CompanyProfileReportRow>(sql, parameters)
            .ToListAsync();
    }
}
