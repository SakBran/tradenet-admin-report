using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using API.Model;

namespace API.Service.Reports
{
    /// <summary>
    /// The grouping dimension for the "By X" / Daily / Total Value summary reports.
    /// These reports group the underlying licence/permit detail rows and show
    /// distinct licence counts and summed values per group.
    /// </summary>
    public enum ReportAggregateDimension
    {
        Section,
        Method,
        Country,
        Company,
        HSCode,
        Daily,
        TotalValue,
    }

    /// <summary>
    /// One detail line feeding an aggregate report. Each _Fast / HS Code source
    /// maps its own detail row onto this shape before grouping.
    /// </summary>
    public sealed class AggregateSourceRow
    {
        public string? SakhanCode { get; init; }
        public string? SakhanName { get; init; }
        public string? SectionName { get; init; }
        public string? MethodName { get; init; }

        /// <summary>Trade counterparty country (buyer for export, seller for import).</summary>
        public string? Country { get; init; }
        public string? CompanyName { get; init; }
        public string? CompanyRegistrationNo { get; init; }
        public string? HSCode { get; init; }
        public string? HSDescription { get; init; }
        public string LicenceNo { get; init; } = string.Empty;
        public DateTime? LicenceDate { get; init; }
        public decimal Amount { get; init; }
        public string? Currency { get; init; }
    }

    /// <summary>
    /// A single grouped output row. Only the fields relevant to a given report's
    /// dimension are populated; the rest stay null and are ignored by the frontend
    /// column config for that report.
    /// </summary>
    public sealed class ReportAggregateResult
    {
        public string? SakhanCode { get; set; }
        public string? SakhanName { get; set; }
        public string? SectionName { get; set; }
        public string? MethodName { get; set; }
        public string? Country { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyRegistrationNo { get; set; }
        public string? HSCode { get; set; }
        public string? HSDescription { get; set; }
        public string? Date { get; set; }
        public int NoOfLicences { get; set; }
        public decimal? TotalValue { get; set; }
        public string? Currency { get; set; }

        // The old RDLC "Total USD Value" column. Left null by Aggregate itself; for the
        // Daily reports it is filled afterwards by ReportUsdConversionService (the FX
        // conversion needs the ExchangeRate table). Non-Daily dimensions don't render
        // this column, so it stays null for them.
        public decimal? TotalUSDValue { get; set; }
    }

    public static class ReportAggregationService
    {
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        /// <summary>
        /// Group the detail rows by the report's dimension (plus currency, and Sakhan
        /// when <paramref name="includeSakhan"/> is set) and return the ordered list of
        /// aggregate rows: distinct licence count and summed amount per group.
        /// </summary>
        public static List<ReportAggregateResult> Aggregate(
            IEnumerable<AggregateSourceRow> rows,
            ReportAggregateDimension dimension,
            bool includeSakhan)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var grouped = rows
                .GroupBy(row => BuildKey(row, dimension, includeSakhan))
                .Select(group => new ReportAggregateResult
                {
                    SakhanCode = includeSakhan ? group.Key.Sakhan : null,
                    SectionName = dimension == ReportAggregateDimension.Section ? group.Key.Label : null,
                    MethodName = dimension == ReportAggregateDimension.Method ? group.Key.Label : null,
                    Country = dimension == ReportAggregateDimension.Country ? group.Key.Label : null,
                    CompanyName = dimension == ReportAggregateDimension.Company || dimension == ReportAggregateDimension.HSCode
                        ? group.Key.CompanyName
                        : null,
                    CompanyRegistrationNo = dimension == ReportAggregateDimension.Company || dimension == ReportAggregateDimension.HSCode
                        ? group.Key.CompanyRegistrationNo
                        : null,
                    HSCode = dimension == ReportAggregateDimension.HSCode ? group.Key.Label : null,
                    HSDescription = dimension == ReportAggregateDimension.HSCode ? group.Key.HSDescription : null,
                    Date = dimension == ReportAggregateDimension.Daily ? group.Key.Label : null,
                    Currency = group.Key.Currency,
                    NoOfLicences = group
                        .Select(row => row.LicenceNo)
                        .Where(licenceNo => !string.IsNullOrEmpty(licenceNo))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    TotalValue = group.Sum(row => row.Amount),
                    TotalUSDValue = null, // Daily: filled later by ReportUsdConversionService.
                })
                .ToList();

