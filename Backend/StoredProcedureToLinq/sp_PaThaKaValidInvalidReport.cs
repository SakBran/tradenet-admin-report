using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_PaThaKaValidInvalidReportRequest
{
    public DateTime Date { get; set; }
    public int BusinessTypeId { get; set; }
    public int LineofBusinessId { get; set; }
    public string State { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class sp_PaThaKaValidInvalidReportResult
{
    public string CompanyRegistrationNo { get; set; } = null!;
    public DateTime IssuedDate { get; set; }
    public string CompanyName { get; set; } = null!;
    public DateTime CompanyRegistrationDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BusinessType { get; set; } = null!;
    public string? LineofBusiness { get; set; }
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
}

public static class sp_PaThaKaValidInvalidReport
{
    private const string Valid = "valid";

    public static IQueryable<sp_PaThaKaValidInvalidReportResult> Query(
        TradeNetDbContext db,
        sp_PaThaKaValidInvalidReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var query =
            from paThaKa in db.PaThaKas
            join businessType in db.BusinessTypes on paThaKa.BusinessTypeId equals businessType.Id
            join lineofBusiness in db.LineofBusinesses on paThaKa.LineofBusinessId equals lineofBusiness.Id
            where (request.Type == Valid ? paThaKa.EndDate > request.Date : paThaKa.EndDate < request.Date)
                && (request.BusinessTypeId == 0 || paThaKa.BusinessTypeId == request.BusinessTypeId)
                && (request.LineofBusinessId == 0 || paThaKa.LineofBusinessId == request.LineofBusinessId)
                && (request.Status == string.Empty || paThaKa.Status == request.Status)
                && (request.State == string.Empty || paThaKa.State == request.State)
            orderby paThaKa.IssuedDate
            select new sp_PaThaKaValidInvalidReportResult
            {
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                IssuedDate = paThaKa.IssuedDate,
                CompanyName = paThaKa.CompanyName,
                CompanyRegistrationDate = paThaKa.CompanyRegistrationDate,
                EndDate = paThaKa.EndDate,
                BusinessType = businessType.Name,
                LineofBusiness = lineofBusiness.Name,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode
            };

        return query;
    }
}
