using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_ImportLicenceDaily_Detail_ReportRequest
{
    public string Type { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int PaThaKaTypeId { get; set; }
    public int ExportImportSectionId { get; set; }
    public int ExportImportMethodId { get; set; }
    public int ExportImportIncotermId { get; set; }
    public int SellerCountryId { get; set; }
    public string CompanyRegistrationNo { get; set; } = string.Empty;
    public int SakhanId { get; set; }
}

public sealed class sp_ImportLicenceDaily_Detail_ReportResult
{
    public int PaThaKaTypeId { get; set; }
    public string PaThaKaTypeCode { get; set; } = null!;
    public string PaThaKaTypeName { get; set; } = null!;
    public int? SakhanId { get; set; }
    public string? SakhanCode { get; set; }
    public string? SakhanName { get; set; }
    public int ExportImportSectionId { get; set; }
    public int ExportImportMethodId { get; set; }
    public int ExportImportIncotermId { get; set; }
    public int SellerCountryId { get; set; }
    public string SectionCode { get; set; } = null!;
    public string SectionName { get; set; } = null!;
    public string LicenceNo { get; set; } = null!;
    public DateTime? LicenceDate { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string SellerName { get; set; } = null!;
    public string SellerAddress { get; set; } = null!;
    public string? SellerCountry { get; set; }
    public string PortofDischarge { get; set; } = null!;
    public DateTime? LastDate { get; set; }
    public string MethodName { get; set; } = null!;
    public string? ConsignedCountry { get; set; }
    public string? CountryofOrigin { get; set; }
    public string HSCode { get; set; } = null!;
    public string? HSDescription { get; set; }
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Conditions { get; set; }
}

public static class sp_ImportLicenceDaily_Detail_Report
{
    public static IQueryable<sp_ImportLicenceDaily_Detail_ReportResult> Query(
        TradeNetDbContext db,
        sp_ImportLicenceDaily_Detail_ReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var detailRequest = new sp_ImportLicenceDetailReportRequest
        {
            Type = request.Type,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            PaThaKaTypeId = request.PaThaKaTypeId,
            ExportImportSectionId = request.ExportImportSectionId,
            ExportImportMethodId = request.ExportImportMethodId,
            ExportImportIncotermId = request.ExportImportIncotermId,
            SellerCountryId = request.SellerCountryId,
            CompanyRegistrationNo = request.CompanyRegistrationNo,
            SakhanId = request.SakhanId
        };

        return sp_ImportLicenceDetailReport.Query(db, detailRequest)
            .Select(row => new sp_ImportLicenceDaily_Detail_ReportResult
            {
                PaThaKaTypeId = row.PaThaKaTypeId,
                PaThaKaTypeCode = row.PaThaKaTypeCode,
                PaThaKaTypeName = row.PaThaKaTypeName,
                SakhanId = row.SakhanId,
                SakhanCode = row.SakhanCode,
                SakhanName = row.SakhanName,
                ExportImportSectionId = row.ExportImportSectionId,
                ExportImportMethodId = row.ExportImportMethodId,
                ExportImportIncotermId = row.ExportImportIncotermId,
                SellerCountryId = row.SellerCountryId,
                SectionCode = row.SectionCode,
                SectionName = row.SectionName,
                LicenceNo = row.LicenceNo,
                LicenceDate = row.LicenceDate,
                CompanyRegistrationNo = row.CompanyRegistrationNo,
                CompanyName = row.CompanyName,
                UnitLevel = row.UnitLevel,
                StreetNumberStreetName = row.StreetNumberStreetName,
                QuarterCityTownship = row.QuarterCityTownship,
                State = row.State,
                Country = row.Country,
                PostalCode = row.PostalCode,
                SellerName = row.SellerName,
                SellerAddress = row.SellerAddress,
                SellerCountry = row.SellerCountry,
                PortofDischarge = row.PortofDischarge,
                LastDate = row.LastDate,
                MethodName = row.MethodName,
                ConsignedCountry = request.Type == "Border" ? row.ConsignedCountry : null,
                CountryofOrigin = request.Type == "Border" ? row.CountryofOrigin : null,
                HSCode = row.HSCode,
                HSDescription = row.HSDescription,
                Unit = row.Unit,
                Price = row.Price,
                Quantity = row.Quantity,
                Amount = row.Amount,
                Currency = row.Currency,
                Conditions = row.Conditions
            });
    }
}
