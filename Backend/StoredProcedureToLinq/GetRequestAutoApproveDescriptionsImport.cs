using API.DBContext;
using API.Model.TradeNet;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class GetRequestAutoApproveDescriptionsImportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public static class GetRequestAutoApproveDescriptionsImport
{
    public static IQueryable<RequestAutoApproveDescriptionImport> Query(
        TradeNetDbContext db,
        GetRequestAutoApproveDescriptionsImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return db.RequestAutoApproveDescriptionImports
            .Where(description =>
                description.CreatedDate >= request.FromDate
                && description.CreatedDate <= request.ToDate);
    }
}
