using API.DBContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_EICCPendingCertificateListRequest
{
    public string Type { get; set; } = string.Empty;
    public string FormType { get; set; } = string.Empty;
    public DateTime EICCDate { get; set; }
    public int ProductGroupId { get; set; }
    public int ProductItemId { get; set; }
}

public sealed class sp_EICCPendingCertificateListResult
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

public static class sp_EICCPendingCertificateList
{
    public static IQueryable<sp_EICCPendingCertificateListResult> Query(
        TradeNetDbContext db,
        sp_EICCPendingCertificateListRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return CertificateRegistrationRows(db, request)
            .Concat(LicencePermitRows(db, request))
            .Concat(BorderLicencePermitRows(db, request))
            .Where(row => request.FormType == string.Empty || EF.Functions.Like(row.FormType, request.FormType + "%"))
            .OrderBy(row => row.CreatedDate)
            .Select(row => new sp_EICCPendingCertificateListResult
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

    private static IQueryable<EiccPendingCertificateRow> CertificateRegistrationRows(
        TradeNetDbContext db,
        sp_EICCPendingCertificateListRequest request)
    {
        return CertificateBaseRows(db, request, "Certificate")
            .Join(db.BusinessServiceAgencyRegistrations,
                certificate => certificate.TransactionId,
                registration => registration.Id,
                (certificate, registration) => new { certificate, registration })
            .Join(db.Eiccnos,
                row => row.registration.EiccnoId,
                eiccNo => eiccNo.Id,
                (row, eiccNo) => new EiccPendingCertificateRow
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
            .Concat(CertificateBaseRows(db, request, "Certificate")
                .Join(db.DutyFreeShopRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccPendingCertificateRow
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
            .Concat(CertificateBaseRows(db, request, "Certificate")
                .Join(db.PaThaKaRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccPendingCertificateRow
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
            .Concat(CertificateBaseRows(db, request, "Certificate")
                .Join(db.ReExportRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccPendingCertificateRow
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
            .Concat(CertificateBaseRows(db, request, "Certificate")
                .Join(db.SaleCenterRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccPendingCertificateRow
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
            .Concat(CertificateBaseRows(db, request, "Certificate")
                .Where(certificate => EF.Functions.Like(certificate.FormType, "Show Room%"))
                .Join(db.ShowRoomRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccPendingCertificateRow
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
            .Concat(CertificateBaseRows(db, request, "Certificate")
                .Join(db.WholeSaleRetailRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccPendingCertificateRow
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
            .Concat(CertificateBaseRows(db, request, "Certificate")
                .Join(db.WineImportationRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccPendingCertificateRow
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
                    }));
    }

    private static IQueryable<EiccPendingCertificateRow> LicencePermitRows(
        TradeNetDbContext db,
        sp_EICCPendingCertificateListRequest request)
    {
        return LicencePermitBaseRows(db, request, "LicencePermit")
            .Join(db.ExportLicences,
                certificate => certificate.TransactionId,
                licence => licence.Id,
                (certificate, licence) => new { certificate, licence })
            .Join(db.Eiccnos,
                row => row.licence.EiccnoId,
                eiccNo => eiccNo.Id,
                (row, eiccNo) => new EiccPendingCertificateRow
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
            .Concat(LicencePermitBaseRows(db, request, "LicencePermit")
                .Join(db.ImportLicences,
                    certificate => certificate.TransactionId,
                    licence => licence.Id,
                    (certificate, licence) => new { certificate, licence })
                .Join(db.Eiccnos,
                    row => row.licence.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccPendingCertificateRow
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
            .Concat(LicencePermitBaseRows(db, request, "LicencePermit")
                .Join(db.ExportPermits,
                    certificate => certificate.TransactionId,
                    permit => permit.Id,
                    (certificate, permit) => new { certificate, permit })
                .Join(db.Eiccnos,
                    row => row.permit.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccPendingCertificateRow
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
            .Concat(LicencePermitBaseRows(db, request, "LicencePermit")
                .Join(db.ImportPermits,
                    certificate => certificate.TransactionId,
                    permit => permit.Id,
                    (certificate, permit) => new { certificate, permit })
                .Join(db.Eiccnos,
                    row => row.permit.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccPendingCertificateRow
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

    private static IQueryable<EiccPendingCertificateRow> BorderLicencePermitRows(
        TradeNetDbContext db,
        sp_EICCPendingCertificateListRequest request)
    {
        return LicencePermitBaseRows(db, request, "BorderLicencePermit")
            .Join(db.BorderExportLicences,
                certificate => certificate.TransactionId,
                licence => licence.Id,
                (certificate, licence) => new { certificate, licence })
            .Join(db.Eiccnos,
                row => row.licence.EiccnoId,
                eiccNo => eiccNo.Id,
                (row, eiccNo) => new EiccPendingCertificateRow
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
            .Concat(LicencePermitBaseRows(db, request, "BorderLicencePermit")
                .Join(db.BorderImportLicences,
                    certificate => certificate.TransactionId,
                    licence => licence.Id,
                    (certificate, licence) => new { certificate, licence })
                .Join(db.Eiccnos,
                    row => row.licence.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccPendingCertificateRow
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
            .Concat(LicencePermitBaseRows(db, request, "BorderLicencePermit")
                .Join(db.BorderExportPermits,
                    certificate => certificate.TransactionId,
                    permit => permit.Id,
                    (certificate, permit) => new { certificate, permit })
                .Join(db.Eiccnos,
                    row => row.permit.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccPendingCertificateRow
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
            .Concat(LicencePermitBaseRows(db, request, "BorderLicencePermit")
                .Join(db.BorderImportPermits,
                    certificate => certificate.TransactionId,
                    permit => permit.Id,
                    (certificate, permit) => new { certificate, permit })
                .Join(db.Eiccnos,
                    row => row.permit.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccPendingCertificateRow
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
        sp_EICCPendingCertificateListRequest request,
        string type)
    {
        return db.Eicccertificates
            .Where(certificate => request.Type == type
                && certificate.Status == "Pending"
                && certificate.IsFinish == false
                && certificate.Eiccdate >= request.EICCDate
                && certificate.Eiccdate <= request.EICCDate);
    }

    private static IQueryable<API.Model.TradeNet.Eicccertificate> LicencePermitBaseRows(
        TradeNetDbContext db,
        sp_EICCPendingCertificateListRequest request,
        string type)
    {
        return CertificateBaseRows(db, request, type)
            .Where(certificate => certificate.ProductGroupId == request.ProductGroupId
                && certificate.ProductItemId == request.ProductItemId);
    }

    private sealed class EiccPendingCertificateRow
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
