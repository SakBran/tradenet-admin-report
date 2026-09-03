using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using API.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// Generic handler backing every report. It instantiates the report's
    /// controller (which implements <see cref="IStreamingExcelReport"/>) through DI,
    /// so the controller's own request mapping and converter calls are reused — the
    /// background worker never duplicates report logic. Rows stream to disk via
    /// <see cref="StreamingExcelWriter"/>.
    ///
    /// The sheet's shape comes from the presentation spec the frontend posted (what the
    /// grid actually showed), unless the controller declares its own typed layout. A job
    /// queued without either is rejected rather than exported as a reflection dump.
    /// </summary>
    public sealed class ControllerStreamingExcelReportJobHandler : IExcelReportJobHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly Type _controllerType;
        private readonly Lazy<Type?> _rowType;

        public ControllerStreamingExcelReportJobHandler(
            Type controllerType,
            string reportKey,
            string defaultTitle,
            string fileNameBase,
            int formatVersion = 1)
        {
            _controllerType = controllerType;
            ReportKey = reportKey;
            DefaultTitle = defaultTitle;
            FileNameBase = fileNameBase;
            FormatVersion = formatVersion;
            _rowType = new Lazy<Type?>(() => ExcelRowTypeResolver.Resolve(controllerType));
        }

        public string ReportKey { get; }
        public string DefaultTitle { get; }
        public string FileNameBase { get; }
        public int FormatVersion { get; }

        /// <summary>The report controller this handler drives.</summary>
        public Type ControllerType => _controllerType;

        /// <summary>True when the controller declares its own columns and needs no spec.</summary>
        public bool HasTypedLayout => typeof(IExcelReportLayoutProvider).IsAssignableFrom(_controllerType);

        /// <summary>
        /// The row type the grid renders, resolved once, lazily (reflection over the
        /// controller's Post signature). Null when it cannot be determined.
        /// </summary>
        public Type? RowType => _rowType.Value;

        public async Task GenerateAsync(ExcelExportContext context)
        {
            var loggerFactory = context.Services.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger<ControllerStreamingExcelReportJobHandler>();

            var report = (IStreamingExcelReport)ActivatorUtilities.CreateInstance(context.Services, _controllerType);

            var request = JsonSerializer.Deserialize(context.RequestJson, report.ExcelRequestType, JsonOptions)
                ?? throw new InvalidOperationException($"Could not deserialize request for '{ReportKey}'.");

            var spec = (request as ReportQueryRequest)?.Excel;

            // Precedence: the controller's typed layout, else the grid's posted spec.
            // Never the old reflection dump — that is what shipped C# property names as
            // column headers.
            ExcelReportLayout layout;
            if (report is IExcelReportLayoutProvider layoutProvider)
            {
                layout = layoutProvider.GetExcelLayout(request);
            }
            else if (spec != null)
            {
                layout = ExcelLayoutBuilder.Build(spec, RowType, logger);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Export '{ReportKey}' was queued without an Excel presentation spec. " +
                    "Refresh the page and export again.");
            }

            var timeProvider = context.Services.GetService<TimeProvider>() ?? TimeProvider.System;
            layout = ExcelLayoutBuilder.WithStandardHeaderBlock(
                layout, spec, DefaultTitle, request, timeProvider.GetLocalNow());

            // Resolved BEFORE streaming: a failure must fail the job instead of leaving a
            // finished-looking file with no footer, and the numbers must describe the same
            // snapshot the rows come from.
            var totals = await ResolveFooterTotalsAsync(context, report, request);

            using var writer = new StreamingExcelWriter(context.Output, report.ExcelWorksheetTitle, layout);
            var sink = new StreamingExcelWriterSink(writer);
            var guarded = RowType == null || report is IExcelReportLayoutProvider
                ? (IExcelRowSink)sink
                : new RowTypeAssertingSink(sink, RowType, ReportKey);

            await report.WriteRowsAsync(request, guarded, context.ChunkSize, context.CancellationToken);

            writer.AppendFooterRows(ExcelFooterBuilder.Build(layout, totals, writer.TotalDataRows));
            writer.Finish();
            context.RowCount = (int)Math.Min(writer.TotalDataRows, int.MaxValue);
            context.SheetCount = writer.SheetCount;
        }

        private async Task<ReportFooterTotals?> ResolveFooterTotalsAsync(
            ExcelExportContext context,
            IStreamingExcelReport report,
            object request)
        {
            var resolver = context.Services.GetService<IExcelFooterTotalsResolver>();
            if (resolver == null)
            {
                return null;
            }

            return await resolver.ResolveAsync(report, _controllerType, request, context.CancellationToken);
        }

        /// <summary>
        /// In generic (spec-built) mode the columns are bound against the row type the
        /// controller's Post advertises. If WriteRowsAsync appends something else every
        /// cell would silently come out blank, so say so loudly instead.
        /// </summary>
        private sealed class RowTypeAssertingSink : IExcelRowSink
        {
            private readonly IExcelRowSink _inner;
            private readonly Type _expectedRowType;
            private readonly string _reportKey;
            private bool _checked;

            public RowTypeAssertingSink(IExcelRowSink inner, Type expectedRowType, string reportKey)
            {
                _inner = inner;
                _expectedRowType = expectedRowType;
                _reportKey = reportKey;
            }

            public void Append<T>(IReadOnlyList<T> rows)
            {
                if (!_checked && rows.Count > 0)
                {
                    foreach (var row in rows)
                    {
                        if (row == null)
                        {
                            continue;
                        }

                        _checked = true;
                        if (!_expectedRowType.IsInstanceOfType(row))
                        {
                            throw new InvalidOperationException(
                                $"Excel export '{_reportKey}': the exported columns were bound to " +
                                $"{_expectedRowType.Name} (the type this report's Post returns) but WriteRowsAsync " +
                                $"appended {row.GetType().Name}. Both must stream the same row type or the sheet " +
                                "comes out blank.");
                        }

                        break;
                    }
                }

                _inner.Append(rows);
            }

            public void BeginSection(int sectionIndex) => _inner.BeginSection(sectionIndex);

            public void AppendNote(string text) => _inner.AppendNote(text);
        }
    }
}
