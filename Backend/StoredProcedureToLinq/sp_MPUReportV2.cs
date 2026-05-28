using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_MPUReportV2Request
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string FormType { get; set; } = string.Empty;
    public string PaymentType { get; set; } = string.Empty;
}

public sealed class sp_MPUReportV2Result
{
    public string Sakhan { get; set; } = null!;
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

public static class sp_MPUReportV2
{
    private const string PaThaKaCardType = "Pa Tha Ka";
    private const string IndividualTradingCardType = "Individual Trading";

    public static IQueryable<sp_MPUReportV2Result> Query(
        TradeNetDbContext db,
        sp_MPUReportV2Request request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var rows = MemberRows(db, request)
            .Concat(PaThaKaRegistrationRows(db, request))
            .Concat(PaThaKaRows(db, request, db.BusinessServiceAgencyRegistrations.Select(row => new PaThaKaMpuV2Registration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                FormType = "Business Service Agency",
                SakhanId = 0
            })))
            .Concat(PaThaKaRows(db, request, db.DutyFreeShopRegistrations.Select(row => new PaThaKaMpuV2Registration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                FormType = "Duty Free Shop",
                SakhanId = 0
            })))
            .Concat(PaThaKaRows(db, request, db.ReExportRegistrations.Select(row => new PaThaKaMpuV2Registration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                FormType = "Re-Export",
                SakhanId = 0
            })))
            .Concat(PaThaKaRows(db, request, db.SaleCenterRegistrations.Select(row => new PaThaKaMpuV2Registration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                FormType = row.RegistrationType,
                SakhanId = 0
            })))
            .Concat(PaThaKaRows(db, request, db.ShowRoomRegistrations.Select(row => new PaThaKaMpuV2Registration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                FormType = row.RegistrationType,
                SakhanId = 0
            })))
            .Concat(PaThaKaRows(db, request, db.WholeSaleRetailRegistrations.Select(row => new PaThaKaMpuV2Registration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                FormType = row.RegistrationType,
                SakhanId = 0
            })))
            .Concat(PaThaKaRows(db, request, db.WineImportationRegistrations.Select(row => new PaThaKaMpuV2Registration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                FormType = "Wine Imporation",
                SakhanId = 0
            })))
            .Concat(PaThaKaRows(db, request, db.ExportLicences.Select(row => new PaThaKaMpuV2Registration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                FormType = "Export Licence",
                SakhanId = 0
            })))
            .Concat(PaThaKaRows(db, request, db.ImportLicences.Select(row => new PaThaKaMpuV2Registration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                FormType = "Import Licence",
                SakhanId = 0,
                RequireVoucherNo = true
            })))
            .Concat(DeleteDataRows(db, request))
            .Concat(PaThaKaRows(db, request, db.ExportPermits.Select(row => new PaThaKaMpuV2Registration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                FormType = "Export Permit",
                SakhanId = 0
            })))
            .Concat(PaThaKaRows(db, request, db.ImportPermits.Select(row => new PaThaKaMpuV2Registration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                FormType = "Import Permit",
                SakhanId = 0
            })))
            .Concat(PaThaKaRows(db, request, db.BorderExportLicences
                .Where(row => row.CardType == PaThaKaCardType)
                .Select(row => new PaThaKaMpuV2Registration
                {
                    Id = row.Id,
                    PaThaKaId = row.PaThaKaId!,
                    FormType = "Border Export Licence",
                    SakhanId = row.SakhanId
                })))
            .Concat(IndividualTradingRows(db, request, db.BorderExportLicences
                .Where(row => row.CardType == IndividualTradingCardType)
                .Select(row => new IndividualTradingMpuV2Registration
                {
                    Id = row.Id,
                    IndividualTradingId = row.IndividualTradingId!,
                    FormType = "Border Export Licence",
                    SakhanId = row.SakhanId
                })))
            .Concat(PaThaKaRows(db, request, db.BorderImportLicences
                .Where(row => row.CardType == PaThaKaCardType)
                .Select(row => new PaThaKaMpuV2Registration
                {
                    Id = row.Id,
                    PaThaKaId = row.PaThaKaId!,
                    FormType = "Border Import Licence",
                    SakhanId = row.SakhanId
                })))
            .Concat(IndividualTradingRows(db, request, db.BorderImportLicences
                .Where(row => row.CardType == IndividualTradingCardType)
                .Select(row => new IndividualTradingMpuV2Registration
                {
                    Id = row.Id,
                    IndividualTradingId = row.IndividualTradingId!,
                    FormType = "Border Import Licence",
                    SakhanId = row.SakhanId
                })))
            .Concat(PaThaKaRows(db, request, db.BorderExportPermits.Select(row => new PaThaKaMpuV2Registration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId!,
                FormType = "Border Export Permit",
                SakhanId = row.SakhanId
            })))
            .Concat(PaThaKaRows(db, request, db.BorderImportPermits.Select(row => new PaThaKaMpuV2Registration
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId!,
                FormType = "Border Import Permit",
                SakhanId = row.SakhanId
            })));

        return
            from row in rows
            join mpu in db.MpupaymentTransactions on row.TransactionId equals mpu.TransactionId into mpuJoin
            from mpu in mpuJoin
                .Where(transaction => transaction.Mocamount == row.TotalAmount.ToString())
                .DefaultIfEmpty()
            where request.FormType == string.Empty || row.FormType == request.FormType
            orderby row.PaymentDate, row.SortOrder
            select new sp_MPUReportV2Result
            {
                Sakhan = db.Sakhans
                    .Where(sakhan => sakhan.Id == row.SakhanId)
                    .Select(sakhan => sakhan.Code)
                    .FirstOrDefault() ?? string.Empty,
                TransactionDateTime = mpu.TransactionDateTime,
                CompanyName = db.PaThaKas
                    .Where(paThaKa => paThaKa.CompanyRegistrationNo == row.CompanyRegistrationNo)
                    .Select(paThaKa => paThaKa.CompanyName)
                    .FirstOrDefault() ?? string.Empty,
                CompanyRegistrationNo = mpu.PaThaKaNo,
                ApplicationNo = mpu.ApplicationNo,
                MerchantId = mpu.MerchantId,
                AccountNo = mpu.AccountNo,
                InvoiceNo = mpu.InvoiceNo,
                ApprovalCode = mpu.ApprovalCode,
                TransactionRefNo = mpu.TransactionRefNo,
                TransactionAmount = mpu.TransactionAmount,
                MOCAmount = mpu.Mocamount,
                IMAmount = mpu.Imamount,
                FormType = mpu.FormType,
                ApplyType = mpu.ApplyType,
                VoucherNo = row.VoucherNo
            };
    }

    private static IQueryable<MpuV2AccountRow> MemberRows(
        TradeNetDbContext db,
        sp_MPUReportV2Request request)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in db.MemberRegistrations on payment.TransactionId equals registration.Id
            select new MpuV2AccountRow
            {
                TransactionId = payment.TransactionId,
                TotalAmount = payment.TotalAmount,
                PaymentDate = payment.PaymentDate,
                VoucherNo = payment.VoucherNo,
                SortOrder = payment.SortOrder,
                CompanyRegistrationNo = string.Empty,
                SakhanId = 0,
                FormType = "Member"
            };
    }

    private static IQueryable<MpuV2AccountRow> PaThaKaRegistrationRows(
        TradeNetDbContext db,
        sp_MPUReportV2Request request)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in db.PaThaKaRegistrations on payment.TransactionId equals registration.Id
            select new MpuV2AccountRow
            {
                TransactionId = payment.TransactionId,
                TotalAmount = payment.TotalAmount,
                PaymentDate = payment.PaymentDate,
                VoucherNo = payment.VoucherNo,
                SortOrder = payment.SortOrder,
                CompanyRegistrationNo = registration.CompanyRegistrationNo,
                SakhanId = 0,
                FormType = "Pa Tha Ka"
            };
    }

    private static IQueryable<MpuV2AccountRow> PaThaKaRows(
        TradeNetDbContext db,
        sp_MPUReportV2Request request,
        IQueryable<PaThaKaMpuV2Registration> registrations)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in registrations on payment.TransactionId equals registration.Id
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            where !registration.RequireVoucherNo || payment.VoucherNo != null
            select new MpuV2AccountRow
            {
                TransactionId = payment.TransactionId,
                TotalAmount = payment.TotalAmount,
                PaymentDate = payment.PaymentDate,
                VoucherNo = payment.VoucherNo,
                SortOrder = payment.SortOrder,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                SakhanId = registration.SakhanId,
                FormType = registration.FormType
            };
    }

    private static IQueryable<MpuV2AccountRow> IndividualTradingRows(
        TradeNetDbContext db,
        sp_MPUReportV2Request request,
        IQueryable<IndividualTradingMpuV2Registration> registrations)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in registrations on payment.TransactionId equals registration.Id
            join individualTrading in db.IndividualTradings on registration.IndividualTradingId equals individualTrading.Id
            select new MpuV2AccountRow
            {
                TransactionId = payment.TransactionId,
                TotalAmount = payment.TotalAmount,
                PaymentDate = payment.PaymentDate,
                VoucherNo = payment.VoucherNo,
                SortOrder = payment.SortOrder,
                CompanyRegistrationNo = individualTrading.Tinno,
                SakhanId = registration.SakhanId,
                FormType = registration.FormType
            };
    }

    private static IQueryable<MpuV2AccountRow> DeleteDataRows(
        TradeNetDbContext db,
        sp_MPUReportV2Request request)
    {
        return
            from payment in PaymentRows(db, request)
            join deleteData in db.DeleteData on payment.TransactionId equals deleteData.Id.ToString()
            join paThaKa in db.PaThaKas on deleteData.PaThaKaId equals paThaKa.Id
            where payment.VoucherNo != null
            select new MpuV2AccountRow
            {
                TransactionId = payment.TransactionId,
                TotalAmount = payment.TotalAmount,
                PaymentDate = payment.PaymentDate,
                VoucherNo = payment.VoucherNo,
                SortOrder = payment.SortOrder,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                SakhanId = 0,
                FormType = "Import Licence"
            };
    }

    private static IQueryable<MpuV2PaymentRow> PaymentRows(
        TradeNetDbContext db,
        sp_MPUReportV2Request request)
    {
        return
            from transaction in db.AccountTransactions
            join detail in db.AccountTransactionDetails on transaction.Id equals detail.AccountTransactionId
            join title in db.AccountTitles on detail.AccountTitleId equals title.Id
            where transaction.IsPayment
                && transaction.VoucherDate >= request.FromDate
                && transaction.VoucherDate <= request.ToDate
            select new MpuV2PaymentRow
            {
                TransactionId = transaction.TransactionId,
                TotalAmount = transaction.TotalAmount,
                PaymentDate = transaction.PaymentDate,
                VoucherNo = transaction.VoucherNo,
                SortOrder = title.SortOrder
            };
    }

    private sealed class MpuV2PaymentRow
    {
        public string TransactionId { get; set; } = null!;
        public double TotalAmount { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string? VoucherNo { get; set; }
        public int SortOrder { get; set; }
    }

    private sealed class MpuV2AccountRow
    {
        public string TransactionId { get; set; } = null!;
        public double TotalAmount { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string? VoucherNo { get; set; }
        public int SortOrder { get; set; }
        public string? CompanyRegistrationNo { get; set; }
        public int SakhanId { get; set; }
        public string FormType { get; set; } = null!;
    }

    private sealed class PaThaKaMpuV2Registration
    {
        public string Id { get; set; } = null!;
        public string PaThaKaId { get; set; } = null!;
        public string FormType { get; set; } = null!;
        public int SakhanId { get; set; }
        public bool RequireVoucherNo { get; set; }
    }

    private sealed class IndividualTradingMpuV2Registration
    {
        public string Id { get; set; } = null!;
        public string IndividualTradingId { get; set; } = null!;
        public string FormType { get; set; } = null!;
        public int SakhanId { get; set; }
    }
}
