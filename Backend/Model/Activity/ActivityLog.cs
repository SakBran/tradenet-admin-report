using System;

namespace API.Model.Activity
{
    /// <summary>
    /// One row of the user-activity audit trail. Captures sign-ins, server-side API
    /// actions (searches / report views / exports / drill-downs — every authenticated
    /// request) and client-side events (page navigation, logout). Rows are produced off
    /// the request path via the activity-log queue + background writer and stored in
    /// TemplateDB (the app's metadata DB, alongside Users/ExcelExportJobs).
    /// </summary>
    public class ActivityLog
    {
        public Guid Id { get; set; }

        /// <summary>When the activity happened (UTC).</summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// The acting user's id (JWT <c>ClaimTypes.Name</c>). For sign-in events the
        /// claims aren't populated yet, so this falls back to the login name. Null when
        /// the request is anonymous and carries no identifying name.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>Human-readable user name when known (e.g. the sign-in name).</summary>
        public string? UserName { get; set; }

        /// <summary>
        /// "Server" (captured by <see cref="API.Middleware.ActivityLoggingMiddleware"/>)
        /// or "Client" (posted by the frontend tracker).
        /// </summary>
        public string Source { get; set; } = "Server";

        /// <summary>SignIn | SignInFailed | ApiRequest | Navigation | Logout | Click.</summary>
        public string EventType { get; set; } = "ApiRequest";

        /// <summary>Client IP (forwarded-header aware — see Program.cs UseForwardedHeaders).</summary>
        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public string? HttpMethod { get; set; }

        /// <summary>Request path (server events) or client route (client events).</summary>
        public string Path { get; set; } = string.Empty;

        public string? QueryString { get; set; }

        /// <summary>
        /// Request body / client payload as JSON — password-redacted and size-capped.
        /// Null when there is no body (e.g. GET) or it was not JSON.
        /// </summary>
        public string? DetailsJson { get; set; }

        public int? StatusCode { get; set; }

        public int? DurationMs { get; set; }
    }
}
