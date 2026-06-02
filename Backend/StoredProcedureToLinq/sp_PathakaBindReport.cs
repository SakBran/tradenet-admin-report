using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_PathakaBindReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public sealed class sp_PathakaBindReportResult
{
    public DateTime ApplicationDate { get; set; }
    public DateTime? ApproveDate { get; set; }
    public string ApplicationNo { get; set; } = null!;
    public string BindApplicationNo { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? PaThaKaNo { get; set; }
    public string? MemberCode { get; set; }
    public string? Email { get; set; }
    public string CompanyName { get; set; } = null!;
}

public sealed class sp_PathakaBindReportRow
{
    public DateTime ApplicationDate { get; set; }
    public DateTime? ApproveDate { get; set; }
    public string ApplicationNo { get; set; } = null!;
    public string BindApplicationNo { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? PaThaKaNo { get; set; }
    public string? MemberCode { get; set; }
    public string? Email { get; set; }
    public string CompanyName { get; set; } = null!;
    public int TotalCount { get; set; }

    public sp_PathakaBindReportResult ToResult() => new()
    {
        ApplicationDate = ApplicationDate,
        ApproveDate = ApproveDate,
        ApplicationNo = ApplicationNo,
        BindApplicationNo = BindApplicationNo,
        Status = Status,
        PaThaKaNo = PaThaKaNo,
        MemberCode = MemberCode,
        Email = Email,
        CompanyName = CompanyName,
    };
}

/// <summary>
/// Executes <c>dbo.sp_PathakaBindReport_pagination</c> directly (NOT the
/// untouched original). See StoredProcedureMigrations/sp_PathakaBindReport_pagination.sql.
/// </summary>
public static class sp_PathakaBindReport
{
    public static async Task<List<sp_PathakaBindReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_PathakaBindReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null)
    {
        return await ExecuteQueryable(db, request, sortColumn, sortOrder, pageIndex, pageSize)
            .ToListAsync();
    }

    public static IQueryable<sp_PathakaBindReportRow> ExecuteQueryable(
        TradeNetDbContext db,
        sp_PathakaBindReportRequest request,
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
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
        };

        const string sql =
            "EXEC dbo.sp_PathakaBindReport_pagination @FromDate, @ToDate, " +
            "@SortColumn, @SortOrder, @PageIndex, @PageSize";

        return db.Database.SqlQueryRaw<sp_PathakaBindReportRow>(sql, parameters);
    }
}
