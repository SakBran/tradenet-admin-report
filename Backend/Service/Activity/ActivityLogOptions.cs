namespace API.Service.Activity
{
    /// <summary>
    /// Bound from the "ActivityLog" config section. All have safe defaults so the
    /// feature works without explicit config.
    /// </summary>
    public sealed class ActivityLogOptions
    {
        public const string SectionName = "ActivityLog";

        /// <summary>Master on/off switch. When false, nothing is captured or written.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Entries older than this are purged by the cleanup worker (default ~12 months).</summary>
        public int RetentionDays { get; set; } = 365;

        /// <summary>Cleanup sweep interval (minutes).</summary>
        public int CleanupIntervalMinutes { get; set; } = 360;

        /// <summary>Max captured request-body size (bytes); larger bodies are truncated.</summary>
        public int MaxBodyBytes { get; set; } = 32 * 1024;

        /// <summary>In-memory queue capacity. On overflow the oldest queued entry is dropped (never blocks a request).</summary>
        public int QueueCapacity { get; set; } = 10000;

        /// <summary>Max rows the writer inserts per DB round-trip.</summary>
        public int BatchSize { get; set; } = 200;
    }
}
