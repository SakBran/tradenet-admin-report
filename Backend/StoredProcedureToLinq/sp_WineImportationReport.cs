using API.DBContext;
using API.Model.TradeNet;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace API.StoredProcedureToLinq;

public sealed class sp_WineImportationReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime Date { get; set; }
    public string ApplyType { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class sp_WineImportationReportResult
{
    public int? ApplicationCount { get; set; }
    public string? ApplyType { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? WineImportationNo { get; set; }
    public string? CompanyName { get; set; }
    public string? UnitLevel { get; set; }
    public string? StreetNumberStreetName { get; set; }
    public string? QuarterCityTownship { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? Name { get; set; }
    public string? NRCNo { get; set; }
    public string? FL11Name { get; set; }
    public string? FL11NRCNo { get; set; }
    public string? FL4Name { get; set; }
    public string? FL4NRCNo { get; set; }
    public string? FL5Name { get; set; }
    public string? FL5NRCNo { get; set; }
    public string? WineType { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public static class sp_WineImportationReport
{
    private const string Approved = "Approved";
    private const string Summary = "Summary";
    private const string Valid = "Valid";
    private const string Invalid = "Invalid";
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    public static IQueryable<sp_WineImportationReportResult> Query(
        TradeNetDbContext db,
        sp_WineImportationReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Type == Summary)
        {
            return SummaryQuery(db, request);
        }

        if (request.ApplyType == Valid)
        {
            return WineImportationDetailQuery(db, wineImportation => wineImportation.EndDate > request.Date);
        }

        if (request.ApplyType == Invalid)
        {
            return WineImportationDetailQuery(db, wineImportation => wineImportation.EndDate < request.Date);
        }

        return from wineImportation in db.WineImportations
               join paThaKa in db.PaThaKas on wineImportation.PaThaKaId equals paThaKa.Id
               join registration in db.WineImportationRegistrations
                   on wineImportation.WineImportationNo equals registration.WineImportationNo
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
               where registration.ApplyType == request.ApplyType
                   && registration.Status == Approved
                   && registration.CreatedDate >= request.FromDate
                   && registration.CreatedDate <= request.ToDate
               select new sp_WineImportationReportResult
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

    private static IQueryable<sp_WineImportationReportResult> SummaryQuery(
        TradeNetDbContext db,
        sp_WineImportationReportRequest request)
    {
        return GroupedSummaryRow(db.WineImportationRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "New"
                    && registration.Status == Approved)
                .Select(_ => 1), "New")
            .Concat(GroupedSummaryRow(db.WineImportationRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Cancel"
                    && registration.Status == Approved)
                .Select(_ => 1), "Cancel"))
            .Concat(CountSummaryRow(db.WineImportationRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Extension"
                    && registration.Status == Approved)
                .Select(_ => 1), "Extension"))
            .Concat(CountSummaryRow(db.WineImportations
                .Where(wineImportation => wineImportation.EndDate > request.Date)
                .Select(_ => 1), Valid))
            .Concat(CountSummaryRow(db.WineImportations
                .Where(wineImportation => wineImportation.EndDate < request.Date)
                .Select(_ => 1), Invalid));
    }

    private static IQueryable<sp_WineImportationReportResult> GroupedSummaryRow(
        IQueryable<int> source,
        string applyType)
    {
        return source
            .GroupBy(_ => 1)
            .Select(group => new sp_WineImportationReportResult
            {
                ApplicationCount = group.Count(),
                ApplyType = applyType
            });
    }

    private static IQueryable<sp_WineImportationReportResult> CountSummaryRow(
        IQueryable<int> source,
        string applyType)
    {
        return source
            .DefaultIfEmpty()
            .GroupBy(_ => 1)
            .Select(group => new sp_WineImportationReportResult
            {
                ApplicationCount = group.Sum(),
                ApplyType = applyType
            });
    }

    private static IQueryable<sp_WineImportationReportResult> WineImportationDetailQuery(
        TradeNetDbContext db,
        Expression<Func<WineImportation, bool>> predicate)
    {
        return from wineImportation in db.WineImportations.Where(predicate)
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
               select new sp_WineImportationReportResult
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
