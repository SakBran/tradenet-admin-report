using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using API.Model;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// Where a report streams its rows. Backed by <see cref="StreamingExcelWriter"/>
    /// in production; rows are written to disk a chunk at a time. The column set comes
    /// from the export's layout (built from the posted presentation spec, or declared by
    /// the controller); only the legacy no-layout path infers it from the first row.
    /// </summary>
    public interface IExcelRowSink
    {
        void Append<T>(IReadOnlyList<T> rows);

        /// <summary>
        /// Switches to a composite sheet's <paramref name="sectionIndex"/>-th table
        /// (<see cref="ExcelReportLayout.Sections"/>): writes its title and header row and
        /// restarts its row numbering. A no-op for single-table reports.
        /// </summary>
        void BeginSection(int sectionIndex)
        {
        }

        /// <summary>
        /// Writes a trailing single-cell line under the data, e.g. a composite page's
        /// "Total USD Value: 1,234.5678". A no-op for reports that never call it.
        /// </summary>
        void AppendNote(string text)
        {
        }
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
    /// this controls its own Excel column list — header text, order and cell formats —
    /// instead of having them built from the posted presentation spec. The standard
    /// header block (title, From/To, Exported) is still added on top for every report.
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
    /// Opt-out of the default footer-totals probe. Implement it when replaying the
    /// report's <c>Post</c> would be wasteful or wrong (page-dependent totals, or a
    /// probe path that times out) and compute the same numbers directly.
    ///
    /// Mark the implementation <c>[NonAction]</c> on the controller.
    /// </summary>
    public interface IExcelFooterTotalsProvider
    {
        Task<ReportFooterTotals?> GetExcelFooterTotalsAsync(object request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Declares the row type a report appends when it cannot be inferred from the bare
    /// <c>Post</c> action's <c>ActionResult&lt;ApiResult&lt;T&gt;&gt;</c>.
    /// </summary>
    public interface IExcelRowTypeProvider
    {
        Type ExcelRowType { get; }
    }

    /// <summary>
    /// The grid's footer numbers for one export: the same shapes the JSON response
    /// carries (<see cref="IReportTotals"/>), snapshotted before streaming starts.
    /// </summary>
    public sealed record ReportFooterTotals(
        IReadOnlyDictionary<string, decimal>? ColumnTotals,
        ReportCurrencyTotalsSummary? CurrencyTotals)
    {
        public bool IsEmpty => (ColumnTotals == null || ColumnTotals.Count == 0)
            && (CurrencyTotals?.Currencies == null || CurrencyTotals.Currencies.Count == 0);
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

    /// <summary>
    /// The generation of the SHARED sheet shape (header block, footer rows, freeze
    /// pane). Added to every report's <see cref="ExcelFormatVersionAttribute"/> when the
    /// handler is registered, so one bump here invalidates every cached export at once.
    /// Bump it whenever the shared shape changes again.
    /// </summary>
    public static class ExcelExportFormat
    {
        public const int Generation = 1;
    }

    internal sealed class StreamingExcelWriterSink : IExcelRowSink
    {
        private readonly StreamingExcelWriter _writer;

        public StreamingExcelWriterSink(StreamingExcelWriter writer) => _writer = writer;

        public void Append<T>(IReadOnlyList<T> rows) => _writer.AppendRows(rows);

        public void BeginSection(int sectionIndex) => _writer.BeginSection(sectionIndex);

        public void AppendNote(string text) => _writer.AppendNote(text);
    }
}
