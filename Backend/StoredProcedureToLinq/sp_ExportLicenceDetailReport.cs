using System;

namespace API.StoredProcedureToLinq;

public sealed class sp_ExportLicenceDetailReportRequest
{
    public string Type { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int PaThaKaTypeId { get; set; }
    public int ExportImportSectionId { get; set; }
    public int ExportImportMethodId { get; set; }
    public int ExportImportIncotermId { get; set; }
    public int BuyerCountryId { get; set; }
    public string CompanyRegistrationNo { get; set; } = string.Empty;
    public int SakhanId { get; set; }
    public string Auto { get; set; } = string.Empty;
    public string HSCode { get; set; } = string.Empty;
}

public sealed class sp_ExportLicenceDetailReportResult
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
    public int BuyerCountryId { get; set; }
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
    public string BuyerName { get; set; } = null!;
    public string BuyerAddress { get; set; } = null!;
    public string? BuyerCountry { get; set; }
    public string PortofExport { get; set; } = null!;
    public string PortofDischarge { get; set; } = null!;
    public DateTime? LastDate { get; set; }
    public string MethodName { get; set; } = null!;
    public string DestinationCountry { get; set; } = null!;
    public string? ConsignedCountry { get; set; }
    public string? CountryofOrigin { get; set; }
    public string HSCode { get; set; } = null!;
    public string HSDescription { get; set; } = null!;
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Conditions { get; set; }
    public string? ApplicationNo { get; set; }
    public DateTime? ApplicationDate { get; set; }
    public string? CommodityType { get; set; }
    public DateTime? ApproveDate { get; set; }
}
