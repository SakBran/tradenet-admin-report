using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.DBContext;
using API.Model.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Service.Reports
{
    public sealed class DataImportWorker : BackgroundService
    {
        private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(30);
        private const int MaxAttempts = 3;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DataImportWorker> _logger;
        private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

        public DataImportWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<DataImportWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
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
                    _logger.LogError(ex, "Data import worker loop error.");
                    processedOne = false;
                }

                if (!processedOne)
                {
                    try
                    {
                        await Task.Delay(IdleDelay, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task<bool> TryProcessOneAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;

            if (!await JobTableExistsAsync(db, stoppingToken))
            {
                return false;
            }

            var candidate = await db.DataImportJobs
                .Where(j => j.Status == DataImportJobStatus.Queued
                    || (j.Status == DataImportJobStatus.Processing
                        && j.LeaseExpiresAtUtc != null
                        && j.LeaseExpiresAtUtc < now))
                .OrderBy(j => j.CreatedAtUtc)
                .Select(j => j.Id)
                .FirstOrDefaultAsync(stoppingToken);

            if (candidate == Guid.Empty)
            {
                return false;
            }

            var leaseExpiry = now.Add(LeaseDuration);
            var claimed = await db.DataImportJobs
                .Where(j => j.Id == candidate
                    && (j.Status == DataImportJobStatus.Queued
                        || (j.Status == DataImportJobStatus.Processing
                            && j.LeaseExpiresAtUtc != null
                            && j.LeaseExpiresAtUtc < now)))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, DataImportJobStatus.Processing)
                    .SetProperty(j => j.LeaseOwner, _workerId)
                    .SetProperty(j => j.LeaseExpiresAtUtc, leaseExpiry)
                    .SetProperty(j => j.StartedAtUtc, j => j.StartedAtUtc ?? now)
                    .SetProperty(j => j.AttemptCount, j => j.AttemptCount + 1)
                    .SetProperty(j => j.ErrorMessage, (string?)null),
                    stoppingToken);

            if (claimed == 0)
            {
                return true;
            }

            var job = await db.DataImportJobs.FirstOrDefaultAsync(j => j.Id == candidate, stoppingToken);
            if (job == null)
            {
                return true;
            }

            await ProcessAsync(scope.ServiceProvider, db, job, stoppingToken);
            return true;
        }

        private async Task ProcessAsync(
            IServiceProvider services,
            ApplicationDbContext db,
            DataImportJob job,
            CancellationToken stoppingToken)
        {
            try
            {
                var dataImportService = services.GetRequiredService<IDataImportService>();
                var nextDate = job.StartDate.Date.AddDays(job.ProcessedDays);

                while (nextDate <= job.EndDate.Date)
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    var result = await dataImportService.ImportAsync(
                        job.LicenceType,
                        nextDate,
                        nextDate,
                        stoppingToken);

                    job.ProcessedDays += 1;
                    job.TotalRows += result.Rows.Count;
                    job.LeaseExpiresAtUtc = DateTime.UtcNow.Add(LeaseDuration);

                    await db.DataImportJobs
                        .Where(j => j.Id == job.Id)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(j => j.ProcessedDays, job.ProcessedDays)
                            .SetProperty(j => j.TotalRows, job.TotalRows)
                            .SetProperty(j => j.LeaseExpiresAtUtc, job.LeaseExpiresAtUtc),
                            stoppingToken);

                    nextDate = nextDate.AddDays(1);
                }

                var completedAt = DateTime.UtcNow;
                await db.DataImportJobs
                    .Where(j => j.Id == job.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(j => j.Status, DataImportJobStatus.Completed)
                        .SetProperty(j => j.CompletedAtUtc, completedAt)
                        .SetProperty(j => j.LeaseOwner, (string?)null)
                        .SetProperty(j => j.LeaseExpiresAtUtc, (DateTime?)null)
                        .SetProperty(j => j.ErrorMessage, (string?)null),
                        stoppingToken);

                _logger.LogInformation(
                    "Data import job {JobId} completed: {ProcessedDays}/{TotalDays} day(s), {Rows} row(s).",
                    job.Id,
                    job.ProcessedDays,
                    job.TotalDays,
                    job.TotalRows);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                var willRetry = job.AttemptCount < MaxAttempts;
                var status = willRetry ? DataImportJobStatus.Queued : DataImportJobStatus.Failed;

                await db.DataImportJobs
                    .Where(j => j.Id == job.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(j => j.Status, status)
                        .SetProperty(j => j.ErrorMessage, Truncate(ex.Message, 1000))
                        .SetProperty(j => j.LeaseOwner, (string?)null)
                        .SetProperty(j => j.LeaseExpiresAtUtc, (DateTime?)null)
                        .SetProperty(j => j.CompletedAtUtc, willRetry ? null : DateTime.UtcNow),
                        CancellationToken.None);

                _logger.LogError(
                    ex,
                    "Data import job {JobId} failed on attempt {Attempt}. Will retry: {Retry}.",
                    job.Id,
                    job.AttemptCount,
                    willRetry);
            }
        }

        private static string Truncate(string value, int max)
            => value.Length <= max ? value : value[..max];

        private static async Task<bool> JobTableExistsAsync(
            ApplicationDbContext db,
            CancellationToken cancellationToken)
        {
            const string sql = "SELECT CASE WHEN OBJECT_ID(N'dbo.DataImportJobs', N'U') IS NULL THEN 0 ELSE 1 END;";
            var connection = db.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) == 1;
        }
    }
}
