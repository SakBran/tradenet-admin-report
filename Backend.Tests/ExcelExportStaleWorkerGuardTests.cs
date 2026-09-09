using System.Text.RegularExpressions;
using API.DBContext;
using API.Model.ExcelExport;
using API.Service.ExcelExport;
using Backend.Controllers;
using Backend.Controllers.Report;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Tests;

/// <summary>
/// Every <see cref="ExcelExportWorker"/> that can reach TemplateDB competes for the same rows, so
/// an OLDER API instance left running anywhere produces a share of every report's exports from a
/// stale build — twice observed in production (the Border Export Permit Voucher footer on
/// 2026-09-07, the Account Summary DCCA export on 2026-09-09), and undetectable from the code.
///
/// The guard: new jobs are enqueued as <see cref="ExcelExportJobStatus.QueuedV2"/> and claimed as
/// <see cref="ExcelExportJobStatus.ProcessingV2"/>. An older build's claim query is compiled
/// against the literals 0 and 1 and cannot be changed, so it never matches a new job.
///
/// These tests pin both halves: the stale build finds nothing, and we have not locked ourselves
/// out in the process.
/// </summary>
public sealed class ExcelExportStaleWorkerGuardTests
{
    /// <summary>
    /// The stale build's candidate/claim predicate, written with raw ints on purpose. This is a
    /// transcription of a query compiled into a binary we do not control, so it must keep finding
    /// no work even if the enum members are renamed or the current predicate is rewritten.
    /// See <c>ExcelExportWorker</c> as of commit ffc840d.
    /// </summary>
    private static Func<ExcelExportJob, bool> StaleWorkerClaimQuery(DateTime now)
        => j => (int)j.Status == 0
            || ((int)j.Status == 1 && j.LeaseExpiresAtUtc != null && j.LeaseExpiresAtUtc < now);

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

    private static async Task<ExcelExportJob> EnqueueAsync(ExcelExportJobService service, ApplicationDbContext db)
    {
        var result = await service.EnqueueAsync(
            "AccountSummaryReport",
            new AccountSummaryReportRequest { FromDate = new DateTime(2025, 2, 1), ToDate = new DateTime(2025, 2, 28) },
            new DateTime(2025, 2, 28),
            "tester");

        return await db.ExcelExportJobs.SingleAsync(j => j.Id == result.JobId);
    }

    [Fact]
    public async Task A_newly_queued_job_is_invisible_to_a_stale_worker()
    {
        var (service, db) = Create(nameof(A_newly_queued_job_is_invisible_to_a_stale_worker));

        var job = await EnqueueAsync(service, db);

        Assert.Equal(ExcelExportJobStatus.QueuedV2, job.Status);
        Assert.False(StaleWorkerClaimQuery(DateTime.UtcNow)(job));
    }

    [Fact]
    public async Task A_claimed_job_whose_lease_expired_is_still_invisible_to_a_stale_worker()
    {
        // The leak this closes: guarding only the queued state leaves the orphan-reclaim branch
        // (Processing + expired lease) open, so a stale worker takes the job the moment a lease
        // lapses — which is precisely when a worker has died and a reclaim is due.
        var (service, db) = Create(nameof(A_claimed_job_whose_lease_expired_is_still_invisible_to_a_stale_worker));
        var job = await EnqueueAsync(service, db);

        job.Status = ExcelExportJobStatus.ProcessingV2;
        job.LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        Assert.False(StaleWorkerClaimQuery(DateTime.UtcNow)(job));
    }

    [Fact]
    public void A_retry_never_requeues_a_job_a_stale_worker_could_take()
    {
        var job = new ExcelExportJob { Status = ExcelExportWorker.RequeueStatus };

        Assert.Equal(ExcelExportJobStatus.QueuedV2, ExcelExportWorker.RequeueStatus);
        Assert.False(StaleWorkerClaimQuery(DateTime.UtcNow)(job));
    }

    [Fact]
    public async Task The_current_worker_can_still_claim_what_it_enqueues()
    {
        // The other half: a guard that also locked THIS build out would stall every export in the
        // system, so assert the real predicate the worker uses — not a copy of it.
        var (service, db) = Create(nameof(The_current_worker_can_still_claim_what_it_enqueues));
        await EnqueueAsync(service, db);

        var now = DateTime.UtcNow;
        Assert.Single(await db.ExcelExportJobs.Where(ExcelExportWorker.Claimable(now)).ToListAsync());
    }

