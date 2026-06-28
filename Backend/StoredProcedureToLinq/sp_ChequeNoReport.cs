using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_ChequeNoReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int ChequeNoId { get; set; }
}

public sealed class sp_ChequeNoReportResult
{
    public int ChequeId { get; set; }
    public string? ChequeNo { get; set; }
    public DateTime? Date { get; set; }
    public string? SDate { get; set; }
    public double Amount { get; set; }
}

public sealed class sp_ChequeNoReportRow
{
    public int ChequeId { get; set; }
    public string? ChequeNo { get; set; }
    public DateTime? Date { get; set; }
    public string? SDate { get; set; }
    public double Amount { get; set; }
    public int? TotalCount { get; set; }

    public sp_ChequeNoReportResult ToResult() => new()
    {
        ChequeId = ChequeId,
        ChequeNo = ChequeNo,
        Date = Date,
        SDate = SDate,
        Amount = Amount,
    };
}

public static class sp_ChequeNoReport
{
    public static async Task<List<sp_ChequeNoReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_ChequeNoReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null,
        bool includeTotalCount = true)
    {
        return await ExecuteQueryable(db, request, sortColumn, sortOrder, pageIndex, pageSize, includeTotalCount)
            .ToListAsync();
    }

    public static async Task<IReadOnlyDictionary<string, decimal>> ExecuteColumnTotalsAsync(
        TradeNetDbContext db,
        sp_ChequeNoReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var amount = await Query(db, request).SumAsync(row => row.Amount);

        return new Dictionary<string, decimal>
        {
            ["amount"] = (decimal)amount
        };
    }

    public static IQueryable<sp_ChequeNoReportRow> ExecuteQueryable(
        TradeNetDbContext db,
        sp_ChequeNoReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null,
        bool includeTotalCount = true)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new[]
        {
            new SqlParameter("@FromDate", request.FromDate),
            new SqlParameter("@ToDate", request.ToDate),
            new SqlParameter("@ChequeNoId", request.ChequeNoId),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
            new SqlParameter("@IncludeTotalCount", includeTotalCount),
        };

        const string sql =
            "EXEC dbo.sp_ChequeNoReport_pagination @FromDate, @ToDate, @ChequeNoId, " +
            "@SortColumn, @SortOrder, @PageIndex, @PageSize, @IncludeTotalCount";

        return db.Database.SqlQueryRaw<sp_ChequeNoReportRow>(sql, parameters);
    }

    public static IQueryable<sp_ChequeNoReportResult> Query(
        TradeNetDbContext db,
        sp_ChequeNoReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return
            from transaction in db.AccountTransactions
            join detail in db.AccountTransactionDetails on transaction.Id equals detail.AccountTransactionId
            join title in db.AccountTitles on detail.AccountTitleId equals title.Id
            join chequeNo in db.ChequeNos on title.ChequeNoId equals chequeNo.Id
            where transaction.IsPayment
                && transaction.VoucherDate >= request.FromDate
                && transaction.VoucherDate <= request.ToDate
                && (request.ChequeNoId == 0 || chequeNo.Id == request.ChequeNoId)
            group detail by new
            {
                ChequeId = chequeNo.Id,
                ChequeNo = chequeNo.Code,
                transaction.VoucherDate
            }
            into grouped
            orderby grouped.Key.VoucherDate, grouped.Key.ChequeId
            select new sp_ChequeNoReportResult
            {
                ChequeId = grouped.Key.ChequeId,
                ChequeNo = grouped.Key.ChequeNo,
                Date = grouped.Key.VoucherDate,
                SDate = grouped.Key.VoucherDate == null
                    ? null
                    : (grouped.Key.VoucherDate.Value.Day < 10 ? "0" : string.Empty)
                    + grouped.Key.VoucherDate.Value.Day.ToString()
                    + "/"
                    + (grouped.Key.VoucherDate.Value.Month < 10 ? "0" : string.Empty)
                    + grouped.Key.VoucherDate.Value.Month.ToString()
                    + "/"
                    + grouped.Key.VoucherDate.Value.Year.ToString(),
                Amount = grouped.Sum(detail => detail.Amount)
            };
    }
}
