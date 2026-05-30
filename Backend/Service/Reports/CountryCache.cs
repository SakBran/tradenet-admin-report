using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.DBContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace API.Service.Reports;

/// <summary>
/// Application-wide, in-memory list of all countries, loaded lazily on first use and refreshed
/// on demand once it is older than <see cref="Ttl"/>. The refresh is request-driven (triggered by
/// the next report call after the data goes stale) rather than timer-driven, so it does not depend
/// on a long-lived background process surviving an IIS app-pool idle shutdown or recycle. Report
/// APIs resolve comma-separated country id columns to display names from this list after
/// materialization, so the database is never queried (and EF never emits a per-row correlated
/// subquery) for country names.
///
/// Registered as a singleton; the held list is swapped atomically on refresh, so reads are
/// lock-free and always see a complete snapshot.
/// </summary>
public interface ICountryCache
{
    /// <summary>All countries, ordered by id. Empty only before the first load completes.</summary>
    IReadOnlyList<ReportLookupEntry> Countries { get; }

    /// <summary>Resolves a comma-separated country-id column to display names (see <see cref="ReportLookupCache.ResolveCsv"/>).</summary>
    string ResolveCsv(string? csvIds);

    /// <summary>
    /// Loads the list if it has never been populated, or reloads it if the cached copy is older
    /// than <see cref="CountryCache.Ttl"/>; otherwise a no-op. Call this before reading
    /// <see cref="Countries"/> on every request so the cache stays fresh without a background timer.
    /// </summary>
    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);
}

public sealed class CountryCache : ICountryCache
{
    /// <summary>How long a loaded list is considered fresh before the next request triggers a reload.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile IReadOnlyList<ReportLookupEntry> _countries = Array.Empty<ReportLookupEntry>();
    // UTC ticks of the last successful load; 0 means never loaded. Read/written lock-free.
    private long _loadedAtTicks;

    public CountryCache(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public IReadOnlyList<ReportLookupEntry> Countries => _countries;

    public string ResolveCsv(string? csvIds) => ReportLookupCache.ResolveCsv(csvIds, _countries);

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (IsFresh())
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!IsFresh())
            {
                await LoadCoreAsync(cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsFresh()
    {
        var loadedAt = Interlocked.Read(ref _loadedAtTicks);
        return loadedAt != 0
            && _countries.Count > 0
            && DateTime.UtcNow.Ticks - loadedAt < Ttl.Ticks;
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradeNetDbContext>();

        _countries = await db.Countries
            .AsNoTracking()
            .OrderBy(country => country.Id)
            .Select(country => new ReportLookupEntry
            {
                Id = country.Id,
                Name = country.Name ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        Interlocked.Exchange(ref _loadedAtTicks, DateTime.UtcNow.Ticks);
    }
}
