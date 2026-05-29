using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_ImportPermitDetailReportRequest
{
    public string Type { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int PaThaKaTypeId { get; set; }
    public int ExportImportSectionId { get; set; }
    public int SellerCountryId { get; set; }
    public string CompanyRegistrationNo { get; set; } = string.Empty;
    public int SakhanId { get; set; }
}

public sealed class sp_ImportPermitDetailReportResult
{
    public int PaThaKaTypeId { get; set; }
    public string PaThaKaTypeCode { get; set; } = null!;
    public string PaThaKaTypeName { get; set; } = null!;
    public int? SakhanId { get; set; }
    public string? SakhanCode { get; set; }
    public string? SakhanName { get; set; }
    public int ExportImportSectionId { get; set; }
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
    public string AuthorisedAgentName { get; set; } = null!;
    public string AuthorisedAgentAddress { get; set; } = null!;
    public string? SellerCountry { get; set; }
    public string PortofShipment { get; set; } = null!;
    public string PortofDischarge { get; set; } = null!;
    public string CountryofOrigin { get; set; } = null!;
    public DateTime? LastDate { get; set; }
    public string HSCode { get; set; } = null!;
    public string? HSDescription { get; set; }
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string NRCNo { get; set; } = null!;
    public string PermitType { get; set; } = null!;
    public string? Conditions { get; set; }
    public DateTime? ApproveDate { get; set; }
}
