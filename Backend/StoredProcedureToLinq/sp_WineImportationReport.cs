using System;

namespace API.StoredProcedureToLinq;

public sealed class sp_WineImportationReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime Date { get; set; }
    public string ApplyType { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class sp_WineImportationReportResult
{
    public int? ApplicationCount { get; set; }
    public string? ApplyType { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? WineImportationNo { get; set; }
    public string? CompanyName { get; set; }
    public string? UnitLevel { get; set; }
    public string? StreetNumberStreetName { get; set; }
    public string? QuarterCityTownship { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? Name { get; set; }
    public string? NRCNo { get; set; }
    public string? FL11Name { get; set; }
    public string? FL11NRCNo { get; set; }
    public string? FL4Name { get; set; }
    public string? FL4NRCNo { get; set; }
    public string? FL5Name { get; set; }
    public string? FL5NRCNo { get; set; }
    public string? WineType { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? EndDate { get; set; }
}
