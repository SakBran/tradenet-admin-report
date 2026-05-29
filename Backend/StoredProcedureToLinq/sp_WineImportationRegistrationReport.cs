using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_WineImportationRegistrationReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public string ApplyType { get; set; } = string.Empty;
}

public sealed class sp_WineImportationRegistrationReportResult
{
    public DateTime? Date { get; set; }
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string WineImportationNo { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? NRCNo { get; set; }
    public string FL11Name { get; set; } = null!;
    public string? FL11NRCNo { get; set; }
    public string FL4Name { get; set; } = null!;
    public string? FL4NRCNo { get; set; }
    public string FL5Name { get; set; } = null!;
    public string? FL5NRCNo { get; set; }
    public string WineType { get; set; } = string.Empty;
    public string PaymentType { get; set; } = null!;
    public string? VoucherNo { get; set; }
    public DateTime? VoucherDate { get; set; }
    public double TotalAmount { get; set; }
}
