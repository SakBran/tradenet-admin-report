using API.DBContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_MPUReport_Seperated_OnineFeeRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string FormType { get; set; } = string.Empty;
    public string PaymentType { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
}

public sealed class sp_MPUReport_Seperated_OnineFeeResult
{
    public int Id { get; set; }
    public string? Sakhan { get; set; }
    public DateTime? TransactionDateTime { get; set; }
    public string CompanyName { get; set; } = null!;
    public string? CompanyRegistrationNo { get; set; }
    public string? ApplicationNo { get; set; }
    public string? MerchantId { get; set; }
    public string? AccountNo { get; set; }
    public string? InvoiceNo { get; set; }
    public string? ApprovalCode { get; set; }
    public string? TransactionRefNo { get; set; }
    public string? TransactionAmount { get; set; }
    public string? MOCAmount { get; set; }
    public string? IMAmount { get; set; }
    public string? FormType { get; set; }
    public string? ApplyType { get; set; }
    public string? VoucherNo { get; set; }
}

public static class sp_MPUReport_Seperated_OnineFee
{
    public static IQueryable<sp_MPUReport_Seperated_OnineFeeResult> Query(
        TradeNetDbContext db,
        sp_MPUReport_Seperated_OnineFeeRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return QueryRows(db, request, applicationNoIsEmpty: true)
            .Union(QueryRows(db, request, applicationNoIsEmpty: false))
            .OrderBy(row => row.TransactionDateTime);
    }

    private static IQueryable<sp_MPUReport_Seperated_OnineFeeResult> QueryRows(
        TradeNetDbContext db,
        sp_MPUReport_Seperated_OnineFeeRequest request,
        bool applicationNoIsEmpty)
    {
        return db.MpupaymentTransactions
            .Where(transaction =>
                transaction.ResponseCode == "00"
                && transaction.TransactionDateTime != null
                && transaction.TransactionDateTime >= request.FromDate
                && transaction.TransactionDateTime <= request.ToDate
                && transaction.FormType != null
                && (request.FormType == string.Empty || EF.Functions.Like(transaction.FormType, request.FormType + "%"))
                && transaction.PaymentType == request.PaymentType
                && (applicationNoIsEmpty
                    ? transaction.ApplicationNo == string.Empty
                    : transaction.ApplicationNo != string.Empty)
                && (transaction.FormType == "Border Import Licence"
                    || transaction.FormType == "Import Licence"
                    || transaction.FormType == "Import Permit"
                    || transaction.FormType == "Border Import Permit"))
            .Select(transaction => new sp_MPUReport_Seperated_OnineFeeResult
            {
                Id = transaction.Id,
                Sakhan = transaction.Sakhan,
                TransactionDateTime = transaction.TransactionDateTime,
                CompanyName = db.PaThaKas
                    .Where(paThaKa => paThaKa.PaThaKaNo == transaction.PaThaKaNo
                        || paThaKa.CompanyRegistrationNo == transaction.PaThaKaNo)
                    .Select(paThaKa => paThaKa.CompanyName)
                    .FirstOrDefault() ?? string.Empty,
                CompanyRegistrationNo = transaction.PaThaKaNo,
                ApplicationNo = transaction.ApplicationNo,
                MerchantId = transaction.MerchantId,
                AccountNo = transaction.AccountNo,
                InvoiceNo = transaction.InvoiceNo,
                ApprovalCode = transaction.ApprovalCode,
                TransactionRefNo = transaction.TransactionRefNo,
                TransactionAmount = transaction.TransactionAmount,
                MOCAmount = transaction.Mocamount,
                IMAmount = transaction.Imamount,
                FormType = transaction.FormType,
                ApplyType = transaction.ApplyType,
                VoucherNo =
                    (from accountTransaction in db.AccountTransactions
                     join detail in db.AccountTransactionDetails on accountTransaction.Id equals detail.AccountTransactionId
                     where accountTransaction.TransactionId == transaction.TransactionId
                        && accountTransaction.TotalAmount == 10000
                        && detail.AccountTitleId == 1
                     orderby accountTransaction.CreatedDate descending
                     select accountTransaction.VoucherNo)
                    .FirstOrDefault()
            });
    }
}
