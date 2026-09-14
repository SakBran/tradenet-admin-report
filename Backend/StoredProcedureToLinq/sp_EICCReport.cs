using API.DBContext;
using API.Service.Reports;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_EICCReportRequest
{
    public string Type { get; set; } = string.Empty;
    public string FormType { get; set; } = string.Empty;
    public DateTime EICCDate { get; set; }
    public int ProductGroupId { get; set; }
    public int ProductItemId { get; set; }
    public string EICCStatus { get; set; } = string.Empty;
}

public sealed class sp_EICCReportResult
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
    public string PaThaKaNo { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string? UnitLevel { get; set; }
    public string StreetNumberStreetName { get; set; } = null!;
    public string QuarterCityTownship { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Country { get; set; } = null!;
    public string? PostalCode { get; set; }
    public string? Remark { get; set; }

    /// <summary>
    /// The legacy report's "Company Address" cell. The old MVC model built it in C# from the
    /// six address columns (<c>CommonRepository.GetAddress</c>, Reports.cs:5404 on
    /// origin/master), so it is composed here rather than in SQL -- same algorithm, same bytes.
    /// Computed, so the grid JSON and the Excel row map both expose it as `companyAddress`.
    /// </summary>
    public string CompanyAddress => LegacyCompanyAddress.Compose(
        UnitLevel,
        StreetNumberStreetName,
        QuarterCityTownship,
        State,
        Country,
        PostalCode);
}

/// <summary>
/// The Tradenet 2.0 admin's <c>dbo.sp_EICCReport</c> -- the data behind
/// <c>ReportControl/EICCReport.rdlc</c>, reached from three sidebar entries that differ only
/// by <c>@Type</c> (Certificate / LicencePermit / BorderLicencePermit; _Layout.cshtml:2052,
/// 2075, 2089 on origin/master).
///
/// The filters, the branch shapes and the odd bits are the old procedure's, not corrected:
/// the EICC date is matched exactly (<c>&gt;= @EICCDate AND &lt;= @EICCDate</c>, not a whole-day
/// window), the card type is a <c>LIKE @FormType + '%'</c> prefix match, and only the Export
/// Licence branch demands an exact ProductGroupId/ProductItemId match (every other branch
/// treats 0 as "all") -- see <see cref="LicencePermitBaseRows"/>.
/// </summary>
public static class sp_EICCReport
{
    /// <summary>The <c>@Type</c> values the legacy screen passes (AppConfig.cs:1312-1314).</summary>
    public const string CertificateType = "Certificate";
    public const string LicencePermitType = "LicencePermit";
    public const string BorderLicencePermitType = "BorderLicencePermit";

    public static IQueryable<sp_EICCReportResult> Query(
        TradeNetDbContext db,
        sp_EICCReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        // The legacy procedure UNIONed all three type branches behind `@Type = '...'`
        // predicates, so every run paid for all of them (the Certificate branch alone is
        // eight joins-and-unions). Only one branch can ever return rows, so build just that
        // one. Each branch still carries its own `request.Type == type` guard, which is what
        // makes an unrecognised Type return nothing.
        var rows = request.Type switch
        {
            LicencePermitType => LicencePermitRows(db, request),
            BorderLicencePermitType => BorderLicencePermitRows(db, request),
            _ => CertificateRows(db, request),
        };

        return rows
            .Where(row => request.FormType == string.Empty || EF.Functions.Like(row.FormType, request.FormType + "%"))
            // CreatedDate is the legacy ORDER BY. EICCId breaks ties so the grid's OFFSET
            // paging cannot repeat or drop a row between pages; the old report printed every
            // row on one scrolling page and never needed it.
            .OrderBy(row => row.CreatedDate)
            .ThenBy(row => row.EICCId)
            .Select(row => new sp_EICCReportResult
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
                CreatedDate = row.CreatedDate,
                PaThaKaNo = row.PaThaKaNo,
                CompanyName = row.CompanyName,
                UnitLevel = row.UnitLevel,
                StreetNumberStreetName = row.StreetNumberStreetName,
                QuarterCityTownship = row.QuarterCityTownship,
                State = row.State,
                Country = row.Country,
                PostalCode = row.PostalCode,
                Remark = row.Remark
            });
    }

    private static IQueryable<EiccReportRow> CertificateRows(
        TradeNetDbContext db,
        sp_EICCReportRequest request)
    {
        return CertificateBaseRows(db, request, CertificateType)
            .Join(db.BusinessServiceAgencyRegistrations,
                certificate => certificate.TransactionId,
                registration => registration.Id,
                (certificate, registration) => new { certificate, registration })
            .Join(db.Eiccnos,
                row => row.registration.EiccnoId,
                eiccNo => eiccNo.Id,
                (row, eiccNo) => new { row.certificate, row.registration, eiccNo })
            .Join(db.PaThaKas,
                row => row.registration.PaThaKaId,
                paThaKa => paThaKa.Id,
                (row, paThaKa) => new EiccReportRow
                {
                    EICCId = row.certificate.Id,
                    Id = row.registration.Id,
                    FormType = row.certificate.FormType,
                    ApplyType = row.registration.ApplyType,
                    ApplicationNo = row.registration.ApplicationNo,
                    ApplicationDate = row.registration.ApplicationDate,
                    EICCNo = row.eiccNo.Code,
                    EICCDate = row.certificate.Eiccdate,
                    EICCStatus = row.registration.Eiccstatus,
                    CreatedDate = row.certificate.CreatedDate,
                    PaThaKaNo = paThaKa.PaThaKaNo,
                    CompanyName = paThaKa.CompanyName,
                    UnitLevel = paThaKa.UnitLevel,
                    StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                    QuarterCityTownship = paThaKa.QuarterCityTownship,
                    State = paThaKa.State,
                    Country = paThaKa.Country,
                    PostalCode = paThaKa.PostalCode,
                    Remark = row.certificate.Remark
                })
            .Concat(CertificateBaseRows(db, request, CertificateType)
                .Join(db.DutyFreeShopRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new { row.certificate, row.registration, eiccNo })
                .Join(db.PaThaKas,
                    row => row.registration.PaThaKaId,
                    paThaKa => paThaKa.Id,
                    (row, paThaKa) => new EiccReportRow
                    {
                        EICCId = row.certificate.Id,
                        Id = row.registration.Id,
                        FormType = row.certificate.FormType,
                        ApplyType = row.registration.ApplyType,
                        ApplicationNo = row.registration.ApplicationNo,
                        ApplicationDate = row.registration.ApplicationDate,
                        EICCNo = row.eiccNo.Code,
                        EICCDate = row.certificate.Eiccdate,
                        EICCStatus = row.registration.Eiccstatus,
                        CreatedDate = row.certificate.CreatedDate,
                        PaThaKaNo = paThaKa.PaThaKaNo,
                        CompanyName = paThaKa.CompanyName,
                        UnitLevel = paThaKa.UnitLevel,
                        StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                        QuarterCityTownship = paThaKa.QuarterCityTownship,
                        State = paThaKa.State,
                        Country = paThaKa.Country,
                        PostalCode = paThaKa.PostalCode,
                        Remark = row.certificate.Remark
                    }))
            .Concat(CertificateBaseRows(db, request, CertificateType)
                .Join(db.PaThaKaRegistrations,
                    certificate => certificate.TransactionId,
                    registration => registration.Id,
                    (certificate, registration) => new { certificate, registration })
                .Join(db.Eiccnos,
                    row => row.registration.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (row, eiccNo) => new EiccReportRow
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
                        CreatedDate = row.certificate.CreatedDate,
                        PaThaKaNo = row.registration.CompanyRegistrationNo,
                        CompanyName = row.registration.CompanyName,
                        UnitLevel = row.registration.UnitLevel,
                        StreetNumberStreetName = row.registration.StreetNumberStreetName,
                        QuarterCityTownship = row.registration.QuarterCityTownship,
                        State = row.registration.State,
                        Country = row.registration.Country,
                        PostalCode = row.registration.PostalCode,
                        Remark = row.certificate.Remark
                    }))
            .Concat(CertificatePaThaKaRows(db, request, db.ReExportRegistrations.Select(row => new RegistrationAddressRow
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                EICCNoId = row.EiccnoId,
                ApplyType = row.ApplyType,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                EICCStatus = row.Eiccstatus
            })))
            .Concat(CertificatePaThaKaRows(db, request, db.SaleCenterRegistrations.Select(row => new RegistrationAddressRow
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                EICCNoId = row.EiccnoId,
                ApplyType = row.ApplyType,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                EICCStatus = row.Eiccstatus
            })))
            .Concat(CertificatePaThaKaRows(db, request, db.ShowRoomRegistrations
                .Where(_ => true)
                .Select(row => new RegistrationAddressRow
                {
                    Id = row.Id,
                    PaThaKaId = row.PaThaKaId,
                    EICCNoId = row.EiccnoId,
                    ApplyType = row.ApplyType,
                    ApplicationNo = row.ApplicationNo,
                    ApplicationDate = row.ApplicationDate,
                    EICCStatus = row.Eiccstatus
                }), showRoomOnly: true))
            .Concat(CertificatePaThaKaRows(db, request, db.WholeSaleRetailRegistrations.Select(row => new RegistrationAddressRow
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                EICCNoId = row.EiccnoId,
                ApplyType = row.ApplyType,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                EICCStatus = row.Eiccstatus
            })))
            .Concat(CertificatePaThaKaRows(db, request, db.WineImportationRegistrations.Select(row => new RegistrationAddressRow
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId,
                EICCNoId = row.EiccnoId,
                ApplyType = row.ApplyType,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                EICCStatus = row.Eiccstatus
            })));
    }

    private static IQueryable<EiccReportRow> CertificatePaThaKaRows(
        TradeNetDbContext db,
        sp_EICCReportRequest request,
        IQueryable<RegistrationAddressRow> registrations,
        bool showRoomOnly = false)
    {
        return
            from certificate in CertificateBaseRows(db, request, CertificateType)
            join registration in registrations on certificate.TransactionId equals registration.Id
            join eiccNo in db.Eiccnos on registration.EICCNoId equals eiccNo.Id
            join paThaKa in db.PaThaKas on registration.PaThaKaId equals paThaKa.Id
            where !showRoomOnly || EF.Functions.Like(certificate.FormType, "Show Room%")
            select new EiccReportRow
            {
                EICCId = certificate.Id,
                Id = registration.Id,
                FormType = certificate.FormType,
                ApplyType = registration.ApplyType,
                ApplicationNo = registration.ApplicationNo,
                ApplicationDate = registration.ApplicationDate,
                EICCNo = eiccNo.Code,
                EICCDate = certificate.Eiccdate,
                EICCStatus = registration.EICCStatus,
                CreatedDate = certificate.CreatedDate,
                PaThaKaNo = paThaKa.PaThaKaNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                Remark = certificate.Remark
            };
    }

    private static IQueryable<EiccReportRow> LicencePermitRows(
        TradeNetDbContext db,
        sp_EICCReportRequest request)
    {
        return LicencePermitBaseRows(db, request, LicencePermitType, strictProductFilter: true)
            .Join(db.ExportLicences,
                certificate => certificate.TransactionId,
                licence => licence.Id,
                (certificate, licence) => new { certificate, licence })
            .Join(db.Eiccnos,
                row => row.licence.EiccnoId,
                eiccNo => eiccNo.Id,
                (row, eiccNo) => new { row.certificate, row.licence, eiccNo })
            .Join(db.PaThaKas,
                row => row.licence.PaThaKaId,
                paThaKa => paThaKa.Id,
                (row, paThaKa) => new EiccReportRow
                {
                    EICCId = row.certificate.Id,
                    Id = row.licence.Id,
                    FormType = row.certificate.FormType,
                    ApplyType = row.licence.ApplyType,
                    ApplicationNo = row.licence.ApplicationNo,
                    ApplicationDate = row.licence.ApplicationDate,
                    EICCNo = row.eiccNo.Code,
                    EICCDate = row.certificate.Eiccdate,
                    EICCStatus = row.licence.Eiccstatus,
                    CreatedDate = row.certificate.CreatedDate,
                    PaThaKaNo = paThaKa.PaThaKaNo,
                    CompanyName = paThaKa.CompanyName,
                    UnitLevel = paThaKa.UnitLevel,
                    StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                    QuarterCityTownship = paThaKa.QuarterCityTownship,
                    State = paThaKa.State,
                    Country = paThaKa.Country,
                    PostalCode = paThaKa.PostalCode,
                    Remark = row.certificate.Remark
                })
            .Concat(
            from certificate in LicencePermitBaseRows(db, request, LicencePermitType, strictProductFilter: false)
            join licence in db.ImportLicences on certificate.TransactionId equals licence.Id
            join eiccNo in db.Eiccnos on licence.EiccnoId equals eiccNo.Id
            join paThaKa in db.PaThaKas on licence.PaThaKaId equals paThaKa.Id
            select new EiccReportRow
            {
                EICCId = certificate.Id,
                Id = licence.Id,
                FormType = certificate.FormType,
                ApplyType = licence.ApplyType,
                ApplicationNo = licence.ApplicationNo,
                ApplicationDate = licence.ApplicationDate,
                EICCNo = eiccNo.Code,
                EICCDate = certificate.Eiccdate,
                EICCStatus = licence.Eiccstatus,
                CreatedDate = certificate.CreatedDate,
                PaThaKaNo = paThaKa.PaThaKaNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                Remark = certificate.Remark
            })
            .Concat(
            from certificate in LicencePermitBaseRows(db, request, LicencePermitType, strictProductFilter: false)
            join permit in db.ExportPermits on certificate.TransactionId equals permit.Id
            join eiccNo in db.Eiccnos on permit.EiccnoId equals eiccNo.Id
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            select new EiccReportRow
            {
                EICCId = certificate.Id,
                Id = permit.Id,
                FormType = certificate.FormType,
                ApplyType = permit.ApplyType,
                ApplicationNo = permit.ApplicationNo,
                ApplicationDate = permit.ApplicationDate,
                EICCNo = eiccNo.Code,
                EICCDate = certificate.Eiccdate,
                EICCStatus = permit.Eiccstatus,
                CreatedDate = certificate.CreatedDate,
                PaThaKaNo = paThaKa.PaThaKaNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                Remark = certificate.Remark
            })
            .Concat(
            from certificate in LicencePermitBaseRows(db, request, LicencePermitType, strictProductFilter: false)
            join permit in db.ImportPermits on certificate.TransactionId equals permit.Id
            join eiccNo in db.Eiccnos on permit.EiccnoId equals eiccNo.Id
            join paThaKa in db.PaThaKas on permit.PaThaKaId equals paThaKa.Id
            select new EiccReportRow
            {
                EICCId = certificate.Id,
                Id = permit.Id,
                FormType = certificate.FormType,
                ApplyType = permit.ApplyType,
                ApplicationNo = permit.ApplicationNo,
                ApplicationDate = permit.ApplicationDate,
                EICCNo = eiccNo.Code,
                EICCDate = certificate.Eiccdate,
                EICCStatus = permit.Eiccstatus,
                CreatedDate = certificate.CreatedDate,
                PaThaKaNo = paThaKa.PaThaKaNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                Remark = certificate.Remark
            });
    }

    private static IQueryable<EiccReportRow> BorderLicencePermitRows(
        TradeNetDbContext db,
        sp_EICCReportRequest request)
    {
        return BorderRows(db, request, db.BorderExportLicences.Select(row => new LicencePermitAddressRow
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId!,
                EICCNoId = row.EiccnoId,
                ApplyType = row.ApplyType,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                EICCStatus = row.Eiccstatus
            }))
            .Concat(BorderRows(db, request, db.BorderImportLicences.Select(row => new LicencePermitAddressRow
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId!,
                EICCNoId = row.EiccnoId,
                ApplyType = row.ApplyType,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                EICCStatus = row.Eiccstatus
            })))
            .Concat(BorderRows(db, request, db.BorderExportPermits.Select(row => new LicencePermitAddressRow
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId!,
                EICCNoId = row.EiccnoId,
                ApplyType = row.ApplyType,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                EICCStatus = row.Eiccstatus
            })))
            .Concat(BorderRows(db, request, db.BorderImportPermits.Select(row => new LicencePermitAddressRow
            {
                Id = row.Id,
                PaThaKaId = row.PaThaKaId!,
                EICCNoId = row.EiccnoId,
                ApplyType = row.ApplyType,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                EICCStatus = row.Eiccstatus
            })));
    }

    private static IQueryable<EiccReportRow> BorderRows(
        TradeNetDbContext db,
        sp_EICCReportRequest request,
        IQueryable<LicencePermitAddressRow> source)
    {
        return
            from certificate in LicencePermitBaseRows(db, request, BorderLicencePermitType, strictProductFilter: false)
            join row in source on certificate.TransactionId equals row.Id
            join eiccNo in db.Eiccnos on row.EICCNoId equals eiccNo.Id
            join paThaKa in db.PaThaKas on row.PaThaKaId equals paThaKa.Id
            select new EiccReportRow
            {
                EICCId = certificate.Id,
                Id = row.Id,
                FormType = certificate.FormType,
                ApplyType = row.ApplyType,
                ApplicationNo = row.ApplicationNo,
                ApplicationDate = row.ApplicationDate,
                EICCNo = eiccNo.Code,
                EICCDate = certificate.Eiccdate,
                EICCStatus = row.EICCStatus,
                CreatedDate = certificate.CreatedDate,
                PaThaKaNo = paThaKa.PaThaKaNo,
                CompanyName = paThaKa.CompanyName,
                UnitLevel = paThaKa.UnitLevel,
                StreetNumberStreetName = paThaKa.StreetNumberStreetName,
                QuarterCityTownship = paThaKa.QuarterCityTownship,
                State = paThaKa.State,
                Country = paThaKa.Country,
                PostalCode = paThaKa.PostalCode,
                Remark = certificate.Remark
            };
    }

    private static IQueryable<API.Model.TradeNet.Eicccertificate> CertificateBaseRows(
        TradeNetDbContext db,
        sp_EICCReportRequest request,
        string type)
    {
        return db.Eicccertificates
            .Where(certificate => request.Type == type
                && certificate.Status == request.EICCStatus
                && certificate.Eiccdate >= request.EICCDate
                && certificate.Eiccdate <= request.EICCDate);
    }

    private static IQueryable<API.Model.TradeNet.Eicccertificate> LicencePermitBaseRows(
        TradeNetDbContext db,
        sp_EICCReportRequest request,
        string type,
        bool strictProductFilter)
    {
        return CertificateBaseRows(db, request, type)
            .Where(certificate => strictProductFilter
                ? certificate.ProductGroupId == request.ProductGroupId
                    && certificate.ProductItemId == request.ProductItemId
                : (request.ProductGroupId == 0 || certificate.ProductGroupId == request.ProductGroupId)
                    && (request.ProductItemId == 0 || certificate.ProductItemId == request.ProductItemId));
    }

    private sealed class RegistrationAddressRow
    {
        public string Id { get; set; } = null!;
        public string PaThaKaId { get; set; } = null!;
        public int? EICCNoId { get; set; }
        public string ApplyType { get; set; } = null!;
        public string ApplicationNo { get; set; } = null!;
        public DateTime ApplicationDate { get; set; }
        public string? EICCStatus { get; set; }
    }

    private sealed class LicencePermitAddressRow
    {
        public string Id { get; set; } = null!;
        public string PaThaKaId { get; set; } = null!;
        public int? EICCNoId { get; set; }
        public string ApplyType { get; set; } = null!;
        public string ApplicationNo { get; set; } = null!;
        public DateTime ApplicationDate { get; set; }
        public string? EICCStatus { get; set; }
    }

    private sealed class EiccReportRow
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
        public string PaThaKaNo { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public string? UnitLevel { get; set; }
        public string StreetNumberStreetName { get; set; } = null!;
        public string QuarterCityTownship { get; set; } = null!;
        public string State { get; set; } = null!;
        public string Country { get; set; } = null!;
        public string? PostalCode { get; set; }
        public string? Remark { get; set; }
    }
}
