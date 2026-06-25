using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using API.DBContext;
using API.Model.Activity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Service.Activity
{
    /// <summary>
    /// Single consumer that drains the activity-log queue and bulk-inserts entries into
    /// TemplateDB in batches. Runs off the request path so capturing activity never adds
    /// latency to a user's (already slow) report request. A DB hiccup drops the current
    /// batch and keeps going — it never crashes the host or stalls the queue.
    /// </summary>
    public sealed class ActivityLogWriterWorker : BackgroundService
    {
        private readonly IActivityLogQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ActivityLogOptions _options;
        private readonly ILogger<ActivityLogWriterWorker> _logger;

        public ActivityLogWriterWorker(
            IActivityLogQueue queue,
            IServiceScopeFactory scopeFactory,
            IOptions<ActivityLogOptions> options,
            ILogger<ActivityLogWriterWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var reader = _queue.Reader;
            var batchSize = Math.Max(1, _options.BatchSize);
            var batch = new List<ActivityLog>(batchSize);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!await reader.WaitToReadAsync(stoppingToken))
                    {
                        break;
                    }

                    while (batch.Count < batchSize && reader.TryRead(out var entry))
                    {
                        batch.Add(entry);
                    }

                    if (batch.Count > 0)
                    {
                        await FlushAsync(batch, stoppingToken);
                        batch.Clear();
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Activity log writer failed to flush a batch of {Count} entries.", batch.Count);
                    // Drop the failed batch so a single bad row can't wedge the writer forever.
                    batch.Clear();
                    try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        private async Task FlushAsync(List<ActivityLog> batch, CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ActivityLogs.AddRange(batch);
            await db.SaveChangesAsync(stoppingToken);
        }
    }
}
