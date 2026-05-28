using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_ReExportByPaThaKaReportRequest
{
    public string CompanyRegistrationNo { get; set; } = string.Empty;
}

public sealed class sp_ReExportByPaThaKaReportResult
{
    public string CompanyRegistrationNo { get; set; } = null!;
    public string ReExportNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string? ReExportUnitLevel { get; set; }
    public string ReExportStreetNumberStreetName { get; set; } = null!;
    public string ReExportQuarterCityTownship { get; set; } = null!;
    public string ReExportState { get; set; } = null!;
    public string ReExportCountry { get; set; } = null!;
    public string? ReExportPostalCode { get; set; }
    public DateTime IssuedDate { get; set; }
    public DateTime EndDate { get; set; }
}

public static class sp_ReExportByPaThaKaReport
{
    public static IQueryable<sp_ReExportByPaThaKaReportResult> Query(
        TradeNetDbContext db,
        sp_ReExportByPaThaKaReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from reExport in db.ReExports
               join paThaKa in db.PaThaKas on reExport.PaThaKaId equals paThaKa.Id
               where paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo
               select new sp_ReExportByPaThaKaReportResult
               {
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   ReExportNo = reExport.ReExportNo,
                   CompanyName = paThaKa.CompanyName,
                   UnitLevel = paThaKa.UnitLevel,
                   StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   QuarterCityTownship = paThaKa.QuarterCityTownship,
                   State = paThaKa.State,
                   Country = paThaKa.Country,
                   PostalCode = paThaKa.PostalCode,
                   ReExportUnitLevel = paThaKa.UnitLevel,
                   ReExportStreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   ReExportQuarterCityTownship = paThaKa.QuarterCityTownship,
                   ReExportState = paThaKa.State,
                   ReExportCountry = paThaKa.Country,
                   ReExportPostalCode = paThaKa.PostalCode,
                   IssuedDate = reExport.IssuedDate,
                   EndDate = reExport.EndDate
               };
    }
}
