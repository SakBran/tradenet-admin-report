using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using API.DBContext;
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

            var requestJson = JsonSerializer.Serialize(request, request.GetType(), JsonOptions);
            var filterHash = ExcelExportHasher.ComputeHash(reportKey, requestJson);
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
                ReportTitle = handler.DefaultTitle,
                FilterHash = filterHash,
                RequestJson = requestJson,
                Status = ExcelExportJobStatus.Queued,
                IsPeriodClosed = isPeriodClosed,
                FileName = $"{handler.FileNameBase}_{now:yyyyMMdd_HHmmss}.xlsx",
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

        private static string DownloadUrl(Guid id) => $"api/ExcelExport/{id}/download";
    }
}
