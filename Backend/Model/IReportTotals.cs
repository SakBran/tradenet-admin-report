using System.Collections.Generic;

namespace API.Model
{
    /// <summary>
    /// The footer totals a report's JSON response carries. Implemented by
    /// <see cref="ApiResult{T}"/> so the Excel export can read the SAME numbers the grid
    /// footer shows by replaying the report's own <c>Post</c>, without knowing the row type.
    /// </summary>
    public interface IReportTotals
    {
        /// <summary>Per-column grand totals keyed by the column's dataIndex.</summary>
        IReadOnlyDictionary<string, decimal>? ColumnTotals { get; }

        /// <summary>Per-currency licence counts and summed values, plus the grand count.</summary>
        ReportCurrencyTotalsSummary? CurrencyTotals { get; }
    }
}
