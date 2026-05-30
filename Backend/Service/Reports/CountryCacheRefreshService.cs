using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Service.Reports;

/// <summary>
/// Populates <see cref="ICountryCache"/> once at application startup and then reloads it every
/// hour, regardless of request traffic. A refresh failure is logged and retried on the next tick;
/// the previously loaded list keeps serving in the meantime.
/// </summary>
public sealed class CountryCacheRefreshService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(1);

    private readonly ICountryCache _countryCache;
    private readonly ILogger<CountryCacheRefreshService> _logger;

    public CountryCacheRefreshService(ICountryCache countryCache, ILogger<CountryCacheRefreshService> logger)
    {
        _countryCache = countryCache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        try
        {
            do
            {
                await RefreshOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Application is shutting down.
        }
    }

    private async Task RefreshOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _countryCache.RefreshAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh the country cache; keeping the previously loaded list.");
        }
    }
}
