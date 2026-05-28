using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_LicencePermitSearchRequest
{
    public string LicenceNo { get; set; } = string.Empty;
}

public sealed class sp_LicencePermitSearchResult
{
    public string Id { get; set; } = null!;
    public string FormType { get; set; } = null!;
}

public static class sp_LicencePermitSearch
{
    private const string Approved = "Approved";
    private const string New = "New";

    public static IQueryable<sp_LicencePermitSearchResult> Query(
        TradeNetDbContext db,
        sp_LicencePermitSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return AllLicencePermitRows(db)
            .Where(row => row.LicenceNo == request.LicenceNo)
            .OrderByDescending(row => row.CreatedDate)
            .Take(1)
            .Select(row => new sp_LicencePermitSearchResult
            {
                Id = row.Id,
                FormType = row.FormType
            });
    }

    private static IQueryable<LicencePermitSearchRow> AllLicencePermitRows(TradeNetDbContext db)
    {
        return db.ExportLicences
            .Where(row => row.Status == Approved && row.ApplyType == New)
            .Select(row => new LicencePermitSearchRow
            {
                Id = row.Id,
                FormType = "Export Licence",
                LicenceNo = row.ExportLicenceNo,
                CreatedDate = row.CreatedDate
            })
            .Concat(db.ExportLicences
                .Where(row => row.Status == Approved && row.ApplyType != New)
                .Select(row => new LicencePermitSearchRow
                {
                    Id = row.Id,
                    FormType = "Export Licence",
                    LicenceNo = row.OldExportLicenceNo,
                    CreatedDate = row.CreatedDate
                }))
            .Concat(db.ExportPermits
                .Where(row => row.Status == Approved && row.ApplyType == New)
                .Select(row => new LicencePermitSearchRow
                {
                    Id = row.Id,
                    FormType = "Export Permit",
                    LicenceNo = row.ExportPermitNo,
                    CreatedDate = row.CreatedDate
                }))
            .Concat(db.ExportPermits
                .Where(row => row.Status == Approved && row.ApplyType != New)
                .Select(row => new LicencePermitSearchRow
                {
                    Id = row.Id,
                    FormType = "Export Permit",
                    LicenceNo = row.OldExportPermitNo,
                    CreatedDate = row.CreatedDate
                }))
            .Concat(db.ImportLicences
                .Where(row => row.Status == Approved && row.ApplyType == New)
                .Select(row => new LicencePermitSearchRow
                {
                    Id = row.Id,
                    FormType = "Import Licence",
                    LicenceNo = row.ImportLicenceNo,
                    CreatedDate = row.CreatedDate
                }))
            .Concat(db.ImportLicences
                .Where(row => row.Status == Approved && row.ApplyType != New)
                .Select(row => new LicencePermitSearchRow
                {
                    Id = row.Id,
                    FormType = "Import Licence",
                    LicenceNo = row.OldImportLicenceNo,
                    CreatedDate = row.CreatedDate
                }))
            .Concat(db.ImportPermits
                .Where(row => row.Status == Approved && row.ApplyType == New)
                .Select(row => new LicencePermitSearchRow
                {
                    Id = row.Id,
                    FormType = "Import Permit",
                    LicenceNo = row.ImportPermitNo,
                    CreatedDate = row.CreatedDate
                }))
            .Concat(db.ImportPermits
                .Where(row => row.Status == Approved && row.ApplyType != New)
                .Select(row => new LicencePermitSearchRow
                {
                    Id = row.Id,
                    FormType = "Import Permit",
                    LicenceNo = row.OldImportPermitNo,
                    CreatedDate = row.CreatedDate
                }))
            .Concat(db.BorderExportLicences
                .Where(row => row.Status == Approved && row.ApplyType == New)
                .Select(row => new LicencePermitSearchRow
                {
                    Id = row.Id,
                    FormType = "Border Export Licence",
                    LicenceNo = row.ExportLicenceNo,
                    CreatedDate = row.CreatedDate
                }))
            .Concat(db.BorderExportLicences
                .Where(row => row.Status == Approved && row.ApplyType != New)
                .Select(row => new LicencePermitSearchRow
                {
                    Id = row.Id,
                    FormType = "Border Export Licence",
                    LicenceNo = row.OldExportLicenceNo,
                    CreatedDate = row.CreatedDate
                }))
            .Concat(db.BorderExportPermits
                .Where(row => row.Status == Approved && row.ApplyType == New)
                .Select(row => new LicencePermitSearchRow
                {
                    Id = row.Id,
                    FormType = "Border Export Permit",
                    LicenceNo = row.ExportPermitNo,
                    CreatedDate = row.CreatedDate
                }))
            .Concat(db.BorderExportPermits
                .Where(row => row.Status == Approved && row.ApplyType != New)
                .Select(row => new LicencePermitSearchRow
                {
                    Id = row.Id,
                    FormType = "Border Export Permit",
                    LicenceNo = row.OldExportPermitNo,
                    CreatedDate = row.CreatedDate
                }))
            .Concat(db.BorderImportLicences
                .Where(row => row.Status == Approved && row.ApplyType == New)
                .Select(row => new LicencePermitSearchRow
                {
                    Id = row.Id,
                    FormType = "Border Import Licence",
                    LicenceNo = row.ImportLicenceNo,
                    CreatedDate = row.CreatedDate
                }))
            .Concat(db.BorderImportLicences
                .Where(row => row.Status == Approved && row.ApplyType != New)
                .Select(row => new LicencePermitSearchRow
                {
                    Id = row.Id,
                    FormType = "Border Import Licence",
                    LicenceNo = row.OldImportLicenceNo,
                    CreatedDate = row.CreatedDate
                }))
            .Concat(db.BorderImportPermits
                .Where(row => row.Status == Approved && row.ApplyType == New)
                .Select(row => new LicencePermitSearchRow
                {
                    Id = row.Id,
                    FormType = "Border Import Permit",
                    LicenceNo = row.ImportPermitNo,
                    CreatedDate = row.CreatedDate
                }))
            .Concat(db.BorderImportPermits
                .Where(row => row.Status == Approved && row.ApplyType != New)
                .Select(row => new LicencePermitSearchRow
                {
                    Id = row.Id,
                    FormType = "Border Import Permit",
                    LicenceNo = row.OldImportPermitNo,
                    CreatedDate = row.CreatedDate
                }));
    }

    private sealed class LicencePermitSearchRow
    {
        public string Id { get; set; } = null!;
        public string FormType { get; set; } = null!;
        public string? LicenceNo { get; set; }
        public DateTime? CreatedDate { get; set; }
    }
}
