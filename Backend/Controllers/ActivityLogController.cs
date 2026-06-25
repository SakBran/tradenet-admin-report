using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using API.DBContext;
using API.Model;
using API.Model.Activity;
using API.Service.Activity;
using API.Service.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ActivityLogController : ControllerBase
    {
        private readonly IActivityLogQueue _queue;
        private readonly ApplicationDbContext _db;
        private readonly ActivityLogOptions _options;

        public ActivityLogController(
            IActivityLogQueue queue,
            ApplicationDbContext db,
            IOptions<ActivityLogOptions> options)
        {
            _queue = queue;
            _db = db;
            _options = options.Value;
        }

        /// <summary>
        /// Ingests a client-side event (page navigation, logout, button click) from the
        /// frontend tracker. Identity and IP are taken from the authenticated request,
        /// never trusted from the payload.
        /// </summary>
        [HttpPost("client")]
        public IActionResult LogClientEvent([FromBody] ClientActivityEvent? evt)
        {
            if (evt == null || !_options.Enabled)
            {
                return Ok();
            }

            var userId = User.FindFirst(ClaimTypes.Name)?.Value;

            var details = evt.Details?.GetRawText();
            if (details != null && details.Length > _options.MaxBodyBytes)
            {
                details = details[.._options.MaxBodyBytes];
            }

            var entry = new ActivityLog
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                UserId = Truncate(userId, 450),
                UserName = Truncate(userId, 256),
                Source = "Client",
                EventType = Truncate(string.IsNullOrWhiteSpace(evt.EventType) ? "Click" : evt.EventType, 50)!,
                IpAddress = Truncate(HttpContext.Connection.RemoteIpAddress?.ToString(), 64),
                UserAgent = Truncate(Request.Headers.UserAgent.ToString(), 512),
                HttpMethod = null,
                Path = Truncate(evt.Path, 512) ?? string.Empty,
                DetailsJson = details,
            };

            _queue.TryEnqueue(entry);
            return Ok();
        }

        /// <summary>
        /// Admin-only paged search over the activity log. Returns the standard
        /// <see cref="ApiResult{T}"/> the grid consumes, so it slots straight into BasicTable.
        /// </summary>
        [HttpPost("search")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResult<ActivityLog>>> Search([FromBody] ActivityLogSearchRequest request)
        {
            request ??= new ActivityLogSearchRequest();

            var query = _db.ActivityLogs.AsNoTracking();

            if (request.FromDate.HasValue)
            {
                query = query.Where(a => a.TimestampUtc >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(a => a.TimestampUtc <= request.ToDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.UserId))
            {
                query = query.Where(a => a.UserId == request.UserId);
            }

            if (!string.IsNullOrWhiteSpace(request.EventType))
            {
                query = query.Where(a => a.EventType == request.EventType);
            }

            // Default newest-first; a user-clicked column sort (request.SortColumn) overrides this downstream.
            query = query.OrderByDescending(a => a.TimestampUtc);

            var result = await ReportQueryService.CreatePagedResultAsync(query, request);
            return Ok(result);
        }

        private static string? Truncate(string? value, int max)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.Length <= max ? value : value[..max];
        }
    }
}
