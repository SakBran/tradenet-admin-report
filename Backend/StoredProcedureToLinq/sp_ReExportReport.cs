using API.DBContext;
using API.Model.TradeNet;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace API.StoredProcedureToLinq;

public sealed class sp_ReExportReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime Date { get; set; }
    public string ApplyType { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class sp_ReExportReportResult
{
    public int? ApplicationCount { get; set; }
    public string? ApplyType { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? ReExportNo { get; set; }
    public string? CompanyName { get; set; }
    public string? UnitLevel { get; set; }
    public string? StreetNumberStreetName { get; set; }
    public string? QuarterCityTownship { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? ReExportUnitLevel { get; set; }
    public string? ReExportStreetNumberStreetName { get; set; }
    public string? ReExportQuarterCityTownship { get; set; }
    public string? ReExportState { get; set; }
    public string? ReExportCountry { get; set; }
    public string? ReExportPostalCode { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public static class sp_ReExportReport
{
    private const string Approved = "Approved";
    private const string Summary = "Summary";
    private const string Valid = "Valid";
    private const string Invalid = "Invalid";

    public static IQueryable<sp_ReExportReportResult> Query(
        TradeNetDbContext db,
        sp_ReExportReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Type == Summary)
        {
            return SummaryQuery(db, request);
        }

        if (request.ApplyType == Valid)
        {
            return ReExportDetailQuery(db, reExport => reExport.EndDate > request.Date);
        }

        if (request.ApplyType == Invalid)
        {
            return ReExportDetailQuery(db, reExport => reExport.EndDate < request.Date);
        }

        return from reExport in db.ReExports
               join paThaKa in db.PaThaKas on reExport.PaThaKaId equals paThaKa.Id
               join registration in db.ReExportRegistrations on reExport.ReExportNo equals registration.ReExportNo
               where registration.ApplyType == request.ApplyType
                   && registration.Status == Approved
                   && registration.CreatedDate >= request.FromDate
                   && registration.CreatedDate <= request.ToDate
               select new sp_ReExportReportResult
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

    private static IQueryable<sp_ReExportReportResult> SummaryQuery(
        TradeNetDbContext db,
        sp_ReExportReportRequest request)
    {
        return GroupedSummaryRow(db.ReExportRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "New"
                    && registration.Status == Approved)
                .Select(_ => 1), "New")
            .Concat(GroupedSummaryRow(db.ReExportRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Cancel"
                    && registration.Status == Approved)
                .Select(_ => 1), "Cancel"))
            .Concat(CountSummaryRow(db.ReExportRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Extension"
                    && registration.Status == Approved)
                .Select(_ => 1), "Extension"))
            .Concat(CountSummaryRow(db.ReExports
                .Where(reExport => reExport.EndDate > request.Date)
                .Select(_ => 1), Valid))
            .Concat(CountSummaryRow(db.ReExports
                .Where(reExport => reExport.EndDate < request.Date)
                .Select(_ => 1), Invalid));
    }

    private static IQueryable<sp_ReExportReportResult> GroupedSummaryRow(
        IQueryable<int> source,
        string applyType)
    {
        return source
            .GroupBy(_ => 1)
            .Select(group => new sp_ReExportReportResult
            {
                ApplicationCount = group.Count(),
                ApplyType = applyType
            });
    }

    private static IQueryable<sp_ReExportReportResult> CountSummaryRow(
        IQueryable<int> source,
        string applyType)
    {
        return source
            .DefaultIfEmpty()
            .GroupBy(_ => 1)
            .Select(group => new sp_ReExportReportResult
            {
                ApplicationCount = group.Sum(),
                ApplyType = applyType
            });
    }

    private static IQueryable<sp_ReExportReportResult> ReExportDetailQuery(
        TradeNetDbContext db,
        Expression<Func<ReExport, bool>> predicate)
    {
        return from reExport in db.ReExports.Where(predicate)
               join paThaKa in db.PaThaKas on reExport.PaThaKaId equals paThaKa.Id
               select new sp_ReExportReportResult
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
