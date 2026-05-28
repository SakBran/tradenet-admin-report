using API.DBContext;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace API.StoredProcedureToLinq;

public sealed class sp_DashboardFeedbackRequest
{
    public string PaThaKaId { get; set; } = string.Empty;
    public string IndividualTradingId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
}

public sealed class sp_DashboardFeedbackResult
{
    public int TotalCount { get; set; }
    public string ApplyType { get; set; } = null!;
    public string FormType { get; set; } = null!;
}

public static class sp_DashboardFeedback
{
    private const string Reject = "Reject";

    public static IQueryable<sp_DashboardFeedbackResult> Query(
        TradeNetDbContext db,
        sp_DashboardFeedbackRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return CountRows(db.PaThaKaRegistrations,
                row => row.Status == Reject && row.MemberId == request.MemberId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Pa Tha Ka" })
            .Concat(CountRows(db.IndividualTradingRegistrations,
                row => row.Status == Reject && row.MemberId == request.MemberId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Individual Trading" }))
            .Concat(CountRows(db.WholeSaleRetailRegistrations,
                row => row.Status == Reject && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = row.RegistrationType }))
            .Concat(CountRows(db.WineImportationRegistrations,
                row => row.Status == Reject && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Wine Importation" }))
            .Concat(CountRows(db.DutyFreeShopRegistrations,
                row => row.Status == Reject && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Duty Free Shop" }))
            .Concat(CountRows(db.ReExportRegistrations,
                row => row.Status == Reject && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Re-Export" }))
            .Concat(CountRows(db.BusinessServiceAgencyRegistrations,
                row => row.Status == Reject && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Business Service Agency" }))
            .Concat(CountRows(db.SaleCenterRegistrations,
                row => row.Status == Reject && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = row.RegistrationType }))
            .Concat(CountRows(db.ShowRoomRegistrations,
                row => row.Status == Reject && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = row.RegistrationType }))
            .Concat(CountRows(db.ExportLicences,
                row => row.Status == Reject && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Export Licence" }))
            .Concat(CountRows(db.ImportLicences,
                row => row.Status == Reject && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Import Licence" }))
            .Concat(CountRows(db.ExportPermits,
                row => row.Status == Reject && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Export Permit" }))
            .Concat(CountRows(db.ImportPermits,
                row => row.Status == Reject && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Import Permit" }))
            .Concat(CountRows(db.BorderExportLicences,
                row => row.Status == Reject
                    && ((row.PaThaKaId == request.PaThaKaId && row.PaThaKaId != null)
                        || (row.IndividualTradingId == request.IndividualTradingId && row.IndividualTradingId != null)),
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Border Export Licence" }))
            .Concat(CountRows(db.BorderImportLicences,
                row => row.Status == Reject
                    && ((row.PaThaKaId == request.PaThaKaId && row.PaThaKaId != null)
                        || (row.IndividualTradingId == request.IndividualTradingId && row.IndividualTradingId != null)),
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Border Import Licence" }))
            .Concat(CountRows(db.BorderExportPermits,
                row => row.Status == Reject && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Border Export Permit" }))
            .Concat(CountRows(db.BorderImportPermits,
                row => row.Status == Reject && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Border Import Permit" }));
    }

    private static IQueryable<sp_DashboardFeedbackResult> CountRows<TEntity>(
        IQueryable<TEntity> source,
        Expression<Func<TEntity, bool>> predicate,
        Expression<Func<TEntity, DashboardGroupRow>> selector)
        where TEntity : class
    {
        return source
            .Where(predicate)
            .Select(selector)
            .GroupBy(row => new { row.ApplyType, row.FormType })
            .Select(group => new sp_DashboardFeedbackResult
            {
                TotalCount = group.Count(),
                ApplyType = group.Key.ApplyType,
                FormType = group.Key.FormType
            });
    }

    private sealed class DashboardGroupRow
    {
        public string ApplyType { get; set; } = null!;
        public string FormType { get; set; } = null!;
    }
}
