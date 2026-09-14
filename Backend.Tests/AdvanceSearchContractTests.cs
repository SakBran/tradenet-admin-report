using System.Reflection;
using API.StoredProcedureToLinq;
using Backend.Controllers.Report;

namespace Backend.Tests;

/// <summary>
/// The eight Advance Search endpoints against the old admin's one Advance Search screen
/// (Views/Reports/AdvanceSearch.cshtml + tradenet-2.0-api AdvanceSearchRepository.Search).
/// No database: these pin the request mapping, which type each endpoint asks for, which boxes
/// each type can actually filter on, and the defects that were repaired.
/// </summary>
public sealed class AdvanceSearchContractTests
{
    public static TheoryData<Type, string> TypePerController() => new()
    {
        { typeof(AdvanceSearchImportLicenceController), "Import Licence" },
        { typeof(AdvanceSearchExportLicenceController), "Export Licence" },
        { typeof(AdvanceSearchImportPermitController), "Import Permit" },
        { typeof(AdvanceSearchExportPermitController), "Export Permit" },
        { typeof(AdvanceSearchBorderImportLicenceController), "Border Import Licence" },
        { typeof(AdvanceSearchBorderExportLicenceController), "Border Export Licence" },
        { typeof(AdvanceSearchBorderImportPermitController), "Border Import Permit" },
        { typeof(AdvanceSearchBorderExportPermitController), "Border Export Permit" },
    };

    private static readonly Type[] BorderControllers =
    {
        typeof(AdvanceSearchBorderImportLicenceController),
        typeof(AdvanceSearchBorderExportLicenceController),
        typeof(AdvanceSearchBorderImportPermitController),
        typeof(AdvanceSearchBorderExportPermitController),
    };

    private static readonly Type[] OverseaControllers =
    {
        typeof(AdvanceSearchImportLicenceController),
        typeof(AdvanceSearchExportLicenceController),
        typeof(AdvanceSearchImportPermitController),
        typeof(AdvanceSearchExportPermitController),
    };

    private static Type RequestType(Type controllerType) =>
        controllerType.Assembly.GetType(
            $"{controllerType.Namespace}.{controllerType.Name[..^"Controller".Length]}Request")!;

    private static bool HasProperty(Type controllerType, string name) =>
        RequestType(controllerType).GetProperty(name, BindingFlags.Public | BindingFlags.Instance) != null;

    // --- the eight tiles ----------------------------------------------------

    [Theory]
    [MemberData(nameof(TypePerController))]
    public void Each_endpoint_pins_its_own_licence_or_permit_type(Type controllerType, string expectedType)
    {
        var procedureRequest = Assert.IsType<sp_AdvanceSearchRequest>(
            ReportTestHelper.CreateProcedureRequest(controllerType));

        Assert.Equal(expectedType, procedureRequest.TypeOfLicenceOrPermit);
        Assert.Contains(expectedType, sp_AdvanceSearch.AdvanceSearchType.All);
    }

    [Fact]
    public void The_eight_tiles_are_all_of_them()
    {
        Assert.Equal(8, sp_AdvanceSearch.AdvanceSearchType.All.Length);
        Assert.Equal(
            sp_AdvanceSearch.AdvanceSearchType.All.Length,
            sp_AdvanceSearch.AdvanceSearchType.All.Distinct().Count());
    }

    // --- which boxes each type can actually filter on ------------------------

    [Theory]
    [MemberData(nameof(TypePerController))]
    public void A_type_declares_only_the_filters_its_table_carries(Type controllerType, string legacyType)
    {
        // ImportPermit and BorderImportPermit have no ModeofTransport or ConsignedCountryId
        // column; neither Permit family has ExportImportMethodId or ExportImportIncotermId.
        // The legacy branches had those predicates commented out.
        var isPermit = legacyType.Contains("Permit", StringComparison.Ordinal);
        var isImportPermit = legacyType.EndsWith("Import Permit", StringComparison.Ordinal);

        Assert.Equal(!isImportPermit, HasProperty(controllerType, "ModeOfTransport"));
        Assert.Equal(!isImportPermit, HasProperty(controllerType, "ConsignedCountry"));
        Assert.Equal(!isPermit, HasProperty(controllerType, "MethodOfImportExport"));
        Assert.Equal(!isPermit, HasProperty(controllerType, "Incoterm"));
    }

