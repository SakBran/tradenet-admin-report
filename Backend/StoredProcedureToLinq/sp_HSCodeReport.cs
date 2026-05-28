using API.DBContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_HSCodeReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string FormType { get; set; } = string.Empty;
    public string FilterType { get; set; } = string.Empty;
    public string HSCode { get; set; } = string.Empty;
    public int SakhanId { get; set; }
}

public sealed class sp_HSCodeReportResult
{
    public int? SakhanId { get; set; }
    public string? SectionCode { get; set; }
    public int HSCodeId { get; set; }
    public string HSCode { get; set; } = null!;
    public string? HSDescription { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string LicenceNo { get; set; } = null!;
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
}

public static class sp_HSCodeReport
{
    private const string New = "New";
    private const string Approved = "Approved";
    private const string PaThaKaCardType = "Pa Tha Ka";
    private const string IndividualTradingCardType = "Individual Trading";

    public static IQueryable<sp_HSCodeReportResult> Query(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return request.FormType switch
        {
            "Export Licence" => ExportLicenceRows(db, request),
            "Import Licence" => ImportLicenceRows(db, request),
            "Export Permit" => ExportPermitRows(db, request),
            "Import Permit" => ImportPermitRows(db, request),
            "Border Export Licence" => BorderExportLicenceRows(db, request),
            "Border Import Licence" => BorderImportLicenceRows(db, request),
            "Border Export Permit" => BorderExportPermitRows(db, request),
            "Border Import Permit" => BorderImportPermitRows(db, request),
            _ => EmptyRows(db)
        };
    }

    private static IQueryable<sp_HSCodeReportResult> ExportLicenceRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        return
            from licence in db.ExportLicences
            join item in db.ExportLicenceItems on licence.Id equals item.ExportLicenceId
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.LicenceDate >= request.FromDate
                && licence.LicenceDate <= request.ToDate
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            orderby hsCode.Id
            select new sp_HSCodeReportResult
            {
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = licence.ExportLicenceNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };
    }

