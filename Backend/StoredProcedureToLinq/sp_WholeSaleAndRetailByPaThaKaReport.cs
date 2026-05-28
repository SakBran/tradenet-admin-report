using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_WholeSaleAndRetailByPaThaKaReportRequest
{
    public string CompanyRegistrationNo { get; set; } = string.Empty;
}

public sealed class sp_WholeSaleAndRetailByPaThaKaReportResult
{
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string WholeSaleRetailNo { get; set; } = null!;
    public string? WholeSaleRetailUnitLevel { get; set; }
    public string WholeSaleRetailStreetNumberStreetName { get; set; } = null!;
    public string WholeSaleRetailQuarterCityTownship { get; set; } = null!;
    public string WholeSaleRetailState { get; set; } = null!;
    public string WholeSaleRetailCountry { get; set; } = null!;
    public string? WholeSaleRetailPostalCode { get; set; }
    public DateTime WholeSaleRetailIssuedDate { get; set; }
    public DateTime WholeSaleRetailEndDate { get; set; }
}

public static class sp_WholeSaleAndRetailByPaThaKaReport
{
    public static IQueryable<sp_WholeSaleAndRetailByPaThaKaReportResult> Query(
        TradeNetDbContext db,
        sp_WholeSaleAndRetailByPaThaKaReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from wholeSaleRetail in db.WholeSaleRetails
               join paThaKa in db.PaThaKas on wholeSaleRetail.PaThaKaId equals paThaKa.Id
               where paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo
               select new sp_WholeSaleAndRetailByPaThaKaReportResult
               {
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   CompanyName = wholeSaleRetail.CompanyName,
                   UnitLevel = paThaKa.UnitLevel,
                   StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   QuarterCityTownship = paThaKa.QuarterCityTownship,
                   State = paThaKa.State,
                   Country = paThaKa.Country,
                   PostalCode = paThaKa.PostalCode,
                   WholeSaleRetailNo = wholeSaleRetail.WholeSaleRetailNo,
                   WholeSaleRetailUnitLevel = wholeSaleRetail.WholeSaleRetailUnitLevel,
                   WholeSaleRetailStreetNumberStreetName = wholeSaleRetail.WholeSaleRetailStreetNumberStreetName,
                   WholeSaleRetailQuarterCityTownship = wholeSaleRetail.WholeSaleRetailQuarterCityTownship,
                   WholeSaleRetailState = wholeSaleRetail.WholeSaleRetailState,
                   WholeSaleRetailCountry = wholeSaleRetail.WholeSaleRetailCountry,
                   WholeSaleRetailPostalCode = wholeSaleRetail.WholeSaleRetailPostalCode,
                   WholeSaleRetailIssuedDate = wholeSaleRetail.IssuedDate,
                   WholeSaleRetailEndDate = wholeSaleRetail.EndDate
               };
    }
}
