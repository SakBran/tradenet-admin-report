using API.DBContext;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace API.StoredProcedureToLinq;

public sealed class sp_DashboardPaymentRequest
{
    public string PaThaKaId { get; set; } = string.Empty;
    public string IndividualTradingId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
}

public sealed class sp_DashboardPaymentResult
{
    public int TotalCount { get; set; }
    public string ApplyType { get; set; } = null!;
    public string FormType { get; set; } = null!;
}

public static class sp_DashboardPayment
{
    private const string PaymentReady = "Payment Ready";

    public static IQueryable<sp_DashboardPaymentResult> Query(
        TradeNetDbContext db,
        sp_DashboardPaymentRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return CountRows(db.PaThaKaRegistrations,
                row => row.Status == PaymentReady && row.MemberId == request.MemberId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Pa Tha Ka" })
            .Concat(CountRows(db.IndividualTradingRegistrations,
                row => row.Status == PaymentReady && row.MemberId == request.MemberId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Individual Trading" }))
            .Concat(CountRows(db.WholeSaleRetailRegistrations,
                row => row.Status == PaymentReady && row.PaThaKaId == request.PaThaKaId && row.PaThaKaId != string.Empty,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = row.RegistrationType }))
            .Concat(CountRows(db.WineImportationRegistrations,
                row => row.Status == PaymentReady && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Wine Importation" }))
            .Concat(CountRows(db.DutyFreeShopRegistrations,
                row => row.Status == PaymentReady && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Duty Free Shop" }))
            .Concat(CountRows(db.ReExportRegistrations,
                row => row.Status == PaymentReady && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Re-Export" }))
            .Concat(CountRows(db.BusinessServiceAgencyRegistrations,
                row => row.Status == PaymentReady && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Business Service Agency" }))
            .Concat(CountRows(db.SaleCenterRegistrations,
                row => row.Status == PaymentReady && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = row.RegistrationType }))
            .Concat(CountRows(db.ShowRoomRegistrations,
                row => row.Status == PaymentReady && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = row.RegistrationType }))
            .Concat(CountRows(db.ExportLicences,
                row => row.Status == PaymentReady && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Export Licence" }))
            .Concat(CountRows(db.ImportLicences,
                row => row.Status == PaymentReady && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Import Licence" }))
            .Concat(CountRows(db.ExportPermits,
                row => row.Status == PaymentReady && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Export Permit" }))
            .Concat(CountRows(db.ImportPermits,
                row => row.Status == PaymentReady && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Import Permit" }))
            .Concat(CountRows(db.BorderExportLicences,
                row => row.Status == PaymentReady
                    && ((row.PaThaKaId == request.PaThaKaId && row.PaThaKaId != null)
                        || (row.IndividualTradingId == request.IndividualTradingId && row.IndividualTradingId != null)),
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Border Export Licence" }))
            .Concat(CountRows(db.BorderImportLicences,
                row => row.Status == PaymentReady
                    && ((row.PaThaKaId == request.PaThaKaId && row.PaThaKaId != null)
                        || (row.IndividualTradingId == request.IndividualTradingId && row.IndividualTradingId != null)),
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Border Import Licence" }))
            .Concat(CountRows(db.BorderExportPermits,
                row => row.Status == PaymentReady && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Border Export Permit" }))
            .Concat(CountRows(db.BorderImportPermits,
                row => row.Status == PaymentReady && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Border Import Permit" }));
    }

    private static IQueryable<sp_DashboardPaymentResult> CountRows<TEntity>(
        IQueryable<TEntity> source,
        Expression<Func<TEntity, bool>> predicate,
        Expression<Func<TEntity, DashboardGroupRow>> selector)
        where TEntity : class
    {
        return source
            .Where(predicate)
            .Select(selector)
            .GroupBy(row => new { row.ApplyType, row.FormType })
            .Select(group => new sp_DashboardPaymentResult
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
