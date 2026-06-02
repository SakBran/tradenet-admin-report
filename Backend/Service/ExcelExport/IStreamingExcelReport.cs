using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// Where a report streams its rows. Backed by <see cref="StreamingExcelWriter"/>
    /// in production; rows are written to disk a chunk at a time. Column headers are
    /// inferred from the runtime type of the first appended row.
    /// </summary>
    public interface IExcelRowSink
    {
        void Append<T>(IReadOnlyList<T> rows);
    }

    /// <summary>
    /// Implemented by a report controller so the background export worker can
    /// regenerate the report's rows later, reusing the controller's own request
    /// mapping (<c>TryCreateReportRequest</c>) and converter calls — no logic is
    /// duplicated. The worker streams the rows into a file on disk.
    ///
    /// Mark the explicit <see cref="WriteRowsAsync"/> implementation
    /// <c>[NonAction]</c> on the controller so MVC does not treat it as an endpoint.
    /// </summary>
    public interface IStreamingExcelReport
    {
        /// <summary>Worksheet title baked into the generated file.</summary>
        string ExcelWorksheetTitle { get; }

        /// <summary>The controller's request DTO type (used to deserialize the stored request).</summary>
        Type ExcelRequestType { get; }

        /// <summary>
        /// Streams the report's rows into <paramref name="sink"/> in chunks of
        /// <paramref name="chunkSize"/>. <paramref name="request"/> is the deserialized
        /// request DTO (already validated at enqueue time).
        /// </summary>
        Task WriteRowsAsync(
            object request,
            IExcelRowSink sink,
            int chunkSize,
            CancellationToken cancellationToken);
    }

    internal sealed class StreamingExcelWriterSink : IExcelRowSink
    {
        private readonly StreamingExcelWriter _writer;

        public StreamingExcelWriterSink(StreamingExcelWriter writer) => _writer = writer;

        public void Append<T>(IReadOnlyList<T> rows) => _writer.AppendRows(rows);
    }
}
