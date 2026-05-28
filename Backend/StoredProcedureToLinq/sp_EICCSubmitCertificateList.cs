using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_EICCSubmitCertificateListRequest
{
    public string EICCStatus { get; set; } = string.Empty;
    public int UserId { get; set; }
}

public sealed class sp_EICCSubmitCertificateListResult
{
    public string Id { get; set; } = null!;
    public string FormType { get; set; } = null!;
    public string ApplyType { get; set; } = null!;
    public string ApplicationNo { get; set; } = null!;
    public DateTime ApplicationDate { get; set; }
    public string SApplicationDate { get; set; } = null!;
    public string CompanyRegistrationNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? SEndDate { get; set; }
    public string EICCNo { get; set; } = null!;
    public DateTime? EICCDate { get; set; }
    public string? SEICCDate { get; set; }
    public string? EICCStatus { get; set; }
}

public static class sp_EICCSubmitCertificateList
{
    private const string Approved = "Approved";

    public static IQueryable<sp_EICCSubmitCertificateListResult> Query(
        TradeNetDbContext db,
        sp_EICCSubmitCertificateListRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return BranchRows(db, request)
            .OrderBy(row => row.ApplicationDate)
            .Select(row => new sp_EICCSubmitCertificateListResult
            {
                Id = row.Id,
                FormType = row.FormType,
                ApplyType = row.ApplyType,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                SApplicationDate = row.ApplicationDate.Day.ToString()
                    + "/"
                    + row.ApplicationDate.Month.ToString()
                    + "/"
                    + row.ApplicationDate.Year.ToString(),
                CompanyRegistrationNo = row.CompanyRegistrationNo,
                CompanyName = row.CompanyName,
                SEndDate = row.EndDate == null
                    ? null
                    : row.EndDate.Value.Day.ToString()
                        + "/"
                        + row.EndDate.Value.Month.ToString()
                        + "/"
                        + row.EndDate.Value.Year.ToString(),
                EICCNo = row.EICCNo,
                EICCDate = row.EICCDate,
                SEICCDate = row.EICCDate == null
                    ? null
                    : row.EICCDate.Value.Day.ToString()
                        + "/"
                        + row.EICCDate.Value.Month.ToString()
                        + "/"
                        + row.EICCDate.Value.Year.ToString(),
                EICCStatus = row.EICCStatus
            });
    }

