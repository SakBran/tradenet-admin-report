using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_SaleCenterByPaThaKaReportRequest
{
    public string CompanyRegistrationNo { get; set; } = string.Empty;
}

public sealed class sp_SaleCenterByPaThaKaReportResult
{
    public string CompanyRegistrationNo { get; set; } = null!;
    public string SaleCenterNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string SaleCenterName { get; set; } = null!;
    public string? NRCNo { get; set; }
    public string? SaleCenterBusinessServiceAgencyNo { get; set; }
    public string? SaleCenterUnitLevel { get; set; }
    public string SaleCenterStreetNumberStreetName { get; set; } = null!;
    public string SaleCenterQuarterCityTownship { get; set; } = null!;
    public string SaleCenterState { get; set; } = null!;
    public string SaleCenterCountry { get; set; } = null!;
    public string? SaleCenterPostalCode { get; set; }
    public DateTime SaleCenterIssuedDate { get; set; }
    public DateTime SaleCenterEndDate { get; set; }
}

public static class sp_SaleCenterByPaThaKaReport
{
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    public static IQueryable<sp_SaleCenterByPaThaKaReportResult> Query(
        TradeNetDbContext db,
        sp_SaleCenterByPaThaKaReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from saleCenter in db.SaleCenters
               join paThaKa in db.PaThaKas on saleCenter.PaThaKaId equals paThaKa.Id
               from businessServiceAgency in db.BusinessServiceAgencies
                   .Where(agency => saleCenter.BusinessServiceAgencyId == agency.Id)
                   .DefaultIfEmpty()
               from nrcPrefix in db.Nrcprefixes
                   .Where(prefix => saleCenter.NrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from nrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => saleCenter.NrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               where paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo
               select new sp_SaleCenterByPaThaKaReportResult
               {
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   SaleCenterNo = saleCenter.SaleCenterNo,
                   CompanyName = paThaKa.CompanyName,
                   UnitLevel = paThaKa.UnitLevel,
                   StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   QuarterCityTownship = paThaKa.QuarterCityTownship,
                   State = paThaKa.State,
                   Country = paThaKa.Country,
                   PostalCode = paThaKa.PostalCode,
                   SaleCenterName = saleCenter.Name,
                   NRCNo = saleCenter.Nrctype == CurrentNrcType && saleCenter.Nrcno != string.Empty
                       ? nrcPrefix.StatePrefix.ToString() + "/" + nrcPrefix.TownshipPrefix + nrcPrefixCode.Code + saleCenter.Nrcno
                       : saleCenter.Nrctype == OldNrcType && saleCenter.Nrcno != string.Empty
                           ? saleCenter.Nrcno
                           : string.Empty,
                   SaleCenterBusinessServiceAgencyNo = saleCenter.BusinessServiceAgencyId == string.Empty
                       ? string.Empty
                       : businessServiceAgency.BusinessServiceAgencyNo,
                   SaleCenterUnitLevel = paThaKa.UnitLevel,
                   SaleCenterStreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   SaleCenterQuarterCityTownship = paThaKa.QuarterCityTownship,
                   SaleCenterState = paThaKa.State,
                   SaleCenterCountry = paThaKa.Country,
                   SaleCenterPostalCode = paThaKa.PostalCode,
                   SaleCenterIssuedDate = saleCenter.IssuedDate,
                   SaleCenterEndDate = saleCenter.EndDate
               };
    }
}
