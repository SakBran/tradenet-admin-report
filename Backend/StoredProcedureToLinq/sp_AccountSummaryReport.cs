using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_AccountSummaryReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string FormType { get; set; } = string.Empty;
    public int SakhanId { get; set; }
}

public sealed class sp_AccountSummaryReportResult
{
    public string Id { get; set; } = null!;
    public DateTime? VoucherDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? VoucherNo { get; set; }
    public string? CompanyName { get; set; }
    public string TransactionTitle { get; set; } = null!;
    public double Amount { get; set; }
    public string AccountTitleCode { get; set; } = null!;
    public int SortOrder { get; set; }
    public int SakhanId { get; set; }
    public string? LocationCode { get; set; }
    public string FormType { get; set; } = null!;
}

public sealed class sp_AccountSummaryReportRow
{
    public string Id { get; set; } = null!;
    public DateTime? VoucherDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? VoucherNo { get; set; }
    public string? CompanyName { get; set; }
    public string TransactionTitle { get; set; } = null!;
    public double Amount { get; set; }
    public string AccountTitleCode { get; set; } = null!;
    public int SortOrder { get; set; }
    public int SakhanId { get; set; }
    public string? LocationCode { get; set; }
    public string FormType { get; set; } = null!;
    public int? TotalCount { get; set; }

    public sp_AccountSummaryReportResult ToResult() => new()
    {
        Id = Id,
        VoucherDate = VoucherDate,
        PaymentDate = PaymentDate,
        CompanyRegistrationNo = CompanyRegistrationNo,
        VoucherNo = VoucherNo,
        CompanyName = CompanyName,
        TransactionTitle = TransactionTitle,
        Amount = Amount,
        AccountTitleCode = AccountTitleCode,
        SortOrder = SortOrder,
        SakhanId = SakhanId,
        LocationCode = LocationCode,
        FormType = FormType,
    };
}

public static class sp_AccountSummaryReport
{
    private const int CommandTimeoutSeconds = 180;

    public static async Task<List<sp_AccountSummaryReportRow>> ExecuteAsync(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request,
        string? sortColumn = null,
        string? sortOrder = null,
        int? pageIndex = null,
        int? pageSize = null,
        bool includeTotalCount = true)
    {
        ArgumentNullException.ThrowIfNull(db);

        var previousTimeout = db.Database.GetCommandTimeout();
        db.Database.SetCommandTimeout(CommandTimeoutSeconds);

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

    public static async Task<IReadOnlyDictionary<string, decimal>> ExecuteColumnTotalsAsync(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var previousTimeout = db.Database.GetCommandTimeout();
        db.Database.SetCommandTimeout(CommandTimeoutSeconds);

        try
        {
            var amount = await Query(db, request).SumAsync(row => row.Amount);

            return new Dictionary<string, decimal>
            {
                ["amount"] = (decimal)amount
            };
        }
        finally
        {
            db.Database.SetCommandTimeout(previousTimeout);
        }
    }

    public static IQueryable<sp_AccountSummaryReportRow> ExecuteQueryable(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request,
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
            new SqlParameter("@SakhanId", request.SakhanId),
            new SqlParameter("@SortColumn", (object?)sortColumn ?? DBNull.Value),
            new SqlParameter("@SortOrder", (object?)sortOrder ?? DBNull.Value),
            new SqlParameter("@PageIndex", (object?)pageIndex ?? DBNull.Value),
            new SqlParameter("@PageSize", (object?)pageSize ?? DBNull.Value),
            new SqlParameter("@IncludeTotalCount", includeTotalCount),
        };

        const string sql =
            "EXEC dbo.sp_AccountSummaryReport_pagination @FromDate, @ToDate, @FormType, @SakhanId, " +
            "@SortColumn, @SortOrder, @PageIndex, @PageSize, @IncludeTotalCount";

        return db.Database.SqlQueryRaw<sp_AccountSummaryReportRow>(sql, parameters);
    }

    public static IQueryable<sp_AccountSummaryReportResult> Query(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var query =
            MemberBranch(db, request)
                .Concat(PaThaKaRegistrationBranch(db, request))
                .Concat(BusinessServiceAgencyBranch(db, request))
                .Concat(DutyFreeShopBranch(db, request))
                .Concat(ReExportBranch(db, request))
                .Concat(SaleCenterBranch(db, request))
                .Concat(ShowRoomBranch(db, request))
                .Concat(EvshowRoomBranch(db, request))
                .Concat(EvcycleShowRoomBranch(db, request))
                .Concat(WholeSaleRetailBranch(db, request))
                .Concat(WineImportationBranch(db, request))
                .Concat(DeleteDataNptBranch(db, request))
                .Concat(DeleteDataBorderBranch(db, request))
                .Concat(ExportLicenceBranch(db, request))
                .Concat(ImportLicenceBranch(db, request))
                .Concat(ExportPermitBranch(db, request))
                .Concat(ImportPermitBranch(db, request))
                .Concat(BorderExportLicencePaThaKaBranch(db, request))
                .Concat(BorderExportLicenceIndividualTradingBranch(db, request))
                .Concat(BorderImportLicencePaThaKaBranch(db, request))
                .Concat(BorderImportLicenceIndividualTradingBranch(db, request))
                .Concat(BorderExportPermitBranch(db, request))
                .Concat(BorderImportPermitBranch(db, request));

        return query
            .Where(row =>
                (request.FormType == string.Empty || row.FormType == request.FormType)
                && (request.SakhanId == 0 || row.SakhanId == request.SakhanId))
            .OrderBy(row => row.PaymentDate)
            .ThenBy(row => row.SortOrder)
            .Select(row => new sp_AccountSummaryReportResult
            {
                Id = row.Id,
                VoucherDate = row.VoucherDate,
                PaymentDate = row.PaymentDate,
                CompanyRegistrationNo = row.CompanyRegistrationNo,
                VoucherNo = row.VoucherNo,
                CompanyName = row.CompanyName,
                TransactionTitle = row.TransactionTitle,
                Amount = row.Amount,
                AccountTitleCode = row.AccountTitleCode,
                SortOrder = row.SortOrder,
                SakhanId = row.SakhanId,
                LocationCode = row.LocationCode,
                FormType = row.FormType
            });
    }

