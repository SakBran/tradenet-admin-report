using System;
using System.Collections.Generic;
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

        public DataImportController(IDataImportService dataImportService)
        {
            _dataImportService = dataImportService;
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

        [HttpPost]
        public async Task<ActionResult<DataImportResult>> Post(
            [FromBody] DataImportRequest? request,
            CancellationToken cancellationToken)
        {
            if (request is null || request.StartDate == default || request.EndDate == default)
            {
                return BadRequest("Start date and end date are required.");
            }

            try
            {
                return Ok(await _dataImportService.ImportAsync(
                    request.LicenceType,
                    request.StartDate,
                    request.EndDate,
                    cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
