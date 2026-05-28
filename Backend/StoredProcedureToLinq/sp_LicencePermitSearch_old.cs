using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_LicencePermitSearch_oldRequest
{
    public string LicenceNo { get; set; } = string.Empty;
}

public sealed class sp_LicencePermitSearch_oldResult
{
    public string Id { get; set; } = null!;
    public string FormType { get; set; } = null!;
}

public static class sp_LicencePermitSearch_old
{
    public static IQueryable<sp_LicencePermitSearch_oldResult> Query(
        TradeNetDbContext db,
        sp_LicencePermitSearch_oldRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return sp_LicencePermitSearch
            .Query(db, new sp_LicencePermitSearchRequest { LicenceNo = request.LicenceNo })
            .Select(row => new sp_LicencePermitSearch_oldResult
            {
                Id = row.Id,
                FormType = row.FormType
            });
    }
}
