using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_MemberRegistrationReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string ApplyType { get; set; } = string.Empty;
}

public sealed class sp_MemberRegistrationReportResult
{
    public string Id { get; set; } = null!;
    public string ApplyType { get; set; } = null!;
    public string MemberCode { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Mobile1 { get; set; } = null!;
    public string? Mobile2 { get; set; }
    public string? Mobile3 { get; set; }
    public string? NRCNo { get; set; }
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public static class sp_MemberRegistrationReport
{
    private const string All = "All";
    private const string Approved = "Approved";
    private const string New = "New";
    private const string Extension = "Extension";
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    public static IQueryable<sp_MemberRegistrationReportResult> Query(
        TradeNetDbContext db,
        sp_MemberRegistrationReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ApplyType == All)
        {
            return NewRows(db, request)
                .Concat(ExtensionRows(db, request, useExtensionDateAsIssuedDate: true))
                .OrderBy(row => row.IssuedDate);
        }

        if (request.ApplyType == New)
        {
            return NewRows(db, request).OrderBy(row => row.IssuedDate);
        }

        if (request.ApplyType == Extension)
        {
            return ExtensionRows(db, request, useExtensionDateAsIssuedDate: false)
                .OrderBy(row => row.IssuedDate);
        }

        return db.MemberRegistrations
            .Where(_ => false)
            .Select(_ => new sp_MemberRegistrationReportResult());
    }

    private static IQueryable<sp_MemberRegistrationReportResult> NewRows(
        TradeNetDbContext db,
        sp_MemberRegistrationReportRequest request)
    {
        return from registration in db.MemberRegistrations
               join state in db.States on registration.StateId equals state.Id
               join country in db.Countries on registration.CountryId equals country.Id
               from nrcPrefix in db.Nrcprefixes
                   .Where(prefix => registration.NrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from nrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => registration.NrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               where registration.IssuedDate >= request.FromDate
                   && registration.IssuedDate <= request.ToDate
                   && registration.ApplyType == New
                   && registration.Status == Approved
               select new sp_MemberRegistrationReportResult
               {
                   Id = registration.Id,
                   ApplyType = registration.ApplyType,
                   MemberCode = registration.MemberCode,
                   Email = registration.Email,
                   FullName = registration.FullName,
                   Mobile1 = registration.Mobile1,
                   Mobile2 = registration.Mobile2,
                   Mobile3 = registration.Mobile3,
                   NRCNo = registration.Nrctype == CurrentNrcType && registration.Nrcno != string.Empty
                       ? nrcPrefix.StatePrefix.ToString() + "/" + nrcPrefix.TownshipPrefix + nrcPrefixCode.Code + registration.Nrcno
                       : registration.Nrctype == OldNrcType && registration.Nrcno != string.Empty
                           ? registration.Nrcno
                           : string.Empty,
                   UnitLevel = registration.UnitLevel,
                   StreetNumberStreetName = registration.StreetNumberStreetName,
                   QuarterCityTownship = registration.QuarterCityTownship,
                   State = state.Name,
                   Country = country.Name,
                   PostalCode = registration.PostalCode,
                   IssuedDate = registration.IssuedDate,
                   StartDate = registration.StartDate,
                   EndDate = registration.EndDate
               };
    }

    private static IQueryable<sp_MemberRegistrationReportResult> ExtensionRows(
        TradeNetDbContext db,
        sp_MemberRegistrationReportRequest request,
        bool useExtensionDateAsIssuedDate)
    {
        return from registration in db.MemberRegistrations
               join state in db.States on registration.StateId equals state.Id
               join country in db.Countries on registration.CountryId equals country.Id
               from nrcPrefix in db.Nrcprefixes
                   .Where(prefix => registration.NrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from nrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => registration.NrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               where registration.ExtensionDate >= request.FromDate
                   && registration.ExtensionDate <= request.ToDate
                   && registration.ApplyType == Extension
                   && registration.Status == Approved
               select new sp_MemberRegistrationReportResult
               {
                   Id = registration.Id,
                   ApplyType = registration.ApplyType,
                   MemberCode = registration.MemberCode,
                   Email = registration.Email,
                   FullName = registration.FullName,
                   Mobile1 = registration.Mobile1,
                   Mobile2 = registration.Mobile2,
                   Mobile3 = registration.Mobile3,
                   NRCNo = registration.Nrctype == CurrentNrcType && registration.Nrcno != string.Empty
                       ? nrcPrefix.StatePrefix.ToString() + "/" + nrcPrefix.TownshipPrefix + nrcPrefixCode.Code + registration.Nrcno
                       : registration.Nrctype == OldNrcType && registration.Nrcno != string.Empty
                           ? registration.Nrcno
                           : string.Empty,
                   UnitLevel = registration.UnitLevel,
                   StreetNumberStreetName = registration.StreetNumberStreetName,
                   QuarterCityTownship = registration.QuarterCityTownship,
                   State = state.Name,
                   Country = country.Name,
                   PostalCode = registration.PostalCode,
                   IssuedDate = useExtensionDateAsIssuedDate ? registration.ExtensionDate : registration.IssuedDate,
                   StartDate = registration.StartDate,
                   EndDate = registration.EndDate
               };
    }
}
