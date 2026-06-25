using API.DBContext;
using System;
using System.Collections.Generic;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_EVCycleShowRoomRegistrationReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string ApplyType { get; set; } = string.Empty;
    public string RegistrationType { get; set; } = string.Empty;
    public List<string> AllowedFormTypes { get; set; } = new();
}

public sealed class sp_EVCycleShowRoomRegistrationReportResult
{
    public DateTime? Date { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string ShowRoomNo { get; set; } = null!;
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
    public string? ShowRoomUnitLevel { get; set; }
    public string ShowRoomStreetNumberStreetName { get; set; } = null!;
    public string ShowRoomQuarterCityTownship { get; set; } = null!;
    public string ShowRoomState { get; set; } = null!;
    public string ShowRoomCountry { get; set; } = null!;
    public string? ShowRoomPostalCode { get; set; }
    public string? ShowRoomUnitLevel2 { get; set; }
    public string? ShowRoomStreetNumberStreetName2 { get; set; }
    public string? ShowRoomQuarterCityTownship2 { get; set; }
    public string? ShowRoomState2 { get; set; }
    public string? ShowRoomCountry2 { get; set; }
    public string? ShowRoomPostalCode2 { get; set; }
    public string? ShowRoomUnitLevel3 { get; set; }
    public string? ShowRoomStreetNumberStreetName3 { get; set; }
    public string? ShowRoomQuarterCityTownship3 { get; set; }
    public string? ShowRoomState3 { get; set; }
    public string? ShowRoomCountry3 { get; set; }
    public string? ShowRoomPostalCode3 { get; set; }
    public string? ShowRoomUnitLevel4 { get; set; }
    public string? ShowRoomStreetNumberStreetName4 { get; set; }
    public string? ShowRoomQuarterCityTownship4 { get; set; }
    public string? ShowRoomState4 { get; set; }
    public string? ShowRoomCountry4 { get; set; }
    public string? ShowRoomPostalCode4 { get; set; }
    public string? ShowRoomUnitLevel5 { get; set; }
    public string? ShowRoomStreetNumberStreetName5 { get; set; }
    public string? ShowRoomQuarterCityTownship5 { get; set; }
    public string? ShowRoomState5 { get; set; }
    public string? ShowRoomCountry5 { get; set; }
    public string? ShowRoomPostalCode5 { get; set; }
    public string PaymentType { get; set; } = null!;
    public string? VoucherNo { get; set; }
    public DateTime? VoucherDate { get; set; }
    public double TotalAmount { get; set; }
}

public static class sp_EVCycleShowRoomRegistrationReport
{
    private const string Approved = "Approved";
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    public static IQueryable<sp_EVCycleShowRoomRegistrationReportResult> Query(
        TradeNetDbContext db,
        sp_EVCycleShowRoomRegistrationReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from registration in db.EvcycleShowRoomRegistrations
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
               select new sp_EVCycleShowRoomRegistrationReportResult
               {
                   Date = registration.CreatedDate,
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   ShowRoomNo = registration.ShowRoomNo,
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
                   ShowRoomUnitLevel = registration.ShowRoomUnitLevel,
                   ShowRoomStreetNumberStreetName = registration.ShowRoomStreetNumberStreetName,
                   ShowRoomQuarterCityTownship = registration.ShowRoomQuarterCityTownship,
                   ShowRoomState = registration.ShowRoomState,
                   ShowRoomCountry = registration.ShowRoomCountry,
                   ShowRoomPostalCode = registration.ShowRoomPostalCode,
                   ShowRoomUnitLevel2 = registration.ShowRoomUnitLevel2,
                   ShowRoomStreetNumberStreetName2 = registration.ShowRoomStreetNumberStreetName2,
                   ShowRoomQuarterCityTownship2 = registration.ShowRoomQuarterCityTownship2,
                   ShowRoomState2 = registration.ShowRoomState2,
                   ShowRoomCountry2 = registration.ShowRoomCountry2,
                   ShowRoomPostalCode2 = registration.ShowRoomPostalCode2,
                   ShowRoomUnitLevel3 = registration.ShowRoomUnitLevel3,
                   ShowRoomStreetNumberStreetName3 = registration.ShowRoomStreetNumberStreetName3,
                   ShowRoomQuarterCityTownship3 = registration.ShowRoomQuarterCityTownship3,
                   ShowRoomState3 = registration.ShowRoomState3,
                   ShowRoomCountry3 = registration.ShowRoomCountry3,
                   ShowRoomPostalCode3 = registration.ShowRoomPostalCode3,
                   ShowRoomUnitLevel4 = registration.ShowRoomUnitLevel4,
                   ShowRoomStreetNumberStreetName4 = registration.ShowRoomStreetNumberStreetName4,
                   ShowRoomQuarterCityTownship4 = registration.ShowRoomQuarterCityTownship4,
                   ShowRoomState4 = registration.ShowRoomState4,
                   ShowRoomCountry4 = registration.ShowRoomCountry4,
                   ShowRoomPostalCode4 = registration.ShowRoomPostalCode4,
                   ShowRoomUnitLevel5 = registration.ShowRoomUnitLevel5,
                   ShowRoomStreetNumberStreetName5 = registration.ShowRoomStreetNumberStreetName5,
                   ShowRoomQuarterCityTownship5 = registration.ShowRoomQuarterCityTownship5,
                   ShowRoomState5 = registration.ShowRoomState5,
                   ShowRoomCountry5 = registration.ShowRoomCountry5,
                   ShowRoomPostalCode5 = registration.ShowRoomPostalCode5,
                   PaymentType = accountTransaction.PaymentType,
                   VoucherNo = accountTransaction.VoucherNo,
                   VoucherDate = accountTransaction.VoucherDate,
                   TotalAmount = accountTransaction.TotalAmount
               };
    }
}
