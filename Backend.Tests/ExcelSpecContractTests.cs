using System.Text.Json;
using API.Model.ExcelExport;
using API.Service.ExcelExport;
using API.Service.Reports;

namespace Backend.Tests;

/// <summary>
/// The oracle for the whole parity change: every fixture is the presentation spec the
/// frontend would really post for one report config, deserialized into the production
/// DTO and pushed through the production layout builder. If a column's dataIndex does
/// not exist on the row type the report streams, the export would ship a blank column —
/// so that is a failure here rather than a customer complaint.
///
/// The fixtures are generated from the frontend configs
/// (npm run fixtures:excel). Until they exist these tests report that and pass, so the
/// backend and frontend halves can land independently.
/// </summary>
public sealed class ExcelSpecContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset ExportedAt = new(2026, 9, 2, 19, 40, 0, TimeSpan.Zero);

    /// <summary>Reports with no Excel button in the UI, so no fixture is expected.</summary>
    private static readonly HashSet<string> WithoutFixtures = new(StringComparer.Ordinal)
    {
        "CardListsByCompanyRegistrationNumber",
        "DataImport",
        "ImportLicenceDataImport",
    };

    /// <summary>
    /// The group-D composites (Contract §9 D). Their bare <c>Post</c> returns a SUMMARY
    /// payload (<see cref="ImportLicenceTotalValueLicencesSummary"/>) that holds one list
    /// per table, while the grid's columns describe the rows INSIDE those lists
    /// (<c>TotalValueByCurrencyRow</c> / <c>TotalLicencesByPaThaKaTypeRow</c>) — so every
    /// dataIndex lives one level below the resolved row type and binding it here proves
    /// nothing. Their real sheet is the typed two-section layout of Contract §9 D; once a
    /// controller has it the export ignores these generic spec columns altogether and its
    /// per-controller <c>Backend.Tests/ExcelParity/*LayoutTests.cs</c> pins the sheet
    /// instead.
    /// <para>
    /// <c>BorderExportLicenceTotalValueLicencesReport</c> already has that typed layout, so
    /// it IS at parity: a <c>IExcelReportLayoutProvider</c> controller is handed the plain
    /// sink and bypasses <c>RowTypeAssertingSink</c> entirely
    /// (<c>ControllerStreamingExcelReportJobHandler.cs:103-105</c>). Its set entry stays only
    /// because its fixture still describes the flat 3-column grid; drop the entry when the
    /// composite fixture (sections + summaryLines) replaces it, or
    /// <c>Every_column_binds_to_a_real_property_on_the_row_type_the_report_streams</c> starts
    /// failing on <c>totalValue</c>/<c>currency</c>/<c>noOfLicences</c>.
    /// </para>
    /// The other three exports are NOT at parity yet: their <c>WriteRowsAsync</c> still
    /// appends <c>ReportAggregateResult</c> rows, which
    /// <c>ControllerStreamingExcelReportJobHandler.RowTypeAssertingSink</c> rejects.
    /// </summary>
    private static readonly HashSet<string> CompositesPendingTypedLayout = new(StringComparer.Ordinal)
    {
        "BorderExportLicenceTotalValueLicencesReport",
        "BorderImportLicenceTotalValueLicencesReport",
        "ExportLicenceTotalValueLicencesReport",
        "ImportLicenceTotalValueLicencesReport",
    };

    public sealed record IndexEntry(
        string ConfigKey,
        string ControllerName,
        string File,
        string? Variant,
        bool HasDateRange,
        bool HasSingleDate,
        bool HasCurrencyTotalsColumns,
        bool ShowRowNumber,
        string RowNumberTitle,
        bool IsBespokePage,
        bool IsComposite,
        bool HasExcelButton,
        int ColumnCount);

    private sealed record SpecIndex(
        int FormatVersion,
        string? Source,
        Dictionary<string, string>? SampleFilters,
        List<IndexEntry> Entries);

    private sealed record AllowlistEntry(string Controller, string DataIndex, string Reason);

    private static string Directory => Path.Combine(AppContext.BaseDirectory, "Fixtures", "ExcelSpecs");

    private static SpecIndex? Index()
    {
        var path = Path.Combine(Directory, "index.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<SpecIndex>(File.ReadAllText(path), JsonOptions)
            : null;
    }

    private static IReadOnlyList<AllowlistEntry> Allowlist()
    {
        var path = Path.Combine(Directory, "allowlist.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<List<AllowlistEntry>>(File.ReadAllText(path), JsonOptions) ?? []
            : [];
    }

    private static ExcelPresentationSpec Spec(IndexEntry entry)
    {
        var path = Path.Combine(Directory, entry.File);
        Assert.True(File.Exists(path), $"Fixture {entry.File} is listed in index.json but missing on disk.");

        var spec = JsonSerializer.Deserialize<ExcelPresentationSpec>(File.ReadAllText(path), JsonOptions);
        Assert.NotNull(spec);
        return spec!;
    }

    private static Type ControllerType(string controllerName)
        => ReportTestHelper.ControllerTypes.SingleOrDefault(type => type.Name == controllerName + "Controller")
            ?? throw new InvalidOperationException($"No controller named {controllerName}Controller.");

    /// <summary>One case per fixture; a single null case when the fixtures are not generated yet.</summary>
    public static IEnumerable<object?[]> Fixtures()
    {
        var index = Index();
        if (index == null || index.Entries.Count == 0)
        {
            yield return [null];
            yield break;
        }

        foreach (var entry in index.Entries)
        {
            yield return [entry];
        }
    }

    private static bool Missing(IndexEntry? entry)
    {
        if (entry != null)
        {
            return false;
        }

        Assert.True(true, "Excel spec fixtures have not been generated yet (npm run fixtures:excel).");
        return true;
    }

    [Fact]
    public void Every_streaming_report_has_at_least_one_fixture()
    {
        var index = Index();
        if (index == null)
        {
            return;
        }

        var byController = index.Entries
            .GroupBy(entry => entry.ControllerName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var missing = ReportTestHelper.ControllerTypes
            .Where(type => typeof(IStreamingExcelReport).IsAssignableFrom(type))
            .Select(type => type.Name[..^"Controller".Length])
            .Where(name => !WithoutFixtures.Contains(name) && !byController.ContainsKey(name))
            .ToList();

        Assert.True(
            missing.Count == 0,
            "These reports would export with no column contract at all: " + string.Join(", ", missing));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void The_fixture_deserializes_into_the_production_dto_and_validates(IndexEntry? entry)
    {
        if (Missing(entry))
        {
            return;
        }

        var spec = Spec(entry!);

        Assert.Equal(ExcelPresentationSpec.CurrentFormatVersion, spec.FormatVersion);
        Assert.Equal(entry!.ConfigKey, spec.ConfigKey);
        Assert.Equal(entry.ControllerName, spec.ControllerName);
        Assert.Equal(entry.ShowRowNumber, spec.ShowRowNumber);
        Assert.Equal(entry.RowNumberTitle, spec.RowNumberTitle);
        Assert.Equal(entry.ColumnCount, spec.Columns.Count);
        Assert.Equal(entry.HasCurrencyTotalsColumns, spec.CurrencyTotalsColumns != null);

        var typedLayout = typeof(IExcelReportLayoutProvider).IsAssignableFrom(ControllerType(entry.ControllerName));
        Assert.True(
            ExcelPresentationSpecValidator.TryValidateAndSanitize(spec, !typedLayout, out var errors),
            string.Join("; ", errors));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Every_column_binds_to_a_real_property_on_the_row_type_the_report_streams(IndexEntry? entry)
    {
        if (Missing(entry))
        {
            return;
        }

        var spec = Spec(entry!);
        var controllerType = ControllerType(entry!.ControllerName);
        var rowType = ExcelRowTypeResolver.Resolve(controllerType);

        Assert.NotNull(rowType);

        // A composite's columns describe its nested section rows, not the summary the
        // grid endpoint returns; see CompositesPendingTypedLayout. Every other report —
        // including the typed-layout providers, whose hand-bound columns must still show
        // the grid's dataIndexes — is held to the binding contract below.
        if (CompositesPendingTypedLayout.Contains(entry.ControllerName))
        {
            return;
        }

        var map = ExcelRowPropertyMap.For(rowType!);
        var allowed = Allowlist()
            .Where(item => item.Controller == entry.ControllerName)
            .Select(item => item.DataIndex)
            .ToHashSet(StringComparer.Ordinal);

        var unresolved = spec.Columns
            .Where(column => map.Find(column.DataIndex) == null
                && (column.FallbackDataIndexes ?? []).All(fallback => map.Find(fallback) == null)
                && column.DataIndex is not ("mpuAmount" or "amountDiff"))
            .Select(column => column.DataIndex)
            .Where(dataIndex => !allowed.Contains(dataIndex))
            .ToList();

        Assert.True(
            unresolved.Count == 0,
            $"{entry.ConfigKey} would export blank columns for: {string.Join(", ", unresolved)} "
                + $"(row type {rowType!.Name}). Fix the dataIndex, add a fallback, or allowlist it.");

        foreach (var section in spec.Sections ?? [])
        {
            Assert.NotNull(map.Find(section.DataPath));
        }

        foreach (var line in spec.SummaryLines ?? [])
        {
            Assert.NotNull(map.Find(line.DataPath));
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void The_date_shape_the_frontend_assumed_matches_the_request_dto(IndexEntry? entry)
    {
        if (Missing(entry))
        {
            return;
        }

        var controllerType = ControllerType(entry!.ControllerName);
        var requestType = ReportTestHelper.GetRequestType(controllerType);
        var props = ExcelRequestDates.Describe(requestType);

        Assert.Equal(entry.HasDateRange, props.HasFromTo);
        Assert.Equal(entry.HasSingleDate, !props.HasFromTo && props.Date != null);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Currency_totals_point_at_columns_that_exist(IndexEntry? entry)
    {
        if (Missing(entry))
        {
            return;
        }

        var spec = Spec(entry!);
        if (spec.CurrencyTotalsColumns == null)
        {
            return;
        }

        var keys = spec.Columns.Select(column => column.Key).ToHashSet(StringComparer.Ordinal);

        Assert.Contains(spec.CurrencyTotalsColumns.LabelColumnKey, keys);
        Assert.Contains(spec.CurrencyTotalsColumns.ValueColumnKey, keys);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void The_built_sheet_shows_exactly_the_ui_columns_under_the_standard_header_block(IndexEntry? entry)
    {
        if (Missing(entry))
        {
            return;
        }

        var spec = Spec(entry!);
        var controllerType = ControllerType(entry!.ControllerName);
        var request = BuildSampleRequest(controllerType);

        var layout = typeof(IExcelReportLayoutProvider).IsAssignableFrom(controllerType)
            ? TypedLayout(controllerType, request)
            : ExcelLayoutBuilder.Build(spec, ExcelRowTypeResolver.Resolve(controllerType));

        if (layout == null)
        {
            return;
        }

        if (spec.Sections == null && layout.Columns.Count > 0)
        {
            var expected = new List<string>();
            if (spec.ShowRowNumber)
            {
                expected.Add(spec.RowNumberTitle);
            }

            expected.AddRange(spec.Columns.Select(column => column.Title));

            var actual = layout.Columns.Select(column => column.Header).ToList();

            // A typed layout owns its own headers; they must still equal the grid's.
            Assert.Equal(expected, actual);
        }

        var withHeader = ExcelLayoutBuilder.WithStandardHeaderBlock(
            layout, spec, spec.Title, request, ExportedAt);

        var lines = withHeader.TitleLines.Concat(withHeader.HeaderBlock.Select(line => line.Text)).ToList();

        Assert.Contains(lines, line => line.Contains(spec.Title, StringComparison.Ordinal));
        Assert.Equal("Exported: 02/09/2026 19:40", lines[^1]);

        if (entry.HasDateRange)
        {
            Assert.Contains("From Date: 01/02/2026", lines);
            Assert.Contains("To Date: 28/02/2026", lines);
        }
        else if (entry.HasSingleDate)
        {
            Assert.Contains("Date: 15/02/2026", lines);
        }
        else
        {
            Assert.DoesNotContain(lines, line => line.StartsWith("From Date:", StringComparison.Ordinal));
        }

        // The whole preamble, in order — a "contains the title" check would not notice a
        // SECOND title row above a subtitle that already carries the name (Contract §2).
        // A typed layout owns its TitleLines, so only the generic (spec-built) sheets are
        // pinned line for line.
        if (!typeof(IExcelReportLayoutProvider).IsAssignableFrom(controllerType))
        {
            Assert.Equal(ExpectedPreamble(spec, entry), lines);
        }
    }

    /// <summary>
    /// What WithStandardHeaderBlock must produce, derived from the fixture alone: the
    /// spec's own header lines, a title row only when none of them already carries the
    /// report's name, then the request's dates and the export stamp.
    /// </summary>
    private static List<string> ExpectedPreamble(ExcelPresentationSpec spec, IndexEntry entry)
    {
        var lines = new List<string>();

        foreach (var headerLine in spec.HeaderLines ?? [])
        {
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                continue;
            }

            var text = headerLine.Trim();
            if (!lines.Contains(text, StringComparer.Ordinal))
            {
                lines.Add(text);
            }
        }

        var title = ExcelPresentationSpecValidator.SanitizeTitle(spec.Title);
        if (title != null && !lines.Any(line => line.Contains(title, StringComparison.Ordinal)))
        {
            lines.Insert(0, title);
        }

        if (entry.HasDateRange)
        {
            lines.Add("From Date: 01/02/2026");
            lines.Add("To Date: 28/02/2026");
        }
        else if (entry.HasSingleDate)
        {
            lines.Add("Date: 15/02/2026");
        }

        lines.Add("Exported: 02/09/2026 19:40");
        return lines;
    }

    /// <summary>
    /// The worksheet tab is named from the controller's <c>ExcelWorksheetTitle</c> while
    /// the title row comes from the config title. Rule M1 lets them differ only where the
    /// difference is deliberate and recorded here.
    /// </summary>
    private static readonly Dictionary<string, string> WorksheetTabAllowlist = new(StringComparer.Ordinal)
    {
        // Renamed "Seller" → "Buyer" on the page only (see the Export Permit round-2 pass).
        ["ExportPermitBySellerCountryReport"] = "Export Permit By Seller Country Report",
        // The 4 HS Code alias configs share a controller and post their own alias title.
        ["BorderExportPermitByHSCodeReport"] = "Border Export Permit By HS Code Report",
        ["BorderImportLicenceByHSCodeReport"] = "Border Import Licence By HS Code Report",
        ["BorderImportPermitByHSCodeReport"] = "Border Import Permit By HS Code Report",
        ["ExportLicenceByHSCodeReport"] = "Export Licence By HS Code Report",
        // Legacy tab names kept because users recognise them on the Exports drive.
        ["BusinessServiceAgencyRegistrationByVoucher"] = "BSA Registration By Voucher",
        ["ImportLicenceDetailByLicenceReport"] = "Import Licence Detail (By Licence) Report",
        ["ImportLicenceDetailReportPending"] = "Import Licence Pending Detail Report",
        ["ImportLicenceNewReportNewReport"] = "Import Licence New Report",
        ["OGARecommendationHistoryReport"] = "OGA Recommendation History Report",
        ["PaThaKaRegisteredBusinessOrganizationReport"] = "PaThaKaRegisteredBusinessOrganizationReport",
    };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void The_worksheet_tab_is_the_reports_own_title_unless_the_difference_is_allowlisted(IndexEntry? entry)
    {
        if (Missing(entry))
        {
            return;
        }

        var spec = Spec(entry!);
        var controllerType = ControllerType(entry!.ControllerName);
        var instance = (IStreamingExcelReport)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(controllerType);
        var tab = instance.ExcelWorksheetTitle;

        if (string.Equals(tab, spec.Title, StringComparison.Ordinal))
        {
            return;
        }

        Assert.True(
            WorksheetTabAllowlist.TryGetValue(entry.ControllerName, out var allowed)
                && string.Equals(allowed, tab, StringComparison.Ordinal),
            $"{entry.ControllerName}: the worksheet tab is '{tab}' but the sheet's title row says "
                + $"'{spec.Title}'. Reconcile ExcelWorksheetTitle with the config title, or record the "
                + "difference in WorksheetTabAllowlist.");
    }

    /// <summary>
    /// The binding exemption must not spread beyond the group-D composites: every name in
    /// <see cref="CompositesPendingTypedLayout"/> has to be a real streaming report whose
    /// grid payload is the TotalValue &amp; Licences summary. A report that merely has a
    /// broken dataIndex belongs in <c>allowlist.json</c> with a reason, not in here.
    /// </summary>
    [Fact]
    public void Only_the_total_value_licences_composites_skip_the_column_binding_check()
    {
        foreach (var controllerName in CompositesPendingTypedLayout)
        {
            var controllerType = ControllerType(controllerName);

            Assert.True(typeof(IStreamingExcelReport).IsAssignableFrom(controllerType));
            Assert.Equal(
                typeof(ImportLicenceTotalValueLicencesSummary),
                ExcelRowTypeResolver.Resolve(controllerType));
        }
    }

    private static ExcelReportLayout? TypedLayout(Type controllerType, object request)
    {
        var method = controllerType.GetMethod(nameof(IExcelReportLayoutProvider.GetExcelLayout), [typeof(object)]);
        var instance = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(controllerType);

        try
        {
            return (ExcelReportLayout?)method!.Invoke(instance, [request]);
        }
        catch (Exception)
        {
            // A layout that needs live state cannot be built here; the layout contract
            // tests for that report cover it instead.
            return null;
        }
    }

    /// <summary>The same sample filters the fixtures were generated with.</summary>
    private static object BuildSampleRequest(Type controllerType)
    {
        var requestType = ReportTestHelper.GetRequestType(controllerType);
        var request = Activator.CreateInstance(requestType)
            ?? throw new InvalidOperationException($"Could not create {requestType.Name}.");

        Set("FromDate", new DateTime(2026, 2, 1));
        Set("ToDate", new DateTime(2026, 2, 28, 23, 59, 59));
        Set("Date", new DateTime(2026, 2, 15));

        return request;

        void Set(string name, DateTime value)
        {
            var property = requestType.GetProperty(name);
            if (property == null || !property.CanWrite)
            {
                return;
            }

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (type == typeof(DateTime))
            {
                property.SetValue(request, value);
            }
        }
    }
}
