using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
    public string? MPUAmount { get; set; }
    public string? AmountDiff { get; set; }
    public string? FormType { get; set; }
    public string? ApplyType { get; set; }
    public string? VoucherNo { get; set; }
    public double? TotalAmount { get; set; }
    public DateTime? PaymentDate { get; set; }
}

public sealed class sp_MPUReport_V3Row
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
    public string? MPUAmount { get; set; }
    public string? AmountDiff { get; set; }
    public string? FormType { get; set; }
    public string? ApplyType { get; set; }
    public string? VoucherNo { get; set; }
    public double? TotalAmount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public int? TotalCount { get; set; }

    public sp_MPUReport_V3Result ToResult() => new()
    {
        Id = Id,
        Sakhan = Sakhan,
        TransactionDateTime = TransactionDateTime,
        CompanyName = CompanyName,
        CompanyRegistrationNo = CompanyRegistrationNo,
        ApplicationNo = ApplicationNo,
        MerchantId = MerchantId,
        AccountNo = AccountNo,
        InvoiceNo = InvoiceNo,
        ApprovalCode = ApprovalCode,
        TransactionRefNo = TransactionRefNo,
        TransactionAmount = TransactionAmount,
        MOCAmount = MOCAmount,
        IMAmount = IMAmount,
        MPUAmount = MPUAmount,
        AmountDiff = AmountDiff,
        FormType = FormType,
        ApplyType = ApplyType,
        VoucherNo = VoucherNo,
        TotalAmount = TotalAmount,
        PaymentDate = PaymentDate,
    };
}

public static class sp_MPUReport_V3
{
    private const int CommandTimeoutSeconds = 180;

    public static async Task<List<sp_MPUReport_V3Row>> ExecuteAsync(
        TradeNetDbContext db,
        sp_MPUReport_V3Request request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null,
        bool includeTotalCount = true)
    {
        ArgumentNullException.ThrowIfNull(db);

        var previousTimeout = db.Database.GetCommandTimeout();
        try
        {
            return await ExecuteQueryable(db, request, sortColumn, sortOrder, pageIndex, pageSize, includeTotalCount)
                .ToListAsync();
        }
        finally
        {
            db.Database.SetCommandTimeout(previousTimeout);
        }
    }

    public static IQueryable<sp_MPUReport_V3Row> ExecuteQueryable(
        TradeNetDbContext db,
        sp_MPUReport_V3Request request,
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
            new SqlParameter("@FormType", request.FormType ?? string.Empty),
            new SqlParameter("@PaymentType", request.PaymentType ?? string.Empty),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
            new SqlParameter("@IncludeTotalCount", includeTotalCount),
        };

        const string sql =
            "EXEC dbo.sp_MPUReport_V3_pagination @FromDate, @ToDate, @FormType, @PaymentType, " +
            "@SortColumn, @SortOrder, @PageIndex, @PageSize, @IncludeTotalCount";

        db.Database.SetCommandTimeout(CommandTimeoutSeconds);

        return db.Database.SqlQueryRaw<sp_MPUReport_V3Row>(sql, parameters);
    }

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
