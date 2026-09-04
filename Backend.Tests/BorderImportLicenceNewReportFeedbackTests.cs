using API.StoredProcedureToLinq;
using Backend.Controllers.Report;

namespace Backend.Tests;

public sealed class BorderImportLicenceNewReportFeedbackTests
{
    [Fact]
    public void Controller_uses_the_selected_calendar_date_and_old_filter_scope()
    {
        var controller = new BorderImportLicenceNewReportNewReportController(null!, null!);
        var method = typeof(BorderImportLicenceNewReportNewReportController).GetMethod(
            "TryCreateReportRequest",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        var request = new BorderImportLicenceNewReportNewReportRequest
        {
            FromDate = new DateTime(2023, 8, 1),
            ToDate = new DateTime(2023, 8, 1, 23, 59, 59),
            CompanyRegistrationNo = "  REG-001  ",
            Auto = "auto",
        };
        object?[] arguments = [request, null, null];

        Assert.True((bool)method.Invoke(controller, arguments)!);
        var procedureRequest = Assert.IsType<sp_NewReportRequest>(arguments[1]);
        Assert.Equal(new DateTime(2023, 8, 1), procedureRequest.ToDate);
        Assert.Equal("REG-001", procedureRequest.CompanyRegistrationNo);
        Assert.Equal(string.Empty, procedureRequest.Auto);
    }

    [Fact]
    public void Currency_totals_build_the_legacy_currency_and_grand_totals()
    {
        var summary = BorderImportLicenceListingCurrencyTotals.ToSummary(
        [
            new()
            {
                Currency = "USD",
                NoOfLicences = 3,
                TotalValue = 1250m,
            },
            new()
            {
                Currency = "EUR",
                NoOfLicences = 2,
                TotalValue = 500m,
            },
        ]);

        Assert.Equal(5, summary.GrandTotalLicences);
        Assert.Collection(
            summary.Currencies,
            usd =>
            {
                Assert.Equal("USD", usd.Currency);
                Assert.Equal(3, usd.NoOfLicences);
                Assert.Equal(1250m, usd.TotalValue);
            },
            eur =>
            {
                Assert.Equal("EUR", eur.Currency);
                Assert.Equal(2, eur.NoOfLicences);
                Assert.Equal(500m, eur.TotalValue);
            });
    }

    [Fact]
    public void Grid_and_footer_queries_exclude_the_day_after_the_selected_to_date()
    {
        var procedureScript = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "StoredProcedureMigrations",
            "sp_NewReport_pagination.sql"));
        var footerQuery = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Backend",
            "StoredProcedureToLinq",
            "BorderImportLicenceListingCurrencyTotals.cs"));

        const string boundary = "CreatedDate < DATEADD(day, 1, CONVERT(date, @ToDate))";

        Assert.Equal(2, CountOccurrences(procedureScript, $"BorderImportLicence.{boundary}"));
        Assert.Equal(2, CountOccurrences(footerQuery, $"licence.{boundary}"));
    }

    [Fact]
    public void Normalized_boundary_includes_the_selected_day_and_excludes_the_next_day()
    {
        var frontendToDate = new DateTime(2023, 8, 1, 23, 59, 59);
        var exclusiveUpperBound = frontendToDate.Date.AddDays(1);

        Assert.True(new DateTime(2023, 8, 1, 23, 59, 59) < exclusiveUpperBound);
        Assert.False(new DateTime(2023, 8, 2, 0, 0, 0) < exclusiveUpperBound);
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
