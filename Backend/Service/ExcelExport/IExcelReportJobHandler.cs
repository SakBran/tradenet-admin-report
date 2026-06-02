using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// What a handler needs to generate one export: the request JSON, scoped
    /// services, the output stream (a file on disk), the chunk size, and a token.
    /// The handler sets <see cref="RowCount"/>/<see cref="SheetCount"/> when known.
    /// </summary>
    public sealed class ExcelExportContext
    {
        public ExcelExportContext(
            IServiceProvider services,
            string requestJson,
            Stream output,
            int chunkSize,
            CancellationToken cancellationToken)
        {
            Services = services;
            RequestJson = requestJson;
            Output = output;
            ChunkSize = chunkSize;
            CancellationToken = cancellationToken;
        }

        public IServiceProvider Services { get; }
        public string RequestJson { get; }
        public Stream Output { get; }
        public int ChunkSize { get; }
        public CancellationToken CancellationToken { get; }

        public int? RowCount { get; set; }
        public int? SheetCount { get; set; }
    }

    /// <summary>
    /// One per report. Writes the complete .xlsx for a job to
    /// <see cref="ExcelExportContext.Output"/>.
    /// </summary>
    public interface IExcelReportJobHandler
    {
        /// <summary>Registry key = controller route name without "Controller".</summary>
        string ReportKey { get; }

        /// <summary>Display title shown in the shared Exports drive.</summary>
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

        public IReadOnlyCollection<string> Keys => _handlers.Keys;
    }
}
