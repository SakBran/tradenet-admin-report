using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Service.Reports
{
    public sealed class DataImportScheduleWorker : BackgroundService
    {
        private static readonly TimeSpan RunTime = new(1, 0, 0);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DataImportScheduleWorker> _logger;

        public DataImportScheduleWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<DataImportScheduleWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextRun = now.Date.Add(RunTime);
                if (nextRun <= now)
                {
                    nextRun = nextRun.AddDays(1);
                }

                var delay = nextRun - now;
                _logger.LogInformation(
                    "Data import schedule next run: {NextRun}. It will import data for {ImportDate}.",
                    nextRun,
                    nextRun.Date.AddDays(-1));

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                var importDate = DateTime.Today.AddDays(-1);
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dataImportService = scope.ServiceProvider.GetRequiredService<IDataImportService>();
                    var result = await dataImportService.ImportAsync(
                        DataImportService.All,
                        importDate,
                        importDate,
                        stoppingToken);

                    _logger.LogInformation(
                        "Data import schedule completed for {ImportDate}. Saved {RowCount} rows.",
                        importDate,
                        result.Rows.Count);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Data import schedule failed for {ImportDate}.", importDate);
                }
            }
        }
    }
}
