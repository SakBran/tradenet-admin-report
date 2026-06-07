using API.DBContext;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

public static partial class sp_HSCodeReport
{
    private sealed class sp_HSCodeAggregateReportResult
    {
        public string? HSCode { get; set; }
        public string? HSDescription { get; set; }
        public string? CompanyRegistrationNo { get; set; }
        public string? CompanyName { get; set; }
        public string? Currency { get; set; }
        public int NoOfLicences { get; set; }
        public decimal TotalValue { get; set; }
        public int? TotalCount { get; set; }
    }

    private static Task<List<sp_HSCodeAggregateReportResult>> ExecuteAggregateStoredProcedureAsync(
        TradeNetDbContext db,
        sp_HSCodeReportRequest request,
        int pageIndex,
        int pageSize,
        bool includeTotalCount)
    {
        pageIndex = Math.Max(0, pageIndex);
        pageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 1000);

        var parameters = new[]
        {
            new SqlParameter("@FromDate", request.FromDate),
            new SqlParameter("@ToDate", request.ToDate),
            new SqlParameter("@FormType", request.FormType),
            new SqlParameter("@FilterType", request.FilterType ?? string.Empty),
            new SqlParameter("@HSCode", request.HSCode ?? string.Empty),
            new SqlParameter("@SakhanId", request.SakhanId),
            new SqlParameter("@PageIndex", pageIndex),
            new SqlParameter("@PageSize", pageSize),
            new SqlParameter("@IncludeTotalCount", includeTotalCount),
        };

        db.Database.SetCommandTimeout(120);

        return db.Database
            .SqlQueryRaw<sp_HSCodeAggregateReportResult>(
                "EXEC dbo.sp_HSCodeReport_pagination @FromDate, @ToDate, @FormType, @FilterType, @HSCode, @SakhanId, @PageIndex, @PageSize, @IncludeTotalCount",
                parameters)
            .ToListAsync();
    }
}
