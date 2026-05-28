using API.DBContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_OnlineFeesReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string FormType { get; set; } = string.Empty;
    public int SakhanId { get; set; }
}

public sealed class sp_OnlineFeesReportResult
{
    public int SakhanId { get; set; }
    public DateTime? VoucherDate { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string FormType { get; set; } = null!;
    public double Amount { get; set; }
    public string Remark { get; set; } = null!;
}

public static class sp_OnlineFeesReport
{
    private const string OnlineFees = "Online Fees";

    public static IQueryable<sp_OnlineFeesReportResult> Query(
        TradeNetDbContext db,
        sp_OnlineFeesReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return BranchRows(db, request)
            .Where(row => (request.FormType == string.Empty || EF.Functions.Like(row.FormType, request.FormType + "%"))
                && (request.SakhanId == 0 || row.SakhanId == request.SakhanId))
            .OrderBy(row => row.VoucherDate)
            .ThenBy(row => row.FormType);
    }

    private static IQueryable<sp_OnlineFeesReportResult> BranchRows(
        TradeNetDbContext db,
        sp_OnlineFeesReportRequest request)
    {
        return
            (from fee in OnlineFeeRows(db, request)
             join registration in db.MemberRegistrations on fee.TransactionId equals registration.Id
             select new sp_OnlineFeesReportResult
             {
                 SakhanId = 0,
                 VoucherDate = fee.VoucherDate,
                 CompanyRegistrationNo = registration.ApplicationNo,
                 CompanyName = string.Empty,
                 FormType = fee.FormType,
                 Amount = fee.Amount,
                 Remark = string.Empty
             })
            .Concat(PaThaKaRegistrationRows(db, request))
            .Concat(PaThaKaBranchRows(db, request, db.BusinessServiceAgencyRegistrations.Select(row => new PaThaKaOnlineFeeRegistration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                ApplicationNo = row.ApplicationNo,
                SakhanId = 0
            })))
            .Concat(PaThaKaBranchRows(db, request, db.DutyFreeShopRegistrations.Select(row => new PaThaKaOnlineFeeRegistration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                ApplicationNo = row.ApplicationNo,
                SakhanId = 0
            })))
            .Concat(PaThaKaBranchRows(db, request, db.ReExportRegistrations.Select(row => new PaThaKaOnlineFeeRegistration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                ApplicationNo = row.ApplicationNo,
                SakhanId = 0
            })))
            .Concat(PaThaKaBranchRows(db, request, db.SaleCenterRegistrations.Select(row => new PaThaKaOnlineFeeRegistration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                ApplicationNo = row.ApplicationNo,
                SakhanId = 0
            })))
            .Concat(PaThaKaBranchRows(db, request, db.ShowRoomRegistrations.Select(row => new PaThaKaOnlineFeeRegistration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                ApplicationNo = row.ApplicationNo,
                SakhanId = 0
            })))
            .Concat(PaThaKaBranchRows(db, request, db.WholeSaleRetailRegistrations.Select(row => new PaThaKaOnlineFeeRegistration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                ApplicationNo = row.ApplicationNo,
                SakhanId = 0
            })))
            .Concat(PaThaKaBranchRows(db, request, db.WineImportationRegistrations.Select(row => new PaThaKaOnlineFeeRegistration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                ApplicationNo = row.ApplicationNo,
                SakhanId = 0
            })))
            .Concat(PaThaKaBranchRows(db, request, db.BorderExportLicences.Select(row => new PaThaKaOnlineFeeRegistration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId!,
                ApplicationNo = row.ApplicationNo,
                SakhanId = row.SakhanId
            })))
            .Concat(PaThaKaBranchRows(db, request, db.BorderExportPermits.Select(row => new PaThaKaOnlineFeeRegistration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId!,
                ApplicationNo = row.ApplicationNo,
                SakhanId = row.SakhanId
            })))
            .Concat(PaThaKaBranchRows(db, request, db.BorderImportLicences.Select(row => new PaThaKaOnlineFeeRegistration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId!,
                ApplicationNo = row.ApplicationNo,
                SakhanId = row.SakhanId
            })))
            .Concat(PaThaKaBranchRows(db, request, db.BorderImportPermits.Select(row => new PaThaKaOnlineFeeRegistration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId!,
                ApplicationNo = row.ApplicationNo,
                SakhanId = row.SakhanId
            })))
            .Concat(PaThaKaBranchRows(db, request, db.ExportLicences.Select(row => new PaThaKaOnlineFeeRegistration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                ApplicationNo = row.ApplicationNo,
                SakhanId = 0
            })))
            .Concat(PaThaKaBranchRows(db, request, db.ExportPermits.Select(row => new PaThaKaOnlineFeeRegistration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                ApplicationNo = row.ApplicationNo,
                SakhanId = 0
            })))
            .Concat(PaThaKaBranchRows(db, request, db.ImportLicences.Select(row => new PaThaKaOnlineFeeRegistration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                ApplicationNo = row.ApplicationNo,
                SakhanId = 0
            })))
            .Concat(PaThaKaBranchRows(db, request, db.ImportPermits.Select(row => new PaThaKaOnlineFeeRegistration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                ApplicationNo = row.ApplicationNo,
                SakhanId = 0
            })));
    }

    private static IQueryable<sp_OnlineFeesReportResult> PaThaKaRegistrationRows(
        TradeNetDbContext db,
        sp_OnlineFeesReportRequest request)
    {
        return
            from fee in OnlineFeeRows(db, request)
            join registration in db.PaThaKaRegistrations on fee.TransactionId equals registration.Id
            select new sp_OnlineFeesReportResult
            {
                SakhanId = 0,
                VoucherDate = fee.VoucherDate,
                CompanyRegistrationNo = registration.CompanyRegistrationNo + "@" + registration.ApplicationNo,
                CompanyName = registration.CompanyName,
                FormType = fee.FormType,
                Amount = fee.Amount,
                Remark = string.Empty
            };
    }

    private static IQueryable<sp_OnlineFeesReportResult> PaThaKaBranchRows(
        TradeNetDbContext db,
        sp_OnlineFeesReportRequest request,
        IQueryable<PaThaKaOnlineFeeRegistration> registrations)
    {
        return
            from fee in OnlineFeeRows(db, request)
            join registration in registrations on fee.TransactionId equals registration.Id
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            select new sp_OnlineFeesReportResult
            {
                SakhanId = registration.SakhanId,
                VoucherDate = fee.VoucherDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo + "@" + registration.ApplicationNo,
                CompanyName = paThaKa.CompanyName,
                FormType = fee.FormType,
                Amount = fee.Amount,
                Remark = string.Empty
            };
    }

    private static IQueryable<OnlineFeeRow> OnlineFeeRows(
        TradeNetDbContext db,
        sp_OnlineFeesReportRequest request)
    {
        return
            from accountTransaction in db.AccountTransactions
            join detail in db.AccountTransactionDetails on accountTransaction.Id equals detail.AccountTransactionId
            join title in db.AccountTitles on detail.AccountTitleId equals title.Id
            where accountTransaction.IsPayment
                && title.FormType == OnlineFees
                && accountTransaction.VoucherDate >= request.FromDate
                && accountTransaction.VoucherDate <= request.ToDate
            select new OnlineFeeRow
            {
                TransactionId = accountTransaction.TransactionId,
                VoucherDate = accountTransaction.VoucherDate,
                FormType = accountTransaction.TransactionFormType,
                Amount = detail.Amount
            };
    }

    private sealed class OnlineFeeRow
    {
        public string TransactionId { get; set; } = null!;
        public DateTime? VoucherDate { get; set; }
        public string FormType { get; set; } = null!;
        public double Amount { get; set; }
    }

    private sealed class PaThaKaOnlineFeeRegistration
    {
        public string Id { get; set; } = null!;
        public string PaThaKaId { get; set; } = null!;
        public string ApplicationNo { get; set; } = null!;
        public int SakhanId { get; set; }
    }
}