    private static IQueryable<EiccSubmitCertificateRow> BranchRows(
        TradeNetDbContext db,
        sp_EICCSubmitCertificateListRequest request)
    {
        return
            (from registration in db.BusinessServiceAgencyRegistrations
             join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
             join eiccNo in db.Eiccnos on registration.EiccnoId equals eiccNo.Id
             where registration.Eiccstatus == request.EICCStatus
                && registration.ApproveUserId == request.UserId
                && registration.IsEiccsubmit == true
             select new EiccSubmitCertificateRow
             {
                 Id = registration.Id,
                 FormType = "Business Service Agency",
                 ApplyType = registration.ApplyType,
                 ApplicationNo = registration.ApplicationNo,
                 ApplicationDate = registration.ApplicationDate,
                 CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                 CompanyName = paThaKa.CompanyName,
                 EndDate = paThaKa.EndDate,
                 EICCNo = eiccNo.Code,
                 EICCDate = registration.Eiccdate,
                 EICCStatus = registration.Eiccstatus
             })
            .Concat(
            from registration in db.DutyFreeShopRegistrations
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            join eiccNo in db.Eiccnos on registration.EiccnoId equals eiccNo.Id
            where registration.Eiccstatus == request.EICCStatus
                && registration.ApproveUserId == request.UserId
                && registration.IsEiccsubmit == true
                && (request.EICCStatus != Approved || registration.IsApprove == false)
            select new EiccSubmitCertificateRow
            {
                Id = registration.Id,
                FormType = "Duty Free Shop",
                ApplyType = registration.ApplyType,
                ApplicationNo = registration.ApplicationNo,
                ApplicationDate = registration.ApplicationDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                EndDate = paThaKa.EndDate,
                EICCNo = eiccNo.Code,
                EICCDate = registration.Eiccdate,
                EICCStatus = registration.Eiccstatus
            })
            .Concat(
            from registration in db.PaThaKaRegistrations
            join eiccNo in db.Eiccnos on registration.EiccnoId equals eiccNo.Id
            let endDate = db.PaThaKas
                .Where(paThaKa => paThaKa.CompanyRegistrationNo == registration.CompanyRegistrationNo)
                .Select(paThaKa => (DateTime?)paThaKa.EndDate)
                .FirstOrDefault()
            where registration.Eiccstatus == request.EICCStatus
                && registration.ApproveUserId == request.UserId
                && registration.IsEiccsubmit == true
                && (request.EICCStatus != Approved || registration.IsApprove == false)
            select new EiccSubmitCertificateRow
            {
                Id = registration.Id,
                FormType = "Pa Tha Ka",
                ApplyType = registration.ApplyType,
                ApplicationNo = registration.ApplicationNo,
                ApplicationDate = registration.ApplicationDate,
                CompanyRegistrationNo = registration.CompanyRegistrationNo,
                CompanyName = registration.CompanyName,
                EndDate = endDate,
                EICCNo = eiccNo.Code,
                EICCDate = registration.Eiccdate,
                EICCStatus = registration.Eiccstatus
            })
            .Concat(
            from registration in db.ReExportRegistrations
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            join eiccNo in db.Eiccnos on registration.EiccnoId equals eiccNo.Id
            where registration.Eiccstatus == request.EICCStatus
                && registration.ApproveUserId == request.UserId
                && registration.IsEiccsubmit == true
                && (request.EICCStatus != Approved || registration.IsApprove == false)
            select new EiccSubmitCertificateRow
            {
                Id = registration.Id,
                FormType = "Re-Export",
                ApplyType = registration.ApplyType,
                ApplicationNo = registration.ApplicationNo,
                ApplicationDate = registration.ApplicationDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                EndDate = paThaKa.EndDate,
                EICCNo = eiccNo.Code,
                EICCDate = registration.Eiccdate,
                EICCStatus = registration.Eiccstatus
            })
            .Concat(
            from registration in db.SaleCenterRegistrations
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            join eiccNo in db.Eiccnos on registration.EiccnoId equals eiccNo.Id
            where registration.Eiccstatus == request.EICCStatus
                && registration.ApproveUserId == request.UserId
                && registration.IsEiccsubmit == true
                && (request.EICCStatus != Approved || registration.IsApprove == false)
            select new EiccSubmitCertificateRow
            {
                Id = registration.Id,
                FormType = registration.RegistrationType,
                ApplyType = registration.ApplyType,
                ApplicationNo = registration.ApplicationNo,
                ApplicationDate = registration.ApplicationDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                EndDate = paThaKa.EndDate,
                EICCNo = eiccNo.Code,
                EICCDate = registration.Eiccdate,
                EICCStatus = registration.Eiccstatus
            })
            .Concat(
            from registration in db.ShowRoomRegistrations
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            join eiccNo in db.Eiccnos on registration.EiccnoId equals eiccNo.Id
            where registration.Eiccstatus == request.EICCStatus
                && registration.ApproveUserId == request.UserId
                && registration.IsEiccsubmit == true
                && (request.EICCStatus != Approved || registration.IsApprove == false)
            select new EiccSubmitCertificateRow
            {
                Id = registration.Id,
                FormType = registration.RegistrationType,
                ApplyType = registration.ApplyType,
                ApplicationNo = registration.ApplicationNo,
                ApplicationDate = registration.ApplicationDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                EndDate = paThaKa.EndDate,
                EICCNo = eiccNo.Code,
                EICCDate = registration.Eiccdate,
                EICCStatus = registration.Eiccstatus
            })
            .Concat(
            from registration in db.WholeSaleRetailRegistrations
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            join eiccNo in db.Eiccnos on registration.EiccnoId equals eiccNo.Id
            where registration.Eiccstatus == request.EICCStatus
                && registration.ApproveUserId == request.UserId
                && registration.IsEiccsubmit == true
                && (request.EICCStatus != Approved || registration.IsApprove == false)
            select new EiccSubmitCertificateRow
            {
                Id = registration.Id,
                FormType = registration.RegistrationType,
                ApplyType = registration.ApplyType,
                ApplicationNo = registration.ApplicationNo,
                ApplicationDate = registration.ApplicationDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                EndDate = paThaKa.EndDate,
                EICCNo = eiccNo.Code,
                EICCDate = registration.Eiccdate,
                EICCStatus = registration.Eiccstatus
            })
            .Concat(
            from registration in db.WineImportationRegistrations
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            join eiccNo in db.Eiccnos on registration.EiccnoId equals eiccNo.Id
            where registration.Eiccstatus == request.EICCStatus
                && registration.ApproveUserId == request.UserId
                && registration.IsEiccsubmit == true
                && (request.EICCStatus != Approved || registration.IsApprove == false)
            select new EiccSubmitCertificateRow
            {
                Id = registration.Id,
                FormType = "Wine Importation",
                ApplyType = registration.ApplyType,
                ApplicationNo = registration.ApplicationNo,
                ApplicationDate = registration.ApplicationDate,
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                CompanyName = paThaKa.CompanyName,
                EndDate = paThaKa.EndDate,
                EICCNo = eiccNo.Code,
                EICCDate = registration.Eiccdate,
                EICCStatus = registration.Eiccstatus
            });
    }

    private sealed class EiccSubmitCertificateRow
    {
        public string Id { get; set; } = null!;
        public string FormType { get; set; } = null!;
        public string ApplyType { get; set; } = null!;
        public string ApplicationNo { get; set; } = null!;
        public DateTime ApplicationDate { get; set; }
        public string CompanyRegistrationNo { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public DateTime? EndDate { get; set; }
        public string EICCNo { get; set; } = null!;
        public DateTime? EICCDate { get; set; }
        public string? EICCStatus { get; set; }
    }
}
