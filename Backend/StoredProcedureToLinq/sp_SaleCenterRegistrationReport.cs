using API.DBContext;
using System;
using System.Collections.Generic;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_SaleCenterRegistrationReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string ApplyType { get; set; } = string.Empty;
    public string RegistrationType { get; set; } = string.Empty;
    public List<string> AllowedFormTypes { get; set; } = new();
}

public sealed class sp_SaleCenterRegistrationReportResult
{
    public DateTime? Date { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string SaleCenterNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string Name { get; set; } = null!;
    public string? NRCNo { get; set; }
    public string? BusinessServiceAgencyNo { get; set; }
    public string? SaleCenterUnitLevel { get; set; }
    public string SaleCenterStreetNumberStreetName { get; set; } = null!;
    public string SaleCenterQuarterCityTownship { get; set; } = null!;
    public string SaleCenterState { get; set; } = null!;
    public string SaleCenterCountry { get; set; } = null!;
    public string? SaleCenterPostalCode { get; set; }
    public string PaymentType { get; set; } = null!;
    public string? VoucherNo { get; set; }
    public DateTime? VoucherDate { get; set; }
    public double TotalAmount { get; set; }
}

public static class sp_SaleCenterRegistrationReport
{
    private const string Approved = "Approved";
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    public static IQueryable<sp_SaleCenterRegistrationReportResult> Query(
        TradeNetDbContext db,
        sp_SaleCenterRegistrationReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from registration in db.SaleCenterRegistrations
               join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
               join accountTransaction in db.AccountTransactions on registration.Id equals accountTransaction.TransactionId
               from businessServiceAgency in db.BusinessServiceAgencies
                   .Where(agency => registration.BusinessServiceAgencyId == agency.Id)
                   .DefaultIfEmpty()
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
                   && request.AllowedFormTypes.Contains(registration.RegistrationType)
               select new sp_SaleCenterRegistrationReportResult
               {
                   Date = registration.CreatedDate,
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   SaleCenterNo = registration.SaleCenterNo,
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
                   BusinessServiceAgencyNo = registration.BusinessServiceAgencyId == string.Empty
                       ? string.Empty
                       : businessServiceAgency.BusinessServiceAgencyNo,
                   SaleCenterUnitLevel = registration.SaleCenterUnitLevel,
                   SaleCenterStreetNumberStreetName = registration.SaleCenterStreetNumberStreetName,
                   SaleCenterQuarterCityTownship = registration.SaleCenterQuarterCityTownship,
                   SaleCenterState = registration.SaleCenterState,
                   SaleCenterCountry = registration.SaleCenterCountry,
                   SaleCenterPostalCode = registration.SaleCenterPostalCode,
                   PaymentType = accountTransaction.PaymentType,
                   VoucherNo = accountTransaction.VoucherNo,
                   VoucherDate = accountTransaction.VoucherDate,
                   TotalAmount = accountTransaction.TotalAmount
               };
    }
}
