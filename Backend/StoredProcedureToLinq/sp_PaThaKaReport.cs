using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_PaThaKaReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BusinessTypeId { get; set; }
    public int LineofBusinessId { get; set; }
    public string State { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class sp_PaThaKaReportResult
{
    public string CompanyRegistrationNo { get; set; } = null!;
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
    public double? Capital { get; set; }
    public string? MICPermitNo { get; set; }
}

public static class sp_PaThaKaReport
{
    public static IQueryable<sp_PaThaKaReportResult> Query(
        TradeNetDbContext db,
        sp_PaThaKaReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from paThaKa in db.PaThaKas
               join businessType in db.BusinessTypes on paThaKa.BusinessTypeId equals businessType.Id
               join lineofBusiness in db.LineofBusinesses on paThaKa.LineofBusinessId equals lineofBusiness.Id
               where paThaKa.IssuedDate >= request.FromDate
                   && paThaKa.IssuedDate <= request.ToDate
                   && (request.BusinessTypeId == 0 || paThaKa.BusinessTypeId == request.BusinessTypeId)
                   && (request.LineofBusinessId == 0 || paThaKa.LineofBusinessId == request.LineofBusinessId)
                   && (request.Status == string.Empty || paThaKa.Status == request.Status)
                   && (request.State == string.Empty || paThaKa.State == request.State)
               orderby paThaKa.IssuedDate
               select new sp_PaThaKaReportResult
               {
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
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
                   PostalCode = paThaKa.PostalCode,
                   Capital = paThaKa.Capital,
                   MICPermitNo = paThaKa.MicpermitNo
               };
    }
}
