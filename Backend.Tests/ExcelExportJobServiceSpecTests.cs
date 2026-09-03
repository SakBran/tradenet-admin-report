using API.DBContext;
using API.Model.ExcelExport;
using API.Service.ExcelExport;
using Backend.Controllers.Report;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Tests;

/// <summary>
/// The queued job row is what the Exports drive shows and what the download is named,
/// so both have to come from the spec the user's own grid produced — not from a
/// prettified controller name.
/// </summary>
public sealed class ExcelExportJobServiceSpecTests
{
    private sealed class NoFileStore : IExcelExportFileStore
    {
        public string BuildRelativePath(string fileName) => fileName;
        public Stream OpenWrite(string relativePath) => new MemoryStream();
        public Stream OpenRead(string relativePath) => new MemoryStream();
        public bool Exists(string? relativePath) => false;
        public long GetSize(string relativePath) => 0;
        public void Delete(string? relativePath) { }
    }

    private static (ExcelExportJobService Service, ApplicationDbContext Db) Create(string name)
    {
        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(name).Options);

        var handler = new ControllerStreamingExcelReportJobHandler(
            typeof(AccountSummaryReportController),
            "AccountSummaryReport",
            "Account Summary Report",
            "AccountSummaryReport",
            formatVersion: 3);

        var service = new ExcelExportJobService(
            db,
            new ExcelReportJobRegistry([handler]),
            new NoFileStore(),
            Options.Create(new ExcelExportOptions()));

        return (service, db);
    }

    private static AccountSummaryReportRequest Request(ExcelPresentationSpec? spec) => new()
    {
        FromDate = new DateTime(2025, 2, 1),
        ToDate = new DateTime(2025, 2, 28),
        Excel = spec,
    };

    private static ExcelPresentationSpec Spec()
    {
        var spec = ExcelSpecFactory.Spec(
            "AccountSummaryReport",
            ExcelSpecFactory.Column("amount", "Deducted Fees", "money"));
        spec.Title = "Account Summary Report";
        spec.FileName = "AccountSummaryReport.xlsx";
        return spec;
    }

    [Fact]
    public async Task The_job_row_takes_its_title_and_file_name_from_the_spec()
    {
        var (service, db) = Create(nameof(The_job_row_takes_its_title_and_file_name_from_the_spec));
        var spec = Spec();
        spec.Title = "Pa Tha Ka Registered Business Organization Report";
        spec.FileName = "PaThaKaRegistered";

        var result = await service.EnqueueAsync("AccountSummaryReport", Request(spec), new DateTime(2025, 2, 28), "tester");

        var job = await db.ExcelExportJobs.SingleAsync(j => j.Id == result.JobId);
        Assert.Equal("Pa Tha Ka Registered Business Organization Report", job.ReportTitle);
        Assert.StartsWith("PaThaKaRegistered_", job.FileName);
        Assert.EndsWith(".xlsx", job.FileName);
    }

    [Fact]
    public async Task Without_a_spec_the_handler_defaults_still_apply()
    {
        var (service, db) = Create(nameof(Without_a_spec_the_handler_defaults_still_apply));

        var result = await service.EnqueueAsync(
            "AccountSummaryReport", Request(null), new DateTime(2025, 2, 28), "tester");

        var job = await db.ExcelExportJobs.SingleAsync(j => j.Id == result.JobId);
        Assert.Equal("Account Summary Report", job.ReportTitle);
        Assert.StartsWith("AccountSummaryReport_", job.FileName);
    }

    [Fact]
    public async Task The_spec_is_part_of_the_dedup_hash_so_a_changed_grid_cannot_reuse_a_cached_file()
    {
        var (service, db) = Create(nameof(The_spec_is_part_of_the_dedup_hash_so_a_changed_grid_cannot_reuse_a_cached_file));

        var first = Spec();
        var second = Spec();
        second.Columns.Add(ExcelSpecFactory.Column("voucherNo", "Voucher No"));

        await service.EnqueueAsync("AccountSummaryReport", Request(first), new DateTime(2025, 2, 28), "tester");
        await service.EnqueueAsync("AccountSummaryReport", Request(second), new DateTime(2025, 2, 28), "tester");

        var hashes = await db.ExcelExportJobs.Select(j => j.FilterHash).Distinct().ToListAsync();
        Assert.Equal(2, hashes.Count);
    }

    [Fact]
    public async Task An_invalid_spec_never_reaches_the_queue()
    {
        var (service, _) = Create(nameof(An_invalid_spec_never_reaches_the_queue));
        var spec = Spec();
        spec.Columns[0].DataIndex = "not a property path";

        await Assert.ThrowsAsync<ExcelPresentationSpecException>(() => service.EnqueueAsync(
            "AccountSummaryReport", Request(spec), new DateTime(2025, 2, 28), "tester"));
    }
}
