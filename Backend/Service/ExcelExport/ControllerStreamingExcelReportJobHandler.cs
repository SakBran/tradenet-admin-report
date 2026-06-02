using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// Generic handler backing every report. It instantiates the report's
    /// controller (which implements <see cref="IStreamingExcelReport"/>) through DI,
    /// so the controller's own request mapping and converter calls are reused — the
    /// background worker never duplicates report logic. Rows stream to disk via
    /// <see cref="StreamingExcelWriter"/>.
    /// </summary>
    public sealed class ControllerStreamingExcelReportJobHandler : IExcelReportJobHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly Type _controllerType;

        public ControllerStreamingExcelReportJobHandler(
            Type controllerType,
            string reportKey,
            string defaultTitle,
            string fileNameBase)
        {
            _controllerType = controllerType;
            ReportKey = reportKey;
            DefaultTitle = defaultTitle;
            FileNameBase = fileNameBase;
        }

        public string ReportKey { get; }
        public string DefaultTitle { get; }
        public string FileNameBase { get; }

        public async Task GenerateAsync(ExcelExportContext context)
        {
            var report = (IStreamingExcelReport)ActivatorUtilities.CreateInstance(context.Services, _controllerType);

            var request = JsonSerializer.Deserialize(context.RequestJson, report.ExcelRequestType, JsonOptions)
                ?? throw new InvalidOperationException($"Could not deserialize request for '{ReportKey}'.");

            using var writer = new StreamingExcelWriter(context.Output, report.ExcelWorksheetTitle);
            var sink = new StreamingExcelWriterSink(writer);

            await report.WriteRowsAsync(request, sink, context.ChunkSize, context.CancellationToken);

            writer.Finish();
            context.RowCount = (int)Math.Min(writer.TotalDataRows, int.MaxValue);
            context.SheetCount = writer.SheetCount;
        }
    }
}
