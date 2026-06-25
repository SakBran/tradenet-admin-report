using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.DBContext;
using API.Model.ExcelExport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// Polls TemplateDB for queued export jobs, claims one with a DB-atomic lease,
    /// and generates the file with chunked streaming. Runs MaxConcurrency parallel
    /// loops. Crashed/abandoned jobs are reclaimed once their lease expires.
    /// </summary>
    public sealed class ExcelExportWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ExcelExportOptions _options;
        private readonly ILogger<ExcelExportWorker> _logger;
        private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

        public ExcelExportWorker(
            IServiceScopeFactory scopeFactory,
            IOptions<ExcelExportOptions> options,
            ILogger<ExcelExportWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var loops = Math.Max(1, _options.MaxConcurrency);
            var tasks = Enumerable.Range(0, loops).Select(_ => RunLoopAsync(stoppingToken));
            return Task.WhenAll(tasks);
        }

        private async Task RunLoopAsync(CancellationToken stoppingToken)
        {
            var idleDelay = TimeSpan.FromSeconds(Math.Max(1, _options.PollSeconds));

            while (!stoppingToken.IsCancellationRequested)
            {
                bool processedOne;
                try
                {
                    processedOne = await TryProcessOneAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Excel export worker loop error.");
                    processedOne = false;
                }

                if (!processedOne)
                {
                    try
                    {
                        await Task.Delay(idleDelay, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>Claims and processes at most one job. Returns false when the queue is empty.</summary>
        private async Task<bool> TryProcessOneAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = DateTime.UtcNow;

            // Candidate: a Queued job, or a Processing job whose lease has expired (orphan).
            var candidate = await db.ExcelExportJobs
                .Where(j => j.Status == ExcelExportJobStatus.Queued
                    || (j.Status == ExcelExportJobStatus.Processing && j.LeaseExpiresAtUtc != null && j.LeaseExpiresAtUtc < now))
                .OrderBy(j => j.CreatedAtUtc)
                .Select(j => j.Id)
                .FirstOrDefaultAsync(stoppingToken);

            if (candidate == Guid.Empty)
            {
                return false;
            }

            var leaseExpiry = now.AddMinutes(Math.Max(1, _options.LeaseMinutes));

            // Atomic claim: only one worker wins the UPDATE guarded by the same status/lease condition.
            var claimed = await db.ExcelExportJobs
                .Where(j => j.Id == candidate
                    && (j.Status == ExcelExportJobStatus.Queued
                        || (j.Status == ExcelExportJobStatus.Processing && j.LeaseExpiresAtUtc != null && j.LeaseExpiresAtUtc < now)))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, ExcelExportJobStatus.Processing)
                    .SetProperty(j => j.LeaseOwner, _workerId)
                    .SetProperty(j => j.LeaseExpiresAtUtc, leaseExpiry)
                    .SetProperty(j => j.StartedAtUtc, now)
                    .SetProperty(j => j.AttemptCount, j => j.AttemptCount + 1),
                    stoppingToken);

            if (claimed == 0)
            {
                // Another worker grabbed it first.
                return true;
            }

            var job = await db.ExcelExportJobs.FirstOrDefaultAsync(j => j.Id == candidate, stoppingToken);
            if (job == null)
            {
                return true;
            }

            await GenerateAsync(scope.ServiceProvider, db, job, stoppingToken);
            return true;
        }

        private async Task GenerateAsync(
            IServiceProvider services,
            ApplicationDbContext db,
            ExcelExportJob job,
            CancellationToken stoppingToken)
        {
            var registry = services.GetRequiredService<ExcelReportJobRegistry>();
            IExcelExportFileStore? fileStore = null;
            string? relativePath = null;

            try
            {
                // Resolve the file store and target path inside the try so any storage error
                // (bad/non-writable path, permission denied) becomes a clean per-job failure
                // with a readable ErrorMessage, never a silent loop stuck in "Processing".
                fileStore = services.GetRequiredService<IExcelExportFileStore>();
                relativePath = fileStore.BuildRelativePath(job.FileName);

                if (!registry.TryGet(job.ReportKey, out var handler))
                {
                    throw new InvalidOperationException($"No handler for report key '{job.ReportKey}'.");
                }

                int sheetCount;
                long rowCount;
                using (var fileStream = fileStore.OpenWrite(relativePath))
                {
                    var context = new ExcelExportContext(services, job.RequestJson, fileStream, _options.ChunkSize, stoppingToken);
                    await handler.GenerateAsync(context);
                    sheetCount = context.SheetCount ?? 0;
                    rowCount = context.RowCount ?? 0;

                    // Publish now that generation succeeded. Staged backends (FTP) upload here;
                    // the local-disk backend writes in place and doesn't implement ICommittableStream.
                    (fileStream as ICommittableStream)?.Commit();
                }

                var size = fileStore.GetSize(relativePath);
                var completedAt = DateTime.UtcNow;

                await db.ExcelExportJobs
                    .Where(j => j.Id == job.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(j => j.Status, ExcelExportJobStatus.Completed)
                        .SetProperty(j => j.FilePath, relativePath)
                        .SetProperty(j => j.FileSizeBytes, size)
                        .SetProperty(j => j.RowCount, (int)Math.Min(rowCount, int.MaxValue))
                        .SetProperty(j => j.SheetCount, sheetCount)
                        .SetProperty(j => j.CompletedAtUtc, completedAt)
                        .SetProperty(j => j.LeaseOwner, (string?)null)
                        .SetProperty(j => j.LeaseExpiresAtUtc, (DateTime?)null)
                        .SetProperty(j => j.ErrorMessage, (string?)null),
                        stoppingToken);

                _logger.LogInformation(
                    "Excel export {JobId} ({ReportKey}) completed: {Rows} rows, {Sheets} sheet(s), {Bytes} bytes.",
                    job.Id, job.ReportKey, rowCount, sheetCount, size);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                // Clean up any partial file. fileStore may be null if resolution itself failed.
                try { fileStore?.Delete(relativePath); } catch { /* best effort */ }

                var willRetry = job.AttemptCount < _options.MaxAttempts;
                var status = willRetry ? ExcelExportJobStatus.Queued : ExcelExportJobStatus.Failed;

                await db.ExcelExportJobs
                    .Where(j => j.Id == job.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(j => j.Status, status)
                        .SetProperty(j => j.ErrorMessage, Truncate(ex.Message, 1000))
                        .SetProperty(j => j.LeaseOwner, (string?)null)
                        .SetProperty(j => j.LeaseExpiresAtUtc, (DateTime?)null),
                        CancellationToken.None);

                _logger.LogError(ex,
                    "Excel export {JobId} ({ReportKey}) failed on attempt {Attempt}. Will retry: {Retry}.",
                    job.Id, job.ReportKey, job.AttemptCount, willRetry);
            }
        }

        private static string Truncate(string value, int max)
            => value.Length <= max ? value : value[..max];
    }
}
