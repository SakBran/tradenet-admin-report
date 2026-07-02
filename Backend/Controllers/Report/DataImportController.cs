using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Service.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers.Report
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DataImportController : ControllerBase
    {
        private readonly IDataImportService _dataImportService;
        private readonly IDataImportJobService _dataImportJobService;

        public DataImportController(
            IDataImportService dataImportService,
            IDataImportJobService dataImportJobService)
        {
            _dataImportService = dataImportService;
            _dataImportJobService = dataImportJobService;
        }

        [HttpGet("LicenceTypes")]
        public ActionResult<IEnumerable<DataImportLicenceTypeOption>> GetLicenceTypes()
        {
            return Ok(_dataImportService.GetLicenceTypes());
        }

        [HttpGet("Status")]
        public async Task<ActionResult<DataImportStatusResult>> GetStatus(
            [FromQuery] DateTime? date,
            CancellationToken cancellationToken)
        {
            return Ok(await _dataImportService.GetStatusAsync(
                (date ?? DateTime.Today.AddDays(-1)).Date,
                cancellationToken));
        }

        [HttpGet("CalendarStatus")]
        public async Task<ActionResult<DataImportCalendarStatusResult>> GetCalendarStatus(
            [FromQuery] int? year,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _dataImportService.GetCalendarStatusAsync(
                    year ?? DateTime.Today.Year,
                    cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("jobs")]
        public async Task<ActionResult<IReadOnlyList<DataImportJobDto>>> GetJobs(
            CancellationToken cancellationToken)
        {
            return Ok(await _dataImportJobService.ListAsync(cancellationToken));
        }

        [HttpGet("jobs/{id:guid}")]
        public async Task<ActionResult<DataImportJobDto>> GetJob(
            Guid id,
            CancellationToken cancellationToken)
        {
            var job = await _dataImportJobService.GetAsync(id, cancellationToken);
            return job == null ? NotFound() : Ok(job);
        }

        [HttpPost("jobs")]
        public async Task<ActionResult<DataImportJobDto>> PostJob(
            [FromBody] DataImportRequest? request,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            try
            {
                var job = await _dataImportJobService.EnqueueAsync(
                    request,
                    User.FindFirst(ClaimTypes.Name)?.Value,
                    cancellationToken);

                return Ok(job);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public Task<ActionResult<DataImportJobDto>> Post(
            [FromBody] DataImportRequest? request,
            CancellationToken cancellationToken)
        {
            return PostJob(request, cancellationToken);
        }
    }
}
