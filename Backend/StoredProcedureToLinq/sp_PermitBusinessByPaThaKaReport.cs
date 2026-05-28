using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_PermitBusinessByPaThaKaReportRequest
{
    public string CompanyRegistrationNo { get; set; } = string.Empty;
}

public sealed class sp_PermitBusinessByPaThaKaReportResult
{
    public string? Description { get; set; }
}

public static class sp_PermitBusinessByPaThaKaReport
{
    public static IQueryable<sp_PermitBusinessByPaThaKaReportResult> Query(
        TradeNetDbContext db,
        sp_PermitBusinessByPaThaKaReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from paThaKa in db.PaThaKas
               from paThaKaPermitBusiness in db.PaThaKaPermitBusinesses
                   .Where(row => row.PaThaKaId == paThaKa.Id)
                   .DefaultIfEmpty()
               from permitBusiness in db.PermitBusinesses
                   .Where(row => row.Id == paThaKaPermitBusiness.PermitBusinessId)
                   .DefaultIfEmpty()
               where paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo
               select new sp_PermitBusinessByPaThaKaReportResult
               {
                   Description = permitBusiness.Description
               };
    }
}
