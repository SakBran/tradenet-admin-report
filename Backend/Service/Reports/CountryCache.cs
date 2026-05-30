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
/// Application-wide, in-memory list of all countries, loaded once at startup and refreshed
/// every hour by <see cref="CountryCacheRefreshService"/>. Report APIs resolve comma-separated
/// country id columns to display names from this list after materialization, so the database is
/// never queried (and EF never emits a per-row correlated subquery) for country names.
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

    /// <summary>Loads the list once if it has never been populated; a no-op once loaded.</summary>
    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);

    /// <summary>Reloads the list from the database (used by the hourly background refresh).</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}

public sealed class CountryCache : ICountryCache
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile IReadOnlyList<ReportLookupEntry> _countries = Array.Empty<ReportLookupEntry>();

    public CountryCache(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public IReadOnlyList<ReportLookupEntry> Countries => _countries;

    public string ResolveCsv(string? csvIds) => ReportLookupCache.ResolveCsv(csvIds, _countries);

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_countries.Count > 0)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_countries.Count == 0)
            {
                await LoadCoreAsync(cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
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
    }
}
