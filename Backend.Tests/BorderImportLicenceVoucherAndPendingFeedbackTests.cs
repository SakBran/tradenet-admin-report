using API.StoredProcedureToLinq;
using API.DBContext;
using Backend.Controllers.Report;
using Microsoft.EntityFrameworkCore;

namespace Backend.Tests;

public sealed class BorderImportLicenceVoucherAndPendingFeedbackTests
{
    [Fact]
    public void Voucher_controller_normalizes_date_and_restores_the_old_default_apply_type()
    {
        var controller = new BorderImportLicenceVoucherReportController(null!, null!);
        var method = typeof(BorderImportLicenceVoucherReportController).GetMethod(
            "TryCreateReportRequest",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        var request = new BorderImportLicenceVoucherReportRequest
        {
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 5, 23, 59, 59),
            ApplyType = string.Empty,
            CompanyRegistrationNo = "  REG-001  ",
        };
        object?[] arguments = [request, null, null];

        Assert.True((bool)method.Invoke(controller, arguments)!);
        var procedureRequest = Assert.IsType<sp_VoucherReportRequest>(arguments[1]);
        Assert.Equal(new DateTime(2026, 1, 5), procedureRequest.ToDate);
        Assert.Equal("New", procedureRequest.ApplyType);
        Assert.Equal("REG-001", procedureRequest.CompanyRegistrationNo);
        Assert.Equal("Border Import Licence", procedureRequest.FormType);
    }

    [Fact]
    public void Voucher_grid_and_total_query_share_the_exclusive_calendar_day_boundary()
    {
        var controller = Source("Backend", "Controllers", "Report", "BorderImportLicenceVoucherReportController.cs");
        var query = Source("Backend", "StoredProcedureToLinq", "sp_VoucherReport.cs");
        var procedure = Source("StoredProcedureMigrations", "sp_VoucherReport_pagination.sql");

        Assert.Contains("ToDate = request.ToDate.Date", controller, StringComparison.Ordinal);
        Assert.Contains("ExecuteAmountTotalAsync", controller, StringComparison.Ordinal);
        Assert.Contains("[\"amount\"] = decimal.Round", controller, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(query, "account.PaymentDate < toDateExclusive"));
        Assert.Equal(
            4,
            CountOccurrences(
                BorderImportVoucherProcedureBranch(procedure),
                "PaymentDate < DATEADD(day, 1, CONVERT(date, @ToDate))"));
    }

    [Fact]
    public void Pending_controller_uses_the_border_query_for_grid_and_excel()
    {
        var controller = Source("Backend", "Controllers", "Report", "BorderImportLicencePendingReportController.cs");

        Assert.Equal(2, CountOccurrences(controller, "sp_PendingReport.Query(_context, procedureRequest!)"));
        Assert.DoesNotContain("sp_PendingReport.ExecuteAsync", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("sp_PendingReport.ExecuteQueryable", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Pending_runtime_query_reads_only_border_licence_tables()
    {
        var options = new DbContextOptionsBuilder<TradeNetDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=QueryTranslationOnly;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;
        using var db = new TradeNetDbContext(options);

        var sql = sp_PendingReport.Query(db, new sp_PendingReportRequest
        {
            FormType = "Border Import Licence",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 5, 23, 59, 59),
        }).ToQueryString();

        Assert.Contains("[BorderImportLicence]", sql, StringComparison.Ordinal);
        Assert.Contains("[BorderImportLicenceItem]", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("[ImportLicence]", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("[ExportLicence]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Pending_pagination_script_has_a_dedicated_border_table_and_item_branch()
    {
        var procedure = Source("StoredProcedureMigrations", "sp_PendingReport_pagination.sql");
        var borderStart = procedure.IndexOf(
            "IF @FormType = N'Border Import Licence'",
            StringComparison.Ordinal);
        var borderEnd = procedure.IndexOf("RETURN;", borderStart, StringComparison.Ordinal);

        Assert.True(borderStart >= 0);
        Assert.True(borderEnd > borderStart);

        var branch = procedure[borderStart..borderEnd];
        Assert.Contains("FROM BorderImportLicence", branch, StringComparison.Ordinal);
        Assert.Contains("FROM BorderImportLicenceItem", branch, StringComparison.Ordinal);
        Assert.DoesNotContain("FROM ImportLicence\n", branch, StringComparison.Ordinal);
        Assert.Contains("Status=''Pending'' or BorderImportLicence.Status=''Reject''", branch, StringComparison.Ordinal);
        Assert.Contains("ExportImportSectionId", branch, StringComparison.Ordinal);
    }

    private static string BorderImportVoucherProcedureBranch(string procedure)
    {
        var start = procedure.IndexOf(
            "ELSE IF @FormType = N'Border Import Licence'",
            StringComparison.Ordinal);
        var end = procedure.IndexOf(
            "ELSE IF @FormType = N'Border Export Permit'",
            start,
            StringComparison.Ordinal);

        Assert.True(start >= 0);
        Assert.True(end > start);
        return procedure[start..end];
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

    private static string Source(params string[] parts)
        => File.ReadAllText(Path.Combine([RepositoryRoot, .. parts]));

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
