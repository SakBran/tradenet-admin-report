using System;
using System.Linq;
using System.Threading.Tasks;
using API.Model;

namespace API.Service.Reports
{
    public static class ReportQueryService
    {
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 1000;

        public static Task<ApiResult<T>> CreatePagedResultAsync<T>(
            IQueryable<T> query,
            ReportQueryRequest request)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(request);

            var pageIndex = Math.Max(0, request.PageIndex);
            var pageSize = request.PageSize <= 0
                ? DefaultPageSize
                : Math.Min(request.PageSize, MaxPageSize);

            return ApiResult<T>.CreateAsync(
                query,
                pageIndex,
                pageSize,
                request.SortColumn,
                request.SortOrder,
                request.FilterColumn,
                request.FilterQuery);
        }
    }
}
