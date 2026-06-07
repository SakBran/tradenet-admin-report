using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace API.Model
{
    public class ApiResult<T>
    {
        private readonly bool? _hasNextPage;

        /// <summary>
        /// Private constructor called by the CreateAsync method.
        /// </summary>
        private ApiResult(
            List<T> data,
            int count,
            int pageIndex,
            int pageSize,
            string sortColumn,
            string sortOrder,
            string filterColumn,
            string filterQuery,
            bool isTotalCountExact = true,
            bool? hasNextPage = null)
        {
            Data = data;
            PageIndex = pageIndex;
            PageSize = pageSize;
            TotalCount = count;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);
            IsTotalCountExact = isTotalCountExact;
            _hasNextPage = hasNextPage;
            SortColumn = sortColumn;
            SortOrder = sortOrder;
            FilterColumn = filterColumn;
            FilterQuery = filterQuery;
        }

        #region Methods
        /// <summary>
        /// Pages, sorts and/or filters a IQueryable source.
        /// </summary>
        /// <param name="source">An IQueryable source of generic type</param>
        /// <param name="pageIndex">Zero-based current page index (0 = first page)</param>
        /// <param name="pageSize">The actual size of each page</param>
        /// <param name="sortColumn">The sorting colum name</param>
        /// <param name="sortOrder">The sorting order ("ASC" or "DESC")</param>
        /// <param name="filterColumn">The filtering column name</param>
        /// <param name="filterQuery">The filtering query (value to lookup)</param>
        /// <returns>
        /// A object containing the IQueryable paged/sorted/filtered result 
        /// and all the relevant paging/sorting/filtering navigation info.
        /// </returns>
        public static async Task<ApiResult<T>> CreateAsync(
            IQueryable<T> source,
            int pageIndex,
            int pageSize,
            string? sortColumn = null,
            string? sortOrder = null,
            string? filterColumn = null,
            string? filterQuery = null)
        {
            pageIndex = Math.Max(0, pageIndex);
            pageSize = NormalizePageSize(pageSize);
            source = ApplyFilter(source, filterColumn, filterQuery);

            var count = await CountAsyncSafe(source);

            source = ApplySort(source, sortColumn, ref sortOrder)
                .Skip(pageIndex * pageSize)
                .Take(pageSize);

            var data = await ToListAsyncSafe(source);
            return new ApiResult<T>(
                data,
                count,
                pageIndex,
                pageSize,
                sortColumn ?? "",
                sortOrder ?? "",
                filterColumn ?? "",
                filterQuery ?? "");
        }

        public static async Task<ApiResult<T>> CreateFastPageAsync(
            IQueryable<T> source,
            int pageIndex,
            int pageSize,
            string? sortColumn = null,
            string? sortOrder = null,
            string? filterColumn = null,
            string? filterQuery = null,
            bool includeTotalCount = false)
        {
            if (includeTotalCount)
            {
                return await CreateAsync(
                    source,
                    pageIndex,
                    pageSize,
                    sortColumn,
                    sortOrder,
                    filterColumn,
                    filterQuery);
            }

            pageIndex = Math.Max(0, pageIndex);
            pageSize = NormalizePageSize(pageSize);
            source = ApplyFilter(source, filterColumn, filterQuery);
            source = ApplySort(source, sortColumn, ref sortOrder);

            var pageRows = await ToListAsyncSafe(
                source
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize + 1));
            var hasNextPage = pageRows.Count > pageSize;

            if (hasNextPage)
            {
                pageRows.RemoveRange(pageSize, pageRows.Count - pageSize);
            }

            var estimatedCount = pageIndex * pageSize
                + pageRows.Count
                + (hasNextPage ? 1 : 0);

            return new ApiResult<T>(
                pageRows,
                estimatedCount,
                pageIndex,
                pageSize,
                sortColumn ?? "",
                sortOrder ?? "",
                filterColumn ?? "",
                filterQuery ?? "",
                isTotalCountExact: false,
                hasNextPage: hasNextPage);
        }

        public static ApiResult<T> CreateFastPageFromRows(
            List<T> pageRows,
            int pageIndex,
            int pageSize,
            string? sortColumn = null,
            string? sortOrder = null,
            string? filterColumn = null,
            string? filterQuery = null)
        {
            pageIndex = Math.Max(0, pageIndex);
            pageSize = NormalizePageSize(pageSize);

            var hasNextPage = pageRows.Count > pageSize;
            if (hasNextPage)
            {
                pageRows.RemoveRange(pageSize, pageRows.Count - pageSize);
            }

            var estimatedCount = pageIndex * pageSize
                + pageRows.Count
                + (hasNextPage ? 1 : 0);

            return new ApiResult<T>(
                pageRows,
                estimatedCount,
                pageIndex,
                pageSize,
                sortColumn ?? "",
                sortOrder ?? "",
                filterColumn ?? "",
                filterQuery ?? "",
                isTotalCountExact: false,
                hasNextPage: hasNextPage);
        }

        public static ApiResult<T> CreatePageFromRows(
            List<T> pageRows,
            int totalCount,
            int pageIndex,
            int pageSize,
            string? sortColumn = null,
            string? sortOrder = null,
            string? filterColumn = null,
            string? filterQuery = null)
        {
            pageIndex = Math.Max(0, pageIndex);
            pageSize = NormalizePageSize(pageSize);

            return new ApiResult<T>(
                pageRows,
                Math.Max(0, totalCount),
                pageIndex,
                pageSize,
                sortColumn ?? "",
                sortOrder ?? "",
                filterColumn ?? "",
                filterQuery ?? "");
        }

        private static int NormalizePageSize(int pageSize)
        {
            return pageSize <= 0 ? 10 : pageSize;
        }

        private static IQueryable<T> ApplyFilter(
            IQueryable<T> source,
            string? filterColumn,
            string? filterQuery)
        {
            if (string.IsNullOrEmpty(filterColumn)
                || string.IsNullOrEmpty(filterQuery)
                || !IsValidProperty(filterColumn))
            {
                return source;
            }

            if (filterColumn.Contains("Date"))
            {
                var endate = Convert.ToDateTime(filterQuery);
                endate = endate.AddDays(1);
                string EndDateSQLparam = filterColumn + " < @0";
                source = source.Where(EndDateSQLparam, endate);

                var startDate = Convert.ToDateTime(filterQuery);
                string StartDateSQLparam = filterColumn + " > @0";
                source = source.Where(StartDateSQLparam, startDate);

                return source;
            }

            try
            {
                return source.Where(
                    string.Format("{0}.Contains(@0)",
                    filterColumn),
                    filterQuery);
            }
            catch
            {
                return source.Where(
                    string.Format("{0}==@0",
                    filterColumn),
                    filterQuery);
            }
        }

        private static IQueryable<T> ApplySort(
            IQueryable<T> source,
            string? sortColumn,
            ref string? sortOrder)
        {
            if (string.IsNullOrEmpty(sortColumn)
                || !IsValidProperty(sortColumn))
            {
                return source;
            }

            sortOrder = !string.IsNullOrEmpty(sortOrder)
                && sortOrder.ToUpper() == "ASC"
                ? "ASC"
                : "DESC";

            return source.OrderBy(
                string.Format(
                    "{0} {1}",
                    sortColumn,
                    sortOrder)
                );
        }

        private static async Task<int> CountAsyncSafe(IQueryable<T> source)
        {
            if (source.Provider is Microsoft.EntityFrameworkCore.Query.IAsyncQueryProvider)
            {
                return await source.CountAsync();
            }

            return source.Count();
        }

        private static async Task<List<T>> ToListAsyncSafe(IQueryable<T> source)
        {
            if (source.Provider is Microsoft.EntityFrameworkCore.Query.IAsyncQueryProvider)
            {
                return await source.ToListAsync();
            }

            return source.ToList();
        }

        /// <summary>
        /// Checks if the given property name exists
        /// to protect against SQL injection attacks
        /// </summary>
        public static bool IsValidProperty(
            string propertyName,
            bool throwExceptionIfNotFound = true)
        {
            var prop = typeof(T).GetProperty(
                propertyName,
                BindingFlags.IgnoreCase |
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.Instance);
            if (prop == null && throwExceptionIfNotFound)
                throw new NotSupportedException(
                    string.Format(
                        "ERROR: Property '{0}' does not exist.",
                        propertyName)
                    );
            return prop != null;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The data result.
        /// </summary>
        public List<T> Data { get; private set; }

        /// <summary>
        /// Zero-based index of current page.
        /// </summary>
        public int PageIndex { get; private set; }

        /// <summary>
        /// Number of items contained in each page.
        /// </summary>
        public int PageSize { get; private set; }

        /// <summary>
        /// Total items count
        /// </summary>
        public int TotalCount { get; private set; }

        /// <summary>
        /// Total pages count
        /// </summary>
        public int TotalPages { get; private set; }

        /// <summary>
        /// TRUE when TotalCount is exact, FALSE when it is estimated for fast pagination.
        /// </summary>
        public bool IsTotalCountExact { get; private set; }

        /// <summary>
        /// TRUE if the current page has a previous page, FALSE otherwise.
        /// </summary>
        public bool HasPreviousPage
        {
            get
            {
                return (PageIndex > 0);
            }
        }

        /// <summary>
        /// TRUE if the current page has a next page, FALSE otherwise.
        /// </summary>
        public bool HasNextPage
        {
            get
            {
                return _hasNextPage ?? ((PageIndex + 1) < TotalPages);
            }
        }

        /// <summary>
        /// Sorting Column name (or null if none set)
        /// </summary>
        public string SortColumn { get; set; }

        /// <summary>
        /// Sorting Order ("ASC", "DESC" or null if none set)
        /// </summary>
        public string SortOrder { get; set; }

        /// <summary>
        /// Filter Column name (or null if none set)
        /// </summary>
        public string FilterColumn { get; set; }

        /// <summary>
        /// Filter Query string
        /// (to be used within the given FilterColumn)
        /// </summary>
        public string FilterQuery { get; set; }

        /// <summary>
        /// Optional per-column grand totals, keyed by the column's serialized name
        /// (the frontend column dataIndex, e.g. "companyCount"). When set, the grid
        /// renders a footer "Total" row. Null for reports without a totals row, so
        /// existing reports are unaffected (omitted from the response when null).
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyDictionary<string, decimal>? ColumnTotals { get; set; }

        /// <summary>
        /// Optional currency-grouped summary footer for the Extension reports
        /// (legacy ExtensionReport.rdlc "Currency" group: per-currency licence count
        /// and summed value, plus a grand total licence count). Null for reports that
        /// don't render it, so it is omitted from the response when unset.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReportCurrencyTotalsSummary? CurrencyTotals { get; set; }
        #endregion
    }

    /// <summary>One per-currency line of the Extension report summary footer.</summary>
    public sealed class ReportCurrencyTotal
    {
        public string Currency { get; set; } = string.Empty;
        public int NoOfLicences { get; set; }
        public decimal TotalValue { get; set; }
    }

    /// <summary>
    /// The Extension report summary footer: one <see cref="ReportCurrencyTotal"/> per
    /// currency and the grand total licence count across all currencies (mirrors the
    /// legacy RDLC "Total:N licence(s)" row).
    /// </summary>
    public sealed class ReportCurrencyTotalsSummary
    {
        public IReadOnlyList<ReportCurrencyTotal> Currencies { get; set; } = new List<ReportCurrencyTotal>();
        public int GrandTotalLicences { get; set; }
    }
}
