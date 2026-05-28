using API.DBContext;
using API.Model.TradeNet;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace API.StoredProcedureToLinq;

public sealed class sp_WholeSaleRetailReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime Date { get; set; }
    public string ApplyType { get; set; } = string.Empty;
    public string FormType { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class sp_WholeSaleRetailReportResult
{
    public int? ApplicationCount { get; set; }
    public string? ApplyType { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? WholeSaleRetailNo { get; set; }
    public string? CompanyName { get; set; }
    public string? UnitLevel { get; set; }
    public string? StreetNumberStreetName { get; set; }
    public string? QuarterCityTownship { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? WholeSaleRetailUnitLevel { get; set; }
    public string? WholeSaleRetailStreetNumberStreetName { get; set; }
    public string? WholeSaleRetailQuarterCityTownship { get; set; }
    public string? WholeSaleRetailState { get; set; }
    public string? WholeSaleRetailCountry { get; set; }
    public string? WholeSaleRetailostalCode { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public static class sp_WholeSaleRetailReport
{
    private const string Approved = "Approved";
    private const string Summary = "Summary";
    private const string Valid = "Valid";
    private const string Invalid = "Invalid";

    public static IQueryable<sp_WholeSaleRetailReportResult> Query(
        TradeNetDbContext db,
        sp_WholeSaleRetailReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Type == Summary)
        {
            return SummaryQuery(db, request);
        }

        if (request.ApplyType == Valid)
        {
            return WholeSaleRetailDetailQuery(
                db,
                request,
                wholeSaleRetail => wholeSaleRetail.EndDate > request.Date);
        }

        if (request.ApplyType == Invalid)
        {
            return WholeSaleRetailDetailQuery(
                db,
                request,
                wholeSaleRetail => wholeSaleRetail.EndDate < request.Date);
        }

        return from wholeSaleRetail in db.WholeSaleRetails
               join paThaKa in db.PaThaKas on wholeSaleRetail.PaThaKaId equals paThaKa.Id
               join registration in db.WholeSaleRetailRegistrations
                   on wholeSaleRetail.WholeSaleRetailNo equals registration.WholeSaleRetailNo
               where registration.ApplyType == request.ApplyType
                   && registration.Status == Approved
                   && wholeSaleRetail.RegistrationType == request.FormType
                   && registration.CreatedDate >= request.FromDate
                   && registration.CreatedDate <= request.ToDate
               select new sp_WholeSaleRetailReportResult
               {
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   WholeSaleRetailNo = wholeSaleRetail.WholeSaleRetailNo,
                   CompanyName = paThaKa.CompanyName,
                   UnitLevel = paThaKa.UnitLevel,
                   StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   QuarterCityTownship = paThaKa.QuarterCityTownship,
                   State = paThaKa.State,
                   Country = paThaKa.Country,
                   PostalCode = paThaKa.PostalCode,
                   WholeSaleRetailUnitLevel = paThaKa.UnitLevel,
                   WholeSaleRetailStreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   WholeSaleRetailQuarterCityTownship = paThaKa.QuarterCityTownship,
                   WholeSaleRetailState = paThaKa.State,
                   WholeSaleRetailCountry = paThaKa.Country,
                   WholeSaleRetailostalCode = paThaKa.PostalCode,
                   IssuedDate = wholeSaleRetail.IssuedDate,
                   EndDate = wholeSaleRetail.EndDate
               };
    }

    private static IQueryable<sp_WholeSaleRetailReportResult> SummaryQuery(
        TradeNetDbContext db,
        sp_WholeSaleRetailReportRequest request)
    {
        return GroupedSummaryRow(db.WholeSaleRetailRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "New"
                    && registration.Status == Approved
                    && registration.RegistrationType == request.FormType)
                .Select(_ => 1), "New")
            .Concat(GroupedSummaryRow(db.WholeSaleRetailRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Cancel"
                    && registration.Status == Approved
                    && registration.RegistrationType == request.FormType)
                .Select(_ => 1), "Cancel"))
            .Concat(CountSummaryRow(db.WholeSaleRetailRegistrations
                .Where(registration =>
                    registration.CreatedDate >= request.FromDate
                    && registration.CreatedDate <= request.ToDate
                    && registration.ApplyType == "Extension"
                    && registration.Status == Approved
                    && registration.RegistrationType == request.FormType)
                .Select(_ => 1), "Extension"))
            .Concat(CountSummaryRow(db.WholeSaleRetails
                .Where(wholeSaleRetail =>
                    wholeSaleRetail.EndDate > request.Date
                    && wholeSaleRetail.RegistrationType == request.FormType)
                .Select(_ => 1), Valid))
            .Concat(CountSummaryRow(db.WholeSaleRetails
                .Where(wholeSaleRetail =>
                    wholeSaleRetail.EndDate < request.Date
                    && wholeSaleRetail.RegistrationType == request.FormType)
                .Select(_ => 1), Invalid));
    }

    private static IQueryable<sp_WholeSaleRetailReportResult> GroupedSummaryRow(
        IQueryable<int> source,
        string applyType)
    {
        return source
            .GroupBy(_ => 1)
            .Select(group => new sp_WholeSaleRetailReportResult
            {
                ApplicationCount = group.Count(),
                ApplyType = applyType
            });
    }

    private static IQueryable<sp_WholeSaleRetailReportResult> CountSummaryRow(
        IQueryable<int> source,
        string applyType)
    {
        return source
            .DefaultIfEmpty()
            .GroupBy(_ => 1)
            .Select(group => new sp_WholeSaleRetailReportResult
            {
                ApplicationCount = group.Sum(),
                ApplyType = applyType
            });
    }

    private static IQueryable<sp_WholeSaleRetailReportResult> WholeSaleRetailDetailQuery(
        TradeNetDbContext db,
        sp_WholeSaleRetailReportRequest request,
        Expression<Func<WholeSaleRetail, bool>> predicate)
    {
        return from wholeSaleRetail in db.WholeSaleRetails.Where(predicate)
               join paThaKa in db.PaThaKas on wholeSaleRetail.PaThaKaId equals paThaKa.Id
               where wholeSaleRetail.RegistrationType == request.FormType
               select new sp_WholeSaleRetailReportResult
               {
                   CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                   WholeSaleRetailNo = wholeSaleRetail.WholeSaleRetailNo,
                   CompanyName = paThaKa.CompanyName,
                   UnitLevel = paThaKa.UnitLevel,
                   StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   QuarterCityTownship = paThaKa.QuarterCityTownship,
                   State = paThaKa.State,
                   Country = paThaKa.Country,
                   PostalCode = paThaKa.PostalCode,
                   WholeSaleRetailUnitLevel = paThaKa.UnitLevel,
                   WholeSaleRetailStreetNumberStreetName = paThaKa.StreetNumberStreetName,
                   WholeSaleRetailQuarterCityTownship = paThaKa.QuarterCityTownship,
                   WholeSaleRetailState = paThaKa.State,
                   WholeSaleRetailCountry = paThaKa.Country,
                   WholeSaleRetailostalCode = paThaKa.PostalCode,
                   IssuedDate = wholeSaleRetail.IssuedDate,
                   EndDate = wholeSaleRetail.EndDate
               };
    }
}
