using System;

namespace API.StoredProcedureToLinq;

/// <summary>
/// Date-window helpers shared by the listing reports that must return exactly what the
/// original Tradenet 2.0 admin returned.
/// </summary>
public static class ReportDateWindow
{
    /// <summary>
    /// Makes a report's "to" date inclusive of the whole day, the way the old admin app did.
    /// </summary>
    /// <remarks>
    /// The old app (<c>Business/Reports.cs</c>) always sent <c>@ToDate = "&lt;day&gt; 23:59:59"</c>
    /// and its stored procedures filtered <c>CreatedDate &lt;= @ToDate</c>. This front end already
    /// sends <c>T23:59:59</c>, so this is a no-op for UI traffic; it only upgrades a date-only
    /// (midnight) <c>ToDate</c> — Swagger, drill-down links, direct API callers — to the whole day,
    /// so those callers do not silently lose the last day once the procedures stop using
    /// <c>DATEADD(day, 1, @ToDate)</c>.
    ///
    /// Uses <c>AddSeconds(-1)</c>, NOT <c>AddTicks(-1)</c>: a <c>SqlParameter</c> of type
    /// <c>datetime</c> has 1/300-second accuracy, so 23:59:59.9999999 rounds UP to the next day's
    /// midnight and would re-admit rows the old system excluded.
    /// </remarks>
    public static DateTime InclusiveEndOfDay(DateTime toDate)
        => toDate.TimeOfDay == TimeSpan.Zero && toDate.Date < DateTime.MaxValue.Date
            ? toDate.Date.AddDays(1).AddSeconds(-1)
            : toDate;
}
