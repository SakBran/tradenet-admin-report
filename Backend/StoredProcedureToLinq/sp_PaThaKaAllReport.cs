using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_PaThaKaAllReportRequest
{
    public int BusinessTypeId { get; set; }
    public int LineofBusinessId { get; set; }
    public string State { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class sp_PaThaKaAllReportResult
{
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string OwnerName { get; set; } = null!;
    public string? OwnerNRC { get; set; }
    public DateTime CompanyRegistrationDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BusinessType { get; set; } = null!;
    public string? LineofBusiness { get; set; }
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string Mobile1 { get; set; } = null!;
    public string? Mobile2 { get; set; }
    public string? Mobile3 { get; set; }
    public string? Fax { get; set; }
    public string Email { get; set; } = null!;
    public double? Capital { get; set; }
    public string? Currency { get; set; }
    public int? Terms { get; set; }
    public DateTime DecisionDate { get; set; }
    public string? DecisionName { get; set; }
    public string? DecisionPosition { get; set; }
    public string Status { get; set; } = null!;
    public string? MICPermitNo { get; set; }
}

public static class sp_PaThaKaAllReport
{
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    public static IQueryable<sp_PaThaKaAllReportResult> Query(
        TradeNetDbContext db,
        sp_PaThaKaAllReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from paThaKa in db.PaThaKas
               join businessType in db.BusinessTypes on paThaKa.BusinessTypeId equals businessType.Id
               join lineofBusiness in db.LineofBusinesses on paThaKa.LineofBusinessId equals lineofBusiness.Id
               join currency in db.Currencies on paThaKa.CurrencyId equals (int?)currency.Id
               join cardFees in db.CardRegistrationFees on paThaKa.CardRegistrationFeesId equals cardFees.Id
               join decisionCode in db.DecisionCodes on paThaKa.DecisionCodeId equals decisionCode.Id
               from ownerNrcPrefix in db.Nrcprefixes
                   .Where(prefix => paThaKa.OwnerNrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from ownerNrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => paThaKa.OwnerNrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               where (request.BusinessTypeId == 0 || paThaKa.BusinessTypeId == request.BusinessTypeId)
                   && (request.LineofBusinessId == 0 || paThaKa.LineofBusinessId == request.LineofBusinessId)
                   && (request.Status == string.Empty || paThaKa.Status == request.Status)
                   && (request.State == string.Empty || paThaKa.State == request.State)
               orderby paThaKa.IssuedDate
               select new sp_PaThaKaAllReportResult
               {
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   CompanyName = paThaKa.CompanyName,
                   OwnerName = paThaKa.OwnerName,
                   OwnerNRC = paThaKa.OwnerNrctype == CurrentNrcType && paThaKa.OwnerNrcno != string.Empty
                       ? ownerNrcPrefix.StatePrefix.ToString() + "/" + ownerNrcPrefix.TownshipPrefix + ownerNrcPrefixCode.Code + paThaKa.OwnerNrcno
                       : paThaKa.OwnerNrctype == OldNrcType && paThaKa.OwnerNrcno != string.Empty
                           ? paThaKa.OwnerNrcno
                           : string.Empty,
                   CompanyRegistrationDate = paThaKa.CompanyRegistrationDate,
                   EndDate = paThaKa.EndDate,
                   BusinessType = businessType.Name,
                   LineofBusiness = lineofBusiness.Name,
                   UnitLevel = paThaKa.UnitLevel,
                   StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   QuarterCityTownship = paThaKa.QuarterCityTownship,
                   State = paThaKa.State,
                   Country = paThaKa.Country,
                   PostalCode = paThaKa.PostalCode,
                   Mobile1 = paThaKa.Mobile1,
                   Mobile2 = paThaKa.Mobile2,
                   Mobile3 = paThaKa.Mobile3,
                   Fax = paThaKa.Fax,
                   Email = paThaKa.Email,
                   Capital = paThaKa.Capital,
                   Currency = currency.Code,
                   Terms = cardFees.Terms,
                   DecisionDate = paThaKa.DecisionDate,
                   DecisionName = decisionCode.Name,
                   DecisionPosition = decisionCode.Position,
                   Status = paThaKa.Status,
                   MICPermitNo = paThaKa.MicpermitNo
               };
    }
}
