using API.DBContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_MPUReport_V3Request
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string FormType { get; set; } = string.Empty;
    public string PaymentType { get; set; } = string.Empty;
}

public sealed class sp_MPUReport_V3Result
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
    public double? TotalAmount { get; set; }
    public DateTime? PaymentDate { get; set; }
}

public static class sp_MPUReport_V3
{
    public static IQueryable<sp_MPUReport_V3Result> Query(
        TradeNetDbContext db,
        sp_MPUReport_V3Request request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var mpuRows =
            from transaction in db.MpupaymentTransactions
            select new
            {
                Transaction = transaction,
                RowNumber = db.MpupaymentTransactions.Count(other =>
                    other.TransactionId == transaction.TransactionId
                    && (other.TransactionDateTime == null
                        || other.TransactionDateTime < transaction.TransactionDateTime
                        || (other.TransactionDateTime == transaction.TransactionDateTime
                            && other.Id <= transaction.Id)))
            };

        var accountRows =
            from account in db.AccountTransactions
            select new
            {
                Account = account,
                RowNumber = db.AccountTransactions.Count(other =>
                    other.TransactionId == account.TransactionId
                    && (other.CreatedDate < account.CreatedDate
                        || (other.CreatedDate == account.CreatedDate
                            && string.Compare(other.Id, account.Id) <= 0)))
            };

        return
            from mpuRow in mpuRows
            join accountRow in accountRows
                on new { mpuRow.Transaction.TransactionId, mpuRow.RowNumber }
                equals new { accountRow.Account.TransactionId, accountRow.RowNumber }
                into accountGroup
            from accountRow in accountGroup.DefaultIfEmpty()
            where mpuRow.Transaction.TransactionDateTime >= request.FromDate
                && mpuRow.Transaction.TransactionDateTime <= request.ToDate
                && mpuRow.Transaction.ResponseCode == "00"
                && accountRow.Account.VoucherNo != null
                && mpuRow.Transaction.FormType != null
                && (request.FormType == string.Empty || EF.Functions.Like(mpuRow.Transaction.FormType, request.FormType + "%"))
                && mpuRow.Transaction.PaymentType == request.PaymentType
            orderby mpuRow.Transaction.TransactionId, mpuRow.Transaction.TransactionDateTime
            select new sp_MPUReport_V3Result
            {
                Id = mpuRow.Transaction.Id,
                Sakhan = mpuRow.Transaction.Sakhan,
                TransactionDateTime = mpuRow.Transaction.TransactionDateTime,
                CompanyName = db.PaThaKas
                    .Where(paThaKa => paThaKa.PaThaKaNo == mpuRow.Transaction.PaThaKaNo
                        || paThaKa.CompanyRegistrationNo == mpuRow.Transaction.PaThaKaNo)
                    .Select(paThaKa => paThaKa.CompanyName)
                    .FirstOrDefault() ?? string.Empty,
                CompanyRegistrationNo = mpuRow.Transaction.PaThaKaNo,
                ApplicationNo = mpuRow.Transaction.ApplicationNo,
                MerchantId = mpuRow.Transaction.MerchantId,
                AccountNo = mpuRow.Transaction.AccountNo,
                InvoiceNo = mpuRow.Transaction.InvoiceNo,
                ApprovalCode = mpuRow.Transaction.ApprovalCode,
                TransactionRefNo = mpuRow.Transaction.TransactionRefNo,
                TransactionAmount = mpuRow.Transaction.TransactionAmount,
                MOCAmount = mpuRow.Transaction.Mocamount,
                IMAmount = mpuRow.Transaction.Imamount,
                FormType = mpuRow.Transaction.FormType,
                ApplyType = mpuRow.Transaction.ApplyType,
                VoucherNo = accountRow.Account.VoucherNo,
                TotalAmount = accountRow.Account.TotalAmount,
                PaymentDate = accountRow.Account.PaymentDate
            };
    }
}
