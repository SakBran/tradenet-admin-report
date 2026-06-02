using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.DBContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// Periodically deletes expired export files and their rows so the persistent
    /// folder doesn't grow unbounded (1-day retention by default).
    /// </summary>
    public sealed class ExcelExportCleanupWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ExcelExportOptions _options;
        private readonly ILogger<ExcelExportCleanupWorker> _logger;

        public ExcelExportCleanupWorker(
            IServiceScopeFactory scopeFactory,
            IOptions<ExcelExportOptions> options,
            ILogger<ExcelExportCleanupWorker> logger)
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
                    _logger.LogError(ex, "Excel export cleanup sweep failed.");
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
            var fileStore = scope.ServiceProvider.GetRequiredService<IExcelExportFileStore>();

            var now = DateTime.UtcNow;
            var expired = await db.ExcelExportJobs
                .Where(j => j.ExpiresAtUtc < now)
                .Select(j => new { j.Id, j.FilePath })
                .ToListAsync(stoppingToken);

            if (expired.Count == 0)
            {
                return;
            }

            foreach (var item in expired)
            {
                try { fileStore.Delete(item.FilePath); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not delete export file {Path}.", item.FilePath); }
            }

            var ids = new HashSet<Guid>();
            foreach (var item in expired)
            {
                ids.Add(item.Id);
            }

            await db.ExcelExportJobs
                .Where(j => ids.Contains(j.Id))
                .ExecuteDeleteAsync(stoppingToken);

            _logger.LogInformation("Excel export cleanup removed {Count} expired export(s).", expired.Count);
        }
    }
}