    [Theory]
    [MemberData(nameof(TypePerController))]
    public void Every_type_filters_the_boxes_they_all_share(Type controllerType, string legacyType)
    {
        _ = legacyType;

        foreach (var name in new[]
        {
            "FromDate", "ToDate", "Pathaka", "Section", "SellerCountry",
            "PortOfDischarge", "CountryOfOrigin", "Description", "StatementCode", "ApplyType",
        })
        {
            Assert.True(HasProperty(controllerType, name), $"{controllerType.Name} should accept {name}.");
        }
    }

    [Fact]
    public void Only_the_Border_types_take_the_Office_box()
    {
        // It is accepted and ignored on all four, exactly as in the legacy Web API, where
        // `data.Office` has no references at all. See the note on the DTO property.
        Assert.All(BorderControllers, type => Assert.True(HasProperty(type, "Office")));
        Assert.All(OverseaControllers, type => Assert.False(HasProperty(type, "Office")));
    }

    [Theory]
    [MemberData(nameof(TypePerController))]
    public void No_endpoint_carries_the_legacy_Airport_field(Type controllerType, string legacyType)
    {
        _ = legacyType;

        // The old DTO declared it and the old JS read `$("#Airport").val()`, but the markup has
        // no such control, so it always posted undefined and nothing ever read it.
        Assert.False(HasProperty(controllerType, "Airport"));
    }

    [Theory]
    [MemberData(nameof(TypePerController))]
    public void Section_is_a_string_because_its_all_is_blank_not_zero(Type controllerType, string legacyType)
    {
        _ = legacyType;

        Assert.Equal(typeof(string), RequestType(controllerType).GetProperty("Section")!.PropertyType);
    }

    // --- the repaired defects ------------------------------------------------

    [Theory]
    [InlineData("Sea", new[] { "Sea" })]
    [InlineData("Sea,Road", new[] { "Sea", "Road" })]
    // The legacy builder read modeList[3] of a three-element list, so this was an HTTP 400.
    [InlineData("Sea,Road,Air", new[] { "Sea", "Road", "Air" })]
    public void A_mode_selection_is_kept_as_the_modes_themselves(string modes, string[] expected)
    {
        // Each mode is matched on its own against the comma-joined column, so picking several
        // means "carries any of them". The legacy code permuted the selection and matched the
        // column whole, which meant "carries exactly this set" and almost never hit.
        Assert.Equal(expected, sp_AdvanceSearch.SplitModes(modes));
    }

    [Fact]
    public void A_repeated_or_padded_mode_is_normalised()
    {
        Assert.Equal(new[] { "Sea", "Road" }, sp_AdvanceSearch.SplitModes(" Sea , Road ,Sea "));
    }

    [Fact]
    public void No_mode_selected_means_no_mode_filter()
    {
        Assert.Null(sp_AdvanceSearch.SplitModes(""));
        Assert.Null(sp_AdvanceSearch.SplitModes(null));
        Assert.Null(sp_AdvanceSearch.SplitModes(" , "));
    }

    [Fact]
    public void A_single_country_is_one_id()
    {
        Assert.Equal(new[] { 12 }, sp_AdvanceSearch.SplitCountryIds("12"));
    }

    [Fact]
    public void A_multi_country_selection_keeps_every_id()
    {
        // The legacy predicate compared the whole picked string to the whole stored column, and
        // on the two int columns reached Convert.ToInt32("5,12") and returned HTTP 400. Every
        // selected id is now matched on its own -- "any of these countries".
        Assert.Equal(new[] { 5, 12 }, sp_AdvanceSearch.SplitCountryIds("5,12"));
    }

    [Fact]
    public void A_country_selection_that_parses_to_nothing_still_matches_nothing()
    {
        // It must not widen into "all" -- that would silently return every row.
        Assert.Equal(new[] { sp_AdvanceSearch.NoMatchId }, sp_AdvanceSearch.SplitCountryIds("abc"));
    }

    [Fact]
    public void No_country_selected_means_no_country_filter()
    {
        Assert.Null(sp_AdvanceSearch.SplitCountryIds(""));
        Assert.Null(sp_AdvanceSearch.SplitCountryIds(null));
    }

