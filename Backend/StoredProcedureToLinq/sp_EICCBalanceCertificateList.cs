using API.DBContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_EICCBalanceCertificateListRequest
{
    public string FormType { get; set; } = string.Empty;
    public DateTime EICCDate { get; set; }
}

public sealed class sp_EICCBalanceCertificateListResult
{
    public string EICCId { get; set; } = null!;
    public string Id { get; set; } = null!;
    public string FormType { get; set; } = null!;
    public string ApplyType { get; set; } = null!;
    public string ApplicationNo { get; set; } = null!;
    public string SApplicationDate { get; set; } = null!;
    public string EICCNo { get; set; } = null!;
    public DateTime EICCDate { get; set; }
    public string SEICCDate { get; set; } = null!;
    public string? EICCStatus { get; set; }
    public DateTime CreatedDate { get; set; }
}

public static class sp_EICCBalanceCertificateList
{
    public static IQueryable<sp_EICCBalanceCertificateListResult> Query(
        TradeNetDbContext db,
        sp_EICCBalanceCertificateListRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return BranchRows(db, request)
            .Where(row => request.FormType == string.Empty || EF.Functions.Like(row.FormType, request.FormType + "%"))
            .OrderBy(row => row.CreatedDate)
            .Select(row => new sp_EICCBalanceCertificateListResult
            {
                EICCId = row.EICCId,
                Id = row.Id,
                FormType = row.FormType,
                ApplyType = row.ApplyType,
                ApplicationNo = row.ApplicationNo,
                SApplicationDate = row.ApplicationDate.Day.ToString()
                    + "/"
                    + row.ApplicationDate.Month.ToString()
                    + "/"
                    + row.ApplicationDate.Year.ToString(),
                EICCNo = row.EICCNo,
                EICCDate = row.EICCDate,
                SEICCDate = row.EICCDate.Day.ToString()
                    + "/"
                    + row.EICCDate.Month.ToString()
                    + "/"
                    + row.EICCDate.Year.ToString(),
                EICCStatus = row.EICCStatus,
                CreatedDate = row.CreatedDate
            });
    }

