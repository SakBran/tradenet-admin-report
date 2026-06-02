using System;
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

        public ExcelExportController(ApplicationDbContext db, IExcelExportFileStore fileStore)
        {
            _db = db;
            _fileStore = fileStore;
        }

        /// <summary>All exports, newest first (shared visibility).</summary>
        [HttpGet("jobs")]
        public async Task<ActionResult> GetJobs()
        {
            var jobs = await _db.ExcelExportJobs
                .OrderByDescending(j => j.CreatedAtUtc)
                .ToListAsync();

            return Ok(jobs.Select(ToDto));
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

            return Ok(ToDto(job));
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
                return Conflict($"Export is not ready (status: {job.Status}).");
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

        private static object ToDto(ExcelExportJob j) => new
        {
            id = j.Id,
            reportKey = j.ReportKey,
            reportTitle = j.ReportTitle,
            status = j.Status.ToString(),
            fileName = j.FileName,
            fileSizeBytes = j.FileSizeBytes,
            rowCount = j.RowCount,
            sheetCount = j.SheetCount,
            isPeriodClosed = j.IsPeriodClosed,
            requestedBy = j.RequestedByUserName,
            errorMessage = j.ErrorMessage,
            createdAtUtc = j.CreatedAtUtc,
            startedAtUtc = j.StartedAtUtc,
            completedAtUtc = j.CompletedAtUtc,
            expiresAtUtc = j.ExpiresAtUtc,
            downloadUrl = j.Status == ExcelExportJobStatus.Completed ? $"ExcelExport/{j.Id}/download" : null
        };
    }
}
