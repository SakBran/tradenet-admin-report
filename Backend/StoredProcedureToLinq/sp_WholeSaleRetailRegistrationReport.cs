using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_WholeSaleRetailRegistrationReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string ApplyType { get; set; } = string.Empty;
    public string RegistrationType { get; set; } = string.Empty;
}

public sealed class sp_WholeSaleRetailRegistrationReportResult
{
    public DateTime? Date { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string WholeSaleRetailNo { get; set; } = null!;
    public string WholeSalRetailName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string? WholeSaleRetailUnitLevel { get; set; }
    public string WholeSaleRetailStreetNumberStreetName { get; set; } = null!;
    public string WholeSaleRetailQuarterCityTownship { get; set; } = null!;
    public string WholeSaleRetailState { get; set; } = null!;
    public string WholeSaleRetailCountry { get; set; } = null!;
    public string? WholeSaleRetailPostalCode { get; set; }
    public string PaymentType { get; set; } = null!;
    public string? VoucherNo { get; set; }
    public DateTime? VoucherDate { get; set; }
    public double TotalAmount { get; set; }
}

public static class sp_WholeSaleRetailRegistrationReport
{
    private const string Approved = "Approved";

    public static IQueryable<sp_WholeSaleRetailRegistrationReportResult> Query(
        TradeNetDbContext db,
        sp_WholeSaleRetailRegistrationReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return DirectPaymentRows(db, request)
            .Concat(PaThaKaPaymentRows(db, request))
            .OrderBy(row => row.Date);
    }

    private static IQueryable<sp_WholeSaleRetailRegistrationReportResult> DirectPaymentRows(
        TradeNetDbContext db,
        sp_WholeSaleRetailRegistrationReportRequest request)
    {
        return from registration in db.WholeSaleRetailRegistrations
               join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
               join accountTransaction in db.AccountTransactions on registration.Id equals accountTransaction.TransactionId
               where registration.ApplyType == request.ApplyType
                   && registration.Status == Approved
                   && accountTransaction.IsPayment
                   && (request.PaymentType == string.Empty || accountTransaction.PaymentType == request.PaymentType)
                   && registration.CreatedDate >= request.FromDate
                   && registration.CreatedDate <= request.ToDate
                   && registration.RegistrationType == request.RegistrationType
               select new sp_WholeSaleRetailRegistrationReportResult
               {
                   Date = registration.CreatedDate,
                   CompanyRegistrationNo = registration.CompanyRegistrationNo,
                   CompanyName = registration.CompanyName,
                   WholeSaleRetailNo = registration.WholeSaleRetailNo,
                   WholeSalRetailName = registration.Name,
                   UnitLevel = paThaKa.UnitLevel,
                   StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   QuarterCityTownship = paThaKa.QuarterCityTownship,
                   State = paThaKa.State,
                   Country = paThaKa.Country,
                   PostalCode = paThaKa.PostalCode,
                   WholeSaleRetailUnitLevel = registration.WholeSaleRetailUnitLevel,
                   WholeSaleRetailStreetNumberStreetName = registration.WholeSaleRetailStreetNumberStreetName,
                   WholeSaleRetailQuarterCityTownship = registration.WholeSaleRetailQuarterCityTownship,
                   WholeSaleRetailState = registration.WholeSaleRetailState,
                   WholeSaleRetailCountry = registration.WholeSaleRetailCountry,
                   WholeSaleRetailPostalCode = registration.WholeSaleRetailPostalCode,
                   PaymentType = accountTransaction.PaymentType,
                   VoucherNo = accountTransaction.VoucherNo,
                   VoucherDate = accountTransaction.VoucherDate,
                   TotalAmount = accountTransaction.TotalAmount
               };
    }

    private static IQueryable<sp_WholeSaleRetailRegistrationReportResult> PaThaKaPaymentRows(
        TradeNetDbContext db,
        sp_WholeSaleRetailRegistrationReportRequest request)
    {
        return from registration in db.WholeSaleRetailRegistrations
               join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
               join paThaKaRegistration in db.PaThaKaRegistrations
                   on registration.Id equals paThaKaRegistration.WholeSaleRetailRegistrationId
               join accountTransaction in db.AccountTransactions on paThaKaRegistration.Id equals accountTransaction.TransactionId
               where paThaKaRegistration.IsWholeSale
                   && registration.ApplyType == request.ApplyType
                   && registration.Status == Approved
                   && accountTransaction.IsPayment
                   && (request.PaymentType == string.Empty || accountTransaction.PaymentType == request.PaymentType)
                   && registration.CreatedDate >= request.FromDate
                   && registration.CreatedDate <= request.ToDate
                   && registration.RegistrationType == request.RegistrationType
               select new sp_WholeSaleRetailRegistrationReportResult
               {
                   Date = registration.CreatedDate,
                   CompanyRegistrationNo = registration.CompanyRegistrationNo,
                   CompanyName = registration.CompanyName,
                   WholeSaleRetailNo = registration.WholeSaleRetailNo,
                   WholeSalRetailName = registration.Name,
                   UnitLevel = paThaKa.UnitLevel,
                   StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   QuarterCityTownship = paThaKa.QuarterCityTownship,
                   State = paThaKa.State,
                   Country = paThaKa.Country,
                   PostalCode = paThaKa.PostalCode,
                   WholeSaleRetailUnitLevel = registration.WholeSaleRetailUnitLevel,
                   WholeSaleRetailStreetNumberStreetName = registration.WholeSaleRetailStreetNumberStreetName,
                   WholeSaleRetailQuarterCityTownship = registration.WholeSaleRetailQuarterCityTownship,
                   WholeSaleRetailState = registration.WholeSaleRetailState,
                   WholeSaleRetailCountry = registration.WholeSaleRetailCountry,
                   WholeSaleRetailPostalCode = registration.WholeSaleRetailPostalCode,
                   PaymentType = accountTransaction.PaymentType,
                   VoucherNo = accountTransaction.VoucherNo,
                   VoucherDate = accountTransaction.VoucherDate,
                   TotalAmount = accountTransaction.TotalAmount
               };
    }
}
