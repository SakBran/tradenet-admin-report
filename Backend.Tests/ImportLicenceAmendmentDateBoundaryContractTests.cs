namespace Backend.Tests;

public sealed class ImportLicenceAmendmentDateBoundaryContractTests
{
    [Fact]
    public void Amendment_controller_normalizes_an_end_of_day_value_to_the_selected_calendar_date()
    {
        var controller = new Backend.Controllers.Report.ImportLicenceAmendmentReportController(
            null!,
            null!);
        var method = typeof(Backend.Controllers.Report.ImportLicenceAmendmentReportController)
            .GetMethod(
                "TryCreateReportRequest",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        var request = new Backend.Controllers.Report.ImportLicenceAmendmentReportRequest
        {
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 5, 23, 59, 59),
        };
        object?[] arguments = [request, null, null];

        Assert.True((bool)method.Invoke(controller, arguments)!);
        var procedureRequest = Assert.IsType<API.StoredProcedureToLinq.sp_AmendReportRequest>(
            arguments[1]);
        Assert.Equal(new DateTime(2026, 1, 5), procedureRequest.ToDate);
    }

    [Theory]
    [InlineData("ImportLicenceAmendmentReportController.cs")]
    [InlineData("ImportLicenceActualAmendmentReportController.cs")]
    public void Controller_passes_to_date_as_a_calendar_date(string controllerName)
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Backend",
            "Controllers",
            "Report",
            controllerName));

        Assert.Contains("ToDate = request.ToDate.Date", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ToDate = request.ToDate,", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Actual_amend_controller_uses_the_legacy_stored_procedure_path()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Backend",
            "Controllers",
            "Report",
            "ImportLicenceActualAmendmentReportController.cs"));

        Assert.Contains("sp_ActualAmendReport.ExecuteAsync", source, StringComparison.Ordinal);
        Assert.Contains("sp_ActualAmendReport.ExecuteQueryable", source, StringComparison.Ordinal);
        Assert.DoesNotContain("sp_ActualAmendReport.Query", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Actual_amend_report_keeps_the_legacy_columns_and_data_mapping()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Frontend",
            "src",
            "Report",
            "config",
            "reportConfigs.ts"));

        const string reportKey = "\n  ImportLicenceActualAmendmentReport: {";
        var start = source.IndexOf(reportKey, StringComparison.Ordinal);
        Assert.True(start >= 0);

        var nextReport = source.IndexOf("\n  ImportLicence", start + reportKey.Length, StringComparison.Ordinal);
        Assert.True(nextReport > start);

        var reportConfig = source[start..nextReport];
        Assert.Contains("dataIndex: 'oldLicenceNo'", reportConfig, StringComparison.Ordinal);
        Assert.Contains("title: 'Curency'", reportConfig, StringComparison.Ordinal);
        Assert.Contains("title: 'HSCode'", reportConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("title: 'hsCode'", reportConfig, StringComparison.Ordinal);
    }

    [Fact]
    public void Daily_report_uses_the_global_page_size()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Frontend",
            "src",
            "Report",
            "config",
            "reportConfigs.ts"));

        const string reportKey = "\n  ImportLicenceDailyReportNewLicenceReport: {";
        var start = source.IndexOf(reportKey, StringComparison.Ordinal);
        Assert.True(start >= 0);

        var nextReport = source.IndexOf("\n  ImportLicence", start + reportKey.Length, StringComparison.Ordinal);
        Assert.True(nextReport > start);

        var reportConfig = source[start..nextReport];
        Assert.DoesNotContain("defaultPageSize:", reportConfig, StringComparison.Ordinal);
        Assert.Contains("showRowNumber: true", reportConfig, StringComparison.Ordinal);
    }

    [Fact]
    public void Currency_totals_use_the_same_normalized_date_boundary_as_the_grid()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "StoredProcedureMigrations",
            "sp_ImportLicenceListingCurrencyTotals.sql"));

        const string normalizedBoundary =
            "ImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))";

        Assert.Equal(2, CountOccurrences(source, normalizedBoundary));
    }

    [Theory]
    [InlineData("sp_AmendReport_pagination.sql")]
    [InlineData("sp_ActualAmendReport_pagination.sql")]
    public void Import_licence_branch_normalizes_to_date_before_adding_one_day(string scriptName)
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "StoredProcedureMigrations",
            scriptName));

        const string normalizedBoundary =
            "ImportLicence.CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))";
        const string unnormalizedBoundary =
            "ImportLicence.CreatedDate < DATEADD(day, 1, @ToDate)";

        Assert.Equal(2, CountOccurrences(source, normalizedBoundary));
        Assert.DoesNotMatch(
            $@"(?<![A-Za-z]){System.Text.RegularExpressions.Regex.Escape(unnormalizedBoundary)}",
            source);
    }

    [Fact]
    public void Normalized_boundary_includes_the_selected_day_and_excludes_the_next_day()
    {
        var frontendToDate = new DateTime(2026, 1, 5, 23, 59, 59);
        var exclusiveUpperBound = frontendToDate.Date.AddDays(1);

        Assert.True(new DateTime(2026, 1, 5, 23, 59, 59) < exclusiveUpperBound);
        Assert.False(new DateTime(2026, 1, 6, 0, 0, 0) < exclusiveUpperBound);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var offset = 0;

        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null
                && !Directory.Exists(Path.Combine(directory.FullName, "Frontend")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}