    private static IQueryable<EiccBalanceCertificateRow> BranchRows(
        TradeNetDbContext db,
        sp_EICCBalanceCertificateListRequest request)
    {
        return CertificateBaseRows(db, request)
            .Join(db.BusinessServiceAgencyRegistrations,
                certificate => certificate.TransactionId,
                registration => registration.Id,
                (certificate, registration) => new { certificate, registration })
            .Join(db.Eiccnos,
                row => row.registration.EiccnoId,
                eiccNo => eiccNo.Id,
                (row, eiccNo) => new EiccBalanceCertificateRow
                {
                    EICCId = row.certificate.Id,
                    Id = row.registration.Id,
                    FormType = row.certificate.FormType,
                    ApplyType = row.registration.ApplyType,
                    ApplicationNo = row.registration.ApplicationNo,
                    ApplicationDate = row.registration.ApplicationDate,
                    EICCNo = eiccNo.Code,
                    EICCDate = row.certificate.Eiccdate,
                    EICCStatus = row.registration.Eiccstatus,
                    CreatedDate = row.certificate.CreatedDate
                })
            .Concat(CertificateBaseRows(db, request)
                .Join(db.DutyFreeShopRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccBalanceCertificateRow
                    {
                        EICCId = row.certificate.Id,
                        Id = row.registration.Id,
                        FormType = row.certificate.FormType,
                        ApplyType = row.registration.ApplyType,
                        ApplicationNo = row.registration.ApplicationNo,
                        ApplicationDate = row.registration.ApplicationDate,
                        EICCNo = eiccNo.Code,
                        EICCDate = row.certificate.Eiccdate,
                        EICCStatus = row.registration.Eiccstatus,
                        CreatedDate = row.certificate.CreatedDate
                    }))
            .Concat(CertificateBaseRows(db, request)
                .Join(db.PaThaKaRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccBalanceCertificateRow
                    {
                        EICCId = row.certificate.Id,
                        Id = row.registration.Id,
                        FormType = row.certificate.FormType,
                        ApplyType = row.registration.ApplyType,
                        ApplicationNo = row.registration.ApplicationNo,
                        ApplicationDate = row.registration.ApplicationDate,
                        EICCNo = eiccNo.Code,
                        EICCDate = row.certificate.Eiccdate,
                        EICCStatus = row.registration.Eiccstatus,
                        CreatedDate = row.certificate.CreatedDate
                    }))
            .Concat(CertificateBaseRows(db, request)
                .Join(db.ReExportRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccBalanceCertificateRow
                    {
                        EICCId = row.certificate.Id,
                        Id = row.registration.Id,
                        FormType = row.certificate.FormType,
                        ApplyType = row.registration.ApplyType,
                        ApplicationNo = row.registration.ApplicationNo,
                        ApplicationDate = row.registration.ApplicationDate,
                        EICCNo = eiccNo.Code,
                        EICCDate = row.certificate.Eiccdate,
                        EICCStatus = row.registration.Eiccstatus,
                        CreatedDate = row.certificate.CreatedDate
                    }))
            .Concat(CertificateBaseRows(db, request)
                .Join(db.SaleCenterRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccBalanceCertificateRow
                    {
                        EICCId = row.certificate.Id,
                        Id = row.registration.Id,
                        FormType = row.certificate.FormType,
                        ApplyType = row.registration.ApplyType,
                        ApplicationNo = row.registration.ApplicationNo,
                        ApplicationDate = row.registration.ApplicationDate,
                        EICCNo = eiccNo.Code,
                        EICCDate = row.certificate.Eiccdate,
                        EICCStatus = row.registration.Eiccstatus,
                        CreatedDate = row.certificate.CreatedDate
                    }))
            .Concat(CertificateBaseRows(db, request)
                .Join(db.ShowRoomRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccBalanceCertificateRow
                    {
                        EICCId = row.certificate.Id,
                        Id = row.registration.Id,
                        FormType = row.certificate.FormType,
                        ApplyType = row.registration.ApplyType,
                        ApplicationNo = row.registration.ApplicationNo,
                        ApplicationDate = row.registration.ApplicationDate,
                        EICCNo = eiccNo.Code,
                        EICCDate = row.certificate.Eiccdate,
                        EICCStatus = row.registration.Eiccstatus,
                        CreatedDate = row.certificate.CreatedDate
                    }))
            .Concat(CertificateBaseRows(db, request)
                .Join(db.WholeSaleRetailRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccBalanceCertificateRow
                    {
                        EICCId = row.certificate.Id,
                        Id = row.registration.Id,
                        FormType = row.certificate.FormType,
                        ApplyType = row.registration.ApplyType,
                        ApplicationNo = row.registration.ApplicationNo,
                        ApplicationDate = row.registration.ApplicationDate,
                        EICCNo = eiccNo.Code,
                        EICCDate = row.certificate.Eiccdate,
                        EICCStatus = row.registration.Eiccstatus,
                        CreatedDate = row.certificate.CreatedDate
                    }))
            .Concat(CertificateBaseRows(db, request)
                .Join(db.WineImportationRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccBalanceCertificateRow
                    {
                        EICCId = row.certificate.Id,
                        Id = row.registration.Id,
                        FormType = row.certificate.FormType,
                        ApplyType = row.registration.ApplyType,
                        ApplicationNo = row.registration.ApplicationNo,
                        ApplicationDate = row.registration.ApplicationDate,
                        EICCNo = eiccNo.Code,
                        EICCDate = row.certificate.Eiccdate,
                        EICCStatus = row.registration.Eiccstatus,
                        CreatedDate = row.certificate.CreatedDate
                    }))
            .Concat(LicencePermitRows(db, request))
            .Concat(BorderLicencePermitRows(db, request));
    }

