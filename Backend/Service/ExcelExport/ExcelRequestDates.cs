using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace API.Service.ExcelExport
{
    /// <summary>Which date fields a report's request DTO carries.</summary>
    public enum ExcelRequestDateShape
    {
        /// <summary>No date field — the sheet gets no date line, and that is correct.</summary>
        None = 0,

        /// <summary>FromDate + ToDate → "From Date: …" and "To Date: …" lines.</summary>
        Range = 1,

        /// <summary>A single Date → one "Date: …" line.</summary>
        Single = 2,
    }

    /// <summary>The date properties found on one request type.</summary>
    public readonly record struct ExcelRequestDateProps(
        PropertyInfo? FromDate,
        PropertyInfo? ToDate,
        PropertyInfo? Date)
    {
        public bool HasFromTo => FromDate != null && ToDate != null;

        public ExcelRequestDateShape Shape => HasFromTo
            ? ExcelRequestDateShape.Range
            : Date != null
                ? ExcelRequestDateShape.Single
                : ExcelRequestDateShape.None;
    }

    /// <summary>
    /// Turns a report request DTO into the sheet's date meta lines. The grid never
    /// showed From/To rows, so these come from the request itself — which is also the
    /// only place that still knows the filter the user actually applied.
    ///
    /// Always InvariantCulture: in a .NET custom date format "/" is the date-separator
    /// placeholder, so a server running a "." culture would otherwise emit "01.02.2026".
    /// </summary>
    public static class ExcelRequestDates
    {
        /// <summary>Which date properties the request type declares.</summary>
        public static ExcelRequestDateProps Describe(Type requestType)
        {
            ArgumentNullException.ThrowIfNull(requestType);

            return new ExcelRequestDateProps(
                FindDateProperty(requestType, "FromDate"),
                FindDateProperty(requestType, "ToDate"),
                FindDateProperty(requestType, "Date"));
        }

        /// <summary>
        /// "From Date: 01/02/2026" + "To Date: 28/02/2026", or a single "Date: 15/02/2026",
        /// or nothing at all.
        /// </summary>
        public static IReadOnlyList<ExcelHeaderLine> Describe(object request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var props = Describe(request.GetType());
            var lines = new List<ExcelHeaderLine>(2);

            if (props.HasFromTo)
            {
                var from = Read(props.FromDate!, request);
                var to = Read(props.ToDate!, request);

                // All or nothing: a half-filled range would print one date line and leave
                // the reader guessing which end of the window they are looking at.
                if (from != null && to != null)
                {
                    lines.Add(Line("From Date", from.Value));
                    lines.Add(Line("To Date", to.Value));
                }

                return lines;
            }

            if (props.Date != null && Read(props.Date, request) is { } single)
            {
                lines.Add(Line("Date", single));
            }

            return lines;
        }

        /// <summary>"Exported: 02/09/2026 19:40" — when the file was generated, server local time.</summary>
        public static ExcelHeaderLine ExportedLine(DateTimeOffset exportedAt)
            => ExcelHeaderLine.Meta(string.Format(
                CultureInfo.InvariantCulture,
                "Exported: {0:dd/MM/yyyy HH:mm}",
                exportedAt.DateTime));

        public static string Format(DateTime value)
            => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        /// <summary>
        /// The property's value, or null when the filter was never set. A non-nullable
        /// <c>DateTime</c> that the user left empty arrives as <c>default</c>, and
        /// "From Date: 01/01/0001" is worse than no line at all — so an unset date is
        /// treated as absent (the one documented deviation from Contract §2 step 3).
        /// </summary>
        private static DateTime? Read(PropertyInfo property, object request)
            => property.GetValue(request) is DateTime value && value != default
                ? value
                : null;

        private static ExcelHeaderLine Line(string label, DateTime value)
            => ExcelHeaderLine.Meta($"{label}: {Format(value)}");

        private static PropertyInfo? FindDateProperty(Type requestType, string name)
        {
            var property = requestType.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property == null || property.GetIndexParameters().Length > 0 || !property.CanRead)
            {
                return null;
            }

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            return type == typeof(DateTime) ? property : null;
        }
    }
}