    private static IQueryable<AccountPaymentRow> PaymentRows(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from transaction in db.AccountTransactions
            join detail in db.AccountTransactionDetails on transaction.Id equals detail.AccountTransactionId
            join title in db.AccountTitles on detail.AccountTitleId equals title.Id
            where transaction.IsPayment
                && transaction.VoucherDate >= request.FromDate
                && transaction.VoucherDate <= request.ToDate
            select new AccountPaymentRow
            {
                Id = transaction.Id,
                TransactionId = transaction.TransactionId,
                VoucherDate = transaction.VoucherDate,
                PaymentDate = transaction.PaymentDate,
                VoucherNo = transaction.VoucherNo,
                TransactionTitle = title.Description,
                Amount = detail.Amount,
                AccountTitleCode = title.Code,
                SortOrder = title.SortOrder
            };
    }

    private static IQueryable<AccountSummaryRow> MemberBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in db.MemberRegistrations on payment.TransactionId equals registration.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = string.Empty,
                VoucherNo = payment.VoucherNo,
                CompanyName = string.Empty,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = "Member"
            };
    }

    private static IQueryable<AccountSummaryRow> PaThaKaRegistrationBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in db.PaThaKaRegistrations on payment.TransactionId equals registration.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = registration.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = registration.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = "Pa Tha Ka"
            };
    }

    private static IQueryable<AccountSummaryRow> BusinessServiceAgencyBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in db.BusinessServiceAgencyRegistrations on payment.TransactionId equals registration.Id
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = "Business Service Agency"
            };
    }

    private static IQueryable<AccountSummaryRow> DutyFreeShopBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in db.DutyFreeShopRegistrations on payment.TransactionId equals registration.Id
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = "Duty Free Shop"
            };
    }

    private static IQueryable<AccountSummaryRow> ReExportBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in db.ReExportRegistrations on payment.TransactionId equals registration.Id
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = "Re-Export"
            };
    }

    private static IQueryable<AccountSummaryRow> SaleCenterBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in db.SaleCenterRegistrations on payment.TransactionId equals registration.Id
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = registration.RegistrationType
            };
    }

    private static IQueryable<AccountSummaryRow> ShowRoomBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in db.ShowRoomRegistrations on payment.TransactionId equals registration.Id
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = registration.RegistrationType
            };
    }

    private static IQueryable<AccountSummaryRow> EvshowRoomBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in db.EvshowRoomRegistrations on payment.TransactionId equals registration.Id
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = registration.RegistrationType
            };
    }

    private static IQueryable<AccountSummaryRow> EvcycleShowRoomBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in db.EvcycleShowRoomRegistrations on payment.TransactionId equals registration.Id
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = registration.RegistrationType
            };
    }

    private static IQueryable<AccountSummaryRow> WholeSaleRetailBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in db.WholeSaleRetailRegistrations on payment.TransactionId equals registration.Id
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = registration.RegistrationType
            };
    }

    private static IQueryable<AccountSummaryRow> WineImportationBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in db.WineImportationRegistrations on payment.TransactionId equals registration.Id
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = "Wine Imporation"
            };
    }

    private static IQueryable<AccountSummaryRow> DeleteDataNptBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join deleteData in db.DeleteData on payment.TransactionId equals deleteData.TransactionId
            join paThaKa in db.PaThaKas on deleteData.PaThaKaId equals paThaKa.Id
            where deleteData.SakhanId == 0
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = "Import Licence"
            };
    }

    private static IQueryable<AccountSummaryRow> DeleteDataBorderBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join deleteData in db.DeleteData on payment.TransactionId equals deleteData.TransactionId
            join sakhan in db.Sakhans on deleteData.SakhanId equals (int?)sakhan.Id
            join paThaKa in db.PaThaKas on deleteData.PaThaKaId equals paThaKa.Id
            where deleteData.SakhanId != 0
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = sakhan.Id,
                LocationCode = sakhan.Code,
                FormType = "Border Export Licence"
            };
    }

    private static IQueryable<AccountSummaryRow> ExportLicenceBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join licence in db.ExportLicences on payment.TransactionId equals licence.Id
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = "Export Licence"
            };
    }

    private static IQueryable<AccountSummaryRow> ImportLicenceBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join licence in db.ImportLicences on payment.TransactionId equals licence.Id
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = "Import Licence"
            };
    }

    private static IQueryable<AccountSummaryRow> ExportPermitBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join permit in db.ExportPermits on payment.TransactionId equals permit.Id
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = "Export Permit"
            };
    }

    private static IQueryable<AccountSummaryRow> ImportPermitBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join permit in db.ImportPermits on payment.TransactionId equals permit.Id
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = 0,
                LocationCode = "NPT",
                FormType = "Import Permit"
            };
    }

    private static IQueryable<AccountSummaryRow> BorderExportLicencePaThaKaBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join licence in db.BorderExportLicences on payment.TransactionId equals licence.Id
            join sakhan in db.Sakhans on licence.SakhanId equals sakhan.Id
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            where licence.CardType == "Pa Tha Ka"
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = sakhan.Id,
                LocationCode = sakhan.Code,
                FormType = "Border Export Licence"
            };
    }

    private static IQueryable<AccountSummaryRow> BorderExportLicenceIndividualTradingBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join licence in db.BorderExportLicences on payment.TransactionId equals licence.Id
            join sakhan in db.Sakhans on licence.SakhanId equals sakhan.Id
            join individualTrading in db.IndividualTradings on licence.IndividualTradingId equals individualTrading.Id
            where licence.CardType == "Individual Trading"
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = individualTrading.Tinno,
                VoucherNo = payment.VoucherNo,
                CompanyName = individualTrading.Name,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = sakhan.Id,
                LocationCode = sakhan.Code,
                FormType = "Border Export Licence"
            };
    }

    private static IQueryable<AccountSummaryRow> BorderImportLicencePaThaKaBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join licence in db.BorderImportLicences on payment.TransactionId equals licence.Id
            join sakhan in db.Sakhans on licence.SakhanId equals sakhan.Id
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            where licence.CardType == "Pa Tha Ka"
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = sakhan.Id,
                LocationCode = sakhan.Code,
                FormType = "Border Import Licence"
            };
    }

    private static IQueryable<AccountSummaryRow> BorderImportLicenceIndividualTradingBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join licence in db.BorderImportLicences on payment.TransactionId equals licence.Id
            join sakhan in db.Sakhans on licence.SakhanId equals sakhan.Id
            join individualTrading in db.IndividualTradings on licence.IndividualTradingId equals individualTrading.Id
            where licence.CardType == "Individual Trading"
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = individualTrading.Tinno,
                VoucherNo = payment.VoucherNo,
                CompanyName = individualTrading.Name,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = sakhan.Id,
                LocationCode = sakhan.Code,
                FormType = "Border Import Licence"
            };
    }

    private static IQueryable<AccountSummaryRow> BorderExportPermitBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join permit in db.BorderExportPermits on payment.TransactionId equals permit.Id
            join sakhan in db.Sakhans on permit.SakhanId equals sakhan.Id
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = sakhan.Id,
                LocationCode = sakhan.Code,
                FormType = "Border Export Permit"
            };
    }

    private static IQueryable<AccountSummaryRow> BorderImportPermitBranch(
        TradeNetDbContext db,
        sp_AccountSummaryReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join permit in db.BorderImportPermits on payment.TransactionId equals permit.Id
            join sakhan in db.Sakhans on permit.SakhanId equals sakhan.Id
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            select new AccountSummaryRow
            {
                Id = payment.Id,
                VoucherDate = payment.VoucherDate,
                PaymentDate = payment.PaymentDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                VoucherNo = payment.VoucherNo,
                CompanyName = paThaKa.CompanyName,
                TransactionTitle = payment.TransactionTitle,
                Amount = payment.Amount,
                AccountTitleCode = payment.AccountTitleCode,
                SortOrder = payment.SortOrder,
                SakhanId = sakhan.Id,
                LocationCode = sakhan.Code,
                FormType = "Border Import Permit"
            };
    }

    private sealed class AccountPaymentRow
    {
        public string Id { get; set; } = null!;
        public string TransactionId { get; set; } = null!;
        public DateTime? VoucherDate { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string? VoucherNo { get; set; }
        public string TransactionTitle { get; set; } = null!;
        public double Amount { get; set; }
        public string AccountTitleCode { get; set; } = null!;
        public int SortOrder { get; set; }
    }

    private sealed class AccountSummaryRow
    {
        public string Id { get; set; } = null!;
        public DateTime? VoucherDate { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string? CompanyRegistrationNo { get; set; }
        public string? VoucherNo { get; set; }
        public string? CompanyName { get; set; }
        public string TransactionTitle { get; set; } = null!;
        public double Amount { get; set; }
        public string AccountTitleCode { get; set; } = null!;
        public int SortOrder { get; set; }
        public int SakhanId { get; set; }
        public string? LocationCode { get; set; }
        public string FormType { get; set; } = null!;
    }
}
