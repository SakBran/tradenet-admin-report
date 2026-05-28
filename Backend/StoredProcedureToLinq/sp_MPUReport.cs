using API.DBContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_MPUReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string FormType { get; set; } = string.Empty;
    public string PaymentType { get; set; } = string.Empty;
}

public sealed class sp_MPUReportResult
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

public static class sp_MPUReport
{
    private static readonly DateTime FeeCutoff = new(2025, 11, 15);

    public static IQueryable<sp_MPUReportResult> Query(
        TradeNetDbContext db,
        sp_MPUReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var onlineFeeAmount = request.FromDate < FeeCutoff ? "3000" : "10000";
        var onlineFeeTotalAmount = request.FromDate < FeeCutoff ? 3000d : 10000d;

        return QueryRows(db, request, onlineFeeAmount, onlineFeeTotalAmount, includeOnlineFeeRows: false)
            .Union(QueryRows(db, request, onlineFeeAmount, onlineFeeTotalAmount, includeOnlineFeeRows: true))
            .OrderBy(row => row.TransactionDateTime);
    }

    private static IQueryable<sp_MPUReportResult> QueryRows(
        TradeNetDbContext db,
        sp_MPUReportRequest request,
        string onlineFeeAmount,
        double onlineFeeTotalAmount,
        bool includeOnlineFeeRows)
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
                && (includeOnlineFeeRows
                    ? transaction.Mocamount == onlineFeeAmount
                    : transaction.Mocamount != onlineFeeAmount))
            .Select(transaction => new sp_MPUReportResult
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
                VoucherNo = db.AccountTransactions
                    .Where(accountTransaction => accountTransaction.TransactionId == transaction.TransactionId
                        && (includeOnlineFeeRows
                            ? accountTransaction.TotalAmount == onlineFeeTotalAmount
                            : accountTransaction.TotalAmount != onlineFeeTotalAmount))
                    .OrderByDescending(accountTransaction => accountTransaction.CreatedDate)
                    .Select(accountTransaction => accountTransaction.VoucherNo)
                    .FirstOrDefault()
            });
    }
}
