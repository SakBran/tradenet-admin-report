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

        return
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
            orderby recommendation.CreatedDate, department.SortOrder, section.SortOrder
            select new sp_OGARecommendationListReportResult
            {
                Id = recommendation.Id,
                SDate = recommendation.CreatedDate.Day.ToString()
                    + "/"
                    + recommendation.CreatedDate.Month.ToString()
                    + "/"
                    + recommendation.CreatedDate.Year.ToString(),
                CompanyRegistrationNo = paThaKa.CompanyRegistrationNo,
                OGADepartmentId = recommendation.OgadepartmentId,
                OGASectionId = recommendation.OgasectionId,
                OGADepartmentName = department.EnglishName,
                OGASectionName = section.EnglishName,
                ReferenceNo = recommendation.ReferenceNo,
                FromDate = recommendation.FromDate,
                ToDate = recommendation.ToDate,
                SFromDate = recommendation.FromDate == null
                    ? "-"
                    : recommendation.FromDate.Value.Day.ToString()
                        + "/"
                        + recommendation.FromDate.Value.Month.ToString()
                        + "/"
                        + recommendation.FromDate.Value.Year.ToString(),
                SToDate = recommendation.ToDate == null
                    ? "-"
                    : recommendation.ToDate.Value.Day.ToString()
                        + "/"
                        + recommendation.ToDate.Value.Month.ToString()
                        + "/"
                        + recommendation.ToDate.Value.Year.ToString(),
                Allowance = recommendation.Allowance,
                Terminate = recommendation.IsClosed ? "Yes" : "No",
                IsUsedOnce = recommendation.IsUsedOnce ? "Yes" : "No"
            };
    }
}
