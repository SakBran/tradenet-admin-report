using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_ShowRoomByPaThaKaReportRequest
{
    public string CompanyRegistrationNo { get; set; } = string.Empty;
}

public sealed class sp_ShowRoomByPaThaKaReportResult
{
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
    public DateTime IssuedDate { get; set; }
    public DateTime EndDate { get; set; }
}

public static class sp_ShowRoomByPaThaKaReport
{
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    public static IQueryable<sp_ShowRoomByPaThaKaReportResult> Query(
        TradeNetDbContext db,
        sp_ShowRoomByPaThaKaReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from showRoom in db.ShowRooms
               join paThaKa in db.PaThaKas on showRoom.PaThaKaId equals paThaKa.Id
               from businessServiceAgency in db.BusinessServiceAgencies
                   .Where(agency => showRoom.BusinessServiceAgencyId == agency.Id)
                   .DefaultIfEmpty()
               from nrcPrefix in db.Nrcprefixes
                   .Where(prefix => showRoom.NrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from nrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => showRoom.NrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               where paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo
               select new sp_ShowRoomByPaThaKaReportResult
               {
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   ShowRoomNo = showRoom.ShowRoomNo,
                   CompanyName = showRoom.AuthorizeCompany,
                   UnitLevel = paThaKa.UnitLevel,
                   StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   QuarterCityTownship = paThaKa.QuarterCityTownship,
                   State = paThaKa.State,
                   Country = paThaKa.Country,
                   PostalCode = paThaKa.PostalCode,
                   Name = showRoom.Name,
                   NRCNo = showRoom.Nrctype == CurrentNrcType && showRoom.Nrcno != string.Empty
                       ? nrcPrefix.StatePrefix.ToString() + "/" + nrcPrefix.TownshipPrefix + nrcPrefixCode.Code + showRoom.Nrcno
                       : showRoom.Nrctype == OldNrcType && showRoom.Nrcno != string.Empty
                           ? showRoom.Nrcno
                           : string.Empty,
                   BusinessServiceAgencyNo = showRoom.BusinessServiceAgencyId == string.Empty
                       ? string.Empty
                       : businessServiceAgency.BusinessServiceAgencyNo,
                   ShowRoomUnitLevel = showRoom.AuthorizeCompanyUnitLevel,
                   ShowRoomStreetNumberStreetName = showRoom.AuthorizeCompanyStreetNumberStreetName,
                   ShowRoomQuarterCityTownship = showRoom.AuthorizeCompanyQuarterCityTownship,
                   ShowRoomState = showRoom.AuthorizeCompanyState,
                   ShowRoomCountry = showRoom.AuthorizeCompanyCountry,
                   ShowRoomPostalCode = showRoom.AuthorizeCompanyPostalCode,
                   IssuedDate = showRoom.IssuedDate,
                   EndDate = showRoom.EndDate
               };
    }
}
