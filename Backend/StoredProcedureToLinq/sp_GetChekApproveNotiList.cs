using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_GetChekApproveNotiListRequest
{
    public int UserId { get; set; }
    public string FormType { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
}

public sealed class sp_GetChekApproveNotiListResult
{
    public string FormType { get; set; } = null!;
}

public static class sp_GetChekApproveNotiList
{
    private const string Pending = "Pending";
    private const string CheckUser = "Check User";

    public static IQueryable<sp_GetChekApproveNotiListResult> Query(
        TradeNetDbContext db,
        sp_GetChekApproveNotiListRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var query = request.UserType == CheckUser
            ? CheckUserQuery(db, request.UserId)
            : ApproveUserQuery(db, request.UserId);

        return request.FormType == string.Empty
            ? query
            : query.Where(row => row.FormType == request.FormType);
    }

    private static IQueryable<sp_GetChekApproveNotiListResult> CheckUserQuery(
        TradeNetDbContext db,
        int userId)
    {
        return db.BorderExportLicences
            .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
            .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Border Export Licence" })
            .Concat(db.BorderExportPermits
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Border Export Permit" }))
            .Concat(db.BorderImportLicences
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Border Import Licence" }))
            .Concat(db.BorderImportPermits
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Border Import Permit" }))
            .Concat(db.ExportLicences
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Export Licence" }))
            .Concat(db.ExportPermits
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Export Permit" }))
            .Concat(db.ImportLicences
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Import Licence" }))
            .Concat(db.ImportPermits
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Import Permit" }))
            .Concat(db.PaThaKaRegistrations
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Pa Tha Ka" }))
            .Concat(db.PaThaKaBinds
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Pa Tha Ka" }))
            .Concat(db.BusinessServiceAgencyRegistrations
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Business Service Agency" }))
            .Concat(db.DutyFreeShopRegistrations
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Duty Free Shop" }))
            .Concat(db.IndividualTradingRegistrations
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Individual Trading" }))
            .Concat(db.ReExportRegistrations
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Re-Export" }))
            .Concat(db.SaleCenterRegistrations
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Sale Center" }))
            .Concat(db.ShowRoomRegistrations
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Show Room" }))
            .Concat(db.WholeSaleRetailRegistrations
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.RegistrationType == "Whole Sale" && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Whole Sale" }))
            .Concat(db.WholeSaleRetailRegistrations
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.RegistrationType == "Retail")
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Retail" }))
            .Concat(db.WholeSaleRetailRegistrations
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.RegistrationType == "Whole Sale and Retail" && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Whole Sale and Retail" }))
            .Concat(db.WineImportationRegistrations
                .Where(row => row.CheckUserId == userId && row.IsCheck == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Wine Importation" }));
    }

    private static IQueryable<sp_GetChekApproveNotiListResult> ApproveUserQuery(
        TradeNetDbContext db,
        int userId)
    {
        return db.BorderExportLicences
            .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.Status == Pending
                && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
            .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Border Export Licence" })
            .Concat(db.BorderExportPermits
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Border Export Permit" }))
            .Concat(db.BorderImportLicences
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Border Import Licence" }))
            .Concat(db.BorderImportPermits
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Border Import Permit" }))
            .Concat(db.ExportLicences
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Export Licence" }))
            .Concat(db.ExportPermits
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Export Permit" }))
            .Concat(db.ImportLicences
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Import Licence" }))
            .Concat(db.ImportPermits
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Import Permit" }))
            .Concat(db.PaThaKaRegistrations
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Pa Tha Ka" }))
            .Concat(db.PaThaKaBinds
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.Status == Pending)
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Pa Tha Ka" }))
            .Concat(db.BusinessServiceAgencyRegistrations
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Business Service Agency" }))
            .Concat(db.DutyFreeShopRegistrations
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Duty Free Shop" }))
            .Concat(db.ReExportRegistrations
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Re-Export" }))
            .Concat(db.SaleCenterRegistrations
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Sale Center" }))
            .Concat(db.ShowRoomRegistrations
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Show Room" }))
            .Concat(db.WholeSaleRetailRegistrations
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.RegistrationType == "Whole Sale" && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Whole Sale" }))
            .Concat(db.WholeSaleRetailRegistrations
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.RegistrationType == "Retail" && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Retail" }))
            .Concat(db.WholeSaleRetailRegistrations
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.RegistrationType == "Whole Sale and Retail" && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Whole Sale and Retail" }))
            .Concat(db.WineImportationRegistrations
                .Where(row => row.ApproveUserId == userId && row.IsApprove == false && row.Status == Pending
                    && (row.Eiccstatus == null || row.Eiccstatus == "Reject" || row.Eiccstatus == "Approved"))
                .Select(_ => new sp_GetChekApproveNotiListResult { FormType = "Wine Importation" }));
    }
}
