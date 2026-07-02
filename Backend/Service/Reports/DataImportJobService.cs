using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.DBContext;
using API.Model.Reports;
using Microsoft.EntityFrameworkCore;

namespace API.Service.Reports
{
    public interface IDataImportJobService
    {
        Task<DataImportJobDto> EnqueueAsync(
            DataImportRequest request,
            string? requestedByUserName,
            CancellationToken cancellationToken = default);

        Task<DataImportJobDto?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<DataImportJobDto>> ListAsync(
            CancellationToken cancellationToken = default);
    }

    public sealed class DataImportJobService : IDataImportJobService
    {
        private readonly ApplicationDbContext _db;

        public DataImportJobService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<DataImportJobDto> EnqueueAsync(
            DataImportRequest request,
            string? requestedByUserName,
            CancellationToken cancellationToken = default)
        {
            await EnsureJobTableAsync(cancellationToken);

            if (request.StartDate == default || request.EndDate == default)
            {
                throw new ArgumentException("Start date and end date are required.");
            }

            var fromDate = request.StartDate.Date;
            var toDate = request.EndDate.Date;
            if (toDate < fromDate)
            {
                throw new ArgumentException("End date must be greater than or equal to start date.");
            }

            var licenceType = string.IsNullOrWhiteSpace(request.LicenceType)
                ? DataImportService.All
                : request.LicenceType.Trim();

            var totalDays = (toDate - fromDate).Days + 1;
            var now = DateTime.UtcNow;
            var job = new DataImportJob
            {
                Id = Guid.NewGuid(),
                LicenceType = licenceType,
                StartDate = fromDate,
                EndDate = toDate,
                Status = DataImportJobStatus.Queued,
                TotalDays = totalDays,
                ProcessedDays = 0,
                TotalRows = 0,
                RequestedByUserName = requestedByUserName,
                CreatedAtUtc = now,
                AttemptCount = 0
            };

            _db.DataImportJobs.Add(job);
            await _db.SaveChangesAsync(cancellationToken);

            return ToDto(job);
        }

        public async Task<DataImportJobDto?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            await EnsureJobTableAsync(cancellationToken);

            var job = await _db.DataImportJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

            return job == null ? null : ToDto(job);
        }

        public async Task<IReadOnlyList<DataImportJobDto>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            await EnsureJobTableAsync(cancellationToken);

            var jobs = await _db.DataImportJobs
                .AsNoTracking()
                .OrderByDescending(j => j.CreatedAtUtc)
                .Take(100)
                .ToListAsync(cancellationToken);

            return jobs.Select(ToDto).ToList();
        }

        private async Task EnsureJobTableAsync(CancellationToken cancellationToken)
        {
            const string sql = @"
IF OBJECT_ID(N'dbo.DataImportJobs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DataImportJobs
    (
        Id uniqueidentifier NOT NULL CONSTRAINT PK_DataImportJobs PRIMARY KEY,
        LicenceType nvarchar(80) NOT NULL,
        StartDate datetime2 NOT NULL,
        EndDate datetime2 NOT NULL,
        Status int NOT NULL,
        TotalDays int NOT NULL,
        ProcessedDays int NOT NULL,
        TotalRows int NOT NULL,
        RequestedByUserName nvarchar(256) NULL,
        ErrorMessage nvarchar(max) NULL,
        CreatedAtUtc datetime2 NOT NULL,
        StartedAtUtc datetime2 NULL,
        CompletedAtUtc datetime2 NULL,
        LeaseExpiresAtUtc datetime2 NULL,
        LeaseOwner nvarchar(max) NULL,
        AttemptCount int NOT NULL
    );
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_DataImportJobs_CreatedAtUtc'
      AND object_id = OBJECT_ID(N'dbo.DataImportJobs')
)
BEGIN
    CREATE INDEX IX_DataImportJobs_CreatedAtUtc
        ON dbo.DataImportJobs (CreatedAtUtc);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_DataImportJobs_LeaseExpiresAtUtc'
      AND object_id = OBJECT_ID(N'dbo.DataImportJobs')
)
BEGIN
    CREATE INDEX IX_DataImportJobs_LeaseExpiresAtUtc
        ON dbo.DataImportJobs (LeaseExpiresAtUtc);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_DataImportJobs_Status_CreatedAtUtc'
      AND object_id = OBJECT_ID(N'dbo.DataImportJobs')
)
BEGIN
    CREATE INDEX IX_DataImportJobs_Status_CreatedAtUtc
        ON dbo.DataImportJobs (Status, CreatedAtUtc);
END";

            await _db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }

        internal static DataImportJobDto ToDto(DataImportJob job)
        {
            var progressPercent = job.TotalDays <= 0
                ? 0
                : (int)Math.Round(Math.Min(100m, job.ProcessedDays * 100m / job.TotalDays));

            return new DataImportJobDto
            {
                Id = job.Id,
                LicenceType = job.LicenceType,
                StartDate = job.StartDate,
                EndDate = job.EndDate,
                Status = job.Status.ToString(),
                TotalDays = job.TotalDays,
                ProcessedDays = job.ProcessedDays,
                ProgressPercent = progressPercent,
                TotalRows = job.TotalRows,
                RequestedBy = job.RequestedByUserName,
                ErrorMessage = job.ErrorMessage,
                CreatedAtUtc = job.CreatedAtUtc,
                StartedAtUtc = job.StartedAtUtc,
                CompletedAtUtc = job.CompletedAtUtc,
            };
        }
    }

    public sealed class DataImportJobDto
    {
        public Guid Id { get; set; }

        public string LicenceType { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public string Status { get; set; } = string.Empty;

        public int TotalDays { get; set; }

        public int ProcessedDays { get; set; }

        public int ProgressPercent { get; set; }

        public int TotalRows { get; set; }

        public string? RequestedBy { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? StartedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }
    }
}
