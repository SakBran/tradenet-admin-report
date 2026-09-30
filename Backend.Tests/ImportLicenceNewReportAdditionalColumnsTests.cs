using API.StoredProcedureToLinq;

namespace Backend.Tests;

public sealed class ImportLicenceNewReportAdditionalColumnsTests
{
    [Fact]
    public void Result_mapping_preserves_the_requested_online_licence_and_remark_fields()
    {
        var onlineDate = new DateTime(2026, 9, 30, 7, 27, 59);
        var licenceDate = new DateTime(2026, 9, 30, 7, 28, 57);
        var row = new sp_NewReportRow
        {
            ApplicationNo = "OIL-1-000001-2026",
            ApplicationDate = onlineDate,
            LicenceDate = licenceDate,
            Remark = "Customer-facing application remark",
        };

        var result = row.ToResult();

        Assert.Equal("OIL-1-000001-2026", result.ApplicationNo);
        Assert.Equal(onlineDate, result.ApplicationDate);
        Assert.Equal(licenceDate, result.LicenceDate);
        Assert.Equal("Customer-facing application remark", result.Remark);
    }

    [Fact]
    public void Import_licence_sql_sources_licence_date_from_issued_date()
    {
        var script = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "StoredProcedureMigrations",
            "sp_NewReport_pagination.sql"));
        var branchStart = script.IndexOf("-- Import Licence New listing.", StringComparison.Ordinal);

        // Every other form type served by this shared procedure must still expose
        // the same result shape so EF Core can materialize sp_NewReportRow.
        Assert.Equal(9, CountOccurrences(script, "CAST(NULL AS nvarchar(50)) ApplicationNo"));
        Assert.Equal(9, CountOccurrences(script, "CAST(NULL AS datetime) ApplicationDate"));
        Assert.Equal(9, CountOccurrences(script, "CAST(NULL AS datetime) LicenceDate"));
        Assert.Equal(9, CountOccurrences(script, "CAST(NULL AS nvarchar(max)) Remark"));

        Assert.True(branchStart >= 0, "The Import Licence New branch marker is missing.");
        var importLicenceBranch = script[branchStart..];

        Assert.Contains("ImportLicence.ApplicationNo", importLicenceBranch, StringComparison.Ordinal);
        Assert.Contains("ImportLicence.ApplicationDate", importLicenceBranch, StringComparison.Ordinal);
        Assert.Contains("ImportLicence.IssuedDate LicenceDate", importLicenceBranch, StringComparison.Ordinal);
        Assert.Contains("ImportLicence.Remark", importLicenceBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("ImportLicence.LicenceDate LicenceDate", importLicenceBranch, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string searchValue) =>
        value.Split(searchValue, StringSplitOptions.None).Length - 1;

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Backend", "API.csproj")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
        }
    }
}
