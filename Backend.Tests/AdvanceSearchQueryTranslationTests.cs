using API.DBContext;
using API.StoredProcedureToLinq;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests;

/// <summary>
/// Proves every Advance Search branch translates to SQL.
///
/// No database is touched: <c>ToQueryString()</c> runs the whole EF Core translation pipeline
/// against the model and returns the command text without opening a connection. That matters
/// here because the report database is unreachable from a dev machine, so an untranslatable
/// expression — the real risk when porting EF6 LINQ to EF Core — would otherwise first show up
/// as a 500 in front of a customer.
///
/// The PaThaKa No filter is left out on purpose: resolving it needs a round trip.
/// </summary>
public sealed class AdvanceSearchQueryTranslationTests
{
    private static TradeNetDbContext Context() =>
        new(new DbContextOptionsBuilder<TradeNetDbContext>()
            // Never connected to; the provider only has to build the model and the SQL.
            .UseSqlServer("Server=localhost;Database=translation-only;Trusted_Connection=False")
            .Options);

    public static TheoryData<string> EveryType()
    {
        var data = new TheoryData<string>();
        foreach (var type in sp_AdvanceSearch.AdvanceSearchType.All)
        {
            data.Add(type);
        }

        return data;
    }

    /// <summary>Every box filled in, so no predicate is skipped.</summary>
    private static sp_AdvanceSearchRequest EveryFilter(string type) => new()
    {
        TypeOfLicenceOrPermit = type,
        StartDate = new DateTime(2025, 1, 1),
        EndDate = new DateTime(2025, 12, 31, 23, 59, 59),
        Section = "3",
        SellerCountry = 12,
        PortOfDischarge = "Yangon",
        ModeOfTransport = "Sea,Road,Air",
        MethodOfImportExport = 4,
        CountryOfOrigin = "5",
        ConsignedCountry = "5",
        Description = "rice",
        Incoterm = 2,
        StatementCode = 7,
        ApplyType = "New",
    };

    [Theory]
    [MemberData(nameof(EveryType))]
    public void Every_type_translates_with_no_filters(string type)
    {
        using var db = Context();

        var sql = sp_AdvanceSearch
            .Query(db, new sp_AdvanceSearchRequest
            {
                TypeOfLicenceOrPermit = type,
                StartDate = new DateTime(2025, 1, 1),
                EndDate = new DateTime(2025, 12, 31),
            })
            .ToQueryString();

        Assert.Contains("SELECT", sql, StringComparison.Ordinal);
        // The date range binds IssuedDate, never LicenceDate.
        Assert.Contains("IssuedDate", sql, StringComparison.Ordinal);
        // Deterministic paging: Description first, then the item key.
        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(EveryType))]
    public void Every_type_translates_with_every_filter_applied(string type)
    {
        using var db = Context();

        var sql = sp_AdvanceSearch.Query(db, EveryFilter(type)).ToQueryString();

        Assert.Contains("SELECT", sql, StringComparison.Ordinal);
        // The multi-mode selection becomes an IN over the permutations of the chosen modes.
        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(EveryType))]
    public void Every_type_joins_its_item_table_so_the_grain_is_one_row_per_item(string type)
    {
        using var db = Context();

        var sql = sp_AdvanceSearch.Query(db, EveryFilter(type)).ToQueryString();

        Assert.Contains("Item", sql, StringComparison.Ordinal);
        // Inner joins throughout, as the legacy query had them: a licence with no item lines,
        // or an unresolvable unit/currency/PaThaKa/Sakhan, must not appear at all.
        Assert.DoesNotContain("LEFT JOIN", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Border Import Licence")]
    [InlineData("Border Export Licence")]
    [InlineData("Border Import Permit")]
    [InlineData("Border Export Permit")]
    public void The_Border_types_join_Sakhan_for_the_extra_column(string type)
    {
        using var db = Context();

        var sql = sp_AdvanceSearch.Query(db, EveryFilter(type)).ToQueryString();

        Assert.Contains("Sakhan", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Import Licence")]
    [InlineData("Export Licence")]
    [InlineData("Import Permit")]
    [InlineData("Export Permit")]
    public void The_oversea_types_do_not_join_Sakhan(string type)
    {
        using var db = Context();

        var sql = sp_AdvanceSearch.Query(db, EveryFilter(type)).ToQueryString();

        Assert.DoesNotContain("Sakhan", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_type_returns_nothing_rather_than_throwing()
    {
        using var db = Context();

        // The legacy repository had no else branch: the query stayed null and the pager threw.
        Assert.Empty(sp_AdvanceSearch.Query(db, new sp_AdvanceSearchRequest
        {
            TypeOfLicenceOrPermit = "Nonsense",
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 12, 31),
        }));
    }
}
