using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.DBContext;
using API.Model;
using API.Service.ExcelExport;
using API.StoredProcedureToLinq;
using Microsoft.EntityFrameworkCore;

namespace API.Service.Reports
{
    /// <summary>
    /// The two things all eight Advance Search controllers do with
    /// <see cref="sp_AdvanceSearch"/>: page it for the grid, and stream it for the Excel queue.
    ///
    /// Both end in the same post-paging step the legacy repository ran
    /// (AdvanceSearchRepository.cs:1033-1039): the country columns come back as raw ids -- a
    /// comma-joined string on six of the eight types -- and are turned into names only after the
    /// page has been cut, because no SQL here can split that column.
    /// </summary>
    public static class AdvanceSearchReport
    {
        public static async Task<ApiResult<sp_AdvanceSearchResult>> PageAsync(
            TradeNetDbContext db,
            sp_AdvanceSearchRequest procedureRequest,
            ReportQueryRequest webRequest)
        {
            var query = sp_AdvanceSearch.Query(db, procedureRequest);
            var result = await ReportQueryService.CreatePagedResultAsync(query, webRequest);

            if (result.Data.Count > 0)
            {
                AdvanceSearchCountryNames.Apply(result.Data, await AdvanceSearchCountryNames.LoadAsync(db));
            }

            return result;
        }

        public static async Task StreamAsync(
            TradeNetDbContext db,
            sp_AdvanceSearchRequest procedureRequest,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            var query = sp_AdvanceSearch.Query(db, procedureRequest);

            // The whole country table, once per export rather than once per chunk.
            IReadOnlyDictionary<int, string> countryNames =
                await AdvanceSearchCountryNames.LoadAsync(db, cancellationToken);

            await foreach (var chunk in query.AsAsyncEnumerable().ChunkAsync(chunkSize, cancellationToken))
            {
                AdvanceSearchCountryNames.Apply(chunk, countryNames);
                sink.Append(chunk);
            }
        }
    }
}
