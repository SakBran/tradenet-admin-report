using API.DBContext;
using API.Model.TradeNet;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace API.StoredProcedureToLinq;

public sealed class sp_SaleCenterReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime Date { get; set; }
    public string ApplyType { get; set; } = string.Empty;
    public string FormType { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class sp_SaleCenterReportResult
{
    public int? ApplicationCount { get; set; }
    public string? ApplyType { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? SaleCenterNo { get; set; }
    public string? CompanyName { get; set; }
    public string? UnitLevel { get; set; }
    public string? StreetNumberStreetName { get; set; }
    public string? QuarterCityTownship { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? Name { get; set; }
    public string? NRCNo { get; set; }
    public string? BusinessServiceAgencyNo { get; set; }
    public string? SaleCenterUnitLevel { get; set; }
    public string? SaleCenterStreetNumberStreetName { get; set; }
    public string? SaleCenterQuarterCityTownship { get; set; }
    public string? SaleCenterState { get; set; }
    public string? SaleCenterCountry { get; set; }
    public string? SaleCenterPostalCode { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public static class sp_SaleCenterReport
{
    private const string Approved = "Approved";
    private const string Summary = "Summary";
    private const string Valid = "Valid";
    private const string Invalid = "Invalid";
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    public static IQueryable<sp_SaleCenterReportResult> Query(
        TradeNetDbContext db,
        sp_SaleCenterReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Type == Summary)
        {
            return SummaryQuery(db, request);
        }

        if (request.ApplyType == Valid)
        {
            return SaleCenterDetailQuery(db, request, saleCenter => saleCenter.EndDate > request.Date);
        }

        if (request.ApplyType == Invalid)
        {
            return SaleCenterDetailQuery(db, request, saleCenter => saleCenter.EndDate < request.Date);
        }

        return from saleCenter in db.SaleCenters
               join paThaKa in db.PaThaKas on saleCenter.PaThaKaId equals paThaKa.Id
               join registration in db.SaleCenterRegistrations
                   on saleCenter.SaleCenterNo equals registration.SaleCenterNo
               from businessServiceAgency in db.BusinessServiceAgencies
                   .Where(agency => saleCenter.BusinessServiceAgencyId == agency.Id)
                   .DefaultIfEmpty()
               from nrcPrefix in db.Nrcprefixes
                   .Where(prefix => saleCenter.NrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from nrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => saleCenter.NrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               where registration.ApplyType == request.ApplyType
                   && registration.Status == Approved
                   && saleCenter.RegistrationType == request.FormType
                   && registration.CreatedDate >= request.FromDate
                   && registration.CreatedDate <= request.ToDate
               select new sp_SaleCenterReportResult
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
                   Name = saleCenter.Name,
                   NRCNo = saleCenter.Nrctype == CurrentNrcType && saleCenter.Nrcno != string.Empty
                       ? nrcPrefix.StatePrefix.ToString() + "/" + nrcPrefix.TownshipPrefix + nrcPrefixCode.Code + saleCenter.Nrcno
                       : saleCenter.Nrctype == OldNrcType && saleCenter.Nrcno != string.Empty
                           ? saleCenter.Nrcno
                           : string.Empty,
                   BusinessServiceAgencyNo = saleCenter.BusinessServiceAgencyId == string.Empty
                       ? string.Empty
                       : businessServiceAgency.BusinessServiceAgencyNo,
                   SaleCenterUnitLevel = paThaKa.UnitLevel,
                   SaleCenterStreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   SaleCenterQuarterCityTownship = paThaKa.QuarterCityTownship,
                   SaleCenterState = paThaKa.State,
                   SaleCenterCountry = paThaKa.Country,
                   SaleCenterPostalCode = paThaKa.PostalCode,
                   IssuedDate = saleCenter.IssuedDate,
                   EndDate = saleCenter.EndDate
               };
    }

    private static IQueryable<sp_SaleCenterReportResult> SummaryQuery(
        TradeNetDbContext db,
        sp_SaleCenterReportRequest request)
    {
        return GroupedSummaryRow(db.SaleCenterRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "New"
                    && registration.Status == Approved
                    && registration.RegistrationType == request.FormType)
                .Select(_ => 1), "New")
            .Concat(GroupedSummaryRow(db.SaleCenterRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Cancel"
                    && registration.Status == Approved
                    && registration.RegistrationType == request.FormType)
                .Select(_ => 1), "Cancel"))
            .Concat(CountSummaryRow(db.SaleCenterRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Extension"
                    && registration.Status == Approved
                    && registration.RegistrationType == request.FormType)
                .Select(_ => 1), "Extension"))
            .Concat(CountSummaryRow(db.SaleCenters
                .Where(saleCenter =>
                    saleCenter.EndDate > request.Date
                    && saleCenter.RegistrationType == request.FormType)
                .Select(_ => 1), Valid))
            .Concat(CountSummaryRow(db.SaleCenters
                .Where(saleCenter =>
                    saleCenter.EndDate < request.Date
                    && saleCenter.RegistrationType == request.FormType)
                .Select(_ => 1), Invalid));
    }

    private static IQueryable<sp_SaleCenterReportResult> GroupedSummaryRow(
        IQueryable<int> source,
        string applyType)
    {
        return source
            .GroupBy(_ => 1)
            .Select(group => new sp_SaleCenterReportResult
            {
                ApplicationCount = group.Count(),
                ApplyType = applyType
            });
    }

    private static IQueryable<sp_SaleCenterReportResult> CountSummaryRow(
        IQueryable<int> source,
        string applyType)
    {
        return source
            .DefaultIfEmpty()
            .GroupBy(_ => 1)
            .Select(group => new sp_SaleCenterReportResult
            {
                ApplicationCount = group.Sum(),
                ApplyType = applyType
            });
    }

    private static IQueryable<sp_SaleCenterReportResult> SaleCenterDetailQuery(
        TradeNetDbContext db,
        sp_SaleCenterReportRequest request,
        Expression<Func<SaleCenter, bool>> predicate)
    {
        return from saleCenter in db.SaleCenters.Where(predicate)
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
               where saleCenter.RegistrationType == request.FormType
               select new sp_SaleCenterReportResult
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
                   Name = saleCenter.Name,
                   NRCNo = saleCenter.Nrctype == CurrentNrcType && saleCenter.Nrcno != string.Empty
                       ? nrcPrefix.StatePrefix.ToString() + "/" + nrcPrefix.TownshipPrefix + nrcPrefixCode.Code + saleCenter.Nrcno
                       : saleCenter.Nrctype == OldNrcType && saleCenter.Nrcno != string.Empty
                           ? saleCenter.Nrcno
                           : string.Empty,
                   BusinessServiceAgencyNo = saleCenter.BusinessServiceAgencyId == string.Empty
                       ? string.Empty
                       : businessServiceAgency.BusinessServiceAgencyNo,
                   SaleCenterUnitLevel = paThaKa.UnitLevel,
                   SaleCenterStreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   SaleCenterQuarterCityTownship = paThaKa.QuarterCityTownship,
                   SaleCenterState = paThaKa.State,
                   SaleCenterCountry = paThaKa.Country,
                   SaleCenterPostalCode = paThaKa.PostalCode,
                   IssuedDate = saleCenter.IssuedDate,
                   EndDate = saleCenter.EndDate
               };
    }
}
