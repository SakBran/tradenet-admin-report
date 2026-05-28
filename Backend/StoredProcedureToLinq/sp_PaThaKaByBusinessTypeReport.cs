using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_PaThaKaByBusinessTypeReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int BusinessTypeId { get; set; }
}

public sealed class sp_PaThaKaByBusinessTypeReportResult
{
    public string BusinessType { get; set; } = null!;
    public int CompanyCount { get; set; }
}

public static class sp_PaThaKaByBusinessTypeReport
{
    private const string Registered = "Registered";

    public static IQueryable<sp_PaThaKaByBusinessTypeReportResult> Query(
        TradeNetDbContext db,
        sp_PaThaKaByBusinessTypeReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from businessType in db.BusinessTypes
               join paThaKa in db.PaThaKas on businessType.Id equals paThaKa.BusinessTypeId
               where paThaKa.IssuedDate >= request.FromDate
                   && paThaKa.IssuedDate <= request.ToDate
                   && (request.BusinessTypeId == 0 || paThaKa.BusinessTypeId == request.BusinessTypeId)
                   && paThaKa.Status == Registered
               group paThaKa by new { businessType.Name, businessType.SortOrder } into groupRows
               orderby groupRows.Key.SortOrder
               select new sp_PaThaKaByBusinessTypeReportResult
               {
                   BusinessType = groupRows.Key.Name,
                   CompanyCount = groupRows.Count()
               };
    }
}
