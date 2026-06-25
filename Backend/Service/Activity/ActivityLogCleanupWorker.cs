using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.DBContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Service.Activity
{
    /// <summary>
    /// Periodically purges activity-log rows older than the retention window so the
    /// table doesn't grow unbounded. Mirrors <see cref="API.Service.ExcelExport.ExcelExportCleanupWorker"/>.
    /// </summary>
    public sealed class ActivityLogCleanupWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ActivityLogOptions _options;
        private readonly ILogger<ActivityLogCleanupWorker> _logger;

        public ActivityLogCleanupWorker(
            IServiceScopeFactory scopeFactory,
            IOptions<ActivityLogOptions> options,
            ILogger<ActivityLogCleanupWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromMinutes(Math.Max(1, _options.CleanupIntervalMinutes));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SweepAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Activity log cleanup sweep failed.");
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task SweepAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, _options.RetentionDays));
            var removed = await db.ActivityLogs
                .Where(a => a.TimestampUtc < cutoff)
                .ExecuteDeleteAsync(stoppingToken);

            if (removed > 0)
            {
                _logger.LogInformation("Activity log cleanup removed {Count} expired entr(ies) older than {Cutoff:u}.", removed, cutoff);
            }
        }
    }
}
