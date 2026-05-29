using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_PaThaKaAllReportRequest
{
    public int BusinessTypeId { get; set; }
    public int LineofBusinessId { get; set; }
    public string State { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class sp_PaThaKaAllReportResult
{
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string OwnerName { get; set; } = null!;
    public string? OwnerNRC { get; set; }
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
    public string Mobile1 { get; set; } = null!;
    public string? Mobile2 { get; set; }
    public string? Mobile3 { get; set; }
    public string? Fax { get; set; }
    public string Email { get; set; } = null!;
    public double? Capital { get; set; }
    public string? Currency { get; set; }
    public int? Terms { get; set; }
    public DateTime DecisionDate { get; set; }
    public string? DecisionName { get; set; }
    public string? DecisionPosition { get; set; }
    public string Status { get; set; } = null!;
    public string? MICPermitNo { get; set; }
}

public sealed class sp_PaThaKaAllReportRow
{
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string OwnerName { get; set; } = null!;
    public string? OwnerNRC { get; set; }
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
    public string Mobile1 { get; set; } = null!;
    public string? Mobile2 { get; set; }
    public string? Mobile3 { get; set; }
    public string? Fax { get; set; }
    public string Email { get; set; } = null!;
    public double? Capital { get; set; }
    public string? Currency { get; set; }
    public int? Terms { get; set; }
    public DateTime DecisionDate { get; set; }
    public string? DecisionName { get; set; }
    public string? DecisionPosition { get; set; }
    public string Status { get; set; } = null!;
    public string? MICPermitNo { get; set; }
    public int TotalCount { get; set; }

    public sp_PaThaKaAllReportResult ToResult() => new()
    {
        CompanyRegistrationNo = CompanyRegistrationNo,
        CompanyName = CompanyName,
        OwnerName = OwnerName,
        OwnerNRC = OwnerNRC,
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
        Mobile1 = Mobile1,
        Mobile2 = Mobile2,
        Mobile3 = Mobile3,
        Fax = Fax,
        Email = Email,
        Capital = Capital,
        Currency = Currency,
        Terms = Terms,
        DecisionDate = DecisionDate,
        DecisionName = DecisionName,
        DecisionPosition = DecisionPosition,
        Status = Status,
        MICPermitNo = MICPermitNo,
    };
}

/// <summary>
/// Executes <c>dbo.sp_PaThaKaAllReport_pagination</c> directly (NOT the
/// untouched original). See StoredProcedureMigrations/sp_PaThaKaAllReport_pagination.sql.
/// </summary>
public static class sp_PaThaKaAllReport
{
    public static async Task<List<sp_PaThaKaAllReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_PaThaKaAllReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new[]
        {
            new SqlParameter("@BusinessTypeId", request.BusinessTypeId),
            new SqlParameter("@LineofBusinessId", request.LineofBusinessId),
            new SqlParameter("@State", request.State ?? string.Empty),
            new SqlParameter("@Status", request.Status ?? string.Empty),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
        };

        const string sql =
            "EXEC dbo.sp_PaThaKaAllReport_pagination @BusinessTypeId, @LineofBusinessId, @State, @Status, " +
            "@SortColumn, @SortOrder, @PageIndex, @PageSize";

        return await db.Database
            .SqlQueryRaw<sp_PaThaKaAllReportRow>(sql, parameters)
            .ToListAsync();
    }
}