            return Order(grouped, dimension, includeSakhan);
        }

        public static ApiResult<ReportAggregateResult> CreatePagedResult(
            IEnumerable<AggregateSourceRow> rows,
            ReportAggregateDimension dimension,
            bool includeSakhan,
            ReportQueryRequest pagingRequest,
            bool includeColumnTotals = false)
        {
            ArgumentNullException.ThrowIfNull(pagingRequest);

            var aggregated = Aggregate(rows, dimension, includeSakhan);

            var pageIndex = Math.Max(0, pagingRequest.PageIndex);
            var pageSize = pagingRequest.PageSize <= 0
                ? DefaultPageSize
                : Math.Min(pagingRequest.PageSize, MaxPageSize);

            var pageRows = aggregated
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToList();

            var result = ApiResult<ReportAggregateResult>.CreatePageFromRows(
                pageRows,
                aggregated.Count,
                pageIndex,
                pageSize,
                null,
                null,
                pagingRequest.FilterColumn,
                pagingRequest.FilterQuery);

            if (includeColumnTotals)
            {
                result.ColumnTotals = BuildColumnTotals(aggregated, dimension);
            }

            return result;
        }

        public static Task<byte[]> CreateExcelWorkbookAsync(
            IEnumerable<AggregateSourceRow> rows,
            ReportAggregateDimension dimension,
            bool includeSakhan,
            ReportQueryRequest pagingRequest,
            string worksheetName)
        {
            ArgumentNullException.ThrowIfNull(pagingRequest);

            var aggregated = Aggregate(rows, dimension, includeSakhan);
            return ExcelGenerator.CreateWorkbookAsync(aggregated.AsQueryable(), pagingRequest, worksheetName);
        }

        /// <summary>
        /// Orders and pages rows that have ALREADY been grouped (e.g. grouped in SQL via GROUP BY),
        /// so the detail set never has to be materialized in memory. Ordering matches
        /// <see cref="Aggregate"/>.
        /// </summary>
        public static ApiResult<ReportAggregateResult> CreatePagedResultFromGroups(
            IReadOnlyList<ReportAggregateResult> grouped,
            ReportAggregateDimension dimension,
            bool includeSakhan,
            ReportQueryRequest pagingRequest,
            bool includeColumnTotals = false)
        {
            ArgumentNullException.ThrowIfNull(grouped);
            ArgumentNullException.ThrowIfNull(pagingRequest);

            var ordered = Order(new List<ReportAggregateResult>(grouped), dimension, includeSakhan);

            var pageIndex = Math.Max(0, pagingRequest.PageIndex);
            var pageSize = pagingRequest.PageSize <= 0
                ? DefaultPageSize
                : Math.Min(pagingRequest.PageSize, MaxPageSize);

            var pageRows = ordered
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToList();

            var result = ApiResult<ReportAggregateResult>.CreatePageFromRows(
                pageRows,
                ordered.Count,
                pageIndex,
                pageSize,
                null,
                null,
                pagingRequest.FilterColumn,
                pagingRequest.FilterQuery);

            if (includeColumnTotals)
            {
                result.ColumnTotals = BuildColumnTotals(ordered, dimension);
            }

            return result;
        }

