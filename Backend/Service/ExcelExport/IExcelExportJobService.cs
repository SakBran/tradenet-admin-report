using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace API.Service.ExcelExport
{
    public enum EnqueueStatus
    {
        /// <summary>A finished, still-valid file already exists — reuse it.</summary>
        Ready,
        /// <summary>A new job was queued for the background worker.</summary>
        Queued,
        /// <summary>An identical request is already queued/processing — wait for it.</summary>
        Processing
    }

    /// <summary>What the enqueue endpoint returns to the client.</summary>
    public sealed class EnqueueResult
    {
        // Serialize as "Ready"/"Queued"/"Processing" so the frontend can switch on it.
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EnqueueStatus Status { get; init; }
        public Guid JobId { get; init; }
        public string? FileName { get; init; }
        public string? DownloadUrl { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public interface IExcelExportJobService
    {
        /// <summary>
        /// Applies the dedup/reuse rule and either returns a ready file, reports an
        /// in-flight duplicate, or queues a new job. <paramref name="toDate"/> drives
        /// the closed-period reuse decision.
        /// </summary>
        Task<EnqueueResult> EnqueueAsync(
            string reportKey,
            object request,
            DateTime toDate,
            string? requestedByUserName);
    }
}