    private static IQueryable<EiccBalanceCertificateRow> LicencePermitRows(
        TradeNetDbContext db,
        sp_EICCBalanceCertificateListRequest request)
    {
        return CertificateBaseRows(db, request)
            .Join(db.ExportLicences,
                certificate => certificate.TransactionId,
                licence => licence.Id,
                (certificate, licence) => new { certificate, licence })
            .Join(db.Eiccnos,
                row => row.licence.EiccnoId,
                eiccNo => eiccNo.Id,
                (row, eiccNo) => new EiccBalanceCertificateRow
                {
                    EICCId = row.certificate.Id,
                    Id = row.licence.Id,
                    FormType = row.certificate.FormType,
                    ApplyType = row.licence.ApplyType,
                    ApplicationNo = row.licence.ApplicationNo,
                    ApplicationDate = row.licence.ApplicationDate,
                    EICCNo = eiccNo.Code,
                    EICCDate = row.certificate.Eiccdate,
                    EICCStatus = row.licence.Eiccstatus,
                    CreatedDate = row.certificate.CreatedDate
                })
            .Concat(CertificateBaseRows(db, request)
                .Join(db.ImportLicences,
                    certificate => certificate.TransactionId,
                    licence => licence.Id,
                    (certificate, licence) => new { certificate, licence })
                .Join(db.Eiccnos,
                    row => row.licence.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccBalanceCertificateRow
                    {
                        EICCId = row.certificate.Id,
                        Id = row.licence.Id,
                        FormType = row.certificate.FormType,
                        ApplyType = row.licence.ApplyType,
                        ApplicationNo = row.licence.ApplicationNo,
                        ApplicationDate = row.licence.ApplicationDate,
                        EICCNo = eiccNo.Code,
                        EICCDate = row.certificate.Eiccdate,
                        EICCStatus = row.licence.Eiccstatus,
                        CreatedDate = row.certificate.CreatedDate
                    }))
            .Concat(CertificateBaseRows(db, request)
                .Join(db.ExportPermits,
                    certificate => certificate.TransactionId,
                    permit => permit.Id,
                    (certificate, permit) => new { certificate, permit })
                .Join(db.Eiccnos,
                    row => row.permit.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccBalanceCertificateRow
                    {
                        EICCId = row.certificate.Id,
                        Id = row.permit.Id,
                        FormType = row.certificate.FormType,
                        ApplyType = row.permit.ApplyType,
                        ApplicationNo = row.permit.ApplicationNo,
                        ApplicationDate = row.permit.ApplicationDate,
                        EICCNo = eiccNo.Code,
                        EICCDate = row.certificate.Eiccdate,
                        EICCStatus = row.permit.Eiccstatus,
                        CreatedDate = row.certificate.CreatedDate
                    }))
            .Concat(CertificateBaseRows(db, request)
                .Join(db.ImportPermits,
                    certificate => certificate.TransactionId,
                    permit => permit.Id,
                    (certificate, permit) => new { certificate, permit })
                .Join(db.Eiccnos,
                    row => row.permit.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccBalanceCertificateRow
                    {
                        EICCId = row.certificate.Id,
                        Id = row.permit.Id,
                        FormType = row.certificate.FormType,
                        ApplyType = row.permit.ApplyType,
                        ApplicationNo = row.permit.ApplicationNo,
                        ApplicationDate = row.permit.ApplicationDate,
                        EICCNo = eiccNo.Code,
                        EICCDate = row.certificate.Eiccdate,
                        EICCStatus = row.permit.Eiccstatus,
                        CreatedDate = row.certificate.CreatedDate
                    }));
    }

