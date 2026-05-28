using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_DutyFreeShopRegistrationReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string ApplyType { get; set; } = string.Empty;
}

public sealed class sp_DutyFreeShopRegistrationReportResult
{
    public DateTime? Date { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string DutyFreeShopNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string Name { get; set; } = null!;
    public string? NRCNo { get; set; }
    public string? DutyFreeShopUnitLevel { get; set; }
    public string DutyFreeShopStreetNumberStreetName { get; set; } = null!;
    public string DutyFreeShopQuarterCityTownship { get; set; } = null!;
    public string DutyFreeShopState { get; set; } = null!;
    public string DutyFreeShopCountry { get; set; } = null!;
    public string? DutyFreeShopPostalCode { get; set; }
    public string PaymentType { get; set; } = null!;
    public string? VoucherNo { get; set; }
    public DateTime? VoucherDate { get; set; }
    public double TotalAmount { get; set; }
}

public static class sp_DutyFreeShopRegistrationReport
{
    private const string Approved = "Approved";
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    public static IQueryable<sp_DutyFreeShopRegistrationReportResult> Query(
        TradeNetDbContext db,
        sp_DutyFreeShopRegistrationReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from registration in db.DutyFreeShopRegistrations
               join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
               join accountTransaction in db.AccountTransactions on registration.Id equals accountTransaction.TransactionId
               from nrcPrefix in db.Nrcprefixes
                   .Where(prefix => registration.NrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from nrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => registration.NrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               where registration.ApplyType == request.ApplyType
                   && registration.Status == Approved
                   && accountTransaction.IsPayment
                   && (request.PaymentType == string.Empty || accountTransaction.PaymentType == request.PaymentType)
                   && registration.CreatedDate >= request.FromDate
                   && registration.CreatedDate <= request.ToDate
               select new sp_DutyFreeShopRegistrationReportResult
               {
                   Date = registration.CreatedDate,
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   DutyFreeShopNo = registration.DutyFreeShopNo,
                   CompanyName = paThaKa.CompanyName,
                   UnitLevel = paThaKa.UnitLevel,
                   StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   QuarterCityTownship = paThaKa.QuarterCityTownship,
                   State = paThaKa.State,
                   Country = paThaKa.Country,
                   PostalCode = paThaKa.PostalCode,
                   Name = registration.Name,
                   NRCNo = registration.Nrctype == CurrentNrcType && registration.Nrcno != string.Empty
                       ? nrcPrefix.StatePrefix.ToString() + "/" + nrcPrefix.TownshipPrefix + nrcPrefixCode.Code + registration.Nrcno
                       : registration.Nrctype == OldNrcType && registration.Nrcno != string.Empty
                           ? registration.Nrcno
                           : string.Empty,
                   DutyFreeShopUnitLevel = registration.LocationUnitLevel,
                   DutyFreeShopStreetNumberStreetName = registration.LocationStreetNumberStreetName,
                   DutyFreeShopQuarterCityTownship = registration.LocationQuarterCityTownship,
                   DutyFreeShopState = registration.LocationState,
                   DutyFreeShopCountry = registration.LocationCountry,
                   DutyFreeShopPostalCode = registration.LocationPostalCode,
                   PaymentType = accountTransaction.PaymentType,
                   VoucherNo = accountTransaction.VoucherNo,
                   VoucherDate = accountTransaction.VoucherDate,
                   TotalAmount = accountTransaction.TotalAmount
               };
    }
}
