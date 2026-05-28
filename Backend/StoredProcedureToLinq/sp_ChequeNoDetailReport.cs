using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_ChequeNoDetailReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int ChequeNoId { get; set; }
}

public sealed class sp_ChequeNoDetailReportResult
{
    public string TransactionId { get; set; } = null!;
    public string FormType { get; set; } = null!;
    public string? ChequeNo { get; set; }
    public string? SDate { get; set; }
    public string? TransactionRefNo { get; set; }
    public DateTime? TransactionDateTime { get; set; }
    public string CardNo { get; set; } = null!;
    public string? PaThaKaNo { get; set; }
    public string CompanyName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string? StreetNumberStreetName { get; set; }
    public string? QuarterCityTownship { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public double Amount { get; set; }
}

public static class sp_ChequeNoDetailReport
{
    private const string PaThaKaCardType = "Pa Tha Ka";
    private const string IndividualTradingCardType = "Individual Trading";

    public static IQueryable<sp_ChequeNoDetailReportResult> Query(
        TradeNetDbContext db,
        sp_ChequeNoDetailReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return MemberRows(db, request)
            .Concat(PaThaKaRegistrationRows(db, request))
            .Concat(PaThaKaRows(db, request, db.BusinessServiceAgencyRegistrations.Select(row => new PaThaKaChequeRegistration
            {
                Id = row.Id,
                FormType = "Business Service Agency",
                CardNo = row.BusinessServiceAgencyNo,
                PaThaKaId = row.PaThaKaId!
            })))
            .Concat(PaThaKaRows(db, request, db.DutyFreeShopRegistrations.Select(row => new PaThaKaChequeRegistration
            {
                Id = row.Id,
                FormType = "Duty Free Shop",
                CardNo = row.DutyFreeShopNo,
                PaThaKaId = row.PaThaKaId!
            })))
            .Concat(PaThaKaRows(db, request, db.ReExportRegistrations.Select(row => new PaThaKaChequeRegistration
            {
                Id = row.Id,
                FormType = "Re-Export",
                CardNo = row.ReExportNo,
                PaThaKaId = row.PaThaKaId
            })))
            .Concat(PaThaKaRows(db, request, db.SaleCenterRegistrations.Select(row => new PaThaKaChequeRegistration
            {
                Id = row.Id,
                FormType = row.RegistrationType,
                CardNo = row.SaleCenterNo,
                PaThaKaId = row.PaThaKaId
            })))
            .Concat(PaThaKaRows(db, request, db.ShowRoomRegistrations.Select(row => new PaThaKaChequeRegistration
            {
                Id = row.Id,
                FormType = row.RegistrationType,
                CardNo = row.ShowRoomNo,
                PaThaKaId = row.PaThaKaId
            })))
            .Concat(PaThaKaRows(db, request, db.WholeSaleRetailRegistrations.Select(row => new PaThaKaChequeRegistration
            {
                Id = row.Id,
                FormType = row.RegistrationType,
                CardNo = row.WholeSaleRetailNo,
                PaThaKaId = row.PaThaKaId
            })))
            .Concat(PaThaKaRows(db, request, db.WineImportationRegistrations.Select(row => new PaThaKaChequeRegistration
            {
                Id = row.Id,
                FormType = "Wine Importation",
                CardNo = row.WineImportationNo,
                PaThaKaId = row.PaThaKaId
            })))
            .Concat(PaThaKaRows(db, request, db.ExportLicences.Select(row => new PaThaKaChequeRegistration
            {
                Id = row.Id,
                FormType = "Export Licence",
                CardNo = row.ExportLicenceNo,
                PaThaKaId = row.PaThaKaId
            })))
            .Concat(PaThaKaRows(db, request, db.ImportLicences.Select(row => new PaThaKaChequeRegistration
            {
                Id = row.Id,
                FormType = "Import Licence",
                CardNo = row.ImportLicenceNo,
                PaThaKaId = row.PaThaKaId
            })))
            .Concat(PaThaKaRows(db, request, db.ExportPermits.Select(row => new PaThaKaChequeRegistration
            {
                Id = row.Id,
                FormType = "Export Permit",
                CardNo = row.ExportPermitNo,
                PaThaKaId = row.PaThaKaId!
            })))
            .Concat(PaThaKaRows(db, request, db.ImportPermits.Select(row => new PaThaKaChequeRegistration
            {
                Id = row.Id,
                FormType = "Import Permit",
                CardNo = row.ImportPermitNo,
                PaThaKaId = row.PaThaKaId!
            })))
            .Concat(PaThaKaRows(db, request, db.BorderExportLicences
                .Where(row => row.CardType == PaThaKaCardType)
                .Select(row => new PaThaKaChequeRegistration
                {
                    Id = row.Id,
                    FormType = "Border Export Licence",
                    CardNo = row.ExportLicenceNo,
                    PaThaKaId = row.PaThaKaId!
                })))
            .Concat(IndividualTradingRows(db, request, db.BorderExportLicences
                .Where(row => row.CardType == IndividualTradingCardType)
                .Select(row => new IndividualTradingChequeRegistration
                {
                    Id = row.Id,
                    FormType = "Border Export Licence",
                    CardNo = row.ExportLicenceNo,
                    IndividualTradingId = row.IndividualTradingId!
                })))
            .Concat(PaThaKaRows(db, request, db.BorderExportPermits.Select(row => new PaThaKaChequeRegistration
            {
                Id = row.Id,
                FormType = "Border Export Permit",
                CardNo = row.ExportPermitNo,
                PaThaKaId = row.PaThaKaId!
            })))
            .Concat(PaThaKaRows(db, request, db.BorderImportLicences
                .Where(row => row.CardType == PaThaKaCardType)
                .Select(row => new PaThaKaChequeRegistration
                {
                    Id = row.Id,
                    FormType = "Border Import Licence",
                    CardNo = row.ImportLicenceNo,
                    PaThaKaId = row.PaThaKaId!
                })))
            .Concat(IndividualTradingRows(db, request, db.BorderImportLicences
                .Where(row => row.CardType == IndividualTradingCardType)
                .Select(row => new IndividualTradingChequeRegistration
                {
                    Id = row.Id,
                    FormType = "Border Import Licence",
                    CardNo = row.ImportLicenceNo,
                    IndividualTradingId = row.IndividualTradingId!
                })))
            .Concat(PaThaKaRows(db, request, db.BorderImportPermits.Select(row => new PaThaKaChequeRegistration
            {
                Id = row.Id,
                FormType = "Border Import Permit",
                CardNo = row.ImportPermitNo,
                PaThaKaId = row.PaThaKaId!
            })))
            .OrderBy(row => row.TransactionDateTime);
    }

