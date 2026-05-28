using API.DBContext;
using System;
using System.Linq;

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

public static class sp_ChequeNoReport
{
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
