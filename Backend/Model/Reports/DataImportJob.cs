using System;

namespace API.Model.Reports
{
    public enum DataImportJobStatus
    {
        Queued = 0,
        Processing = 1,
        Completed = 2,
        Failed = 3
    }

    public sealed class DataImportJob
    {
        public Guid Id { get; set; }

        public string LicenceType { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public DataImportJobStatus Status { get; set; } = DataImportJobStatus.Queued;

        public int TotalDays { get; set; }

        public int ProcessedDays { get; set; }

        public int TotalRows { get; set; }

        public string? RequestedByUserName { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? StartedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public DateTime? LeaseExpiresAtUtc { get; set; }

        public string? LeaseOwner { get; set; }

        public int AttemptCount { get; set; }
    }
}
