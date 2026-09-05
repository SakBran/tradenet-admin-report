using API.DBContext;
using API.Model;
using API.Service.Reports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.StoredProcedureToLinq;

/// <summary>
/// Oversea Export Licence summary helpers (By Section / By Method / By Buyer Country / Company
/// List / Daily) on top of <c>dbo.sp_ExportLicenceSummaryReport</c>.
///
/// The detail GRID used to live here as an inline, hand-rolled seek (licence-key page + per-item
/// round trips). It paged licences while counting items, so pages beyond licences/PageSize were
/// empty and the row count never matched the old report; it now runs through
/// <see cref="sp_ExportLicenceDetailReportV3"/>, the legacy procedure paginated at item grain.
/// </summary>
public static class sp_ExportLicenceDetailReportV2
{
    public static async Task<ApiResult<ReportAggregateResult>> CreateSummaryResultAsync(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request,
        ReportQueryRequest pagingRequest,
        ReportAggregateDimension dimension,
        bool includeColumnTotals = false)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pagingRequest);

        var groups = await GetSummaryRowsAsync(db, request, dimension);
        return ReportAggregationService.CreatePagedResultFromGroups(
            groups,
            dimension,
            includeSakhan: false,
            pagingRequest,
            includeColumnTotals);
    }

    public static async Task<List<ReportAggregateResult>> GetSummaryRowsAsync(
        TradeNetDbContext db,
        sp_ExportLicenceDetailReportRequest request,
        ReportAggregateDimension dimension)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Type != "Oversea")
        {
            return [];
        }

        var dimensionName = dimension switch
        {
            ReportAggregateDimension.Section => "Section",
            ReportAggregateDimension.Method => "Method",
            ReportAggregateDimension.Country => "Country",
            ReportAggregateDimension.Company => "Company",
            ReportAggregateDimension.Daily => "Daily",
            _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, null),
        };

        var rows = await sp_ExportLicenceSummaryReport.ExecuteAsync(db, request, dimensionName);
        var groups = rows.Select(row => row.ToAggregateResult(dimension)).ToList();

        if (dimension == ReportAggregateDimension.Daily)
        {
            await ReportUsdConversionService.FillDailyUsdValuesAsync(db, groups);
        }

        return groups;
    }
}
