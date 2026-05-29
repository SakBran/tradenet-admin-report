using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_PaThaKaReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BusinessTypeId { get; set; }
    public int LineofBusinessId { get; set; }
    public string State { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class sp_PaThaKaReportResult
{
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
    public string? MICPermitNo { get; set; }
}

/// <summary>
/// Row shape returned by <c>EXEC dbo.sp_PaThaKaReport</c>. It is the report
/// projection plus the inline <see cref="TotalCount"/> window count so the
/// caller gets the page and the total number of matching rows in one trip.
/// </summary>
public sealed class sp_PaThaKaReportRow
{
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
    public string? MICPermitNo { get; set; }
    public int TotalCount { get; set; }

    public sp_PaThaKaReportResult ToResult() => new()
    {
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
        MICPermitNo = MICPermitNo,
    };
}

/// <summary>
/// Executes the <c>dbo.sp_PaThaKaReport</c> stored procedure directly.
/// The report intentionally calls the stored procedure (rather than an
/// equivalent LINQ query) so results match the source procedure exactly.
/// Pagination is pushed into SQL Server via the procedure's OFFSET/FETCH
/// parameters; <see cref="sp_PaThaKaReportRow.TotalCount"/> carries the
/// total matching row count.
/// </summary>
public static class sp_PaThaKaReport
{
    /// <summary>
    /// Runs the procedure. Pass <paramref name="pageIndex"/>/<paramref name="pageSize"/>
    /// as <c>null</c> to return every matching row (used by the Excel export);
    /// pass a page size greater than zero to return a single page.
    /// </summary>
    public static async Task<List<sp_PaThaKaReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_PaThaKaReportRequest request,
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
            new SqlParameter("@BusinessTypeId", request.BusinessTypeId),
            new SqlParameter("@LineofBusinessId", request.LineofBusinessId),
            new SqlParameter("@State", request.State ?? string.Empty),
            new SqlParameter("@Status", request.Status ?? string.Empty),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
        };

        // Calls the pagination-aware copy (sp_PaThaKaReport_pagination), NOT the
        // original dbo.sp_PaThaKaReport, which is left untouched.
        // See StoredProcedureMigrations/sp_PaThaKaReport_pagination.sql.
        const string sql =
            "EXEC dbo.sp_PaThaKaReport_pagination @FromDate, @ToDate, @BusinessTypeId, @LineofBusinessId, " +
            "@State, @Status, @SortColumn, @SortOrder, @PageIndex, @PageSize";

        return await db.Database
            .SqlQueryRaw<sp_PaThaKaReportRow>(sql, parameters)
            .ToListAsync();
    }
}
