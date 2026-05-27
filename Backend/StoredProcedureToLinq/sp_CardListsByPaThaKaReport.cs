using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_CardListsByPaThaKaReportRequest
{
    public string CompanyRegistrationNo { get; set; } = string.Empty;
}

public sealed class sp_CardListsByPaThaKaReportResult
{
    public string? MicpermitNo { get; set; }
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
}

public static class sp_CardListsByPaThaKaReport
{
    public static IQueryable<sp_CardListsByPaThaKaReportResult> Query(
        TradeNetDbContext db,
        sp_CardListsByPaThaKaReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return
            from paThaKa in db.PaThaKas
            join businessType in db.BusinessTypes on paThaKa.BusinessTypeId equals businessType.Id
            join lineofBusiness in db.LineofBusinesses on paThaKa.LineofBusinessId equals lineofBusiness.Id
            where paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo
            select new sp_CardListsByPaThaKaReportResult
            {
                MicpermitNo = paThaKa.MicpermitNo,
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
                Capital = paThaKa.Capital
            };
    }
}
