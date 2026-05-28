using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_PendingReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string FormType { get; set; } = string.Empty;
    public int ExportImportSectionId { get; set; }
}

public sealed class sp_PendingReportResult
{
    public string Status { get; set; } = null!;
    public string ApplyType { get; set; } = null!;
    public DateTime ApplicationDate { get; set; }
    public string ApplicationNo { get; set; } = null!;
    public string SectionCode { get; set; } = null!;
    public string SectionName { get; set; } = null!;
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? Currency { get; set; }
    public string? AdditionalDescription { get; set; }
    public decimal Amount { get; set; }
    public string? CommodityType { get; set; }
    public string? HSCode { get; set; }
}

public static class sp_PendingReport
{
    private const string Pending = "Pending";
    private const string Reject = "Reject";

    public static IQueryable<sp_PendingReportResult> Query(
        TradeNetDbContext db,
        sp_PendingReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return
            (from licence in db.ImportLicences
             join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
             join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
             where request.FormType == "Import Licence"
                && (licence.Status == Pending || licence.Status == Reject)
                && licence.ApplicationDate >= request.FromDate
                && licence.ApplicationDate <= request.ToDate
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
             select new sp_PendingReportResult
             {
                 Status = licence.Status,
                 ApplyType = licence.ApplyType,
                 ApplicationDate = licence.ApplicationDate,
                 ApplicationNo = licence.ApplicationNo,
                 SectionCode = section.Code,
                 SectionName = section.Name,
                 CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                 CompanyName = paThaKa.CompanyName,
                 Currency =
                    (from item in db.ImportLicenceItems
                     join currency in db.Currencies on item.CurrencyId equals currency.Id
                     where item.ImportLicenceId == licence.Id
                     select currency.Code)
                    .FirstOrDefault(),
                 AdditionalDescription = db.ImportLicenceItems
                    .Where(item => item.ImportLicenceId == licence.Id)
                    .Select(item => item.Description)
                    .FirstOrDefault(),
                 Amount = db.ImportLicenceItems
                    .Where(item => item.ImportLicenceId == licence.Id)
                    .Select(item => (decimal?)item.Amount)
                    .Sum() ?? 0m,
                 CommodityType = licence.CommodityType,
                 HSCode = db.ImportLicenceItems
                    .Where(item => item.ImportLicenceId == licence.Id)
                    .Select(item => item.Hscode)
                    .FirstOrDefault()
             })
            .Concat(
            from licence in db.ExportLicences
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where request.FormType == "Export Licence"
                && (licence.Status == Pending || licence.Status == Reject)
                && licence.ApplicationDate >= request.FromDate
                && licence.ApplicationDate <= request.ToDate
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
            select new sp_PendingReportResult
            {
                Status = licence.Status,
                ApplyType = licence.ApplyType,
                ApplicationDate = licence.ApplicationDate,
                ApplicationNo = licence.ApplicationNo,
                SectionCode = section.Code,
                SectionName = section.Name,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                Currency =
                    (from item in db.ExportLicenceItems
                     join currency in db.Currencies on item.CurrencyId equals currency.Id
                     where item.ExportLicenceId == licence.Id
                     select currency.Code)
                    .FirstOrDefault(),
                AdditionalDescription = db.ExportLicenceItems
                    .Where(item => item.ExportLicenceId == licence.Id)
                    .Select(item => item.Description)
                    .FirstOrDefault(),
                Amount = db.ExportLicenceItems
                    .Where(item => item.ExportLicenceId == licence.Id)
                    .Select(item => (decimal?)item.Amount)
                    .Sum() ?? 0m,
                CommodityType = licence.CommodityType,
                HSCode = db.ExportLicenceItems
                    .Where(item => item.ExportLicenceId == licence.Id)
                    .Select(item => item.Hscode)
                    .FirstOrDefault()
            })
            .Concat(
            from licence in db.BorderImportLicences
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            where request.FormType == "Border Import Licence"
                && (licence.Status == Pending || licence.Status == Reject)
                && licence.ApplicationDate >= request.FromDate
                && licence.ApplicationDate <= request.ToDate
                && (request.ExportImportSectionId == 0 || licence.ExportImportSectionId == request.ExportImportSectionId)
            select new sp_PendingReportResult
            {
                Status = licence.Status,
                ApplyType = licence.ApplyType,
                ApplicationDate = licence.ApplicationDate,
                ApplicationNo = licence.ApplicationNo,
                SectionCode = section.Code,
                SectionName = section.Name,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                Currency =
                    (from item in db.BorderImportLicenceItems
                     join currency in db.Currencies on item.CurrencyId equals currency.Id
                     where item.BorderImportLicenceId == licence.Id
                     select currency.Code)
                    .FirstOrDefault(),
                AdditionalDescription = db.BorderImportLicenceItems
                    .Where(item => item.BorderImportLicenceId == licence.Id)
                    .Select(item => item.Description)
                    .FirstOrDefault(),
                Amount = db.BorderImportLicenceItems
                    .Where(item => item.BorderImportLicenceId == licence.Id)
                    .Select(item => (decimal?)item.Amount)
                    .Sum() ?? 0m,
                CommodityType = licence.CommodityType,
                HSCode = db.BorderImportLicenceItems
                    .Where(item => item.BorderImportLicenceId == licence.Id)
                    .Select(item => item.Hscode)
                    .FirstOrDefault()
            });
    }
}
