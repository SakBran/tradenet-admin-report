using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_OGARecommendationListReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int OGADepartmentId { get; set; }
    public int OGASectionId { get; set; }
    public string CompanyRegistrationNo { get; set; } = string.Empty;
    public string ReferenceNo { get; set; } = string.Empty;

    /// <summary>"List" (sorted by date) or "GroupBy" (sorted by Department then Section,
    /// mirroring the legacy OGARecommendationGroupByReport.rdlc grouping).</summary>
    public string FilterBy { get; set; } = "List";
}

public sealed class sp_OGARecommendationListReportResult
{
    public string Id { get; set; } = null!;
    public string SDate { get; set; } = null!;
    public string CompanyRegistrationNo { get; set; } = null!;
    public int OGADepartmentId { get; set; }
    public int OGASectionId { get; set; }
    public string? OGADepartmentName { get; set; }
    public string? OGASectionName { get; set; }
    public string ReferenceNo { get; set; } = null!;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string SFromDate { get; set; } = null!;
    public string SToDate { get; set; } = null!;
    public string? Allowance { get; set; }
    public string Terminate { get; set; } = null!;
    public string IsUsedOnce { get; set; } = null!;
}

public static class sp_OGARecommendationListReport
{
    public static IQueryable<sp_OGARecommendationListReportResult> Query(
        TradeNetDbContext db,
        sp_OGARecommendationListReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var rows =
            from recommendation in db.Ogarecommendations
            join paThaKa in db.PaThaKas on recommendation.PaThaKaId equals paThaKa.Id
            join department in db.Ogadepartments on recommendation.OgadepartmentId equals department.Id
            join section in db.Ogasections on recommendation.OgasectionId equals section.Id
            where recommendation.CreatedDate >= request.FromDate
                && recommendation.CreatedDate <= request.ToDate
                && (request.OGADepartmentId == 0 || recommendation.OgadepartmentId == request.OGADepartmentId)
                && (request.OGASectionId == 0 || recommendation.OgasectionId == request.OGASectionId)
                && (request.CompanyRegistrationNo == string.Empty || paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo)
                && (request.ReferenceNo == string.Empty || recommendation.ReferenceNo == request.ReferenceNo)
            select new { recommendation, paThaKa, department, section };

        // "List" sorts by date (legacy OGARecommendationListReport.rdlc); "GroupBy"
        // sorts by Department then Section (legacy OGARecommendationGroupByReport.rdlc,
        // which groups by Department -> Section over the same data).
        var ordered = request.FilterBy == "GroupBy"
            ? rows
                .OrderBy(x => x.department.SortOrder)
                .ThenBy(x => x.section.SortOrder)
                .ThenBy(x => x.recommendation.CreatedDate)
            : rows
                .OrderBy(x => x.recommendation.CreatedDate)
                .ThenBy(x => x.department.SortOrder)
                .ThenBy(x => x.section.SortOrder);

        return ordered.Select(x => new sp_OGARecommendationListReportResult
        {
            Id = x.recommendation.Id,
            SDate = x.recommendation.CreatedDate.Day.ToString()
                + "/"
                + x.recommendation.CreatedDate.Month.ToString()
                + "/"
                + x.recommendation.CreatedDate.Year.ToString(),
            CompanyRegistrationNo = x.paThaKa.CompanyRegistrationNo,
            OGADepartmentId = x.recommendation.OgadepartmentId,
            OGASectionId = x.recommendation.OgasectionId,
            OGADepartmentName = x.department.EnglishName,
            OGASectionName = x.section.EnglishName,
            ReferenceNo = x.recommendation.ReferenceNo,
            FromDate = x.recommendation.FromDate,
            ToDate = x.recommendation.ToDate,
            SFromDate = x.recommendation.FromDate == null
                ? "-"
                : x.recommendation.FromDate.Value.Day.ToString()
                    + "/"
                    + x.recommendation.FromDate.Value.Month.ToString()
                    + "/"
                    + x.recommendation.FromDate.Value.Year.ToString(),
            SToDate = x.recommendation.ToDate == null
                ? "-"
                : x.recommendation.ToDate.Value.Day.ToString()
                    + "/"
                    + x.recommendation.ToDate.Value.Month.ToString()
                    + "/"
                    + x.recommendation.ToDate.Value.Year.ToString(),
            Allowance = x.recommendation.Allowance,
            Terminate = x.recommendation.IsClosed ? "Yes" : "No",
            IsUsedOnce = x.recommendation.IsUsedOnce ? "Yes" : "No"
        });
    }
}