    private static IQueryable<sp_HSCodeReportResult> ImportLicenceRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        return
            from licence in db.ImportLicences
            join item in db.ImportLicenceItems on licence.Id equals item.ImportLicenceId
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.LicenceDate >= request.FromDate
                && licence.LicenceDate <= request.ToDate
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            orderby hsCode.Id
            select new sp_HSCodeReportResult
            {
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = licence.ImportLicenceNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };
    }

    private static IQueryable<sp_HSCodeReportResult> ExportPermitRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        return
            from permit in db.ExportPermits
            join item in db.ExportPermitItems on permit.Id equals item.ExportPermitId
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
            where permit.ApplyType == New
                && permit.Status == Approved
                && permit.LicenceDate >= request.FromDate
                && permit.LicenceDate <= request.ToDate
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            orderby hsCode.Id
            select new sp_HSCodeReportResult
            {
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = permit.ExportPermitNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };
    }

    private static IQueryable<sp_HSCodeReportResult> ImportPermitRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        return
            from permit in db.ImportPermits
            join item in db.ImportPermitItems on permit.Id equals item.ImportPermitId
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
            where permit.ApplyType == New
                && permit.Status == Approved
                && permit.LicenceDate >= request.FromDate
                && permit.LicenceDate <= request.ToDate
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            orderby hsCode.Id
            select new sp_HSCodeReportResult
            {
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = permit.ImportPermitNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };
    }

    private static IQueryable<sp_HSCodeReportResult> BorderExportLicenceRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        var paThaKaRows =
            from licence in db.BorderExportLicences
            join item in db.BorderExportLicenceItems on licence.Id equals item.BorderExportLicenceId
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == PaThaKaCardType
                && licence.LicenceDate >= request.FromDate
                && licence.LicenceDate <= request.ToDate
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            select new sp_HSCodeReportResult
            {
                SakhanId = licence.SakhanId,
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = licence.ExportLicenceNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };

        var individualRows =
            from licence in db.BorderExportLicences
            join item in db.BorderExportLicenceItems on licence.Id equals item.BorderExportLicenceId
            join individualTrading in db.IndividualTradings on licence.IndividualTradingId equals individualTrading.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == IndividualTradingCardType
                && licence.LicenceDate >= request.FromDate
                && licence.LicenceDate <= request.ToDate
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            select new sp_HSCodeReportResult
            {
                SakhanId = licence.SakhanId,
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = licence.ExportLicenceNo,
                CompanyRegistrationNo = individualTrading.Tinno,
                CompanyName = individualTrading.Name
            };

        return paThaKaRows.Concat(individualRows).OrderBy(row => row.HSCodeId);
    }

    private static IQueryable<sp_HSCodeReportResult> BorderImportLicenceRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        var paThaKaRows =
            from licence in db.BorderImportLicences
            join item in db.BorderImportLicenceItems on licence.Id equals item.BorderImportLicenceId
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == PaThaKaCardType
                && licence.LicenceDate >= request.FromDate
                && licence.LicenceDate <= request.ToDate
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            select new sp_HSCodeReportResult
            {
                SakhanId = licence.SakhanId,
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = licence.ImportLicenceNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };

        var individualRows =
            from licence in db.BorderImportLicences
            join item in db.BorderImportLicenceItems on licence.Id equals item.BorderImportLicenceId
            join individualTrading in db.IndividualTradings on licence.IndividualTradingId equals individualTrading.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where licence.ApplyType == New
                && licence.Status == Approved
                && licence.CardType == IndividualTradingCardType
                && licence.LicenceDate >= request.FromDate
                && licence.LicenceDate <= request.ToDate
                && (request.SakhanId == 0 || licence.SakhanId == request.SakhanId)
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            select new sp_HSCodeReportResult
            {
                SakhanId = licence.SakhanId,
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = licence.ImportLicenceNo,
                CompanyRegistrationNo = individualTrading.Tinno,
                CompanyName = individualTrading.Name
            };

        return paThaKaRows.Concat(individualRows).OrderBy(row => row.HSCodeId);
    }

    private static IQueryable<sp_HSCodeReportResult> BorderExportPermitRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        return
            from permit in db.BorderExportPermits
            join item in db.BorderExportPermitItems on permit.Id equals item.BorderExportPermitId
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
            where permit.ApplyType == New
                && permit.Status == Approved
                && permit.LicenceDate >= request.FromDate
                && permit.LicenceDate <= request.ToDate
                && (request.SakhanId == 0 || permit.SakhanId == request.SakhanId)
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            orderby hsCode.Id
            select new sp_HSCodeReportResult
            {
                SakhanId = permit.SakhanId,
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = permit.ExportPermitNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };
    }

    private static IQueryable<sp_HSCodeReportResult> BorderImportPermitRows(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request)
    {
        return
            from permit in db.BorderImportPermits
            join item in db.BorderImportPermitItems on permit.Id equals item.BorderImportPermitId
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            join hsCode in db.Hscodes on item.HscodeId equals hsCode.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            join section in db.ExportImportSections on permit.ExportImportSectionId equals section.Id
            where permit.ApplyType == New
                && permit.Status == Approved
                && permit.LicenceDate >= request.FromDate
                && permit.LicenceDate <= request.ToDate
                && (request.SakhanId == 0 || permit.SakhanId == request.SakhanId)
                && (request.HSCode == string.Empty
                    || (request.FilterType == "Start"
                        ? EF.Functions.Like(hsCode.Code, request.HSCode + "%")
                        : EF.Functions.Like(hsCode.Code, "%" + request.HSCode)))
            orderby hsCode.Id
            select new sp_HSCodeReportResult
            {
                SakhanId = permit.SakhanId,
                SectionCode = section.Code,
                HSCodeId = item.HscodeId,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = item.Amount,
                Currency = currency.Code,
                LicenceNo = permit.ImportPermitNo,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName
            };
    }

    private static IQueryable<sp_HSCodeReportResult> EmptyRows(TradeNetDbContext db)
    {
        return db.Hscodes
            .Where(_ => false)
            .Select(hsCode => new sp_HSCodeReportResult
            {
                SakhanId = hsCode.Id,
                SectionCode = hsCode.Code,
                HSCodeId = hsCode.Id,
                HSCode = hsCode.Code,
                HSDescription = hsCode.Description,
                Amount = 0m,
                Currency = hsCode.Code,
                LicenceNo = hsCode.Code,
                CompanyRegistrationNo = hsCode.Code,
                CompanyName = hsCode.Description
            });
    }
}