    private static IQueryable<sp_ChequeNoDetailReportResult> MemberRows(
        TradeNetDbContext db,
        sp_ChequeNoDetailReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in db.MemberRegistrations on payment.TransactionId equals registration.Id
            select new sp_ChequeNoDetailReportResult
            {
                TransactionId = payment.TransactionId,
                FormType = "Member",
                ChequeNo = payment.ChequeNo,
                SDate = payment.VoucherDate == null
                    ? null
                    : (payment.VoucherDate.Value.Day < 10 ? "0" : string.Empty)
                    + payment.VoucherDate.Value.Day.ToString()
                    + "/"
                    + (payment.VoucherDate.Value.Month < 10 ? "0" : string.Empty)
                    + payment.VoucherDate.Value.Month.ToString()
                    + "/"
                    + payment.VoucherDate.Value.Year.ToString(),
                TransactionRefNo = payment.TransactionRefNo,
                TransactionDateTime = payment.TransactionDateTime,
                CardNo = registration.MemberCode,
                PaThaKaNo = "-",
                CompanyName = "-",
                UnitLevel = string.Empty,
                StreetNumberStreetName = string.Empty,
                QuarterCityTownship = string.Empty,
                State = string.Empty,
                Country = string.Empty,
                PostalCode = string.Empty,
                Amount = payment.Amount
            };
    }

    private static IQueryable<sp_ChequeNoDetailReportResult> PaThaKaRegistrationRows(
        TradeNetDbContext db,
        sp_ChequeNoDetailReportRequest request)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in db.PaThaKaRegistrations on payment.TransactionId equals registration.Id
            select new sp_ChequeNoDetailReportResult
            {
                TransactionId = payment.TransactionId,
                FormType = "Pa Tha Ka",
                ChequeNo = payment.ChequeNo,
                SDate = payment.VoucherDate == null
                    ? null
                    : (payment.VoucherDate.Value.Day < 10 ? "0" : string.Empty)
                    + payment.VoucherDate.Value.Day.ToString()
                    + "/"
                    + (payment.VoucherDate.Value.Month < 10 ? "0" : string.Empty)
                    + payment.VoucherDate.Value.Month.ToString()
                    + "/"
                    + payment.VoucherDate.Value.Year.ToString(),
                TransactionRefNo = payment.TransactionRefNo,
                TransactionDateTime = payment.TransactionDateTime,
                CardNo = registration.PaThaKaNo,
                PaThaKaNo = registration.CompanyRegistrationNo,
                CompanyName = registration.CompanyName,
                UnitLevel = registration.UnitLevel,
                StreetNumberStreetName = registration.StreetNumberStreetName,
                QuarterCityTownship = registration.QuarterCityTownship,
                State = registration.State,
                Country = registration.Country,
                PostalCode = registration.PostalCode,
                Amount = payment.Amount
            };
    }

