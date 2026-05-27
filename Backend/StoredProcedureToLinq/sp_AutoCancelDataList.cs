using API.DBContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_AutoCancelDataListResult
{
    public string TransactionId { get; set; } = null!;
    public string FormType { get; set; } = null!;
}

public static class sp_AutoCancelDataList
{
    private const string PaymentReady = "Payment Ready";

    public static IQueryable<sp_AutoCancelDataListResult> Query(TradeNetDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        return ExportLicenceQuery(db)
            .Concat(ImportLicenceQuery(db))
            .Concat(ExportPermitQuery(db))
            .Concat(ImportPermitQuery(db))
            .Concat(BorderExportLicenceQuery(db))
            .Concat(BorderImportLicenceQuery(db))
            .Concat(BorderExportPermitQuery(db))
            .Concat(BorderImportPermitQuery(db));
    }

    private static IQueryable<sp_AutoCancelDataListResult> ExportLicenceQuery(TradeNetDbContext db)
    {
        return db.ExportLicences
            .Where(licence =>
                licence.Status == PaymentReady
                && EF.Functions.DateDiffDay(licence.ApproveDate, DateTime.Now) >= 10
                && licence.IsAutoCancel == null)
            .Select(licence => new sp_AutoCancelDataListResult
            {
                TransactionId = licence.Id,
                FormType = "Export Licence"
            });
    }

    private static IQueryable<sp_AutoCancelDataListResult> ImportLicenceQuery(TradeNetDbContext db)
    {
        return db.ImportLicences
            .Where(licence =>
                licence.Status == PaymentReady
                && EF.Functions.DateDiffDay(licence.ApproveDate, DateTime.Now) >= 10
                && licence.IsAutoCancel == null)
            .Select(licence => new sp_AutoCancelDataListResult
            {
                TransactionId = licence.Id,
                FormType = "Import Licence"
            });
    }

    private static IQueryable<sp_AutoCancelDataListResult> ExportPermitQuery(TradeNetDbContext db)
    {
        return db.ExportPermits
            .Where(permit =>
                permit.Status == PaymentReady
                && EF.Functions.DateDiffDay(permit.ApproveDate, DateTime.Now) >= 10
                && permit.IsAutoCancel == null)
            .Select(permit => new sp_AutoCancelDataListResult
            {
                TransactionId = permit.Id,
                FormType = "Export Permit"
            });
    }

    private static IQueryable<sp_AutoCancelDataListResult> ImportPermitQuery(TradeNetDbContext db)
    {
        return db.ImportPermits
            .Where(permit =>
                permit.Status == PaymentReady
                && EF.Functions.DateDiffDay(permit.ApproveDate, DateTime.Now) >= 10
                && permit.IsAutoCancel == null)
            .Select(permit => new sp_AutoCancelDataListResult
            {
                TransactionId = permit.Id,
                FormType = "Import Permit"
            });
    }

    private static IQueryable<sp_AutoCancelDataListResult> BorderExportLicenceQuery(TradeNetDbContext db)
    {
        return db.BorderExportLicences
            .Where(licence =>
                licence.Status == PaymentReady
                && EF.Functions.DateDiffDay(licence.ApproveDate, DateTime.Now) >= 10
                && licence.IsAutoCancel == null)
            .Select(licence => new sp_AutoCancelDataListResult
            {
                TransactionId = licence.Id,
                FormType = "Border Export Licence"
            });
    }

    private static IQueryable<sp_AutoCancelDataListResult> BorderImportLicenceQuery(TradeNetDbContext db)
    {
        return db.BorderImportLicences
            .Where(licence =>
                licence.Status == PaymentReady
                && EF.Functions.DateDiffDay(licence.ApproveDate, DateTime.Now) >= 10
                && licence.IsAutoCancel == null)
            .Select(licence => new sp_AutoCancelDataListResult
            {
                TransactionId = licence.Id,
                FormType = "Border Import Licence"
            });
    }

    private static IQueryable<sp_AutoCancelDataListResult> BorderExportPermitQuery(TradeNetDbContext db)
    {
        return db.BorderExportPermits
            .Where(permit =>
                permit.Status == PaymentReady
                && EF.Functions.DateDiffDay(permit.ApproveDate, DateTime.Now) >= 10
                && permit.IsAutoCancel == null)
            .Select(permit => new sp_AutoCancelDataListResult
            {
                TransactionId = permit.Id,
                FormType = "Border Export Permit"
            });
    }

    private static IQueryable<sp_AutoCancelDataListResult> BorderImportPermitQuery(TradeNetDbContext db)
    {
        return db.BorderImportPermits
            .Where(permit =>
                permit.Status == PaymentReady
                && EF.Functions.DateDiffDay(permit.ApproveDate, DateTime.Now) >= 10
                && permit.IsAutoCancel == null)
            .Select(permit => new sp_AutoCancelDataListResult
            {
                TransactionId = permit.Id,
                FormType = "Border Import Permit"
            });
    }
}
