using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_PaThaKaByBusinessTypeReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BusinessTypeId { get; set; }
}

public sealed class sp_PaThaKaByBusinessTypeReportResult
{
    public string BusinessType { get; set; } = null!;
    public int CompanyCount { get; set; }
}

public sealed class sp_PaThaKaByBusinessTypeReportRow
{
    public string BusinessType { get; set; } = null!;
    public int CompanyCount { get; set; }
    public int TotalCount { get; set; }

    public sp_PaThaKaByBusinessTypeReportResult ToResult() => new()
    {
        BusinessType = BusinessType,
        CompanyCount = CompanyCount,
    };
}

/// <summary>
/// Executes <c>dbo.sp_PaThaKaByBusinessTypeReport_pagination</c> directly (NOT the
/// untouched original). See StoredProcedureMigrations/sp_PaThaKaByBusinessTypeReport_pagination.sql.
/// </summary>
public static class sp_PaThaKaByBusinessTypeReport
{
    public static async Task<List<sp_PaThaKaByBusinessTypeReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_PaThaKaByBusinessTypeReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null)
    {
        return await ExecuteQueryable(db, request, sortColumn, sortOrder, pageIndex, pageSize)
            .ToListAsync();
    }

    public static IQueryable<sp_PaThaKaByBusinessTypeReportRow> ExecuteQueryable(
        TradeNetDbContext db,
        sp_PaThaKaByBusinessTypeReportRequest request,
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
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
        };

        const string sql =
            "EXEC dbo.sp_PaThaKaByBusinessTypeReport_pagination @FromDate, @ToDate, @BusinessTypeId, " +
            "@SortColumn, @SortOrder, @PageIndex, @PageSize";

        return db.Database.SqlQueryRaw<sp_PaThaKaByBusinessTypeReportRow>(sql, parameters);
    }
}
