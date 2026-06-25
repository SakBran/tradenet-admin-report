namespace API.Service.ExcelExport
{
    /// <summary>
    /// Bound from the "ExcelExport" config section. All have safe defaults so the
    /// feature works without explicit config.
    /// </summary>
    public sealed class ExcelExportOptions
    {
        public const string SectionName = "ExcelExport";

        /// <summary>
        /// Storage backend for generated files: "Local" (default — disk under <see cref="StorageRoot"/>)
        /// or "Ftp" (upload to an FTP server, see <see cref="Ftp"/>). Case-insensitive.
        /// </summary>
        public string Storage { get; set; } = "Local";

        /// <summary>
        /// Folder where generated .xlsx files live (outside wwwroot — downloads are auth-gated).
        /// Used only when <see cref="Storage"/> is "Local".
        /// Empty/whitespace → a per-OS system temp folder (always writable by the published process);
        /// an absolute path is used as-is; a relative path is resolved under ContentRootPath.
        /// </summary>
        public string StorageRoot { get; set; } = "";

        /// <summary>FTP connection settings, used only when <see cref="Storage"/> is "Ftp".</summary>
        public ExcelExportFtpOptions Ftp { get; set; } = new();

        /// <summary>Rows fetched + written per chunk to keep memory flat.</summary>
        public int ChunkSize { get; set; } = 5000;

        /// <summary>How long a finished file is kept before cleanup deletes it.</summary>
        public int RetentionHours { get; set; } = 24;

        /// <summary>Max jobs generated concurrently by the worker.</summary>
        public int MaxConcurrency { get; set; } = 1;

        /// <summary>Worker poll interval (seconds) when the queue is empty.</summary>
        public int PollSeconds { get; set; } = 5;

        /// <summary>A claimed job's lease length; a crashed worker's job is requeued after this.</summary>
        public int LeaseMinutes { get; set; } = 30;

        /// <summary>Max generation attempts before a job is marked Failed.</summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>Cleanup sweep interval (minutes).</summary>
        public int CleanupIntervalMinutes { get; set; } = 30;
    }
}
