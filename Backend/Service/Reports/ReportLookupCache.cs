using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DBContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace API.Service.Reports;

/// <summary>
/// Shared, lazily-loaded one-day cache for small, stable reference lookup tables
/// (Countries, PortOfDischarges, WineTypes). Report page APIs page scalar rows first,
/// then resolve comma-separated id columns to display names from these cached lists
/// after materialization, so EF never emits a per-row correlated subquery for them.
///
/// The cache is NOT joined inside the database query (an in-memory list cannot be
/// translated to SQL); it is used only to resolve display fields after `.ToList()`.
/// Only small reference tables belong here. Do not cache large/transactional tables.
/// </summary>
public static class ReportLookupCache
{
    private const string CountriesKey = "ReportLookups:Countries:AllNames:v1";
    private const string PortsKey = "ReportLookups:PortOfDischarges:AllNames:v1";
    private const string WineTypesKey = "ReportLookups:WineTypes:AllNames:v1";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(1);

    public static Task<IReadOnlyList<ReportLookupEntry>> GetCountryNamesAsync(
        TradeNetDbContext db,
        IMemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);

        return GetAsync(
            cache,
            CountriesKey,
            () => db.Countries
                .AsNoTracking()
                .OrderBy(country => country.Id)
                .Select(country => new ReportLookupEntry
                {
                    Id = country.Id,
                    Name = country.Name ?? string.Empty
                }));
    }

    public static Task<IReadOnlyList<ReportLookupEntry>> GetPortNamesAsync(
        TradeNetDbContext db,
        IMemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);

        return GetAsync(
            cache,
            PortsKey,
            () => db.PortOfDischarges
                .AsNoTracking()
                .OrderBy(port => port.Id)
                .Select(port => new ReportLookupEntry
                {
                    Id = port.Id,
                    Name = port.Name ?? string.Empty
                }));
    }

    public static Task<IReadOnlyList<ReportLookupEntry>> GetWineTypeNamesAsync(
        TradeNetDbContext db,
        IMemoryCache cache)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cache);

        return GetAsync(
            cache,
            WineTypesKey,
            () => db.WineTypes
                .AsNoTracking()
                .OrderBy(wineType => wineType.Id)
                .Select(wineType => new ReportLookupEntry
                {
                    Id = wineType.Id,
                    Name = wineType.Name ?? string.Empty
                }));
    }

    /// <summary>
    /// Resolves a comma-separated id column to display names, reproducing the original
    /// `from x in db.Lookup where csv.Contains(x.Id) select x.Name` semantics: output
    /// ordered by lookup-table id (the cached list is ordered by id), each id resolved
    /// at most once, ids not present in the lookup omitted.
    /// </summary>
    public static string ResolveCsv(string? csvIds, IReadOnlyList<ReportLookupEntry> lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        var ids = ParseIds(csvIds);
        if (ids.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            ",",
            lookup
                .Where(entry => ids.Contains(entry.Id))
                .Select(entry => entry.Name));
    }

    private static async Task<IReadOnlyList<ReportLookupEntry>> GetAsync(
        IMemoryCache cache,
        string key,
        Func<IQueryable<ReportLookupEntry>> query)
    {
        var entries = await cache.GetOrCreateAsync(
            key,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                return (IReadOnlyList<ReportLookupEntry>)await query().ToListAsync();
            });

        return entries ?? Array.Empty<ReportLookupEntry>();
    }

    private static HashSet<int> ParseIds(string? csvIds)
    {
        if (string.IsNullOrWhiteSpace(csvIds))
        {
            return new HashSet<int>();
        }

        return csvIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
    }
}

public sealed class ReportLookupEntry
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
