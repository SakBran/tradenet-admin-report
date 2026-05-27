using API.DBContext;
using API.Model.TradeNet;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class GetRequestByIdImportRequest
{
    public Guid Id { get; set; }
}

public static class GetRequestByIdImport
{
    public static IQueryable<RequestAutoApproveDescriptionImport> Query(
        TradeNetDbContext db,
        GetRequestByIdImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        var id = request.Id.ToString();

        return db.RequestAutoApproveDescriptionImports
            .Where(description => description.Id == id)
            .Take(1);
    }
}
