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
    public double? TotalAmount { get; set; }
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
    public double? TotalAmount { get; set; }
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
        TotalAmount = TotalAmount,
    };
}

public sealed class sp_MPUReportColumnTotals
{
    public decimal TransactionAmount { get; set; }
    public decimal MOCAmount { get; set; }
    public decimal IMAmount { get; set; }
    public decimal MPUAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountDiff { get; set; }

    public IReadOnlyDictionary<string, decimal> ToDictionary() => new Dictionary<string, decimal>
    {
        ["transactionAmount"] = TransactionAmount,
        ["mocAmount"] = MOCAmount,
        ["imAmount"] = IMAmount,
        ["mpuAmount"] = MPUAmount,
        ["totalAmount"] = TotalAmount,
        ["amountDiff"] = AmountDiff,
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

    public static async Task<IReadOnlyDictionary<string, decimal>> ExecuteColumnTotalsAsync(
        TradeNetDbContext db,
        sp_MPUReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new[]
        {
            new SqlParameter("@FromDate", request.FromDate),
            new SqlParameter("@ToDate", request.ToDate),
            new SqlParameter("@FormType", request.FormType ?? string.Empty),
            new SqlParameter("@PaymentType", request.PaymentType ?? string.Empty),
        };

        const string sql = @"
DECLARE @OnlineFeeAmount nvarchar(50) = CASE WHEN @FromDate < '2025-11-15' THEN N'3000' ELSE N'10000' END;
DECLARE @OnlineFeeTotalAmount decimal(18, 2) = CASE WHEN @FromDate < '2025-11-15' THEN 3000 ELSE 10000 END;

SELECT
    CAST(ISNULL(SUM(amounts.TransactionAmount), 0) AS decimal(18, 2)) AS TransactionAmount,
    CAST(ISNULL(SUM(amounts.MOCAmount), 0) AS decimal(18, 2)) AS MOCAmount,
    CAST(ISNULL(SUM(amounts.IMAmount), 0) AS decimal(18, 2)) AS IMAmount,
    CAST(ISNULL(SUM(amounts.TransactionAmount - amounts.MOCAmount - amounts.IMAmount), 0) AS decimal(18, 2)) AS MPUAmount,
    CAST(ISNULL(SUM(accountTotal.TotalAmount), 0) AS decimal(18, 2)) AS TotalAmount,
    CAST(ISNULL(SUM(amounts.TransactionAmount - amounts.MOCAmount), 0) AS decimal(18, 2)) AS AmountDiff
FROM dbo.MPUPaymentTransaction m
CROSS APPLY (
    SELECT
        ISNULL(TRY_CONVERT(decimal(18, 2),
            CASE
                WHEN LEN(ISNULL(m.TransactionAmount, '')) > 2 THEN LEFT(m.TransactionAmount, LEN(m.TransactionAmount) - 2) + '.' + RIGHT(m.TransactionAmount, 2)
                WHEN LEN(ISNULL(m.TransactionAmount, '')) > 0 THEN '0.' + RIGHT('00' + m.TransactionAmount, 2)
                ELSE '0'
            END), 0) AS TransactionAmount,
        ISNULL(TRY_CONVERT(decimal(18, 2), m.MOCAmount), 0) AS MOCAmount,
        ISNULL(TRY_CONVERT(decimal(18, 2), m.IMAmount), 0) AS IMAmount
) amounts
OUTER APPLY (
    SELECT TOP 1 CAST(a.TotalAmount AS decimal(18, 2)) AS TotalAmount
    FROM dbo.AccountTransaction a
    WHERE m.TransactionId = a.TransactionId
        AND ((m.MOCAmount = @OnlineFeeAmount AND a.TotalAmount = @OnlineFeeTotalAmount)
            OR (m.MOCAmount <> @OnlineFeeAmount AND a.TotalAmount <> @OnlineFeeTotalAmount))
    ORDER BY a.CreatedDate DESC
) accountTotal
WHERE m.ResponseCode = '00'
    AND m.TransactionDateTime IS NOT NULL
    AND m.TransactionDateTime >= @FromDate
    AND m.TransactionDateTime <= @ToDate
    AND m.FormType IS NOT NULL
    AND m.FormType LIKE (CASE WHEN @FormType = '' THEN m.FormType + '%' ELSE @FormType + '%' END)
    AND (@PaymentType = ''
        OR m.PaymentType = @PaymentType
        OR (@PaymentType = 'CitizenPay'
            AND REPLACE(REPLACE(REPLACE(LOWER(ISNULL(m.PaymentType, '')), ' ', ''), '-', ''), '_', '')
                IN ('citizenpay', 'citizen', 'cp')));";

        var totals = (await db.Database
            .SqlQueryRaw<sp_MPUReportColumnTotals>(sql, parameters)
            .ToListAsync())
            .Single();

        return totals.ToDictionary();
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
                    .FirstOrDefault(),
                TotalAmount = db.AccountTransactions
                    .Where(accountTransaction => accountTransaction.TransactionId == transaction.TransactionId
                        && (includeOnlineFeeRows
                            ? accountTransaction.TotalAmount == onlineFeeTotalAmount
                            : accountTransaction.TotalAmount != onlineFeeTotalAmount))
                    .OrderByDescending(accountTransaction => accountTransaction.CreatedDate)
                    .Select(accountTransaction => (double?)accountTransaction.TotalAmount)
                    .FirstOrDefault()
            });
    }
}
