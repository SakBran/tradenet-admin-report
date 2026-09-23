using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DBContext;
using API.Model.ExcelExport;
using API.Service.ExcelExport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers
{
    /// <summary>
    /// The shared "Exports drive": list every generated export, poll status,
    /// download (auth-gated, verifies the file is on disk first), and delete.
    /// Enqueueing happens on each report controller's own [HttpPost("Excel")].
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ExcelExportController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IExcelExportFileStore _fileStore;
        private readonly TradeNetDbContext _tradeNet;

        public ExcelExportController(ApplicationDbContext db, IExcelExportFileStore fileStore, TradeNetDbContext tradeNet)
        {
            _db = db;
            _fileStore = fileStore;
            _tradeNet = tradeNet;
        }

        /// <summary>All exports, newest first (shared visibility).</summary>
        [HttpGet("jobs")]
        public async Task<ActionResult> GetJobs()
        {
            var jobs = await _db.ExcelExportJobs
                .OrderByDescending(j => j.CreatedAtUtc)
                .ToListAsync();

            var names = await ResolveUserNamesAsync(jobs.Select(j => j.RequestedByUserName));
            return Ok(jobs.Select(j => ToDto(j, names)));
        }

        /// <summary>Single job status (for polling).</summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult> GetJob(Guid id)
        {
            var job = await _db.ExcelExportJobs.FirstOrDefaultAsync(j => j.Id == id);
            if (job == null)
            {
                return NotFound();
            }

            var names = await ResolveUserNamesAsync(new[] { job.RequestedByUserName });
            return Ok(ToDto(job, names));
        }

        [HttpGet("{id:guid}/download")]
        public async Task<IActionResult> Download(Guid id)
        {
            var job = await _db.ExcelExportJobs.FirstOrDefaultAsync(j => j.Id == id);
            if (job == null)
            {
                return NotFound();
            }

            if (job.Status != ExcelExportJobStatus.Completed)
            {
                return Conflict($"Export is not ready (status: {StatusName(job.Status)}).");
            }

            // Don't serve blindly: confirm the file actually exists on disk.
            if (!_fileStore.Exists(job.FilePath))
            {
                return StatusCode(StatusCodes.Status410Gone, "The export file is no longer available. Please regenerate it.");
            }

            var stream = _fileStore.OpenRead(job.FilePath!);
            return File(stream, StreamingExcelWriter.ContentType, job.FileName);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var job = await _db.ExcelExportJobs.FirstOrDefaultAsync(j => j.Id == id);
            if (job == null)
            {
                return NotFound();
            }

            try { _fileStore.Delete(job.FilePath); } catch { /* best effort */ }

            _db.ExcelExportJobs.Remove(job);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// The four wire statuses. <see cref="ExcelExportJobStatus.QueuedV2"/> and
        /// <see cref="ExcelExportJobStatus.ProcessingV2"/> are an internal guard against a stale
        /// worker claiming jobs (see that enum), not a new user-visible state, so they report as
        /// their legacy names. The Exports drive polls on exactly these strings and indexes its
        /// tag colours by them (<c>ExportsDrive.tsx</c>), so leaking "QueuedV2" here would stop
        /// the auto-refresh and blank the tag.
        /// </summary>
        internal static string StatusName(ExcelExportJobStatus status) => status switch
        {
            ExcelExportJobStatus.QueuedV2 => nameof(ExcelExportJobStatus.Queued),
            ExcelExportJobStatus.ProcessingV2 => nameof(ExcelExportJobStatus.Processing),
            _ => status.ToString()
        };

        /// <summary>
        /// Jobs store the JWT name claim, which is the TradeNet <c>User.Id</c>
        /// (JWTManagerService), not a name. The users live in TradeNetDB and the jobs in
        /// TemplateDB, so no SQL join is possible: one batched lookup per request instead.
        /// Best effort — if TradeNetDB is unreachable the drive still lists every job with
        /// the raw id rather than failing.
        /// </summary>
        private async Task<IReadOnlyDictionary<string, string>> ResolveUserNamesAsync(IEnumerable<string?> requestedBy)
        {
            var ids = requestedBy
                .Select(v => int.TryParse(v, out var id) ? id : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return new Dictionary<string, string>();
            }

            try
            {
                return await _tradeNet.Users
                    .AsNoTracking()
                    .Where(u => ids.Contains(u.Id))
                    .Select(u => new { u.Id, u.FullName })
                    .ToDictionaryAsync(u => u.Id.ToString(), u => u.FullName);
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        internal static string? DisplayName(string? requestedBy, IReadOnlyDictionary<string, string> names)
            => requestedBy != null && names.TryGetValue(requestedBy, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : requestedBy;

        private static object ToDto(ExcelExportJob j, IReadOnlyDictionary<string, string> names) => new
        {
            id = j.Id,
            reportKey = j.ReportKey,
            reportTitle = j.ReportTitle,
            status = StatusName(j.Status),
            fileName = j.FileName,
            fileSizeBytes = j.FileSizeBytes,
            rowCount = j.RowCount,
            sheetCount = j.SheetCount,
            isPeriodClosed = j.IsPeriodClosed,
            // The user's full name; falls back to the stored value (an id with no user
            // row, or an older job) so the column is never blank.
            requestedBy = DisplayName(j.RequestedByUserName, names),
            requestedById = j.RequestedByUserName,
            // "<machine>:<guid>@<build>" of the worker that produced (or is producing) the file.
            // Two API instances sharing one TemplateDB both used to claim jobs, and a stale build
            // among them wrote stale sheets (2026-09-07: the Border Export Permit Voucher footer;
            // 2026-09-09: the Account Summary DCCA export). QueuedV2 now keeps such a build from
            // claiming at all; this field is how to confirm which host and which build did.
            // A null here means a worker older than ffc840d, and no @build means older than the
            // commit that added the stamp.
            processedBy = j.LeaseOwner,
            errorMessage = j.ErrorMessage,
            createdAtUtc = j.CreatedAtUtc,
            startedAtUtc = j.StartedAtUtc,
            completedAtUtc = j.CompletedAtUtc,
            expiresAtUtc = j.ExpiresAtUtc,
            downloadUrl = j.Status == ExcelExportJobStatus.Completed ? $"ExcelExport/{j.Id}/download" : null
        };
    }
}