    [Theory]
    // Legacy rows written before this build must keep draining, or an export queued across a
    // deploy would be stranded forever.
    [InlineData(ExcelExportJobStatus.Queued, false, true)]
    [InlineData(ExcelExportJobStatus.QueuedV2, false, true)]
    [InlineData(ExcelExportJobStatus.Processing, true, true)]
    [InlineData(ExcelExportJobStatus.ProcessingV2, true, true)]
    [InlineData(ExcelExportJobStatus.Processing, false, false)]
    [InlineData(ExcelExportJobStatus.ProcessingV2, false, false)]
    [InlineData(ExcelExportJobStatus.Completed, false, false)]
    [InlineData(ExcelExportJobStatus.Failed, false, false)]
    public void The_current_worker_claims_exactly_the_right_states(
        ExcelExportJobStatus status, bool leaseExpired, bool expectedClaimable)
    {
        var now = DateTime.UtcNow;
        var job = new ExcelExportJob
        {
            Status = status,
            LeaseExpiresAtUtc = leaseExpired ? now.AddMinutes(-5) : null
        };

        Assert.Equal(expectedClaimable, ExcelExportWorker.Claimable(now).Compile()(job));
    }

    [Theory]
    // The Exports drive polls on exactly these strings and indexes its tag colours by them
    // (ExportsDrive.tsx), so the guard's internal states must not reach the wire.
    [InlineData(ExcelExportJobStatus.QueuedV2, "Queued")]
    [InlineData(ExcelExportJobStatus.ProcessingV2, "Processing")]
    [InlineData(ExcelExportJobStatus.Queued, "Queued")]
    [InlineData(ExcelExportJobStatus.Processing, "Processing")]
    [InlineData(ExcelExportJobStatus.Completed, "Completed")]
    [InlineData(ExcelExportJobStatus.Failed, "Failed")]
    public void The_wire_status_stays_one_of_the_four_names_the_frontend_knows(
        ExcelExportJobStatus status, string expected)
        => Assert.Equal(expected, ExcelExportController.StatusName(status));

    [Fact]
    public async Task An_identical_request_that_is_already_queued_is_not_enqueued_twice()
    {
        // The dedup query has to know about the new in-flight values too, or every repeat click
        // would queue another full export instead of reporting "already being generated".
        var (service, db) = Create(nameof(An_identical_request_that_is_already_queued_is_not_enqueued_twice));

        await EnqueueAsync(service, db);
        var second = await service.EnqueueAsync(
            "AccountSummaryReport",
            new AccountSummaryReportRequest { FromDate = new DateTime(2025, 2, 1), ToDate = new DateTime(2025, 2, 28) },
            new DateTime(2025, 2, 28),
            "tester");

        Assert.Equal(EnqueueStatus.Processing, second.Status);
        Assert.Equal(1, await db.ExcelExportJobs.CountAsync());
    }

    [Fact]
    public void The_claim_predicate_translates_to_sql()
    {
        // The in-memory provider evaluates predicates in C#, so it cannot tell us whether the real
        // provider can translate this one -- and the worker uses it inside ExecuteUpdateAsync,
        // where an untranslatable expression throws at runtime and fails EVERY export. Uses the
        // SqlServer provider with a dummy connection string: ToQueryString compiles the query
        // without opening a connection.
        using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=none;Database=none;Trusted_Connection=True;")
                .Options);

        var id = Guid.NewGuid();
        var sql = db.ExcelExportJobs
            .Where(j => j.Id == id)
            .Where(ExcelExportWorker.Claimable(new DateTime(2026, 9, 9, 8, 0, 0, DateTimeKind.Utc)))
            .ToQueryString();

        // All four claimable states reach the WHERE clause and the lease guard survives. Asserted
        // on meaning, not on EF's formatting: it currently folds the disjunction into
        // "[Status] IN (4, 0) OR ([Status] IN (5, 1) AND [LeaseExpiresAtUtc] ...)", and a future
        // EF may emit equalities instead.
        var where = sql[sql.IndexOf("WHERE", StringComparison.Ordinal)..];
        Assert.Contains("[Status]", where, StringComparison.Ordinal);
        Assert.Contains("[LeaseExpiresAtUtc] IS NOT NULL", where, StringComparison.Ordinal);

        var numbers = Regex.Matches(where, @"(?<![\w.])\d+(?![\w.])").Select(m => m.Value).ToHashSet();
        var claimableStates = new HashSet<string>
        {
            ((int)ExcelExportJobStatus.QueuedV2).ToString(),
            ((int)ExcelExportJobStatus.Queued).ToString(),
            ((int)ExcelExportJobStatus.ProcessingV2).ToString(),
            ((int)ExcelExportJobStatus.Processing).ToString(),
        };

        Assert.Superset(claimableStates, numbers);
    }

    [Fact]
    public void The_stored_status_numbers_are_a_contract()
    {
        // Stored as a plain int, and the backend never runs migrations, so renumbering an existing
        // member would silently reinterpret every row already in TemplateDB.
        Assert.Equal(0, (int)ExcelExportJobStatus.Queued);
        Assert.Equal(1, (int)ExcelExportJobStatus.Processing);
        Assert.Equal(2, (int)ExcelExportJobStatus.Completed);
        Assert.Equal(3, (int)ExcelExportJobStatus.Failed);
        Assert.Equal(4, (int)ExcelExportJobStatus.QueuedV2);
        Assert.Equal(5, (int)ExcelExportJobStatus.ProcessingV2);
    }
}
