using API.DBContext;
using API.Model.TradeNet;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class GetRequestByIdRequest
{
    public Guid Id { get; set; }
}

public static class GetRequestById
{
    public static IQueryable<RequestAutoApproveDescription> Query(
        TradeNetDbContext db,
        GetRequestByIdRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var id = request.Id.ToString();

        return db.RequestAutoApproveDescriptions
            .Where(description => description.Id == id)
            .Take(1);
    }
}