    // --- country names, the step that crashed the four Permit screens ---------

    private static readonly Dictionary<int, string> Countries = new()
    {
        [5] = "MALAYSIA",
        [12] = "THAILAND",
    };

    private static sp_AdvanceSearchResult Resolve(
        string? countryOfOriginText = null,
        int? countryOfOriginNumber = null,
        string? consignedCountryText = null,
        int? consignedCountryNumber = null)
    {
        var row = new sp_AdvanceSearchResult
        {
            CountryOfOriginText = countryOfOriginText,
            CountryOfOriginNumber = countryOfOriginNumber,
            ConsignedCountryText = consignedCountryText,
            ConsignedCountryNumber = consignedCountryNumber,
        };

        AdvanceSearchCountryNames.Apply(new[] { row }, Countries);
        return row;
    }

    [Fact]
    public void An_unset_consigned_country_is_blank_not_a_crash()
    {
        // The four Permit branches never assign it, and the legacy helper called .Split(',')
        // on it regardless -- a NullReferenceException, so those four screens returned HTTP 400
        // for any non-empty result. This is the fix that makes them usable at all.
        var row = Resolve(countryOfOriginText: "5");

        Assert.Equal("MALAYSIA", row.CountryOfOrigin);
        Assert.Equal(string.Empty, row.ConsignedCountry);
    }

    [Fact]
    public void An_empty_country_column_is_blank_not_a_crash()
    {
        // Convert.ToInt32("") in the legacy helper.
        Assert.Equal(string.Empty, Resolve(countryOfOriginText: "").CountryOfOrigin);
    }

    [Fact]
    public void An_id_no_country_carries_is_skipped_not_a_crash()
    {
        // .First() in the legacy helper.
        Assert.Equal(string.Empty, Resolve(countryOfOriginText: "999").CountryOfOrigin);
        Assert.Equal("MALAYSIA", Resolve(countryOfOriginText: "999,5").CountryOfOrigin);
    }

    [Fact]
    public void Several_countries_print_in_the_legacy_reversed_order()
    {
        // countryReplace PREPENDED each name, so "5,12" prints as THAILAND,MALAYSIA. Kept.
        Assert.Equal("THAILAND,MALAYSIA", Resolve(countryOfOriginText: "5,12").CountryOfOrigin);
    }

    [Fact]
    public void An_int_country_column_resolves_the_same_way()
    {
        var row = Resolve(countryOfOriginNumber: 12, consignedCountryNumber: 5);

        Assert.Equal("THAILAND", row.CountryOfOrigin);
        Assert.Equal("MALAYSIA", row.ConsignedCountry);
    }

    // --- the composed address ------------------------------------------------

    [Fact]
    public void The_company_address_concatenates_with_no_separators()
    {
        // AdvanceSearchRepository.cs:194-198 -- five columns, glued straight together.
        var row = new sp_AdvanceSearchResult
        {
            UnitLevel = "No.1",
            StreetNumberStreetName = "Bogyoke Rd",
            QuarterCityTownship = "Latha",
            State = "Yangon",
            Country = "Myanmar",
        };

        Assert.Equal("No.1Bogyoke RdLathaYangonMyanmar", row.CompanyAddress);
    }

    [Fact]
    public void A_null_address_part_drops_out_instead_of_nulling_the_whole_address()
    {
        var row = new sp_AdvanceSearchResult
        {
            UnitLevel = null,
            StreetNumberStreetName = "Bogyoke Rd",
            QuarterCityTownship = "Latha",
            State = "Yangon",
            Country = "Myanmar",
        };

        Assert.Equal("Bogyoke RdLathaYangonMyanmar", row.CompanyAddress);
    }

    // --- Excel ---------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TypePerController))]
    public void The_worksheet_title_names_the_report_and_its_type(Type controllerType, string legacyType)
    {
        var report = (API.Service.ExcelExport.IStreamingExcelReport)
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(controllerType);

        Assert.Equal($"Advance Search for {legacyType}", report.ExcelWorksheetTitle);
        Assert.Equal(RequestType(controllerType), report.ExcelRequestType);
    }
}