    private static IQueryable<sp_ChequeNoDetailReportResult> PaThaKaRows(
        TradeNetDbContext db,
        sp_ChequeNoDetailReportRequest request,
        IQueryable<PaThaKaChequeRegistration> registrations)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in registrations on payment.TransactionId equals registration.Id
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            select new sp_ChequeNoDetailReportResult
            {
                TransactionId = payment.TransactionId,
                FormType = registration.FormType,
                ChequeNo = payment.ChequeNo,
                SDate = payment.VoucherDate == null
                    ? null
                    : (payment.VoucherDate.Value.Day < 10 ? "0" : string.Empty)
                    + payment.VoucherDate.Value.Day.ToString()
                    + "/"
                    + (payment.VoucherDate.Value.Month < 10 ? "0" : string.Empty)
                    + payment.VoucherDate.Value.Month.ToString()
                    + "/"
                    + payment.VoucherDate.Value.Year.ToString(),
                TransactionRefNo = payment.TransactionRefNo,
                TransactionDateTime = payment.TransactionDateTime,
                CardNo = registration.CardNo,
                PaThaKaNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                Amount = payment.Amount
            };
    }

    private static IQueryable<sp_ChequeNoDetailReportResult> IndividualTradingRows(
        TradeNetDbContext db,
        sp_ChequeNoDetailReportRequest request,
        IQueryable<IndividualTradingChequeRegistration> registrations)
    {
        return
            from payment in PaymentRows(db, request)
            join registration in registrations on payment.TransactionId equals registration.Id
            join individualTrading in db.IndividualTradings on registration.IndividualTradingId equals individualTrading.Id
            select new sp_ChequeNoDetailReportResult
            {
                TransactionId = payment.TransactionId,
                FormType = registration.FormType,
                ChequeNo = payment.ChequeNo,
                SDate = payment.VoucherDate == null
                    ? null
                    : (payment.VoucherDate.Value.Day < 10 ? "0" : string.Empty)
                    + payment.VoucherDate.Value.Day.ToString()
                    + "/"
                    + (payment.VoucherDate.Value.Month < 10 ? "0" : string.Empty)
                    + payment.VoucherDate.Value.Month.ToString()
                    + "/"
                    + payment.VoucherDate.Value.Year.ToString(),
                TransactionRefNo = payment.TransactionRefNo,
                TransactionDateTime = payment.TransactionDateTime,
                CardNo = registration.CardNo,
                PaThaKaNo = individualTrading.Tinno,
                CompanyName = individualTrading.Name,
                UnitLevel = individualTrading.UnitLevel,
                StreetNumberStreetName = individualTrading.StreetNumberStreetName,
                QuarterCityTownship = individualTrading.QuarterCityTownship,
                State = individualTrading.State,
                Country = individualTrading.Country,
                PostalCode = individualTrading.PostalCode,
                Amount = payment.Amount
            };
    }

    private static IQueryable<ChequePaymentRow> PaymentRows(
        TradeNetDbContext db,
        sp_ChequeNoDetailReportRequest request)
    {
        return
            from transaction in db.AccountTransactions
            join detail in db.AccountTransactionDetails on transaction.Id equals detail.AccountTransactionId
            join title in db.AccountTitles on detail.AccountTitleId equals title.Id
            join chequeNo in db.ChequeNos on title.ChequeNoId equals chequeNo.Id
            join mpu in db.MpupaymentTransactions on transaction.TransactionId equals mpu.TransactionId
            where transaction.IsPayment
                && mpu.ResponseCode == "00"
                && chequeNo.Id == request.ChequeNoId
                && transaction.VoucherDate >= request.FromDate
                && transaction.VoucherDate <= request.ToDate
            select new ChequePaymentRow
            {
                TransactionId = transaction.TransactionId,
                ChequeNo = chequeNo.Code,
                VoucherDate = transaction.VoucherDate,
                TransactionRefNo = mpu.TransactionRefNo,
                TransactionDateTime = mpu.TransactionDateTime,
                Amount = detail.Amount
            };
    }

    private sealed class ChequePaymentRow
    {
        public string TransactionId { get; set; } = null!;
        public string? ChequeNo { get; set; }
        public DateTime? VoucherDate { get; set; }
        public string? TransactionRefNo { get; set; }
        public DateTime? TransactionDateTime { get; set; }
        public double Amount { get; set; }
    }

    private sealed class PaThaKaChequeRegistration
    {
        public string Id { get; set; } = null!;
        public string FormType { get; set; } = null!;
        public string CardNo { get; set; } = null!;
        public string PaThaKaId { get; set; } = null!;
    }

    private sealed class IndividualTradingChequeRegistration
    {
        public string Id { get; set; } = null!;
        public string FormType { get; set; } = null!;
        public string CardNo { get; set; } = null!;
        public string IndividualTradingId { get; set; } = null!;
    }
}
