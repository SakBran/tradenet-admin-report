using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_PaThaKaValidInvalidReportRequest
{
    public DateTime Date { get; set; }
    public int BusinessTypeId { get; set; }
    public int LineofBusinessId { get; set; }
    public string State { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class sp_PaThaKaValidInvalidReportResult
{
    public string CompanyRegistrationNo { get; set; } = null!;
    public DateTime IssuedDate { get; set; }
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
}

/// <summary>
/// Row shape returned by <c>EXEC dbo.sp_PaThaKaValidInvalidReport_pagination</c>.
/// It is the report projection plus the inline <see cref="TotalCount"/> window
/// count so the caller gets the page and the total matching rows in one trip.
/// </summary>
public sealed class sp_PaThaKaValidInvalidReportRow
{
    public string CompanyRegistrationNo { get; set; } = null!;
    public DateTime IssuedDate { get; set; }
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
    public int TotalCount { get; set; }

    public sp_PaThaKaValidInvalidReportResult ToResult() => new()
    {
        CompanyRegistrationNo = CompanyRegistrationNo,
        IssuedDate = IssuedDate,
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
    };
}

/// <summary>
/// Executes the <c>dbo.sp_PaThaKaValidInvalidReport_pagination</c> stored
/// procedure directly. The report intentionally calls the stored procedure
/// (rather than an equivalent LINQ query) so results match the source procedure
/// exactly. Pagination is pushed into SQL Server via the procedure's
/// OFFSET/FETCH parameters; <see cref="sp_PaThaKaValidInvalidReportRow.TotalCount"/>
/// carries the total matching row count.
/// </summary>
public static class sp_PaThaKaValidInvalidReport
{
    /// <summary>
    /// Runs the procedure. Pass <paramref name="pageIndex"/>/<paramref name="pageSize"/>
    /// as <c>null</c> to return every matching row (used by the Excel export);
    /// pass a page size greater than zero to return a single page.
    /// </summary>
    public static async Task<List<sp_PaThaKaValidInvalidReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_PaThaKaValidInvalidReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new[]
        {
            new SqlParameter("@Date", request.Date),
            new SqlParameter("@BusinessTypeId", request.BusinessTypeId),
            new SqlParameter("@LineofBusinessId", request.LineofBusinessId),
            new SqlParameter("@State", request.State ?? string.Empty),
            new SqlParameter("@Status", request.Status ?? string.Empty),
            new SqlParameter("@Type", request.Type ?? string.Empty),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
        };

        // Calls the pagination-aware copy (sp_PaThaKaValidInvalidReport_pagination),
        // NOT the original dbo.sp_PaThaKaValidInvalidReport, which is left untouched.
        // See StoredProcedureMigrations/sp_PaThaKaValidInvalidReport_pagination.sql.
        const string sql =
            "EXEC dbo.sp_PaThaKaValidInvalidReport_pagination @Date, @BusinessTypeId, @LineofBusinessId, " +
            "@State, @Status, @Type, @SortColumn, @SortOrder, @PageIndex, @PageSize";

        return await db.Database
            .SqlQueryRaw<sp_PaThaKaValidInvalidReportRow>(sql, parameters)
            .ToListAsync();
    }
}
