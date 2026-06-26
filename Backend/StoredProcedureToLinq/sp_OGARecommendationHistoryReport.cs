using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_OGARecommendationHistoryReportRequest
{
    // The human-readable recommendation Reference No (e.g. 001-005-0906202641893),
    // not the internal GUID. ReferenceNo is unique on OGARecommendation.
    public string ReferenceNo { get; set; } = string.Empty;
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
    // Populated only in the recommendation Info row (Type == "Info"); null on history rows.
    public string? SarNo { get; set; }
    public string? Allowance { get; set; }
}

public static class sp_OGARecommendationHistoryReport
{
    public static IQueryable<sp_OGARecommendationHistoryReportResult> Query(
        TradeNetDbContext db,
        sp_OGARecommendationHistoryReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        // RecommendationInfoRow always returns 1 row (even when there are 0 history
        // records), so the page is never completely empty. Its CreatedDate is null,
        // which the two-key sort puts first.
        return RecommendationInfoRow(db, request)
            .Concat(ExportLicenceRows(db, request))
            .Concat(ImportLicenceRows(db, request))
            .Concat(ExportPermitRows(db, request))
            .Concat(ImportPermitRows(db, request))
            .Concat(BorderExportLicenceRows(db, request))
            .Concat(BorderImportLicenceRows(db, request))
            .Concat(BorderExportPermitRows(db, request))
            .Concat(BorderImportPermitRows(db, request))
            .OrderBy(row => row.CreatedDate.HasValue ? 1 : 0)
            .ThenBy(row => row.CreatedDate);
    }

    private static IQueryable<sp_OGARecommendationHistoryReportResult> RecommendationInfoRow(
        TradeNetDbContext db,
        sp_OGARecommendationHistoryReportRequest request)
    {
        return
            from recommendation in db.Ogarecommendations
            join department in db.Ogadepartments
                on recommendation.OgadepartmentId equals department.Id into deptJoin
            from department in deptJoin.DefaultIfEmpty()
            join section in db.Ogasections
                on recommendation.OgasectionId equals section.Id into secJoin
            from section in secJoin.DefaultIfEmpty()
            where recommendation.ReferenceNo == request.ReferenceNo
            select new sp_OGARecommendationHistoryReportResult
            {
                Id = recommendation.Id,
                SDate = recommendation.SarDate.Day.ToString()
                    + "/" + recommendation.SarDate.Month.ToString()
                    + "/" + recommendation.SarDate.Year.ToString(),
                LicenceNo = "",
                Type = "Info",
                Remark = "",
                Balance = "",
                FullName = "",
                Position = "",
                CreatedDate = null,
                ReferenceNo = recommendation.ReferenceNo,
                OGADepartmentName = department == null ? null : department.EnglishName,
                OGASectionName = section == null ? null : section.EnglishName,
                SarNo = recommendation.SarNo,
                Allowance = recommendation.Allowance,
            };
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
            where recommendation.ReferenceNo == request.ReferenceNo
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
                OGASectionName = section.EnglishName,
                SarNo = (string?)null,
                Allowance = (string?)null,
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
            where recommendation.ReferenceNo == request.ReferenceNo
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
                OGASectionName = section.EnglishName,
                SarNo = (string?)null,
                Allowance = (string?)null,
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
            where recommendation.ReferenceNo == request.ReferenceNo
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
                OGASectionName = section.EnglishName,
                SarNo = (string?)null,
                Allowance = (string?)null,
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
            where recommendation.ReferenceNo == request.ReferenceNo
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
                OGASectionName = section.EnglishName,
                SarNo = (string?)null,
                Allowance = (string?)null,
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
            where recommendation.ReferenceNo == request.ReferenceNo
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
                OGASectionName = section.EnglishName,
                SarNo = (string?)null,
                Allowance = (string?)null,
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
            where recommendation.ReferenceNo == request.ReferenceNo
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
                OGASectionName = section.EnglishName,
                SarNo = (string?)null,
                Allowance = (string?)null,
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
            where recommendation.ReferenceNo == request.ReferenceNo
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
                OGASectionName = section.EnglishName,
                SarNo = (string?)null,
                Allowance = (string?)null,
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
            where recommendation.ReferenceNo == request.ReferenceNo
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
                OGASectionName = section.EnglishName,
                SarNo = (string?)null,
                Allowance = (string?)null,
            };
    }
}