        /// <summary>
        /// Grand-total footer row matching the legacy RDLC "TOTAL" row: CountDistinct(LicenceNo)
        /// + Sum(Amount) over ALL groups (not just the page). Daily reports additionally roll up
        /// the USD-normalised value, which is meaningful to sum across currencies.
        /// </summary>
        private static Dictionary<string, decimal> BuildColumnTotals(
            IReadOnlyList<ReportAggregateResult> groups,
            ReportAggregateDimension dimension)
        {
            var totals = new Dictionary<string, decimal>
            {
                ["noOfLicences"] = groups.Sum(group => group.NoOfLicences),
                ["totalValue"] = groups.Sum(group => group.TotalValue ?? 0m),
            };

            if (dimension == ReportAggregateDimension.Daily)
            {
                totals["totalUSDValue"] = decimal.Round(groups.Sum(group => group.TotalUSDValue ?? 0m), 4);
            }

            return totals;
        }

        /// <summary>
        /// Orders rows that have ALREADY been grouped and writes them to an Excel workbook.
        /// </summary>
        public static Task<byte[]> CreateExcelWorkbookFromGroupsAsync(
            IReadOnlyList<ReportAggregateResult> grouped,
            ReportAggregateDimension dimension,
            bool includeSakhan,
            ReportQueryRequest pagingRequest,
            string worksheetName)
        {
            ArgumentNullException.ThrowIfNull(grouped);
            ArgumentNullException.ThrowIfNull(pagingRequest);

            var ordered = Order(new List<ReportAggregateResult>(grouped), dimension, includeSakhan);
            return ExcelGenerator.CreateWorkbookAsync(ordered.AsQueryable(), pagingRequest, worksheetName);
        }

        private static AggregateKey BuildKey(
            AggregateSourceRow row,
            ReportAggregateDimension dimension,
            bool includeSakhan)
        {
            var label = dimension switch
            {
                ReportAggregateDimension.Section => row.SectionName,
                ReportAggregateDimension.Method => row.MethodName,
                ReportAggregateDimension.Country => row.Country,
                ReportAggregateDimension.HSCode => row.HSCode,
                ReportAggregateDimension.Daily => row.LicenceDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                _ => null,
            };

            return new AggregateKey(
                label ?? string.Empty,
                row.Currency ?? string.Empty,
                includeSakhan ? row.SakhanCode ?? string.Empty : string.Empty,
                dimension == ReportAggregateDimension.Company || dimension == ReportAggregateDimension.HSCode
                    ? row.CompanyName ?? string.Empty
                    : string.Empty,
                dimension == ReportAggregateDimension.Company || dimension == ReportAggregateDimension.HSCode
                    ? row.CompanyRegistrationNo ?? string.Empty
                    : string.Empty,
                dimension == ReportAggregateDimension.HSCode ? row.HSDescription ?? string.Empty : string.Empty);
        }

        private static List<ReportAggregateResult> Order(
            List<ReportAggregateResult> rows,
            ReportAggregateDimension dimension,
            bool includeSakhan)
        {
            IOrderedEnumerable<ReportAggregateResult> ordered = dimension switch
            {
                ReportAggregateDimension.Section => rows.OrderBy(row => row.SectionName, StringComparer.OrdinalIgnoreCase),
                ReportAggregateDimension.Method => rows.OrderBy(row => row.MethodName, StringComparer.OrdinalIgnoreCase),
                ReportAggregateDimension.Country => rows.OrderBy(row => row.Country, StringComparer.OrdinalIgnoreCase),
                ReportAggregateDimension.Company => rows.OrderBy(row => row.CompanyName, StringComparer.OrdinalIgnoreCase),
                ReportAggregateDimension.HSCode => rows.OrderBy(row => row.HSCode, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(row => row.CompanyName, StringComparer.OrdinalIgnoreCase),
                ReportAggregateDimension.Daily => rows.OrderBy(row => row.Date, StringComparer.Ordinal),
                _ => rows.OrderBy(row => row.Currency, StringComparer.OrdinalIgnoreCase),
            };

            if (includeSakhan)
            {
                ordered = ordered.ThenBy(row => row.SakhanCode, StringComparer.OrdinalIgnoreCase);
            }

            return ordered
                .ThenBy(row => row.Currency, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private readonly record struct AggregateKey(
            string Label,
            string Currency,
            string Sakhan,
            string CompanyName,
            string CompanyRegistrationNo,
            string HSDescription);
    }
}
