using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_EICCSubmitBorderLicencePermitListRequest
{
    public string EICCStatus { get; set; } = string.Empty;
    public int UserId { get; set; }
}

public sealed class sp_EICCSubmitBorderLicencePermitListResult
{
    public string Id { get; set; } = null!;
    public string FormType { get; set; } = null!;
    public string ApplyType { get; set; } = null!;
    public string ApplicationNo { get; set; } = null!;
    public DateTime ApplicationDate { get; set; }
    public string SApplicationDate { get; set; } = null!;
    public string EICCNo { get; set; } = null!;
    public DateTime? EICCDate { get; set; }
    public string? SEICCDate { get; set; }
    public string? EICCStatus { get; set; }
}

public static class sp_EICCSubmitBorderLicencePermitList
{
    private const string Approved = "Approved";

    public static IQueryable<sp_EICCSubmitBorderLicencePermitListResult> Query(
        TradeNetDbContext db,
        sp_EICCSubmitBorderLicencePermitListRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return db.BorderExportLicences
            .Join(db.Eiccnos,
                licence => licence.EiccnoId,
                eiccNo => eiccNo.Id,
                (licence, eiccNo) => new { licence, eiccNo })
            .Where(row => row.licence.Eiccstatus == request.EICCStatus
                && row.licence.ApproveUserId == request.UserId
                && row.licence.IsEiccsubmit == true
                && (request.EICCStatus != Approved || row.licence.IsApprove == false))
            .Select(row => new sp_EICCSubmitBorderLicencePermitListResult
            {
                Id = row.licence.Id,
                FormType = "Export Licence",
                ApplyType = row.licence.ApplyType,
                ApplicationNo = row.licence.ApplicationNo,
                ApplicationDate = row.licence.ApplicationDate,
                SApplicationDate = row.licence.ApplicationDate.Day.ToString()
                    + "/"
                    + row.licence.ApplicationDate.Month.ToString()
                    + "/"
                    + row.licence.ApplicationDate.Year.ToString(),
                EICCNo = row.eiccNo.Code,
                EICCDate = row.licence.Eiccdate,
                SEICCDate = row.licence.Eiccdate == null
                    ? null
                    : row.licence.Eiccdate.Value.Day.ToString()
                        + "/"
                        + row.licence.Eiccdate.Value.Month.ToString()
                        + "/"
                        + row.licence.Eiccdate.Value.Year.ToString(),
                EICCStatus = row.licence.Eiccstatus
            })
            .Concat(db.BorderImportLicences
                .Join(db.Eiccnos,
                    licence => licence.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (licence, eiccNo) => new { licence, eiccNo })
                .Where(row => row.licence.Eiccstatus == request.EICCStatus
                    && row.licence.ApproveUserId == request.UserId
                    && row.licence.IsEiccsubmit == true
                    && (request.EICCStatus != Approved || row.licence.IsApprove == false))
                .Select(row => new sp_EICCSubmitBorderLicencePermitListResult
                {
                    Id = row.licence.Id,
                    FormType = "Import Licence",
                    ApplyType = row.licence.ApplyType,
                    ApplicationNo = row.licence.ApplicationNo,
                    ApplicationDate = row.licence.ApplicationDate,
                    SApplicationDate = row.licence.ApplicationDate.Day.ToString()
                        + "/"
                        + row.licence.ApplicationDate.Month.ToString()
                        + "/"
                        + row.licence.ApplicationDate.Year.ToString(),
                    EICCNo = row.eiccNo.Code,
                    EICCDate = row.licence.Eiccdate,
                    SEICCDate = row.licence.Eiccdate == null
                        ? null
                        : row.licence.Eiccdate.Value.Day.ToString()
                            + "/"
                            + row.licence.Eiccdate.Value.Month.ToString()
                            + "/"
                            + row.licence.Eiccdate.Value.Year.ToString(),
                    EICCStatus = row.licence.Eiccstatus
                }))
            .Concat(db.BorderExportPermits
                .Join(db.Eiccnos,
                    permit => permit.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (permit, eiccNo) => new { permit, eiccNo })
                .Where(row => row.permit.Eiccstatus == request.EICCStatus
                    && row.permit.ApproveUserId == request.UserId
                    && row.permit.IsEiccsubmit == true
                    && (request.EICCStatus != Approved || row.permit.IsApprove == false))
                .Select(row => new sp_EICCSubmitBorderLicencePermitListResult
                {
                    Id = row.permit.Id,
                    FormType = "Export Permit",
                    ApplyType = row.permit.ApplyType,
                    ApplicationNo = row.permit.ApplicationNo,
                    ApplicationDate = row.permit.ApplicationDate,
                    SApplicationDate = row.permit.ApplicationDate.Day.ToString()
                        + "/"
                        + row.permit.ApplicationDate.Month.ToString()
                        + "/"
                        + row.permit.ApplicationDate.Year.ToString(),
                    EICCNo = row.eiccNo.Code,
                    EICCDate = row.permit.Eiccdate,
                    SEICCDate = row.permit.Eiccdate == null
                        ? null
                        : row.permit.Eiccdate.Value.Day.ToString()
                            + "/"
                            + row.permit.Eiccdate.Value.Month.ToString()
                            + "/"
                            + row.permit.Eiccdate.Value.Year.ToString(),
                    EICCStatus = row.permit.Eiccstatus
                }))
            .Concat(db.BorderImportPermits
                .Join(db.Eiccnos,
                    permit => permit.EiccnoId,
                    eiccNo => eiccNo.Id,
                    (permit, eiccNo) => new { permit, eiccNo })
                .Where(row => row.permit.Eiccstatus == request.EICCStatus
                    && row.permit.ApproveUserId == request.UserId
                    && row.permit.IsEiccsubmit == true
                    && (request.EICCStatus != Approved || row.permit.IsApprove == false))
                .Select(row => new sp_EICCSubmitBorderLicencePermitListResult
                {
                    Id = row.permit.Id,
                    FormType = "Import Permit",
                    ApplyType = row.permit.ApplyType,
                    ApplicationNo = row.permit.ApplicationNo,
                    ApplicationDate = row.permit.ApplicationDate,
                    SApplicationDate = row.permit.ApplicationDate.Day.ToString()
                        + "/"
                        + row.permit.ApplicationDate.Month.ToString()
                        + "/"
                        + row.permit.ApplicationDate.Year.ToString(),
                    EICCNo = row.eiccNo.Code,
                    EICCDate = row.permit.Eiccdate,
                    SEICCDate = row.permit.Eiccdate == null
                        ? null
                        : row.permit.Eiccdate.Value.Day.ToString()
                            + "/"
                            + row.permit.Eiccdate.Value.Month.ToString()
                            + "/"
                            + row.permit.Eiccdate.Value.Year.ToString(),
                    EICCStatus = row.permit.Eiccstatus
                }))
            .OrderBy(row => row.ApplicationDate);
    }
}
