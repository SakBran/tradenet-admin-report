using API.DBContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public sealed class sp_BusinessServiceAgencyReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime Date { get; set; }
    public string ApplyType { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class sp_BusinessServiceAgencyReportResult
{
    public int? ApplicationCount { get; set; }
    public string? ApplyType { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? BusinessServiceAgencyNo { get; set; }
    public string? CompanyName { get; set; }
    public string? UnitLevel { get; set; }
    public string? StreetNumberStreetName { get; set; }
    public string? QuarterCityTownship { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? AuthorizeCompany { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public static class sp_BusinessServiceAgencyReport
{
    private const string Approved = "Approved";
    private const string Summary = "Summary";
    private const string Valid = "Valid";
    private const string Invalid = "Invalid";

    public static IQueryable<sp_BusinessServiceAgencyReportResult> Query(
        TradeNetDbContext db,
        sp_BusinessServiceAgencyReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Type == Summary)
        {
            return SummaryQuery(db, request);
        }

        if (request.ApplyType == Valid)
        {
            return BusinessServiceAgencyDetailQuery(db, agency => agency.EndDate > request.Date);
        }

        if (request.ApplyType == Invalid)
        {
            return BusinessServiceAgencyDetailQuery(db, agency => agency.EndDate < request.Date);
        }

        return
            from agency in db.BusinessServiceAgencies
            join paThaKa in db.PaThaKas on agency.PaThaKaId equals paThaKa.Id
            join registration in db.BusinessServiceAgencyRegistrations
                on agency.BusinessServiceAgencyNo equals registration.BusinessServiceAgencyNo
            where registration.ApplyType == request.ApplyType
                && registration.Status == Approved
                && registration.CreatedDate >= request.FromDate
                && registration.CreatedDate <= request.ToDate
            select new sp_BusinessServiceAgencyReportResult
            {
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                BusinessServiceAgencyNo = agency.BusinessServiceAgencyNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                AuthorizeCompany = agency.AuthorizeCompany,
                IssuedDate = agency.IssuedDate,
                EndDate = agency.EndDate
            };
    }

    public static async Task<RegistrationSummaryRow> SummaryRowAsync(
        TradeNetDbContext db,
        sp_BusinessServiceAgencyReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var registrations = db.BusinessServiceAgencyRegistrations.Where(registration =>
            registration.CreatedDate >= request.FromDate
            && registration.CreatedDate <= request.ToDate
            && registration.Status == Approved);

        var newCount = await registrations.CountAsync(r => r.ApplyType == "New");
        var cancelCount = await registrations.CountAsync(r => r.ApplyType == "Cancel");
        var extensionCount = await registrations.CountAsync(r => r.ApplyType == "Extension");
        var validCount = await db.BusinessServiceAgencies.CountAsync(c =>
            c.EndDate > request.Date);
        var invalidCount = await db.BusinessServiceAgencies.CountAsync(c =>
            c.EndDate < request.Date);

        return RegistrationSummaryRow.Of(newCount, cancelCount, extensionCount, validCount, invalidCount);
    }

    private static IQueryable<sp_BusinessServiceAgencyReportResult> SummaryQuery(
        TradeNetDbContext db,
        sp_BusinessServiceAgencyReportRequest request)
    {
        return SummaryRow(db.BusinessServiceAgencyRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "New"
                    && registration.Status == Approved)
                .Select(_ => 1), "New")
            .Concat(SummaryRow(db.BusinessServiceAgencyRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Cancel"
                    && registration.Status == Approved)
                .Select(_ => 1), "Cancel"))
            .Concat(SummaryRow(db.BusinessServiceAgencyRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Extension"
                    && registration.Status == Approved)
                .Select(_ => 1), "Extension"))
            .Concat(SummaryRow(db.BusinessServiceAgencies
                .Where(agency => agency.EndDate > request.Date)
                .Select(_ => 1), Valid))
            .Concat(SummaryRow(db.BusinessServiceAgencies
                .Where(agency => agency.EndDate < request.Date)
                .Select(_ => 1), Invalid));
    }

    private static IQueryable<sp_BusinessServiceAgencyReportResult> SummaryRow(
        IQueryable<int> source,
        string applyType)
    {
        return source
            .DefaultIfEmpty()
            .GroupBy(_ => 1)
            .Select(group => new sp_BusinessServiceAgencyReportResult
            {
                ApplicationCount = group.Sum(),
                ApplyType = applyType
            });
    }

    private static IQueryable<sp_BusinessServiceAgencyReportResult> BusinessServiceAgencyDetailQuery(
        TradeNetDbContext db,
        System.Linq.Expressions.Expression<Func<API.Model.TradeNet.BusinessServiceAgency, bool>> predicate)
    {
        return
            from agency in db.BusinessServiceAgencies.Where(predicate)
            join paThaKa in db.PaThaKas on agency.PaThaKaId equals paThaKa.Id
            select new sp_BusinessServiceAgencyReportResult
            {
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                BusinessServiceAgencyNo = agency.BusinessServiceAgencyNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                AuthorizeCompany = agency.AuthorizeCompany,
                IssuedDate = agency.IssuedDate,
                EndDate = agency.EndDate
            };
    }
}
