using API.DBContext;
using API.Model.TradeNet;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_ShowRoomReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime Date { get; set; }
    public string ApplyType { get; set; } = string.Empty;
    public string FormType { get; set; } = string.Empty;

    /// <summary>
    /// The RegistrationType (Form Type) values this report may return. A single
    /// value when the user picks a specific Form Type; the family's full set when
    /// the Form Type filter is '--- All ---'.
    /// </summary>
    public List<string> AllowedFormTypes { get; set; } = new();
    public string Type { get; set; } = string.Empty;
}

public sealed class sp_ShowRoomReportResult
{
    public int? ApplicationCount { get; set; }
    public string? ApplyType { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? ShowRoomNo { get; set; }
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
    public string? ShowRoomUnitLevel { get; set; }
    public string? ShowRoomStreetNumberStreetName { get; set; }
    public string? ShowRoomQuarterCityTownship { get; set; }
    public string? ShowRoomState { get; set; }
    public string? ShowRoomCountry { get; set; }
    public string? ShowRoomPostalCode { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public static class sp_ShowRoomReport
{
    private const string Approved = "Approved";
    private const string Summary = "Summary";
    private const string Valid = "Valid";
    private const string Invalid = "Invalid";
    private const string CurrentNrcType = "Current";
    private const string OldNrcType = "Old";

    /// <summary>The Show Room family's RegistrationType (Form Type) values (DB-verified).</summary>
    public static readonly string[] FormTypes =
    {
        "Show Room for Brand New Motor Vehicles",
        "Show Room for Machinery and Mechanical",
    };

    /// <summary>Resolves the submitted Form Type to the set of RegistrationType values to
    /// query: the single chosen value, or the whole family when blank ('--- All ---').</summary>
    public static List<string> ResolveFormTypes(string? submitted)
    {
        var value = submitted?.Trim();
        return !string.IsNullOrEmpty(value) && FormTypes.Contains(value)
            ? new List<string> { value }
            : new List<string>(FormTypes);
    }

    /// <summary>
    /// Builds the single legacy-style summary row (six count columns) for the
    /// Summary report, replacing the old multi-row ApplyType listing.
    /// </summary>
    public static async Task<RegistrationSummaryRow> SummaryRowAsync(
        TradeNetDbContext db,
        sp_ShowRoomReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var registrations = db.ShowRoomRegistrations.Where(registration =>
            registration.CreatedDate >= request.FromDate
            && registration.CreatedDate <= request.ToDate
            && registration.Status == Approved
            && request.AllowedFormTypes.Contains(registration.RegistrationType));

        var newCount = await registrations.CountAsync(r => r.ApplyType == "New");
        var cancelCount = await registrations.CountAsync(r => r.ApplyType == "Cancel");
        var extensionCount = await registrations.CountAsync(r => r.ApplyType == "Extension");
        var validCount = await db.ShowRooms.CountAsync(showRoom =>
            showRoom.EndDate > request.Date
            && request.AllowedFormTypes.Contains(showRoom.RegistrationType));
        var invalidCount = await db.ShowRooms.CountAsync(showRoom =>
            showRoom.EndDate < request.Date
            && request.AllowedFormTypes.Contains(showRoom.RegistrationType));

        return RegistrationSummaryRow.Of(newCount, cancelCount, extensionCount, validCount, invalidCount);
    }

    public static IQueryable<sp_ShowRoomReportResult> Query(
        TradeNetDbContext db,
        sp_ShowRoomReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Type == Summary)
        {
            return SummaryQuery(db, request);
        }

        if (request.ApplyType == Valid)
        {
            return ShowRoomDetailQuery(db, request, showRoom => showRoom.EndDate > request.Date);
        }

        if (request.ApplyType == Invalid)
        {
            return ShowRoomDetailQuery(db, request, showRoom => showRoom.EndDate < request.Date);
        }

        return from showRoom in db.ShowRooms
               join paThaKa in db.PaThaKas on showRoom.PaThaKaId equals paThaKa.Id
               join registration in db.ShowRoomRegistrations on showRoom.ShowRoomNo equals registration.ShowRoomNo
               from businessServiceAgency in db.BusinessServiceAgencies
                   .Where(agency => showRoom.BusinessServiceAgencyId == agency.Id)
                   .DefaultIfEmpty()
               from nrcPrefix in db.Nrcprefixes
                   .Where(prefix => showRoom.NrcprefixId == prefix.Id)
                   .DefaultIfEmpty()
               from nrcPrefixCode in db.NrcprefixCodes
                   .Where(prefixCode => showRoom.NrcprefixCodeId == prefixCode.Id)
                   .DefaultIfEmpty()
               where registration.ApplyType == request.ApplyType
                   && registration.Status == Approved
                   && request.AllowedFormTypes.Contains(showRoom.RegistrationType)
                   && registration.CreatedDate >= request.FromDate
                   && registration.CreatedDate <= request.ToDate
               select new sp_ShowRoomReportResult
               {
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   ShowRoomNo = showRoom.ShowRoomNo,
                   CompanyName = paThaKa.CompanyName,
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
                   ShowRoomUnitLevel = paThaKa.UnitLevel,
                   ShowRoomStreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   ShowRoomQuarterCityTownship = paThaKa.QuarterCityTownship,
                   ShowRoomState = paThaKa.State,
                   ShowRoomCountry = paThaKa.Country,
                   ShowRoomPostalCode = paThaKa.PostalCode,
                   IssuedDate = showRoom.IssuedDate,
                   EndDate = showRoom.EndDate
               };
    }

    private static IQueryable<sp_ShowRoomReportResult> SummaryQuery(
        TradeNetDbContext db,
        sp_ShowRoomReportRequest request)
    {
        return GroupedSummaryRow(db.ShowRoomRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "New"
                    && registration.Status == Approved
                    && request.AllowedFormTypes.Contains(registration.RegistrationType))
                .Select(_ => 1), "New")
            .Concat(GroupedSummaryRow(db.ShowRoomRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Cancel"
                    && registration.Status == Approved
                    && request.AllowedFormTypes.Contains(registration.RegistrationType))
                .Select(_ => 1), "Cancel"))
            .Concat(CountSummaryRow(db.ShowRoomRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Extension"
                    && registration.Status == Approved
                    && request.AllowedFormTypes.Contains(registration.RegistrationType))
                .Select(_ => 1), "Extension"))
            .Concat(CountSummaryRow(db.ShowRooms
                .Where(showRoom =>
                    showRoom.EndDate > request.Date
                    && request.AllowedFormTypes.Contains(showRoom.RegistrationType))
                .Select(_ => 1), Valid))
            .Concat(CountSummaryRow(db.ShowRooms
                .Where(showRoom =>
                    showRoom.EndDate < request.Date
                    && request.AllowedFormTypes.Contains(showRoom.RegistrationType))
                .Select(_ => 1), Invalid));
    }

    private static IQueryable<sp_ShowRoomReportResult> GroupedSummaryRow(
        IQueryable<int> source,
        string applyType)
    {
        return source
            .GroupBy(_ => 1)
            .Select(group => new sp_ShowRoomReportResult
            {
                ApplicationCount = group.Count(),
                ApplyType = applyType
            });
    }

    private static IQueryable<sp_ShowRoomReportResult> CountSummaryRow(
        IQueryable<int> source,
        string applyType)
    {
        return source
            .DefaultIfEmpty()
            .GroupBy(_ => 1)
            .Select(group => new sp_ShowRoomReportResult
            {
                ApplicationCount = group.Sum(),
                ApplyType = applyType
            });
    }

    private static IQueryable<sp_ShowRoomReportResult> ShowRoomDetailQuery(
        TradeNetDbContext db,
        sp_ShowRoomReportRequest request,
        Expression<Func<ShowRoom, bool>> predicate)
    {
        return from showRoom in db.ShowRooms.Where(predicate)
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
               where request.AllowedFormTypes.Contains(showRoom.RegistrationType)
               select new sp_ShowRoomReportResult
               {
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   ShowRoomNo = showRoom.ShowRoomNo,
                   CompanyName = paThaKa.CompanyName,
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
                   ShowRoomUnitLevel = paThaKa.UnitLevel,
                   ShowRoomStreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   ShowRoomQuarterCityTownship = paThaKa.QuarterCityTownship,
                   ShowRoomState = paThaKa.State,
                   ShowRoomCountry = paThaKa.Country,
                   ShowRoomPostalCode = paThaKa.PostalCode,
                   IssuedDate = showRoom.IssuedDate,
                   EndDate = showRoom.EndDate
               };
    }
}
