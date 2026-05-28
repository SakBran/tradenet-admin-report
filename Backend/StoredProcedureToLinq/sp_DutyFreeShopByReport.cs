using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_DutyFreeShopByReportRequest
{
    public string CompanyRegistrationNo { get; set; } = string.Empty;
}

public sealed class sp_DutyFreeShopByReportResult
{
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
    public DateTime IssuedDate { get; set; }
    public DateTime EndDate { get; set; }
}

public static class sp_DutyFreeShopByReport
{
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    public static IQueryable<sp_DutyFreeShopByReportResult> Query(
        TradeNetDbContext db,
        sp_DutyFreeShopByReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from dutyFreeShop in db.DutyFreeShops
               join paThaKa in db.PaThaKas on dutyFreeShop.PaThaKaId equals paThaKa.Id
               from nrcPrefix in db.Nrcprefixes
                   .Where(prefix => dutyFreeShop.NrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from nrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => dutyFreeShop.NrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               where paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo
               select new sp_DutyFreeShopByReportResult
               {
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   DutyFreeShopNo = dutyFreeShop.DutyFreeShopNo,
                   CompanyName = paThaKa.CompanyName,
                   UnitLevel = paThaKa.UnitLevel,
                   StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   QuarterCityTownship = paThaKa.QuarterCityTownship,
                   State = paThaKa.State,
                   Country = paThaKa.Country,
                   PostalCode = paThaKa.PostalCode,
                   Name = dutyFreeShop.Name,
                   NRCNo = dutyFreeShop.Nrctype == CurrentNrcType && dutyFreeShop.Nrcno != string.Empty
                       ? nrcPrefix.StatePrefix.ToString() + "/" + nrcPrefix.TownshipPrefix + nrcPrefixCode.Code + dutyFreeShop.Nrcno
                       : dutyFreeShop.Nrctype == OldNrcType && dutyFreeShop.Nrcno != string.Empty
                           ? dutyFreeShop.Nrcno
                           : string.Empty,
                   DutyFreeShopUnitLevel = paThaKa.UnitLevel,
                   DutyFreeShopStreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   DutyFreeShopQuarterCityTownship = paThaKa.QuarterCityTownship,
                   DutyFreeShopState = paThaKa.State,
                   DutyFreeShopCountry = paThaKa.Country,
                   DutyFreeShopPostalCode = paThaKa.PostalCode,
                   IssuedDate = dutyFreeShop.IssuedDate,
                   EndDate = dutyFreeShop.EndDate
               };
    }
}
