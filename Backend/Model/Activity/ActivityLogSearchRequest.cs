using System;
using System.Text.Json;

namespace API.Model.Activity
{
    /// <summary>
    /// Admin search filter for the activity log. Extends the standard paging/sorting
    /// contract (<see cref="ReportQueryRequest"/>) the grid sends, with audit-specific
    /// filters layered on top before paging.
    /// </summary>
    public class ActivityLogSearchRequest : ReportQueryRequest
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? UserId { get; set; }
        public string? EventType { get; set; }
    }

    /// <summary>Payload posted by the frontend tracker for a client-side event.</summary>
    public class ClientActivityEvent
    {
        /// <summary>Navigation | Logout | Click (free-form; capped server-side).</summary>
        public string? EventType { get; set; }

        /// <summary>The client route / target involved (e.g. "/Report/AccountSummaryReport").</summary>
        public string? Path { get; set; }

        /// <summary>Optional extra context (report title, label, etc.). Stored as JSON.</summary>
        public JsonElement? Details { get; set; }
    }
}
