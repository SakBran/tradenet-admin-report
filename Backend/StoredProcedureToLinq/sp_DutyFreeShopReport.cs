using API.DBContext;
using API.Model.TradeNet;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace API.StoredProcedureToLinq;

public sealed class sp_DutyFreeShopReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime Date { get; set; }
    public string ApplyType { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class sp_DutyFreeShopReportResult
{
    public int? ApplicationCount { get; set; }
    public string? ApplyType { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? DutyFreeShopNo { get; set; }
    public string? CompanyName { get; set; }
    public string? UnitLevel { get; set; }
    public string? StreetNumberStreetName { get; set; }
    public string? QuarterCityTownship { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? Name { get; set; }
    public string? NRCNo { get; set; }
    public string? DutyFreeShopUnitLevel { get; set; }
    public string? DutyFreeShopStreetNumberStreetName { get; set; }
    public string? DutyFreeShopQuarterCityTownship { get; set; }
    public string? DutyFreeShopState { get; set; }
    public string? DutyFreeShopCountry { get; set; }
    public string? DutyFreeShopPostalCode { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public static class sp_DutyFreeShopReport
{
    private const string Approved = "Approved";
    private const string Summary = "Summary";
    private const string Valid = "Valid";
    private const string Invalid = "Invalid";
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    public static IQueryable<sp_DutyFreeShopReportResult> Query(
        TradeNetDbContext db,
        sp_DutyFreeShopReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Type == Summary)
        {
            return SummaryQuery(db, request);
        }

        if (request.ApplyType == Valid)
        {
            return DutyFreeShopDetailQuery(db, shop => shop.EndDate > request.Date);
        }

        if (request.ApplyType == Invalid)
        {
            return DutyFreeShopDetailQuery(db, shop => shop.EndDate < request.Date);
        }

        return from dutyFreeShop in db.DutyFreeShops
               join paThaKa in db.PaThaKas on dutyFreeShop.PaThaKaId equals paThaKa.Id
               join registration in db.DutyFreeShopRegistrations
                   on dutyFreeShop.DutyFreeShopNo equals registration.DutyFreeShopNo
               from nrcPrefix in db.Nrcprefixes
                   .Where(prefix => dutyFreeShop.NrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from nrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => dutyFreeShop.NrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               where registration.ApplyType == request.ApplyType
                   && registration.Status == Approved
                   && registration.CreatedDate >= request.FromDate
                   && registration.CreatedDate <= request.ToDate
               select new sp_DutyFreeShopReportResult
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

    private static IQueryable<sp_DutyFreeShopReportResult> SummaryQuery(
        TradeNetDbContext db,
        sp_DutyFreeShopReportRequest request)
    {
        return GroupedSummaryRow(db.DutyFreeShopRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "New"
                    && registration.Status == Approved)
                .Select(_ => 1), "New")
            .Concat(GroupedSummaryRow(db.DutyFreeShopRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Cancel"
                    && registration.Status == Approved)
                .Select(_ => 1), "Cancel"))
            .Concat(CountSummaryRow(db.DutyFreeShopRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Extension"
                    && registration.Status == Approved)
                .Select(_ => 1), "Extension"))
            .Concat(CountSummaryRow(db.DutyFreeShops
                .Where(shop => shop.EndDate > request.Date)
                .Select(_ => 1), Valid))
            .Concat(CountSummaryRow(db.DutyFreeShops
                .Where(shop => shop.EndDate < request.Date)
                .Select(_ => 1), Invalid));
    }

    private static IQueryable<sp_DutyFreeShopReportResult> GroupedSummaryRow(
        IQueryable<int> source,
        string applyType)
    {
        return source
            .GroupBy(_ => 1)
            .Select(group => new sp_DutyFreeShopReportResult
            {
                ApplicationCount = group.Count(),
                ApplyType = applyType
            });
    }

    private static IQueryable<sp_DutyFreeShopReportResult> CountSummaryRow(
        IQueryable<int> source,
        string applyType)
    {
        return source
            .DefaultIfEmpty()
            .GroupBy(_ => 1)
            .Select(group => new sp_DutyFreeShopReportResult
            {
                ApplicationCount = group.Sum(),
                ApplyType = applyType
            });
    }

    private static IQueryable<sp_DutyFreeShopReportResult> DutyFreeShopDetailQuery(
        TradeNetDbContext db,
        Expression<Func<DutyFreeShop, bool>> predicate)
    {
        return from dutyFreeShop in db.DutyFreeShops.Where(predicate)
               join paThaKa in db.PaThaKas on dutyFreeShop.PaThaKaId equals paThaKa.Id
               from nrcPrefix in db.Nrcprefixes
                   .Where(prefix => dutyFreeShop.NrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from nrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => dutyFreeShop.NrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               select new sp_DutyFreeShopReportResult
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
