using API.DBContext;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace API.StoredProcedureToLinq;

public sealed class sp_DashboardCompletedRequest
{
    public int Month { get; set; }
    public int Year { get; set; }
    public string PaThaKaId { get; set; } = string.Empty;
    public string IndividualTradingId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
}

public sealed class sp_DashboardCompletedResult
{
    public int TotalCount { get; set; }
    public string ApplyType { get; set; } = null!;
    public string FormType { get; set; } = null!;
}

public static class sp_DashboardCompleted
{
    private const string Approved = "Approved";

    public static IQueryable<sp_DashboardCompletedResult> Query(
        TradeNetDbContext db,
        sp_DashboardCompletedRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return CountRows(db.PaThaKaRegistrations,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && row.MemberId == request.MemberId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Pa Tha Ka" })
            .Concat(CountRows(db.IndividualTradingRegistrations,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && row.MemberId == request.MemberId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Individual Trading" }))
            .Concat(CountRows(db.WholeSaleRetailRegistrations,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = row.RegistrationType }))
            .Concat(CountRows(db.WineImportationRegistrations,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Wine Importation" }))
            .Concat(CountRows(db.DutyFreeShopRegistrations,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Duty Free Shop" }))
            .Concat(CountRows(db.ReExportRegistrations,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Re-Export" }))
            .Concat(CountRows(db.BusinessServiceAgencyRegistrations,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Business Service Agency" }))
            .Concat(CountRows(db.SaleCenterRegistrations,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = row.RegistrationType }))
            .Concat(CountRows(db.ShowRoomRegistrations,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = row.RegistrationType }))
            .Concat(CountRows(db.ExportLicences,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Export Licence" }))
            .Concat(CountRows(db.ImportLicences,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Import Licence" }))
            .Concat(CountRows(db.ExportPermits,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Export Permit" }))
            .Concat(CountRows(db.ImportPermits,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Import Permit" }))
            .Concat(CountRows(db.BorderExportLicences,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && ((row.PaThaKaId == request.PaThaKaId && row.PaThaKaId != null)
                        || (row.IndividualTradingId == request.IndividualTradingId && row.IndividualTradingId != null)),
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Border Export Licence" }))
            .Concat(CountRows(db.BorderImportLicences,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && ((row.PaThaKaId == request.PaThaKaId && row.PaThaKaId != null)
                        || (row.IndividualTradingId == request.IndividualTradingId && row.IndividualTradingId != null)),
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Border Import Licence" }))
            .Concat(CountRows(db.BorderExportPermits,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Border Export Permit" }))
            .Concat(CountRows(db.BorderImportPermits,
                row => row.CreatedDate.HasValue
                    && row.CreatedDate.Value.Month == request.Month
                    && row.CreatedDate.Value.Year == request.Year
                    && row.Status == Approved
                    && row.PaThaKaId == request.PaThaKaId,
                row => new DashboardGroupRow { ApplyType = row.ApplyType, FormType = "Border Import Permit" }));
    }

    private static IQueryable<sp_DashboardCompletedResult> CountRows<TEntity>(
        IQueryable<TEntity> source,
        Expression<Func<TEntity, bool>> predicate,
        Expression<Func<TEntity, DashboardGroupRow>> selector)
        where TEntity : class
    {
        return source
            .Where(predicate)
            .Select(selector)
            .GroupBy(row => new { row.ApplyType, row.FormType })
            .Select(group => new sp_DashboardCompletedResult
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
