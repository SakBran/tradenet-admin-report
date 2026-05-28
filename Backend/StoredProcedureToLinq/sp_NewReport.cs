using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_NewReportRequest
{
    public string FormType { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int ExportImportSectionId { get; set; }
    public string CompanyRegistrationNo { get; set; } = string.Empty;
    public int SakhanId { get; set; }
    public string Auto { get; set; } = string.Empty;
}

public sealed class sp_NewReportResult
{
    public DateTime? Date { get; set; }
    public string? SectionCode { get; set; }
    public string? SectionName { get; set; }
    public string? OldLicenceNo { get; set; }
    public string? LicenceNo { get; set; }
    public string? SDate { get; set; }
    public string? CompanyRegistrationNo { get; set; }
    public string? CompanyName { get; set; }
    public string? UnitLevel { get; set; }
    public string? StreetNumberStreetName { get; set; }
    public string? QuarterCityTownship { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? Currency { get; set; }
    public decimal? Amount { get; set; }
    public string? Auto { get; set; }
    public string? Quota { get; set; }
    public string? CommodityType { get; set; }
    public int? SakhanId { get; set; }
    public string? SakhanCode { get; set; }
    public string? SakhanName { get; set; }
}

public static class sp_NewReport
{
    private const string New = "New";
    private const string Approved = "Approved";
    private const string PaThaKaCardType = "Pa Tha Ka";
    private const string IndividualTradingCardType = "Individual Trading";

    public static IQueryable<sp_NewReportResult> Query(
        TradeNetDbContext db,
        sp_NewReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return request.FormType switch
        {
            "Export Licence" => ExportLicenceQuery(db, request),
            "Import Licence" => ImportLicenceQuery(db, request),
            "Export Permit" => ExportPermitQuery(db, request),
            "Import Permit" => ImportPermitQuery(db, request),
            "Border Export Licence" => BorderExportLicenceQuery(db, request),
            "Border Import Licence" => BorderImportLicenceQuery(db, request),
            "Border Export Permit" => BorderExportPermitQuery(db, request),
            "Border Import Permit" => BorderImportPermitQuery(db, request),
            _ => EmptyQuery(db)
        };
    }

    private static IQueryable<sp_NewReportResult> ExportLicenceQuery(
        TradeNetDbContext db,
        sp_NewReportRequest request)
    {
        return
            from licence in db.ExportLicences
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CreatedDate >= request.FromDate
                && licence.CreatedDate <= request.ToDate
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                && (request.Auto == string.Empty || licence.Auto == request.Auto)
            select new sp_NewReportResult
            {
                Date = licence.CreatedDate,
                SectionCode = section.Code,
                SectionName = section.Name,
                OldLicenceNo = licence.OldExportLicenceNo,
                LicenceNo = licence.ExportLicenceNo,
                SDate = licence.LastDate == null
                    ? null
                    : (licence.LastDate.Value.Day < 10 ? "0" : string.Empty)
                    + licence.LastDate.Value.Day.ToString()
                    + "/"
                    + (licence.LastDate.Value.Month < 10 ? "0" : string.Empty)
                    + licence.LastDate.Value.Month.ToString()
                    + "/"
                    + licence.LastDate.Value.Year.ToString(),
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                Currency = (
                    from item in db.ExportLicenceItems
                    join currency in db.Currencies on item.CurrencyId equals currency.Id
                    where item.ExportLicenceId == licence.Id
                    select currency.Code).FirstOrDefault(),
                Amount = db.ExportLicenceItems
                    .Where(item => item.ExportLicenceId == licence.Id)
                    .Sum(item => (decimal?)item.Amount) ?? 0,
                Auto = licence.Auto
            };
    }

    private static IQueryable<sp_NewReportResult> ImportLicenceQuery(
        TradeNetDbContext db,
        sp_NewReportRequest request)
    {
        return
            from licence in db.ImportLicences
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CreatedDate >= request.FromDate
                && licence.CreatedDate <= request.ToDate
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
            select new sp_NewReportResult
            {
                Date = licence.CreatedDate,
                SectionCode = section.Code,
                SectionName = section.Name,
                OldLicenceNo = licence.OldImportLicenceNo,
                LicenceNo = licence.ImportLicenceNo,
                SDate = licence.LastDate == null
                    ? null
                    : (licence.LastDate.Value.Day < 10 ? "0" : string.Empty)
                    + licence.LastDate.Value.Day.ToString()
                    + "/"
                    + (licence.LastDate.Value.Month < 10 ? "0" : string.Empty)
                    + licence.LastDate.Value.Month.ToString()
                    + "/"
                    + licence.LastDate.Value.Year.ToString(),
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                Currency = (
                    from item in db.ImportLicenceItems
                    join currency in db.Currencies on item.CurrencyId equals currency.Id
                    where item.ImportLicenceId == licence.Id
                    select currency.Code).FirstOrDefault(),
                Amount = db.ImportLicenceItems
                    .Where(item => item.ImportLicenceId == licence.Id)
                    .Sum(item => (decimal?)item.Amount) ?? 0,
                Auto = licence.Auto,
                Quota = licence.Quota,
                CommodityType = licence.CommodityType
            };
    }

    private static IQueryable<sp_NewReportResult> ExportPermitQuery(
        TradeNetDbContext db,
        sp_NewReportRequest request)
    {
        return
            from permit in db.ExportPermits
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
            where permit.ApplyType == New
                && permit.Status == Approved
                && permit.CreatedDate >= request.FromDate
                && permit.CreatedDate <= request.ToDate
                && (request.ExportImportSectionId == 0 || permit.ExportImportSectionId == request.ExportImportSectionId)
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
            select new sp_NewReportResult
            {
                Date = permit.CreatedDate,
                SectionCode = section.Code,
                SectionName = section.Name,
                OldLicenceNo = permit.OldExportPermitNo,
                LicenceNo = permit.ExportPermitNo,
                SDate = permit.LastDate == null
                    ? null
                    : (permit.LastDate.Value.Day < 10 ? "0" : string.Empty)
                    + permit.LastDate.Value.Day.ToString()
                    + "/"
                    + (permit.LastDate.Value.Month < 10 ? "0" : string.Empty)
                    + permit.LastDate.Value.Month.ToString()
                    + "/"
                    + permit.LastDate.Value.Year.ToString(),
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                Currency = (
                    from item in db.ExportPermitItems
                    join currency in db.Currencies on item.CurrencyId equals currency.Id
                    where item.ExportPermitId == permit.Id
                    select currency.Code).FirstOrDefault(),
                Amount = db.ExportPermitItems
                    .Where(item => item.ExportPermitId == permit.Id)
                    .Sum(item => (decimal?)item.Amount) ?? 0
            };
    }

    private static IQueryable<sp_NewReportResult> ImportPermitQuery(
        TradeNetDbContext db,
        sp_NewReportRequest request)
    {
        return
            from permit in db.ImportPermits
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
            where permit.ApplyType == New
                && permit.Status == Approved
                && permit.CreatedDate >= request.FromDate
                && permit.CreatedDate <= request.ToDate
                && (request.ExportImportSectionId == 0 || permit.ExportImportSectionId == request.ExportImportSectionId)
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
            select new sp_NewReportResult
            {
                Date = permit.CreatedDate,
                SectionCode = section.Code,
                SectionName = section.Name,
                OldLicenceNo = permit.OldImportPermitNo,
                LicenceNo = permit.ImportPermitNo,
                SDate = permit.LastDate == null
                    ? null
                    : (permit.LastDate.Value.Day < 10 ? "0" : string.Empty)
                    + permit.LastDate.Value.Day.ToString()
                    + "/"
                    + (permit.LastDate.Value.Month < 10 ? "0" : string.Empty)
                    + permit.LastDate.Value.Month.ToString()
                    + "/"
                    + permit.LastDate.Value.Year.ToString(),
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                Currency = (
                    from item in db.ImportPermitItems
                    join currency in db.Currencies on item.CurrencyId equals currency.Id
                    where item.ImportPermitId == permit.Id
                    select currency.Code).FirstOrDefault(),
                Amount = db.ImportPermitItems
                    .Where(item => item.ImportPermitId == permit.Id)
                    .Sum(item => (decimal?)item.Amount) ?? 0
            };
    }

    private static IQueryable<sp_NewReportResult> BorderExportLicenceQuery(
        TradeNetDbContext db,
        sp_NewReportRequest request)
    {
        var paThaKaQuery =
            from licence in db.BorderExportLicences
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            join sakhan in db.Sakhans on licence.SakhanId equals sakhan.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == PaThaKaCardType
                && licence.CreatedDate >= request.FromDate
                && licence.CreatedDate <= request.ToDate
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
                && (request.Auto == string.Empty || licence.Auto == request.Auto)
            select new sp_NewReportResult
            {
                Date = licence.CreatedDate,
                SectionCode = section.Code,
                SectionName = section.Name,
                OldLicenceNo = licence.OldExportLicenceNo,
                LicenceNo = licence.ExportLicenceNo,
                SDate = licence.CreatedDate == null
                    ? null
                    : (licence.CreatedDate.Value.Day < 10 ? "0" : string.Empty)
                    + licence.CreatedDate.Value.Day.ToString()
                    + "/"
                    + (licence.CreatedDate.Value.Month < 10 ? "0" : string.Empty)
                    + licence.CreatedDate.Value.Month.ToString()
                    + "/"
                    + licence.CreatedDate.Value.Year.ToString(),
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                Currency = (
                    from item in db.BorderExportLicenceItems
                    join currency in db.Currencies on item.CurrencyId equals currency.Id
                    where item.BorderExportLicenceId == licence.Id
                    select currency.Code).FirstOrDefault(),
                Amount = db.BorderExportLicenceItems
                    .Where(item => item.BorderExportLicenceId == licence.Id)
                    .Sum(item => (decimal?)item.Amount) ?? 0,
                Auto = licence.Auto,
                SakhanId = sakhan.Id,
                SakhanCode = sakhan.Code,
                SakhanName = sakhan.Name
            };

        var individualTradingQuery =
            from licence in db.BorderExportLicences
            join individualTrading in db.IndividualTradings on licence.IndividualTradingId equals individualTrading.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            join sakhan in db.Sakhans on licence.SakhanId equals sakhan.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == IndividualTradingCardType
                && licence.CreatedDate >= request.FromDate
                && licence.CreatedDate <= request.ToDate
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.CompanyRegistrationNo == string.Empty || individualTrading.Tinno == request.CompanyRegistrationNo)
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
                && (request.Auto == string.Empty || licence.Auto == request.Auto)
            select new sp_NewReportResult
            {
                Date = licence.CreatedDate,
                SectionCode = section.Code,
                SectionName = section.Name,
                OldLicenceNo = licence.OldExportLicenceNo,
                LicenceNo = licence.ExportLicenceNo,
                SDate = licence.CreatedDate == null
                    ? null
                    : (licence.CreatedDate.Value.Day < 10 ? "0" : string.Empty)
                    + licence.CreatedDate.Value.Day.ToString()
                    + "/"
                    + (licence.CreatedDate.Value.Month < 10 ? "0" : string.Empty)
                    + licence.CreatedDate.Value.Month.ToString()
                    + "/"
                    + licence.CreatedDate.Value.Year.ToString(),
                CompanyRegistrationNo = individualTrading.Tinno,
                CompanyName = individualTrading.Name,
                UnitLevel = individualTrading.UnitLevel,
                StreetNumberStreetName = individualTrading.StreetNumberStreetName,
                QuarterCityTownship = individualTrading.QuarterCityTownship,
                State = individualTrading.State,
                Country = individualTrading.Country,
                PostalCode = individualTrading.PostalCode,
                Currency = (
                    from item in db.BorderExportLicenceItems
                    join currency in db.Currencies on item.CurrencyId equals currency.Id
                    where item.BorderExportLicenceId == licence.Id
                    select currency.Code).FirstOrDefault(),
                Amount = db.BorderExportLicenceItems
                    .Where(item => item.BorderExportLicenceId == licence.Id)
                    .Sum(item => (decimal?)item.Amount) ?? 0,
                Auto = licence.Auto,
                SakhanId = sakhan.Id,
                SakhanCode = sakhan.Code,
                SakhanName = sakhan.Name
            };

        return paThaKaQuery.Concat(individualTradingQuery).OrderBy(result => result.Date);
    }

    private static IQueryable<sp_NewReportResult> BorderImportLicenceQuery(
        TradeNetDbContext db,
        sp_NewReportRequest request)
    {
        var paThaKaQuery =
            from licence in db.BorderImportLicences
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            join sakhan in db.Sakhans on licence.SakhanId equals sakhan.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == PaThaKaCardType
                && licence.CreatedDate >= request.FromDate
                && licence.CreatedDate <= request.ToDate
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
            select new sp_NewReportResult
            {
                Date = licence.CreatedDate,
                SectionCode = section.Code,
                SectionName = section.Name,
                OldLicenceNo = licence.OldImportLicenceNo,
                LicenceNo = licence.ImportLicenceNo,
                SDate = licence.CreatedDate == null
                    ? null
                    : (licence.CreatedDate.Value.Day < 10 ? "0" : string.Empty)
                    + licence.CreatedDate.Value.Day.ToString()
                    + "/"
                    + (licence.CreatedDate.Value.Month < 10 ? "0" : string.Empty)
                    + licence.CreatedDate.Value.Month.ToString()
                    + "/"
                    + licence.CreatedDate.Value.Year.ToString(),
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                Currency = (
                    from item in db.BorderImportLicenceItems
                    join currency in db.Currencies on item.CurrencyId equals currency.Id
                    where item.BorderImportLicenceId == licence.Id
                    select currency.Code).FirstOrDefault(),
                Amount = db.BorderImportLicenceItems
                    .Where(item => item.BorderImportLicenceId == licence.Id)
                    .Sum(item => (decimal?)item.Amount) ?? 0,
                Auto = licence.Auto,
                Quota = licence.Quota,
                SakhanId = sakhan.Id,
                SakhanCode = sakhan.Code,
                SakhanName = sakhan.Name
            };

        var individualTradingQuery =
            from licence in db.BorderImportLicences
            join individualTrading in db.IndividualTradings on licence.IndividualTradingId equals individualTrading.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            join sakhan in db.Sakhans on licence.SakhanId equals sakhan.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == IndividualTradingCardType
                && licence.CreatedDate >= request.FromDate
                && licence.CreatedDate <= request.ToDate
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
                && (request.CompanyRegistrationNo == string.Empty || individualTrading.Tinno == request.CompanyRegistrationNo)
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
            select new sp_NewReportResult
            {
                Date = licence.CreatedDate,
                SectionCode = section.Code,
                SectionName = section.Name,
                OldLicenceNo = licence.OldImportLicenceNo,
                LicenceNo = licence.ImportLicenceNo,
                SDate = licence.CreatedDate == null
                    ? null
                    : (licence.CreatedDate.Value.Day < 10 ? "0" : string.Empty)
                    + licence.CreatedDate.Value.Day.ToString()
                    + "/"
                    + (licence.CreatedDate.Value.Month < 10 ? "0" : string.Empty)
                    + licence.CreatedDate.Value.Month.ToString()
                    + "/"
                    + licence.CreatedDate.Value.Year.ToString(),
                CompanyRegistrationNo = individualTrading.Tinno,
                CompanyName = individualTrading.Name,
                UnitLevel = individualTrading.UnitLevel,
                StreetNumberStreetName = individualTrading.StreetNumberStreetName,
                QuarterCityTownship = individualTrading.QuarterCityTownship,
                State = individualTrading.State,
                Country = individualTrading.Country,
                PostalCode = individualTrading.PostalCode,
                Currency = (
                    from item in db.BorderImportLicenceItems
                    join currency in db.Currencies on item.CurrencyId equals currency.Id
                    where item.BorderImportLicenceId == licence.Id
                    select currency.Code).FirstOrDefault(),
                Amount = db.BorderImportLicenceItems
                    .Where(item => item.BorderImportLicenceId == licence.Id)
                    .Sum(item => (decimal?)item.Amount) ?? 0,
                Auto = licence.Auto,
                Quota = licence.Quota,
                SakhanId = sakhan.Id,
                SakhanCode = sakhan.Code,
                SakhanName = sakhan.Name
            };

        return paThaKaQuery.Concat(individualTradingQuery).OrderBy(result => result.Date);
    }

    private static IQueryable<sp_NewReportResult> BorderExportPermitQuery(
        TradeNetDbContext db,
        sp_NewReportRequest request)
    {
        return
            from permit in db.BorderExportPermits
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
            join sakhan in db.Sakhans on permit.SakhanId equals sakhan.Id
            where permit.ApplyType == New
                && permit.Status == Approved
                && permit.CreatedDate >= request.FromDate
                && permit.CreatedDate <= request.ToDate
                && (request.ExportImportSectionId == 0 || permit.ExportImportSectionId == request.ExportImportSectionId)
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                && (request.SakhanId == 0 || permit.SakhanId == request.SakhanId)
            select new sp_NewReportResult
            {
                Date = permit.CreatedDate,
                SectionCode = section.Code,
                SectionName = section.Name,
                OldLicenceNo = permit.OldExportPermitNo,
                LicenceNo = permit.ExportPermitNo,
                SDate = permit.CreatedDate == null
                    ? null
                    : (permit.CreatedDate.Value.Day < 10 ? "0" : string.Empty)
                    + permit.CreatedDate.Value.Day.ToString()
                    + "/"
                    + (permit.CreatedDate.Value.Month < 10 ? "0" : string.Empty)
                    + permit.CreatedDate.Value.Month.ToString()
                    + "/"
                    + permit.CreatedDate.Value.Year.ToString(),
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                Currency = (
                    from item in db.BorderExportPermitItems
                    join currency in db.Currencies on item.CurrencyId equals currency.Id
                    where item.BorderExportPermitId == permit.Id
                    select currency.Code).FirstOrDefault(),
                Amount = db.BorderExportPermitItems
                    .Where(item => item.BorderExportPermitId == permit.Id)
                    .Sum(item => (decimal?)item.Amount) ?? 0,
                SakhanId = sakhan.Id,
                SakhanCode = sakhan.Code,
                SakhanName = sakhan.Name
            };
    }

    private static IQueryable<sp_NewReportResult> BorderImportPermitQuery(
        TradeNetDbContext db,
        sp_NewReportRequest request)
    {
        return
            from permit in db.BorderImportPermits
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
            join sakhan in db.Sakhans on permit.SakhanId equals sakhan.Id
            where permit.ApplyType == New
                && permit.Status == Approved
                && permit.CreatedDate >= request.FromDate
                && permit.CreatedDate <= request.ToDate
                && (request.ExportImportSectionId == 0 || permit.ExportImportSectionId == request.ExportImportSectionId)
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                && (request.SakhanId == 0 || permit.SakhanId == request.SakhanId)
            select new sp_NewReportResult
            {
                Date = permit.CreatedDate,
                SectionCode = section.Code,
                SectionName = section.Name,
                OldLicenceNo = permit.OldImportPermitNo,
                LicenceNo = permit.ImportPermitNo,
                SDate = permit.CreatedDate == null
                    ? null
                    : (permit.CreatedDate.Value.Day < 10 ? "0" : string.Empty)
                    + permit.CreatedDate.Value.Day.ToString()
                    + "/"
                    + (permit.CreatedDate.Value.Month < 10 ? "0" : string.Empty)
                    + permit.CreatedDate.Value.Month.ToString()
                    + "/"
                    + permit.CreatedDate.Value.Year.ToString(),
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                Currency = (
                    from item in db.BorderImportPermitItems
                    join currency in db.Currencies on item.CurrencyId equals currency.Id
                    where item.BorderImportPermitId == permit.Id
                    select currency.Code).FirstOrDefault(),
                Amount = db.BorderImportPermitItems
                    .Where(item => item.BorderImportPermitId == permit.Id)
                    .Sum(item => (decimal?)item.Amount) ?? 0,
                SakhanId = sakhan.Id,
                SakhanCode = sakhan.Code,
                SakhanName = sakhan.Name
            };
    }

    private static IQueryable<sp_NewReportResult> EmptyQuery(TradeNetDbContext db)
    {
        return db.ExportLicences
            .Where(_ => false)
            .Select(licence => new sp_NewReportResult
            {
                Date = licence.CreatedDate,
                SectionCode = licence.ExportImportSectionId.ToString(),
                SectionName = licence.ExportImportSectionId.ToString(),
                OldLicenceNo = licence.OldExportLicenceNo,
                LicenceNo = licence.ExportLicenceNo,
                SDate = licence.ApplicationNo,
                CompanyRegistrationNo = licence.PaThaKaId,
                CompanyName = licence.PaThaKaId,
                UnitLevel = licence.PaThaKaId,
                StreetNumberStreetName = licence.PaThaKaId,
                QuarterCityTownship = licence.PaThaKaId,
                State = licence.PaThaKaId,
                Country = licence.PaThaKaId,
                PostalCode = licence.PaThaKaId,
                Currency = licence.ApplyType,
                Amount = 0m,
                Auto = licence.Auto,
                Quota = licence.CommodityType,
                CommodityType = licence.CommodityType,
                SakhanId = licence.ExportImportSectionId,
                SakhanCode = licence.ExportImportSectionId.ToString(),
                SakhanName = licence.ExportImportSectionId.ToString()
            });
    }
}
