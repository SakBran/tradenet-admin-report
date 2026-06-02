using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using API.DBContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace API.Service.ExcelExport.Handlers
{
    /// <summary>
    /// Handler for the common "report = a single IQueryable&lt;TResult&gt;" shape.
    /// Streams the query with EF's server-side cursor, batched into chunks, so the
    /// full result set never materializes in memory. Covers the reports whose
    /// controllers today call <c>ExcelGenerator.CreateWorkbookAsync(query, ...)</c>.
    /// </summary>
    public sealed class QueryableExcelReportJobHandler<TRequest, TResult> : IExcelReportJobHandler
        where TRequest : class
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly Func<TradeNetDbContext, TRequest, IQueryable<TResult>> _queryFactory;

        public QueryableExcelReportJobHandler(
            string reportKey,
            string title,
            string fileNameBase,
            Func<TradeNetDbContext, TRequest, IQueryable<TResult>> queryFactory)
        {
            ReportKey = reportKey;
            DefaultTitle = title;
            FileNameBase = fileNameBase;
            _queryFactory = queryFactory;
        }

        public string ReportKey { get; }
        public string DefaultTitle { get; }
        public string FileNameBase { get; }

        public async Task GenerateAsync(ExcelExportContext context)
        {
            var request = JsonSerializer.Deserialize<TRequest>(context.RequestJson, JsonOptions)
                ?? throw new InvalidOperationException($"Could not deserialize request for '{ReportKey}'.");

            var db = context.Services.GetRequiredService<TradeNetDbContext>();
            // The converters already project to non-entity result types (no tracking),
            // so the query streams read-only rows. Don't re-apply AsNoTracking — it is
            // invalid on the projected/grouped shapes some converters return.
            var query = _queryFactory(db, request);

            await foreach (var chunk in query.AsAsyncEnumerable().ChunkAsync(context.ChunkSize, context.CancellationToken))
            {
                context.Writer.AppendRows(chunk);
            }
        }
    }
}
