using System;

namespace API.Model.ExcelExport
{
    /// <summary>Lifecycle of a queued Excel export job.</summary>
    public enum ExcelExportJobStatus
    {
        Queued = 0,
        Processing = 1,
        Completed = 2,
        Failed = 3
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

        /// <summary>While Processing, the lease holder + expiry let a restarted app requeue orphans.</summary>
        public DateTime? LeaseExpiresAtUtc { get; set; }
        public string? LeaseOwner { get; set; }

        public int AttemptCount { get; set; }
    }
}
