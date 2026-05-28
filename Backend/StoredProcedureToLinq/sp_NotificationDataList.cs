using API.DBContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace API.StoredProcedureToLinq;

public sealed class sp_NotificationDataListResult
{
    public string Id { get; set; } = null!;
    public string FormType { get; set; } = null!;
    public string MemberId { get; set; } = null!;
    public DateTime EndDate { get; set; }
    public string Email { get; set; } = null!;
    public DateTime WarningDate { get; set; }
}

public static class sp_NotificationDataList
{
    public static IQueryable<sp_NotificationDataListResult> Query(TradeNetDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.PaThaKas
            .Where(paThaKa =>
                paThaKa.MemberId != null
                && -EF.Functions.DateDiffDay(paThaKa.EndDate, DateTime.Now) <= 90)
            .Select(paThaKa => new sp_NotificationDataListResult
            {
                Id = paThaKa.Id,
                FormType = "Pa Tha Ka",
                MemberId = paThaKa.MemberId!,
                EndDate = paThaKa.EndDate,
                Email = paThaKa.Email,
                WarningDate = paThaKa.EndDate.AddDays(-90)
            })
            .Concat(db.IndividualTradings
                .Where(individualTrading =>
                    individualTrading.MemberId != null
                    && -EF.Functions.DateDiffDay(individualTrading.EndDate, DateTime.Now) <= 90)
                .Select(individualTrading => new sp_NotificationDataListResult
                {
                    Id = individualTrading.Id,
                    FormType = "Individual Trading",
                    MemberId = individualTrading.MemberId!,
                    EndDate = individualTrading.EndDate,
                    Email = individualTrading.Email,
                    WarningDate = individualTrading.EndDate.AddDays(-90)
                }))
            .Concat(db.Members
                .Where(member => -EF.Functions.DateDiffDay(member.EndDate, DateTime.Now) <= 90)
                .Select(member => new sp_NotificationDataListResult
                {
                    Id = member.Id,
                    FormType = "Member",
                    MemberId = member.Id,
                    EndDate = member.EndDate,
                    Email = member.Email,
                    WarningDate = member.EndDate.AddDays(-90)
                }));
    }
}
