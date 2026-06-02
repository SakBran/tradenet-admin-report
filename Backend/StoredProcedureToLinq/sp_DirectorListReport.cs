using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_DirectorListReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string CompanyRegistrationNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string NRCType { get; set; } = string.Empty;
    public int NRCPrefixId { get; set; }
    public int NRCPrefixCodeId { get; set; }
    public string NRCNo { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class sp_DirectorListReportResult
{
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public DateTime CompanyRegistrationDate { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BusinessType { get; set; } = null!;
    public string? LineofBusiness { get; set; }
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string? DirectorName { get; set; }
    public string? DirectorNRC { get; set; }
    public string? DirectorPosition { get; set; }
    public string? DirectorNationality { get; set; }
    public string? DirectorUnitLevel { get; set; }
    public string? DirectorStreetNumberStreetName { get; set; }
    public string? DirectorQuarterCityTownship { get; set; }
    public string? DirectorState { get; set; }
    public string? DirectorCountry { get; set; }
    public string? DirectorPostalCode { get; set; }
    public int? DirectorSortOrder { get; set; }
    public string? DirectorBlackList { get; set; }
}

public sealed class sp_DirectorListReportRow
{
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public DateTime CompanyRegistrationDate { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BusinessType { get; set; } = null!;
    public string? LineofBusiness { get; set; }
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string? DirectorName { get; set; }
    public string? DirectorNRC { get; set; }
    public string? DirectorPosition { get; set; }
    public string? DirectorNationality { get; set; }
    public string? DirectorUnitLevel { get; set; }
    public string? DirectorStreetNumberStreetName { get; set; }
    public string? DirectorQuarterCityTownship { get; set; }
    public string? DirectorState { get; set; }
    public string? DirectorCountry { get; set; }
    public string? DirectorPostalCode { get; set; }
    public int? DirectorSortOrder { get; set; }
    public string? DirectorBlackList { get; set; }
    public int TotalCount { get; set; }

    public sp_DirectorListReportResult ToResult() => new()
    {
        CompanyRegistrationNo = CompanyRegistrationNo,
        CompanyName = CompanyName,
        CompanyRegistrationDate = CompanyRegistrationDate,
        IssuedDate = IssuedDate,
        EndDate = EndDate,
        BusinessType = BusinessType,
        LineofBusiness = LineofBusiness,
        UnitLevel = UnitLevel,
        StreetNumberStreetName = StreetNumberStreetName,
        QuarterCityTownship = QuarterCityTownship,
        State = State,
        Country = Country,
        PostalCode = PostalCode,
        DirectorName = DirectorName,
        DirectorNRC = DirectorNRC,
        DirectorPosition = DirectorPosition,
        DirectorNationality = DirectorNationality,
        DirectorUnitLevel = DirectorUnitLevel,
        DirectorStreetNumberStreetName = DirectorStreetNumberStreetName,
        DirectorQuarterCityTownship = DirectorQuarterCityTownship,
        DirectorState = DirectorState,
        DirectorCountry = DirectorCountry,
        DirectorPostalCode = DirectorPostalCode,
        DirectorSortOrder = DirectorSortOrder,
        DirectorBlackList = DirectorBlackList,
    };
}

/// <summary>
/// Executes <c>dbo.sp_DirectorListReport_pagination</c> directly (NOT the
/// untouched original). Preserves both @Type branches. See
/// StoredProcedureMigrations/sp_DirectorListReport_pagination.sql.
/// </summary>
public static class sp_DirectorListReport
{
    public static async Task<List<sp_DirectorListReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_DirectorListReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null)
    {
        return await ExecuteQueryable(db, request, sortColumn, sortOrder, pageIndex, pageSize)
            .ToListAsync();
    }

    public static IQueryable<sp_DirectorListReportRow> ExecuteQueryable(
        TradeNetDbContext db,
        sp_DirectorListReportRequest request,
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
            new SqlParameter("@Name", request.Name ?? string.Empty),
            new SqlParameter("@Nationality", request.Nationality ?? string.Empty),
            new SqlParameter("@NRCType", request.NRCType ?? string.Empty),
            new SqlParameter("@NRCPrefixId", request.NRCPrefixId),
            new SqlParameter("@NRCPrefixCodeId", request.NRCPrefixCodeId),
            new SqlParameter("@NRCNo", request.NRCNo ?? string.Empty),
            new SqlParameter("@Type", request.Type ?? string.Empty),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
        };

        const string sql =
            "EXEC dbo.sp_DirectorListReport_pagination @FromDate, @ToDate, @CompanyRegistrationNo, @Name, " +
            "@Nationality, @NRCType, @NRCPrefixId, @NRCPrefixCodeId, @NRCNo, @Type, " +
            "@SortColumn, @SortOrder, @PageIndex, @PageSize";

        return db.Database.SqlQueryRaw<sp_DirectorListReportRow>(sql, parameters);
    }
}