    private static IQueryable<EiccBalanceCertificateRow> BorderLicencePermitRows(
        TradeNetDbContext db,
        sp_EICCBalanceCertificateListRequest request)
    {
        return CertificateBaseRows(db, request)
            .Join(db.BorderExportLicences,
                certificate => certificate.TransactionId,
                licence => licence.Id,
                (certificate, licence) => new { certificate, licence })
            .Join(db.Eiccnos,
                row => row.licence.EiccnoId,
                eiccNo => eiccNo.Id,
                (row, eiccNo) => new EiccBalanceCertificateRow
                {
                    EICCId = row.certificate.Id,
                    Id = row.licence.Id,
                    FormType = row.certificate.FormType,
                    ApplyType = row.licence.ApplyType,
                    ApplicationNo = row.licence.ApplicationNo,
                    ApplicationDate = row.licence.ApplicationDate,
                    EICCNo = eiccNo.Code,
                    EICCDate = row.certificate.Eiccdate,
                    EICCStatus = row.licence.Eiccstatus,
                    CreatedDate = row.certificate.CreatedDate
                })
            .Concat(CertificateBaseRows(db, request)
                .Join(db.BorderImportLicences,
                    certificate => certificate.TransactionId,
                    licence => licence.Id,
                    (certificate, licence) => new { certificate, licence })
                .Join(db.Eiccnos,
                    row => row.licence.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccBalanceCertificateRow
                    {
                        EICCId = row.certificate.Id,
                        Id = row.licence.Id,
                        FormType = row.certificate.FormType,
                        ApplyType = row.licence.ApplyType,
                        ApplicationNo = row.licence.ApplicationNo,
                        ApplicationDate = row.licence.ApplicationDate,
                        EICCNo = eiccNo.Code,
                        EICCDate = row.certificate.Eiccdate,
                        EICCStatus = row.licence.Eiccstatus,
                        CreatedDate = row.certificate.CreatedDate
                    }))
            .Concat(CertificateBaseRows(db, request)
                .Join(db.BorderExportPermits,
                    certificate => certificate.TransactionId,
                    permit => permit.Id,
                    (certificate, permit) => new { certificate, permit })
                .Join(db.Eiccnos,
                    row => row.permit.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccBalanceCertificateRow
                    {
                        EICCId = row.certificate.Id,
                        Id = row.permit.Id,
                        FormType = row.certificate.FormType,
                        ApplyType = row.permit.ApplyType,
                        ApplicationNo = row.permit.ApplicationNo,
                        ApplicationDate = row.permit.ApplicationDate,
                        EICCNo = eiccNo.Code,
                        EICCDate = row.certificate.Eiccdate,
                        EICCStatus = row.permit.Eiccstatus,
                        CreatedDate = row.certificate.CreatedDate
                    }))
            .Concat(CertificateBaseRows(db, request)
                .Join(db.BorderImportPermits,
                    certificate => certificate.TransactionId,
                    permit => permit.Id,
                    (certificate, permit) => new { certificate, permit })
                .Join(db.Eiccnos,
                    row => row.permit.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccBalanceCertificateRow
                    {
                        EICCId = row.certificate.Id,
                        Id = row.permit.Id,
                        FormType = row.certificate.FormType,
                        ApplyType = row.permit.ApplyType,
                        ApplicationNo = row.permit.ApplicationNo,
                        ApplicationDate = row.permit.ApplicationDate,
                        EICCNo = eiccNo.Code,
                        EICCDate = row.certificate.Eiccdate,
                        EICCStatus = row.permit.Eiccstatus,
                        CreatedDate = row.certificate.CreatedDate
                    }));
    }

    private static IQueryable<API.Model.TradeNet.Eicccertificate> CertificateBaseRows(
        TradeNetDbContext db,
        sp_EICCBalanceCertificateListRequest request)
    {
        return db.Eicccertificates
            .Where(certificate => certificate.Status == "Pending"
                && certificate.IsFinish == false
                && certificate.Eiccdate < request.EICCDate);
    }

    private sealed class EiccBalanceCertificateRow
    {
        public string EICCId { get; set; } = null!;
        public string Id { get; set; } = null!;
        public string FormType { get; set; } = null!;
        public string ApplyType { get; set; } = null!;
        public string ApplicationNo { get; set; } = null!;
        public DateTime ApplicationDate { get; set; }
        public string EICCNo { get; set; } = null!;
        public DateTime EICCDate { get; set; }
        public string? EICCStatus { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
