using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// Everything a handler needs to generate one export: the request JSON to
    /// rebuild its query, scoped services (TradeNetDbContext, caches), the chunk
    /// size, the streaming writer to append rows to, and a cancellation token.
    /// </summary>
    public sealed class ExcelExportContext
    {
        public ExcelExportContext(
            IServiceProvider services,
            string requestJson,
            StreamingExcelWriter writer,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            Services = services;
            RequestJson = requestJson;
            Writer = writer;
            ChunkSize = chunkSize;
            CancellationToken = cancellationToken;
        }

        public IServiceProvider Services { get; }
        public string RequestJson { get; }
        public StreamingExcelWriter Writer { get; }
        public int ChunkSize { get; }
        public CancellationToken CancellationToken { get; }
    }

    /// <summary>
    /// One per report. Rebuilds the report's query from the stored request and
    /// streams its rows into <see cref="ExcelExportContext.Writer"/> in chunks.
    /// </summary>
    public interface IExcelReportJobHandler
    {
        /// <summary>Registry key = controller route name without "Controller".</summary>
        string ReportKey { get; }

        /// <summary>Worksheet title.</summary>
        string DefaultTitle { get; }

        /// <summary>Base download file name (no timestamp / extension).</summary>
        string FileNameBase { get; }

        Task GenerateAsync(ExcelExportContext context);
    }

    /// <summary>Lookup of registered handlers by report key.</summary>
    public sealed class ExcelReportJobRegistry
    {
        private readonly Dictionary<string, IExcelReportJobHandler> _handlers;

        public ExcelReportJobRegistry(IEnumerable<IExcelReportJobHandler> handlers)
        {
            _handlers = new Dictionary<string, IExcelReportJobHandler>(StringComparer.OrdinalIgnoreCase);
            foreach (var handler in handlers)
            {
                _handlers[handler.ReportKey] = handler;
            }
        }

        public bool TryGet(string reportKey, out IExcelReportJobHandler handler)
            => _handlers.TryGetValue(reportKey, out handler!);

        public IExcelReportJobHandler Get(string reportKey)
        {
            if (!_handlers.TryGetValue(reportKey, out var handler))
            {
                throw new InvalidOperationException($"No Excel export handler registered for report key '{reportKey}'.");
            }

            return handler;
        }
    }
}
