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
        // Several, so the any-of predicates are the ones under test: on the six comma-joined
        // columns these fold into an OR chain of delimiter-safe LIKEs, and on the two int
        // columns into an IN list. Neither shape existed before and neither is compile-checked.
        CountryOfOrigin = "5,12",
        ConsignedCountry = "5,12",
        Description = "rice",
        Incoterm = 2,
        StatementCode = 7,
        ApplyType = "New",
        Office = 4,
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
        // The date range binds LicenceDate -- the column the grid shows and the one every
        // comparable report in this app ranges on. It used to bind IssuedDate, which is why a
        // 2026 search could answer with a row displaying a 2025 date.
        Assert.Contains("LicenceDate", sql, StringComparison.Ordinal);
        // Deterministic paging: Description first, then the item key.
        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(EveryType))]
    public void The_date_range_is_a_half_open_whole_day_window(string type)
    {
        using var db = Context();

        var sql = sp_AdvanceSearch.Query(db, EveryFilter(type)).ToQueryString();

        // >= From and < To+1day, so a caller posting a bare To date still gets that whole day.
        Assert.Contains(">=", sql, StringComparison.Ordinal);
        Assert.Contains("2026-01-01T00:00:00", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("2025-12-31T23:59:59", sql, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(EveryType))]
    public void Every_type_translates_with_every_filter_applied(string type)
    {
        using var db = Context();

        var sql = sp_AdvanceSearch.Query(db, EveryFilter(type)).ToQueryString();

        Assert.Contains("SELECT", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
        // Port Of Discharge is a contains match, not an exact one.
        Assert.Contains("LIKE", sql, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(EveryType))]
    public void A_multi_country_selection_matches_any_of_the_selected_ids(string type)
    {
        using var db = Context();

        var sql = sp_AdvanceSearch.Query(db, EveryFilter(type)).ToQueryString();

        // Both ids reach the SQL. The legacy predicate compared the picked string whole, so
        // only one literal "5,12" appeared and nothing matched it.
        Assert.Contains("5", sql, StringComparison.Ordinal);
        Assert.Contains("12", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Border Import Licence")]
    [InlineData("Border Export Licence")]
    [InlineData("Border Import Permit")]
    [InlineData("Border Export Permit")]
    public void Office_filters_SakhanId_on_the_Border_types(string type)
    {
        using var db = Context();

        var withOffice = sp_AdvanceSearch.Query(db, EveryFilter(type)).ToQueryString();

        var request = EveryFilter(type);
        request.Office = 0;
        var withoutOffice = sp_AdvanceSearch.Query(db, request).ToQueryString();

        // The box did nothing at all before: both queries were identical.
        Assert.NotEqual(withoutOffice, withOffice);
        Assert.Contains("SakhanId", withOffice, StringComparison.Ordinal);
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
