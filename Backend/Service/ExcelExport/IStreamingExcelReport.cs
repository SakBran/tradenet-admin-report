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

    /// <summary>
    /// Opt-in companion to <see cref="IStreamingExcelReport"/>. A report that implements
    /// this controls its own Excel title line(s) and its exact column list — header text,
    /// order and cell formats — instead of the reflected property names. Reports that
    /// don't implement it are completely unaffected.
    ///
    /// Mark the implementation <c>[NonAction]</c> on the controller, exactly like
    /// <see cref="IStreamingExcelReport.WriteRowsAsync"/>, or MVC's ApiController
    /// convention rejects it at startup as an unrouted action.
    /// </summary>
    public interface IExcelReportLayoutProvider
    {
        /// <param name="request">
        /// The deserialized request DTO (<see cref="IStreamingExcelReport.ExcelRequestType"/>),
        /// so the title can quote the report's own date range.
        /// </param>
        ExcelReportLayout GetExcelLayout(object request);
    }

    /// <summary>
    /// Bumps the export cache key for one report. <see cref="ExcelExportJobService"/>
    /// reuses an already-generated file for a closed date range whenever the request
    /// hashes the same, so a change to the generated file's SHAPE (title text, column
    /// set, header wording) must bump this or users keep receiving the old file until
    /// it expires. Unversioned reports are version 1 and keep their existing hashes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ExcelFormatVersionAttribute : Attribute
    {
        public ExcelFormatVersionAttribute(int version) => Version = version;

        public int Version { get; }
    }

    internal sealed class StreamingExcelWriterSink : IExcelRowSink
    {
        private readonly StreamingExcelWriter _writer;

        public StreamingExcelWriterSink(StreamingExcelWriter writer) => _writer = writer;

        public void Append<T>(IReadOnlyList<T> rows) => _writer.AppendRows(rows);
    }
}
