using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_BusinessServiceAgencyByPaThakaReportRequest
{
    public string CompanyRegistrationNo { get; set; } = string.Empty;
}

public sealed class sp_BusinessServiceAgencyByPaThakaReportResult
{
    public string CompanyRegistrationNo { get; set; } = null!;
    public string BusinessServiceAgencyNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string AuthorizeCompany { get; set; } = null!;
    public DateTime IssuedDate { get; set; }
    public DateTime EndDate { get; set; }
}

public static class sp_BusinessServiceAgencyByPaThakaReport
{
    public static IQueryable<sp_BusinessServiceAgencyByPaThakaReportResult> Query(
        TradeNetDbContext db,
        sp_BusinessServiceAgencyByPaThakaReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return
            from businessServiceAgency in db.BusinessServiceAgencies
            join paThaKa in db.PaThaKas on businessServiceAgency.PaThaKaId equals paThaKa.Id
            where paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo
            select new sp_BusinessServiceAgencyByPaThakaReportResult
            {
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                BusinessServiceAgencyNo = businessServiceAgency.BusinessServiceAgencyNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                AuthorizeCompany = businessServiceAgency.AuthorizeCompany,
                IssuedDate = businessServiceAgency.IssuedDate,
                EndDate = businessServiceAgency.EndDate
            };
    }
}
