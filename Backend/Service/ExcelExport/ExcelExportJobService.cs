using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using API.DBContext;
using API.Model;
using API.Model.ExcelExport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.Service.ExcelExport
{
    public sealed class ExcelExportJobService : IExcelExportJobService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly ApplicationDbContext _db;
        private readonly ExcelReportJobRegistry _registry;
        private readonly IExcelExportFileStore _fileStore;
        private readonly ExcelExportOptions _options;

        public ExcelExportJobService(
            ApplicationDbContext db,
            ExcelReportJobRegistry registry,
            IExcelExportFileStore fileStore,
            IOptions<ExcelExportOptions> options)
        {
            _db = db;
            _registry = registry;
            _fileStore = fileStore;
            _options = options.Value;
        }

        public async Task<EnqueueResult> EnqueueAsync(
            string reportKey,
            object request,
            DateTime toDate,
            string? requestedByUserName)
        {
            if (!_registry.TryGet(reportKey, out var handler))
            {
                throw new InvalidOperationException($"No Excel export handler registered for report key '{reportKey}'.");
            }

            // The presentation spec is part of the request payload, so it is hashed with
            // it: a grid whose columns changed cannot be served a cached file of the old
            // shape. It is also validated here defensively — the action filter is the
            // primary gate, but nothing may reach the worker unsanitized.
            var spec = (request as ReportQueryRequest)?.Excel;
            if (spec != null)
            {
                var typedLayout = handler is ControllerStreamingExcelReportJobHandler { HasTypedLayout: true };
                ExcelPresentationSpecValidator.ValidateAndSanitize(spec, requireColumns: !typedLayout);

                // A spec from another report would name the job and the download after
                // that report while exporting this one's rows (Contract §3).
                if (!string.Equals(spec.ControllerName, reportKey, StringComparison.Ordinal))
                {
                    throw new ExcelPresentationSpecException(new[]
                    {
                        $"excel.controllerName: '{spec.ControllerName}' is not this report ('{reportKey}').",
                    });
                }
            }

            var requestJson = JsonSerializer.Serialize(request, request.GetType(), JsonOptions);
            var filterHash = ExcelExportHasher.ComputeHash(reportKey, requestJson, handler.FormatVersion);
            var now = DateTime.UtcNow;
            var isPeriodClosed = toDate.Date < DateTime.Today;

            // 1) An identical request already queued/processing → tell the user to wait.
            var inFlight = await _db.ExcelExportJobs
                .Where(j => j.FilterHash == filterHash
                    && (j.Status == ExcelExportJobStatus.Queued || j.Status == ExcelExportJobStatus.Processing))
                .OrderByDescending(j => j.CreatedAtUtc)
                .FirstOrDefaultAsync();

            if (inFlight != null)
            {
                return new EnqueueResult
                {
                    Status = EnqueueStatus.Processing,
                    JobId = inFlight.Id,
                    FileName = inFlight.FileName,
                    Message = "This export is already being generated. It will appear in Exports when ready."
                };
            }

            // 2) Closed (historical) period → reuse a finished, on-disk, unexpired file.
            //    Up-to-date ranges (touching today) always regenerate for fresh data.
            if (isPeriodClosed)
            {
                var completed = await _db.ExcelExportJobs
                    .Where(j => j.FilterHash == filterHash
                        && j.Status == ExcelExportJobStatus.Completed
                        && j.ExpiresAtUtc > now)
                    .OrderByDescending(j => j.CompletedAtUtc)
                    .FirstOrDefaultAsync();

                if (completed != null && _fileStore.Exists(completed.FilePath))
                {
                    return new EnqueueResult
                    {
                        Status = EnqueueStatus.Ready,
                        JobId = completed.Id,
                        FileName = completed.FileName,
                        DownloadUrl = DownloadUrl(completed.Id),
                        Message = "Existing export reused."
                    };
                }
            }

            // 3) Queue a new job.
            var id = Guid.NewGuid();
            var job = new ExcelExportJob
            {
                Id = id,
                ReportKey = reportKey,
                ReportTitle = ExcelPresentationSpecValidator.SanitizeTitle(spec?.Title) ?? handler.DefaultTitle,
                FilterHash = filterHash,
                RequestJson = requestJson,
                Status = ExcelExportJobStatus.Queued,
                IsPeriodClosed = isPeriodClosed,
                // InvariantCulture: on a host whose default culture uses a non-Gregorian
                // calendar the stamp would otherwise not be the Gregorian date.
                FileName = (ExcelPresentationSpecValidator.SanitizeFileNameBase(spec?.FileName) ?? handler.FileNameBase)
                    + "_" + now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".xlsx",
                RequestedByUserName = requestedByUserName,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddHours(_options.RetentionHours),
                AttemptCount = 0
            };

            _db.ExcelExportJobs.Add(job);
            await _db.SaveChangesAsync();

            return new EnqueueResult
            {
                Status = EnqueueStatus.Queued,
                JobId = id,
                FileName = job.FileName,
                Message = "Export queued. It will appear in Exports when ready."
            };
        }

        private static string DownloadUrl(Guid id) => $"ExcelExport/{id}/download";
    }
}
