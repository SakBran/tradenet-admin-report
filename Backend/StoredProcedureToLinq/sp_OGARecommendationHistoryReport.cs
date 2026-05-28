using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_OGARecommendationHistoryReportRequest
{
    public string OGARecommendationId { get; set; } = string.Empty;
}

public sealed class sp_OGARecommendationHistoryReportResult
{
    public string Id { get; set; } = null!;
    public string SDate { get; set; } = null!;
    public string LicenceNo { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Remark { get; set; } = null!;
    public string Balance { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Position { get; set; } = null!;
    public DateTime? CreatedDate { get; set; }
    public string ReferenceNo { get; set; } = null!;
    public string? OGADepartmentName { get; set; }
    public string? OGASectionName { get; set; }
}

public static class sp_OGARecommendationHistoryReport
{
    public static IQueryable<sp_OGARecommendationHistoryReportResult> Query(
        TradeNetDbContext db,
        sp_OGARecommendationHistoryReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return ExportLicenceRows(db, request)
            .Concat(ImportLicenceRows(db, request))
            .Concat(ExportPermitRows(db, request))
            .Concat(ImportPermitRows(db, request))
            .Concat(BorderExportLicenceRows(db, request))
            .Concat(BorderImportLicenceRows(db, request))
            .Concat(BorderExportPermitRows(db, request))
            .Concat(BorderImportPermitRows(db, request))
            .OrderBy(row => row.CreatedDate);
    }

    private static IQueryable<sp_OGARecommendationHistoryReportResult> ExportLicenceRows(
        TradeNetDbContext db,
        sp_OGARecommendationHistoryReportRequest request)
    {
        return
            from history in db.OgarecommendationHistories
            join recommendation in db.Ogarecommendations on history.OgarecommendationId equals recommendation.Id
            join department in db.Ogadepartments on recommendation.OgadepartmentId equals department.Id
            join section in db.Ogasections on recommendation.OgasectionId equals section.Id
            join user in db.Users on history.MocuserId equals user.Id
            join licence in db.ExportLicences on history.LicencePermitId equals licence.Id
            where history.OgarecommendationId == request.OGARecommendationId
                && history.Type == "Export Licence"
            select new sp_OGARecommendationHistoryReportResult
            {
                Id = history.Id,
                SDate = history.CreatedDate == null
                    ? "-"
                    : history.CreatedDate!.Value.Day.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Month.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Year.ToString(),
                LicenceNo = licence.ExportLicenceNo,
                Type = history.Type,
                Remark = history.Remark,
                Balance = history.Balance,
                FullName = user.FullName,
                Position = user.Position,
                CreatedDate = history.CreatedDate,
                ReferenceNo = recommendation.ReferenceNo,
                OGADepartmentName = department.EnglishName,
                OGASectionName = section.EnglishName
            };
    }

    private static IQueryable<sp_OGARecommendationHistoryReportResult> ImportLicenceRows(
        TradeNetDbContext db,
        sp_OGARecommendationHistoryReportRequest request)
    {
        return
            from history in db.OgarecommendationHistories
            join recommendation in db.Ogarecommendations on history.OgarecommendationId equals recommendation.Id
            join department in db.Ogadepartments on recommendation.OgadepartmentId equals department.Id
            join section in db.Ogasections on recommendation.OgasectionId equals section.Id
            join user in db.Users on history.MocuserId equals user.Id
            join licence in db.ImportLicences on history.LicencePermitId equals licence.Id
            where history.OgarecommendationId == request.OGARecommendationId
                && history.Type == "Import Licence"
            select new sp_OGARecommendationHistoryReportResult
            {
                Id = history.Id,
                SDate = history.CreatedDate == null
                    ? "-"
                    : history.CreatedDate!.Value.Day.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Month.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Year.ToString(),
                LicenceNo = licence.ImportLicenceNo,
                Type = history.Type,
                Remark = history.Remark,
                Balance = history.Balance,
                FullName = user.FullName,
                Position = user.Position,
                CreatedDate = history.CreatedDate,
                ReferenceNo = recommendation.ReferenceNo,
                OGADepartmentName = department.EnglishName,
                OGASectionName = section.EnglishName
            };
    }

    private static IQueryable<sp_OGARecommendationHistoryReportResult> ExportPermitRows(
        TradeNetDbContext db,
        sp_OGARecommendationHistoryReportRequest request)
    {
        return
            from history in db.OgarecommendationHistories
            join recommendation in db.Ogarecommendations on history.OgarecommendationId equals recommendation.Id
            join department in db.Ogadepartments on recommendation.OgadepartmentId equals department.Id
            join section in db.Ogasections on recommendation.OgasectionId equals section.Id
            join user in db.Users on history.MocuserId equals user.Id
            join permit in db.ExportPermits on history.LicencePermitId equals permit.Id
            where history.OgarecommendationId == request.OGARecommendationId
                && history.Type == "Export Permit"
            select new sp_OGARecommendationHistoryReportResult
            {
                Id = history.Id,
                SDate = history.CreatedDate == null
                    ? "-"
                    : history.CreatedDate!.Value.Day.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Month.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Year.ToString(),
                LicenceNo = permit.ExportPermitNo,
                Type = history.Type,
                Remark = history.Remark,
                Balance = history.Balance,
                FullName = user.FullName,
                Position = user.Position,
                CreatedDate = history.CreatedDate,
                ReferenceNo = recommendation.ReferenceNo,
                OGADepartmentName = department.EnglishName,
                OGASectionName = section.EnglishName
            };
    }

    private static IQueryable<sp_OGARecommendationHistoryReportResult> ImportPermitRows(
        TradeNetDbContext db,
        sp_OGARecommendationHistoryReportRequest request)
    {
        return
            from history in db.OgarecommendationHistories
            join recommendation in db.Ogarecommendations on history.OgarecommendationId equals recommendation.Id
            join department in db.Ogadepartments on recommendation.OgadepartmentId equals department.Id
            join section in db.Ogasections on recommendation.OgasectionId equals section.Id
            join user in db.Users on history.MocuserId equals user.Id
            join permit in db.ImportPermits on history.LicencePermitId equals permit.Id
            where history.OgarecommendationId == request.OGARecommendationId
                && history.Type == "Import Permit"
            select new sp_OGARecommendationHistoryReportResult
            {
                Id = history.Id,
                SDate = history.CreatedDate == null
                    ? "-"
                    : history.CreatedDate!.Value.Day.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Month.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Year.ToString(),
                LicenceNo = permit.ImportPermitNo,
                Type = history.Type,
                Remark = history.Remark,
                Balance = history.Balance,
                FullName = user.FullName,
                Position = user.Position,
                CreatedDate = history.CreatedDate,
                ReferenceNo = recommendation.ReferenceNo,
                OGADepartmentName = department.EnglishName,
                OGASectionName = section.EnglishName
            };
    }

    private static IQueryable<sp_OGARecommendationHistoryReportResult> BorderExportLicenceRows(
        TradeNetDbContext db,
        sp_OGARecommendationHistoryReportRequest request)
    {
        return
            from history in db.OgarecommendationHistories
            join recommendation in db.Ogarecommendations on history.OgarecommendationId equals recommendation.Id
            join department in db.Ogadepartments on recommendation.OgadepartmentId equals department.Id
            join section in db.Ogasections on recommendation.OgasectionId equals section.Id
            join user in db.Users on history.MocuserId equals user.Id
            join licence in db.BorderExportLicences on history.LicencePermitId equals licence.Id
            where history.OgarecommendationId == request.OGARecommendationId
                && history.Type == "Border Export Licence"
            select new sp_OGARecommendationHistoryReportResult
            {
                Id = history.Id,
                SDate = history.CreatedDate == null
                    ? "-"
                    : history.CreatedDate!.Value.Day.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Month.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Year.ToString(),
                LicenceNo = licence.ExportLicenceNo,
                Type = history.Type,
                Remark = history.Remark,
                Balance = history.Balance,
                FullName = user.FullName,
                Position = user.Position,
                CreatedDate = history.CreatedDate,
                ReferenceNo = recommendation.ReferenceNo,
                OGADepartmentName = department.EnglishName,
                OGASectionName = section.EnglishName
            };
    }

    private static IQueryable<sp_OGARecommendationHistoryReportResult> BorderImportLicenceRows(
        TradeNetDbContext db,
        sp_OGARecommendationHistoryReportRequest request)
    {
        return
            from history in db.OgarecommendationHistories
            join recommendation in db.Ogarecommendations on history.OgarecommendationId equals recommendation.Id
            join department in db.Ogadepartments on recommendation.OgadepartmentId equals department.Id
            join section in db.Ogasections on recommendation.OgasectionId equals section.Id
            join user in db.Users on history.MocuserId equals user.Id
            join licence in db.BorderImportLicences on history.LicencePermitId equals licence.Id
            where history.OgarecommendationId == request.OGARecommendationId
                && history.Type == "Border Import Licence"
            select new sp_OGARecommendationHistoryReportResult
            {
                Id = history.Id,
                SDate = history.CreatedDate == null
                    ? "-"
                    : history.CreatedDate!.Value.Day.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Month.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Year.ToString(),
                LicenceNo = licence.ImportLicenceNo,
                Type = history.Type,
                Remark = history.Remark,
                Balance = history.Balance,
                FullName = user.FullName,
                Position = user.Position,
                CreatedDate = history.CreatedDate,
                ReferenceNo = recommendation.ReferenceNo,
                OGADepartmentName = department.EnglishName,
                OGASectionName = section.EnglishName
            };
    }

    private static IQueryable<sp_OGARecommendationHistoryReportResult> BorderExportPermitRows(
        TradeNetDbContext db,
        sp_OGARecommendationHistoryReportRequest request)
    {
        return
            from history in db.OgarecommendationHistories
            join recommendation in db.Ogarecommendations on history.OgarecommendationId equals recommendation.Id
            join department in db.Ogadepartments on recommendation.OgadepartmentId equals department.Id
            join section in db.Ogasections on recommendation.OgasectionId equals section.Id
            join user in db.Users on history.MocuserId equals user.Id
            join permit in db.BorderExportPermits on history.LicencePermitId equals permit.Id
            where history.OgarecommendationId == request.OGARecommendationId
                && history.Type == "Border Export Permit"
            select new sp_OGARecommendationHistoryReportResult
            {
                Id = history.Id,
                SDate = history.CreatedDate == null
                    ? "-"
                    : history.CreatedDate!.Value.Day.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Month.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Year.ToString(),
                LicenceNo = permit.ExportPermitNo,
                Type = history.Type,
                Remark = history.Remark,
                Balance = history.Balance,
                FullName = user.FullName,
                Position = user.Position,
                CreatedDate = history.CreatedDate,
                ReferenceNo = recommendation.ReferenceNo,
                OGADepartmentName = department.EnglishName,
                OGASectionName = section.EnglishName
            };
    }

    private static IQueryable<sp_OGARecommendationHistoryReportResult> BorderImportPermitRows(
        TradeNetDbContext db,
        sp_OGARecommendationHistoryReportRequest request)
    {
        return
            from history in db.OgarecommendationHistories
            join recommendation in db.Ogarecommendations on history.OgarecommendationId equals recommendation.Id
            join department in db.Ogadepartments on recommendation.OgadepartmentId equals department.Id
            join section in db.Ogasections on recommendation.OgasectionId equals section.Id
            join user in db.Users on history.MocuserId equals user.Id
            join permit in db.BorderImportPermits on history.LicencePermitId equals permit.Id
            where history.OgarecommendationId == request.OGARecommendationId
                && history.Type == "Border Import Permit"
            select new sp_OGARecommendationHistoryReportResult
            {
                Id = history.Id,
                SDate = history.CreatedDate == null
                    ? "-"
                    : history.CreatedDate!.Value.Day.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Month.ToString()
                        + "/"
                        + history.CreatedDate!.Value.Year.ToString(),
                LicenceNo = permit.ImportPermitNo,
                Type = history.Type,
                Remark = history.Remark,
                Balance = history.Balance,
                FullName = user.FullName,
                Position = user.Position,
                CreatedDate = history.CreatedDate,
                ReferenceNo = recommendation.ReferenceNo,
                OGADepartmentName = department.EnglishName,
                OGASectionName = section.EnglishName
            };
    }
}
