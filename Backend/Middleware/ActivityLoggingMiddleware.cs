using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using API.Model.Activity;
using API.Service.Activity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Middleware
{
    /// <summary>
    /// Captures one activity-log entry per authenticated API request: who (JWT name),
    /// from where (forwarded client IP), what (method + path + query + JSON body),
    /// the outcome (status code) and how long it took. The entry is handed to the
    /// background queue — it never blocks or fails the request. Placed after
    /// authentication so user claims and the real client IP are both populated.
    /// </summary>
    public sealed class ActivityLoggingMiddleware
    {
        private const string AuthPathPrefix = "/api/auth";
        private const string ActivityLogPathPrefix = "/api/activitylog";

        // Any body key containing one of these (case-insensitive) is replaced before storage.
        private static readonly string[] SensitiveKeyFragments = { "password", "pwd", "secret", "token" };

        private readonly RequestDelegate _next;
        private readonly IActivityLogQueue _queue;
        private readonly ActivityLogOptions _options;
        private readonly ILogger<ActivityLoggingMiddleware> _logger;

        public ActivityLoggingMiddleware(
            RequestDelegate next,
            IActivityLogQueue queue,
            IOptions<ActivityLogOptions> options,
            ILogger<ActivityLoggingMiddleware> logger)
        {
            _next = next;
            _queue = queue;
            _options = options.Value;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_options.Enabled || ShouldSkip(context))
            {
                await _next(context);
                return;
            }

            // Read the body BEFORE the pipeline consumes it (buffering lets MVC re-read it).
            var body = await TryCaptureBodyAsync(context.Request);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                try
                {
                    Enqueue(context, body, (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                }
                catch (Exception ex)
                {
                    // Logging must never surface as a request failure.
                    _logger.LogWarning(ex, "Failed to enqueue activity-log entry for {Path}.", context.Request.Path);
                }
            }
        }

        private static bool ShouldSkip(HttpContext context)
        {
            if (HttpMethods.IsOptions(context.Request.Method))
            {
                return true; // CORS preflight noise
            }

            var path = context.Request.Path.Value ?? string.Empty;

            // Only API traffic is meaningful here; static files / swagger / SPA assets are skipped.
            if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Never log the activity log's own endpoints (would recurse on every client post).
            return path.StartsWith(ActivityLogPathPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string?> TryCaptureBodyAsync(HttpRequest request)
        {
            if (!(HttpMethods.IsPost(request.Method) || HttpMethods.IsPut(request.Method)))
            {
                return null;
            }

            var contentType = request.ContentType;
            if (string.IsNullOrEmpty(contentType) || !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (request.ContentLength is long length && length > _options.MaxBodyBytes)
            {
                return "[body omitted: too large]";
            }

            try
            {
                request.EnableBuffering();
                using var reader = new StreamReader(
                    request.Body,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024,
                    leaveOpen: true);
                var raw = await reader.ReadToEndAsync();
                request.Body.Position = 0;

                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                if (raw.Length > _options.MaxBodyBytes)
                {
                    raw = raw[.._options.MaxBodyBytes];
                }

                return Redact(raw);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not capture request body for activity log.");
                return null;
            }
        }

        private void Enqueue(HttpContext context, string? body, int durationMs)
        {
            var request = context.Request;
            var path = request.Path.Value ?? string.Empty;
            var status = context.Response.StatusCode;
            var isAuth = path.StartsWith(AuthPathPrefix, StringComparison.OrdinalIgnoreCase);

            var userId = context.User?.FindFirst(ClaimTypes.Name)?.Value;
            var userName = userId;
            var eventType = "ApiRequest";

            if (isAuth)
            {
                // Login is anonymous, so identity comes from the (redacted) request body,
                // and success/failure from the response status.
                eventType = status is >= 200 and < 300 ? "SignIn" : "SignInFailed";
                var actor = TryReadName(body);
                if (!string.IsNullOrEmpty(actor))
                {
                    userName = actor;
                    userId ??= actor;
                }
            }

            var userAgent = request.Headers.UserAgent.ToString();

            var entry = new ActivityLog
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                UserId = Truncate(userId, 450),
                UserName = Truncate(userName, 256),
                Source = "Server",
                EventType = eventType,
                IpAddress = Truncate(context.Connection.RemoteIpAddress?.ToString(), 64),
                UserAgent = string.IsNullOrEmpty(userAgent) ? null : Truncate(userAgent, 512),
                HttpMethod = Truncate(request.Method, 16),
                Path = Truncate(path, 512) ?? string.Empty,
                QueryString = request.QueryString.HasValue ? Truncate(request.QueryString.Value, 1024) : null,
                DetailsJson = body,
                StatusCode = status,
                DurationMs = durationMs,
            };

            _queue.TryEnqueue(entry);
        }

        /// <summary>Replaces sensitive values in a JSON body; returns a marker if it can't be parsed.</summary>
        private static string Redact(string json)
        {
            try
            {
                var node = JsonNode.Parse(json);
                RedactNode(node);
                return node?.ToJsonString() ?? json;
            }
            catch
            {
                return "[redacted: body not valid JSON]";
            }
        }

        private static void RedactNode(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var key in obj.Select(kv => kv.Key).ToList())
                    {
                        if (SensitiveKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                        {
                            obj[key] = "***";
                        }
                        else
                        {
                            RedactNode(obj[key]);
                        }
                    }
                    break;
                case JsonArray array:
                    foreach (var item in array)
                    {
                        RedactNode(item);
                    }
                    break;
            }
        }

        /// <summary>Pulls the login name from a (redacted) auth body: tolerant of "Name"/"name".</summary>
        private static string? TryReadName(string? body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return null;
            }

            try
            {
                if (JsonNode.Parse(body) is JsonObject obj)
                {
                    foreach (var kv in obj)
                    {
                        if (string.Equals(kv.Key, "name", StringComparison.OrdinalIgnoreCase))
                        {
                            return kv.Value?.GetValue<string>();
                        }
                    }
                }
            }
            catch
            {
                // ignore — actor stays null
            }

            return null;
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
