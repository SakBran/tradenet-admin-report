using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_WineImportationByPaThaKaReportRequest
{
    public string CompanyRegistrationNo { get; set; } = string.Empty;
}

public sealed class sp_WineImportationByPaThaKaReportResult
{
    public string CompanyRegistrationNo { get; set; } = null!;
    public string WineImportationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string Name { get; set; } = null!;
    public string? NRCNo { get; set; }
    public string FL11Name { get; set; } = null!;
    public string? FL11NRCNo { get; set; }
    public string FL4Name { get; set; } = null!;
    public string? FL4NRCNo { get; set; }
    public string FL5Name { get; set; } = null!;
    public string? FL5NRCNo { get; set; }
    public string WineType { get; set; } = string.Empty;
    public DateTime IssuedDate { get; set; }
    public DateTime EndDate { get; set; }
}

public static class sp_WineImportationByPaThaKaReport
{
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    public static IQueryable<sp_WineImportationByPaThaKaReportResult> Query(
        TradeNetDbContext db,
        sp_WineImportationByPaThaKaReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from wineImportation in db.WineImportations
               join paThaKa in db.PaThaKas on wineImportation.PaThaKaId equals paThaKa.Id
               from nrcPrefix in db.Nrcprefixes
                   .Where(prefix => wineImportation.NrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from nrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => wineImportation.NrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               from fl11NrcPrefix in db.Nrcprefixes
                   .Where(prefix => wineImportation.Fl11nrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from fl11NrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => wineImportation.Fl11nrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               from fl4NrcPrefix in db.Nrcprefixes
                   .Where(prefix => wineImportation.Fl4nrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from fl4NrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => wineImportation.Fl4nrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               from fl5NrcPrefix in db.Nrcprefixes
                   .Where(prefix => wineImportation.Fl5nrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from fl5NrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => wineImportation.Fl5nrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               where paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo
               select new sp_WineImportationByPaThaKaReportResult
               {
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   WineImportationNo = wineImportation.WineImportationNo,
                   CompanyName = paThaKa.CompanyName,
                   UnitLevel = paThaKa.UnitLevel,
                   StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   QuarterCityTownship = paThaKa.QuarterCityTownship,
                   State = paThaKa.State,
                   Country = paThaKa.Country,
                   PostalCode = paThaKa.PostalCode,
                   Name = wineImportation.Name,
                   NRCNo = wineImportation.Nrctype == CurrentNrcType && wineImportation.Nrcno != string.Empty
                       ? nrcPrefix.StatePrefix.ToString() + "/" + nrcPrefix.TownshipPrefix + nrcPrefixCode.Code + wineImportation.Nrcno
                       : wineImportation.Nrctype == OldNrcType && wineImportation.Nrcno != string.Empty
                           ? wineImportation.Nrcno
                           : string.Empty,
                   FL11Name = wineImportation.Fl11name,
                   FL11NRCNo = wineImportation.Fl11nrctype == CurrentNrcType && wineImportation.Fl11nrcno != string.Empty
                       ? fl11NrcPrefix.StatePrefix.ToString() + "/" + fl11NrcPrefix.TownshipPrefix + fl11NrcPrefixCode.Code + wineImportation.Fl11nrcno
                       : wineImportation.Fl11nrctype == OldNrcType && wineImportation.Fl11nrcno != string.Empty
                           ? wineImportation.Fl11nrcno
                           : string.Empty,
                   FL4Name = wineImportation.Fl4name,
                   FL4NRCNo = wineImportation.Fl4nrctype == CurrentNrcType && wineImportation.Fl4nrcno != string.Empty
                       ? fl4NrcPrefix.StatePrefix.ToString() + "/" + fl4NrcPrefix.TownshipPrefix + fl4NrcPrefixCode.Code + wineImportation.Fl4nrcno
                       : wineImportation.Fl4nrctype == OldNrcType && wineImportation.Fl4nrcno != string.Empty
                           ? wineImportation.Fl4nrcno
                           : string.Empty,
                   FL5Name = wineImportation.Fl5name,
                   FL5NRCNo = wineImportation.Fl5nrctype == CurrentNrcType && wineImportation.Fl5nrcno != string.Empty
                       ? fl5NrcPrefix.StatePrefix.ToString() + "/" + fl5NrcPrefix.TownshipPrefix + fl5NrcPrefixCode.Code + wineImportation.Fl5nrcno
                       : wineImportation.Fl5nrctype == OldNrcType && wineImportation.Fl5nrcno != string.Empty
                           ? wineImportation.Fl5nrcno
                           : string.Empty,
                   WineType = string.Join(",",
                       from wineType in db.WineTypes
                       where ("," + wineImportation.WineTypeId + ",").Contains("," + wineType.Id.ToString() + ",")
                       select wineType.Name),
                   IssuedDate = wineImportation.IssuedDate,
                   EndDate = wineImportation.EndDate
               };
    }
}
