using System.Text.RegularExpressions;

namespace Backend.Tests;

/// <summary>
/// The Export Licence Voucher report shipped with <c>var includeTotalCount = false;</c> in place of
/// <c>request.IncludeTotalCount</c>. Because the grid's exact-count round trip was answered with the
/// procedure's <c>@IncludeTotalCount = 0</c>, TotalCount stayed null forever: the pager could never
/// reach the last page, and the footer block (which is gated on the same flag) never ran, so the
/// rdlc's "TOTAL" row was missing from both the grid and the Excel export.
/// </summary>
public sealed class VoucherReportFooterContractTests
{
    private static readonly string[] VoucherControllers =
    [
        "BorderExportLicenceVoucherReportController",
        "BorderExportPermitVoucherReportController",
        "BorderImportLicenceVoucherReportController",
        "BorderImportPermitVoucherReportController",
        "ExportLicenceVoucherReportController",
        "ExportPermitVoucherReportController",
        "ImportLicenceVoucherReportController",
        "ImportPermitVoucherReportController",
    ];

    [Theory]
    [MemberData(nameof(AllVoucherControllers))]
    public void Voucher_controller_asks_the_procedure_for_the_count_the_grid_requested(string controller)
    {
        var source = ReadController(controller);

        Assert.DoesNotMatch(new Regex(@"includeTotalCount\s*=\s*false", RegexOptions.IgnoreCase), source);
        Assert.Contains("request.IncludeTotalCount", source);
    }

    [Theory]
    [MemberData(nameof(FooterVoucherControllers))]
    public void Voucher_controller_publishes_the_rdlc_total_amount_footer(string controller)
    {
        var source = ReadController(controller);

        Assert.Contains("ExecuteAmountTotalAsync", source);
        Assert.Contains("[\"amount\"]", source);
    }

    public static TheoryData<string> AllVoucherControllers()
    {
        var data = new TheoryData<string>();
        foreach (var controller in VoucherControllers)
        {
            data.Add(controller);
        }

        return data;
    }

    // BorderImportLicenceVoucherReportController is absent: it has never carried the footer, and
    // no customer has asked for it. Add it here when it does.
    public static TheoryData<string> FooterVoucherControllers()
    {
        var data = new TheoryData<string>();
        foreach (var controller in VoucherControllers)
        {
            if (controller != "BorderImportLicenceVoucherReportController")
            {
                data.Add(controller);
            }
        }

        return data;
    }

    private static string ReadController(string controller)
        => File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Backend",
            "Controllers",
            "Report",
            controller + ".cs"));

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Backend")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException("Could not locate the repository root.");
        }
    }
}
