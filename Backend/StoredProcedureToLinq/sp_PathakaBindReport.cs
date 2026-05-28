using API.DBContext;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_PathakaBindReportRequest
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public sealed class sp_PathakaBindReportResult
{
    public DateTime ApplicationDate { get; set; }
    public DateTime? ApproveDate { get; set; }
    public string ApplicationNo { get; set; } = null!;
    public string BindApplicationNo { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? PaThaKaNo { get; set; }
    public string? MemberCode { get; set; }
    public string? Email { get; set; }
    public string CompanyName { get; set; } = null!;
}

public static class sp_PathakaBindReport
{
    public static IQueryable<sp_PathakaBindReportResult> Query(
        TradeNetDbContext db,
        sp_PathakaBindReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        return from bind in db.PaThaKaBinds
               from registration in db.PaThaKaRegistrations
                   .Where(row => row.Id == bind.PaThaKaId)
                   .DefaultIfEmpty()
               from paThaKa in db.PaThaKas
                   .Where(row => row.Id == bind.PaThaKaId)
                   .DefaultIfEmpty()
               from member in db.Members
                   .Where(row => row.Id == bind.MemberId)
                   .DefaultIfEmpty()
               where registration.ApproveDate >= request.FromDate
                   && registration.ApproveDate <= request.ToDate
                   && paThaKa.MemberId != null
               select new sp_PathakaBindReportResult
               {
                   ApplicationDate = bind.ApplicationDate,
                   ApproveDate = registration.ApproveDate,
                   ApplicationNo = bind.ApplicationNo,
                   BindApplicationNo = registration.ApplicationNo,
                   Status = registration.Status,
                   PaThaKaNo = paThaKa.PaThaKaNo,
                   MemberCode = member.MemberCode,
                   Email = member.Email,
                   CompanyName = registration.CompanyName
               };
    }
}
