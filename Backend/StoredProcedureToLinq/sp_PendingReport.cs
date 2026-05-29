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

        var importCurrencyByLicence =
            from firstItem in
                (from item in db.ImportLicenceItems
                 where db.Currencies.Any(currency => currency.Id == item.CurrencyId)
                 group item by item.ImportLicenceId into grouped
                 select new { LicenceId = grouped.Key, ItemId = grouped.Min(item => item.Id) })
            join item in db.ImportLicenceItems on firstItem.ItemId equals item.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            select new { firstItem.LicenceId, currency.Code };

        var importAmountByLicence =
            from item in db.ImportLicenceItems
            group item by item.ImportLicenceId into grouped
            select new { LicenceId = grouped.Key, Amount = grouped.Sum(item => (decimal?)item.Amount) };

        var importFirstItemByLicence =
            from grouped in
                (from item in db.ImportLicenceItems
                 group item by item.ImportLicenceId into g
                 select new { LicenceId = g.Key, ItemId = g.Min(item => item.Id) })
            join item in db.ImportLicenceItems on grouped.ItemId equals item.Id
            select new { grouped.LicenceId, item.Description, item.Hscode };

        var exportCurrencyByLicence =
            from firstItem in
                (from item in db.ExportLicenceItems
                 where db.Currencies.Any(currency => currency.Id == item.CurrencyId)
                 group item by item.ExportLicenceId into grouped
                 select new { LicenceId = grouped.Key, ItemId = grouped.Min(item => item.Id) })
            join item in db.ExportLicenceItems on firstItem.ItemId equals item.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            select new { firstItem.LicenceId, currency.Code };

        var exportAmountByLicence =
            from item in db.ExportLicenceItems
            group item by item.ExportLicenceId into grouped
            select new { LicenceId = grouped.Key, Amount = grouped.Sum(item => (decimal?)item.Amount) };

        var exportFirstItemByLicence =
            from grouped in
                (from item in db.ExportLicenceItems
                 group item by item.ExportLicenceId into g
                 select new { LicenceId = g.Key, ItemId = g.Min(item => item.Id) })
            join item in db.ExportLicenceItems on grouped.ItemId equals item.Id
            select new { grouped.LicenceId, item.Description, item.Hscode };

        var borderImportCurrencyByLicence =
            from firstItem in
                (from item in db.BorderImportLicenceItems
                 where db.Currencies.Any(currency => currency.Id == item.CurrencyId)
                 group item by item.BorderImportLicenceId into grouped
                 select new { LicenceId = grouped.Key, ItemId = grouped.Min(item => item.Id) })
            join item in db.BorderImportLicenceItems on firstItem.ItemId equals item.Id
            join currency in db.Currencies on item.CurrencyId equals currency.Id
            select new { firstItem.LicenceId, currency.Code };

        var borderImportAmountByLicence =
            from item in db.BorderImportLicenceItems
            group item by item.BorderImportLicenceId into grouped
            select new { LicenceId = grouped.Key, Amount = grouped.Sum(item => (decimal?)item.Amount) };

        var borderImportFirstItemByLicence =
            from grouped in
                (from item in db.BorderImportLicenceItems
                 group item by item.BorderImportLicenceId into g
                 select new { LicenceId = g.Key, ItemId = g.Min(item => item.Id) })
            join item in db.BorderImportLicenceItems on grouped.ItemId equals item.Id
            select new { grouped.LicenceId, item.Description, item.Hscode };

        return
            (from licence in db.ImportLicences
             join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
             join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
             join currencyRow in importCurrencyByLicence on licence.Id equals currencyRow.LicenceId into currencyJoin
             from currencyRow in currencyJoin.DefaultIfEmpty()
             join amountRow in importAmountByLicence on licence.Id equals amountRow.LicenceId into amountJoin
             from amountRow in amountJoin.DefaultIfEmpty()
             join firstItemRow in importFirstItemByLicence on licence.Id equals firstItemRow.LicenceId into firstItemJoin
             from firstItemRow in firstItemJoin.DefaultIfEmpty()
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
                 Currency = currencyRow.Code,
                 AdditionalDescription = firstItemRow.Description,
                 Amount = amountRow.Amount ?? 0m,
                 CommodityType = licence.CommodityType,
                 HSCode = firstItemRow.Hscode
             })
            .Concat(
            from licence in db.ExportLicences
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            join currencyRow in exportCurrencyByLicence on licence.Id equals currencyRow.LicenceId into currencyJoin
            from currencyRow in currencyJoin.DefaultIfEmpty()
            join amountRow in exportAmountByLicence on licence.Id equals amountRow.LicenceId into amountJoin
            from amountRow in amountJoin.DefaultIfEmpty()
            join firstItemRow in exportFirstItemByLicence on licence.Id equals firstItemRow.LicenceId into firstItemJoin
            from firstItemRow in firstItemJoin.DefaultIfEmpty()
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
                Currency = currencyRow.Code,
                AdditionalDescription = firstItemRow.Description,
                Amount = amountRow.Amount ?? 0m,
                CommodityType = licence.CommodityType,
                HSCode = firstItemRow.Hscode
            })
            .Concat(
            from licence in db.BorderImportLicences
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            join section in db.ExportImportSections on licence.ExportImportSectionId equals section.Id
            join currencyRow in borderImportCurrencyByLicence on licence.Id equals currencyRow.LicenceId into currencyJoin
            from currencyRow in currencyJoin.DefaultIfEmpty()
            join amountRow in borderImportAmountByLicence on licence.Id equals amountRow.LicenceId into amountJoin
            from amountRow in amountJoin.DefaultIfEmpty()
            join firstItemRow in borderImportFirstItemByLicence on licence.Id equals firstItemRow.LicenceId into firstItemJoin
            from firstItemRow in firstItemJoin.DefaultIfEmpty()
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
                Currency = currencyRow.Code,
                AdditionalDescription = firstItemRow.Description,
                Amount = amountRow.Amount ?? 0m,
                CommodityType = licence.CommodityType,
                HSCode = firstItemRow.Hscode
            });
    }
}
