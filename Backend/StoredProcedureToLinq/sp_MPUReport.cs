using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
    public string? MPUAmount { get; set; }
    public string? AmountDiff { get; set; }
    public string? FormType { get; set; }
    public string? ApplyType { get; set; }
    public string? VoucherNo { get; set; }
}

public sealed class sp_MPUReportRow
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
    public int? TotalCount { get; set; }

    public sp_MPUReportResult ToResult() => new()
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
    };
}

public static class sp_MPUReport
{
    private static readonly DateTime FeeCutoff = new(2025, 11, 15);

    public static async Task<List<sp_MPUReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_MPUReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null,
        bool includeTotalCount = true)
    {
        return await ExecuteQueryable(db, request, sortColumn, sortOrder, pageIndex, pageSize, includeTotalCount)
            .ToListAsync();
    }

    public static IQueryable<sp_MPUReportRow> ExecuteQueryable(
        TradeNetDbContext db,
        sp_MPUReportRequest request,
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
            "EXEC dbo.sp_MPUReport_pagination @FromDate, @ToDate, @FormType, @PaymentType, " +
            "@SortColumn, @SortOrder, @PageIndex, @PageSize, @IncludeTotalCount";

        return db.Database.SqlQueryRaw<sp_MPUReportRow>(sql, parameters);
    }

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
