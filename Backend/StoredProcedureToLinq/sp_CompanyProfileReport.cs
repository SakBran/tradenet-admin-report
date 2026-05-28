using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_CompanyProfileReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string CompanyRegistrationNo { get; set; } = string.Empty;
}

public sealed class sp_CompanyProfileReportResult
{
    public string Id { get; set; } = null!;
    public string CompanyRegistrationNo { get; set; } = null!;
    public DateTime EndDate { get; set; }
    public string CompanyName { get; set; } = null!;
    public DateTime CompanyRegistrationDate { get; set; }
    public string BusinessType { get; set; } = null!;
    public string? LineofBusiness { get; set; }
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public double? Capital { get; set; }
    public string? DirectorName { get; set; }
    public string? DirectorNrc { get; set; }
    public string? DirectorPosition { get; set; }
    public string PermitBusiness { get; set; } = string.Empty;
    public int ExtensionCount { get; set; }
}

public static class sp_CompanyProfileReport
{
    private const string Approved = "Approved";
    private const string Extension = "Extension";

    public static IQueryable<sp_CompanyProfileReportResult> Query(
        TradeNetDbContext db,
        sp_CompanyProfileReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return
            from paThaKa in db.PaThaKas
            join director in db.PaThaKaDirectors on paThaKa.Id equals director.PaThaKaId
            join businessType in db.BusinessTypes on paThaKa.BusinessTypeId equals businessType.Id
            join lineofBusiness in db.LineofBusinesses on paThaKa.LineofBusinessId equals lineofBusiness.Id
            where paThaKa.IssuedDate >= request.FromDate
                && paThaKa.IssuedDate <= request.ToDate
                && (request.CompanyRegistrationNo == string.Empty
                    ? paThaKa.CompanyRegistrationNo == paThaKa.CompanyRegistrationNo
                    : paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
            orderby paThaKa.IssuedDate
            select new sp_CompanyProfileReportResult
            {
                Id = paThaKa.Id,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                EndDate = paThaKa.EndDate,
                CompanyName = paThaKa.CompanyName,
                CompanyRegistrationDate = paThaKa.CompanyRegistrationDate,
                BusinessType = businessType.Name,
                LineofBusiness = lineofBusiness.Name,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                Capital = paThaKa.Capital,
                DirectorName = director.Name,
                DirectorNrc = director.Nrc,
                DirectorPosition = director.Position,
                PermitBusiness = string.Join(",",
                    from paThaKaPermitBusiness in db.PaThaKaPermitBusinesses
                    join permitBusiness in db.PermitBusinesses
                        on paThaKaPermitBusiness.PermitBusinessId equals permitBusiness.Id
                    where paThaKaPermitBusiness.PaThaKaId == paThaKa.Id
                    orderby permitBusiness.SortOrder
                    select permitBusiness.Description),
                ExtensionCount = db.PaThaKaRegistrations.Count(registration =>
                    registration.CompanyRegistrationNo == paThaKa.CompanyRegistrationNo
                    && registration.ApplyType == Extension
                    && registration.Status == Approved)
            };
    }
}
