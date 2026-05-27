using API.DBContext;
using API.Model.TradeNet;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class GetRequestAutoApproveDescriptionsRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public static class GetRequestAutoApproveDescriptions
{
    public static IQueryable<RequestAutoApproveDescription> Query(
        TradeNetDbContext db,
        GetRequestAutoApproveDescriptionsRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return db.RequestAutoApproveDescriptions
            .Where(description =>
                description.CreatedDate >= request.FromDate
                && description.CreatedDate <= request.ToDate);
    }
}
