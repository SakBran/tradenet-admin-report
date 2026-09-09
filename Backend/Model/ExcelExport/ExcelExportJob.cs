using System;

namespace API.Model.ExcelExport
{
    /// <summary>
    /// Lifecycle of a queued Excel export job.
    ///
    /// <para>
    /// <b>Why there are two "queued" and two "processing" values.</b> Every
    /// <c>ExcelExportWorker</c> that can reach TemplateDB competes for the same rows, so an OLDER
    /// API instance left running anywhere (see <c>docs/BorderExportPermitComplaints_2026-09-07.md</c>)
    /// silently produces a share of every report's exports from a stale build. Its claim query is
    /// already compiled against the literals <c>0</c> and <c>1</c> and cannot be changed, so new
    /// jobs are enqueued as <see cref="QueuedV2"/> and claimed as <see cref="ProcessingV2"/>
    /// instead: values it has no name for and therefore never matches. That starves it
    /// deterministically rather than leaving each export a coin toss.
    /// </para>
    /// <para>
    /// The numbers are the contract — this is stored as a plain <c>int</c> and the backend never
    /// runs migrations, so never renumber an existing member. <see cref="Queued"/> and
    /// <see cref="Processing"/> are still claimable so rows already in the table drain, and the
    /// wire format is unchanged: <c>ExcelExportController</c> reports both queued values as
    /// "Queued" and both processing values as "Processing".
    /// </para>
    /// </summary>
    public enum ExcelExportJobStatus
    {
        /// <summary>Legacy queued state. Still claimable; no longer written.</summary>
        Queued = 0,

        /// <summary>Legacy claimed state. Still reclaimable when its lease expires; no longer written.</summary>
        Processing = 1,

        Completed = 2,
        Failed = 3,

        /// <summary>
        /// Queued, and claimable only by a build that knows this value. What
        /// <c>ExcelExportJobService.EnqueueAsync</c> writes, and what a retry falls back to.
        /// </summary>
        QueuedV2 = 4,

        /// <summary>
        /// Claimed counterpart of <see cref="QueuedV2"/>. Needed as well as the queued value:
        /// guarding only the queue would still let a stale worker take the job through its
        /// orphan-reclaim branch (<c>Processing</c> + expired lease) the moment a lease lapsed.
        /// </summary>
        ProcessingV2 = 5
    }

    /// <summary>
    /// A queued/finished Excel export, stored in TemplateDB. The worker reads
    /// <see cref="RequestJson"/> back through the handler keyed by
    /// <see cref="ReportKey"/> to rebuild the query, and writes the generated
    /// file to <see cref="FilePath"/>. Identical requests are matched on
    /// <see cref="FilterHash"/>.
    /// </summary>
    public class ExcelExportJob
    {
        public Guid Id { get; set; }

        /// <summary>Registry key identifying which report handler to run (e.g. "MemberRegistrationReport").</summary>
        public string ReportKey { get; set; } = string.Empty;

        /// <summary>Worksheet / display title.</summary>
        public string ReportTitle { get; set; } = string.Empty;

        /// <summary>sha256 of report key + normalized request JSON; used to dedup/reuse.</summary>
        public string FilterHash { get; set; } = string.Empty;

        /// <summary>The serialized report request, used to rebuild the query in the background.</summary>
        public string RequestJson { get; set; } = string.Empty;

        public ExcelExportJobStatus Status { get; set; } = ExcelExportJobStatus.Queued;

        /// <summary>Path on disk (relative to the configured storage root) once generated.</summary>
        public string? FilePath { get; set; }

        /// <summary>Friendly download file name, e.g. MemberRegistrationReport_20260602_153012.xlsx.</summary>
        public string FileName { get; set; } = string.Empty;

        public long? FileSizeBytes { get; set; }

        public int? RowCount { get; set; }

        public int? SheetCount { get; set; }

        /// <summary>True when the requested range was fully in the past at enqueue time (drives reuse).</summary>
        public bool IsPeriodClosed { get; set; }

        public string? RequestedByUserName { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }

        /// <summary>While Processing, the lease expiry lets a restarted app requeue orphans.</summary>
        public DateTime? LeaseExpiresAtUtc { get; set; }

        /// <summary>
        /// "&lt;MachineName&gt;:&lt;worker guid&gt;" of the worker that claimed the job. Kept after the job
        /// finishes (the jobs API exposes it as <c>processedBy</c>) so a file produced by a stale second
        /// worker sharing this queue can be traced to its host. Re-claiming never looks at this column.
        /// </summary>
        public string? LeaseOwner { get; set; }

        public int AttemptCount { get; set; }
    }
}
