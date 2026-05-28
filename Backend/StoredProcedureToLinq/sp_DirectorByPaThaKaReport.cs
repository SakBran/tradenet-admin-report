using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_DirectorByPaThaKaReportRequest
{
    public string CompanyRegistrationNo { get; set; } = string.Empty;
}

public sealed class sp_DirectorByPaThaKaReportResult
{
    public string? DirectorName { get; set; }
    public string? DirectorNRC { get; set; }
}

public static class sp_DirectorByPaThaKaReport
{
    public static IQueryable<sp_DirectorByPaThaKaReportResult> Query(
        TradeNetDbContext db,
        sp_DirectorByPaThaKaReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from director in db.PaThaKaDirectors
               join paThaKa in db.PaThaKas on director.PaThaKaId equals paThaKa.Id
               where paThaKa.CompanyRegistrationNo == request.CompanyRegistrationNo
               select new sp_DirectorByPaThaKaReportResult
               {
                   DirectorName = director.Name,
                   DirectorNRC = director.Nrc
               };
    }
}
