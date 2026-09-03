using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using API.Model.ExcelExport;
using Microsoft.Extensions.Logging;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// Turns the presentation spec the frontend posted (what the grid actually showed)
    /// into an <see cref="ExcelReportLayout"/>, and puts the standard header block on
    /// top of EVERY layout — typed or generic.
    ///
    /// The cell rules mirror GenericReportPage.tsx's <c>toTableColumn</c> /
    /// <c>formatColumnValue</c> and BasicTable.tsx's cell path exactly, with one
    /// deliberate deviation: a blank cell in a numeric or date column is left EMPTY
    /// instead of showing "N/A", so SUM(), sorting and filtering keep working in Excel.
    /// </summary>
    public static class ExcelLayoutBuilder
    {
        /// <summary>What the grid renders for a blank cell (BasicTable's <c>?? 'N/A'</c>).</summary>
        public const string NullText = "N/A";

        private const string Money4Format = "#,##0.0000";

        /// <summary>
        /// Builds the layout for a report with no typed provider.
        /// <paramref name="rowType"/> is what <c>WriteRowsAsync</c> appends; when it is
        /// null nothing can be bound and every column is exported blank (with a warning).
        /// </summary>
        public static ExcelReportLayout Build(ExcelPresentationSpec spec, Type? rowType, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(spec);

            var map = rowType == null ? null : ExcelRowPropertyMap.For(rowType);

            // Nothing can be bound without a row type. Say so ONCE and export the
            // columns blank: a wall of "N/A" would look like real (empty) data.
            if (map == null && spec.Columns is { Count: > 0 })
            {
                logger?.LogWarning(
                    "Excel export for '{Controller}': the row type behind the grid could not be resolved, "
                        + "so all {ColumnCount} columns will export blank.",
                    spec.ControllerName,
                    spec.Columns.Count);
            }

            var columns = new List<ExcelColumn>((spec.Columns?.Count ?? 0) + 1);
            if (spec.ShowRowNumber)
            {
                columns.Add(ExcelColumn.RowNumber(
                    string.IsNullOrWhiteSpace(spec.RowNumberTitle) ? "No" : spec.RowNumberTitle));
            }

            foreach (var column in spec.Columns ?? new List<ExcelSpecColumn>())
            {
                columns.Add(BuildColumn(column, map, spec.ControllerName, logger));
            }

            var sections = BuildSections(spec, rowType, logger);

            // A layout with neither columns nor sections would put StreamingExcelWriter
            // back into legacy reflection mode — the C# property-name dump this whole
            // change exists to remove (Contract §6.2). The validator already refuses
            // such a spec; this is the belt to its braces.
            if (columns.Count == 0 && sections.Count == 0)
            {
                throw new InvalidOperationException(
                    $"The Excel presentation spec for '{spec.ControllerName}' declares no columns and no "
                        + "sections, so the sheet would fall back to a reflection dump of the row type.");
            }

            return new ExcelReportLayout
            {
                Columns = columns,
                Sections = sections,
                CurrencyTotalsColumns = ToCurrencyTotalsColumns(spec.CurrencyTotalsColumns),
                FreezeHeader = true,
            };
        }

        /// <summary>
        /// Prepends the shared header block to <paramref name="layout"/>: the report
        /// title (unless a line already contains it), the spec's heading/subtitle lines,
        /// the request's From/To (or single Date) lines, and the Exported timestamp.
        /// </summary>
        public static ExcelReportLayout WithStandardHeaderBlock(
            ExcelReportLayout layout,
            ExcelPresentationSpec? spec,
            string? fallbackTitle,
            object? request,
            DateTimeOffset exportedAt)
        {
            ArgumentNullException.ThrowIfNull(layout);

            var existing = new List<string>(layout.TitleLines);
            existing.AddRange(layout.HeaderBlock.Select(line => line.Text));

            var block = new List<ExcelHeaderLine>(layout.HeaderBlock);

            // The spec's heading/subtitle lines are folded in FIRST, because the title
            // decision below has to see them: buildReportHeaderLines' last entry is the
            // reportSubtitle, which normally already carries the report's name.
            foreach (var headerLine in spec?.HeaderLines ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(headerLine))
                {
                    continue;
                }

                var text = headerLine.Trim();
                if (existing.Any(line => string.Equals(line, text, StringComparison.Ordinal)))
                {
                    continue;
                }

                block.Add(ExcelHeaderLine.Heading(text));
                existing.Add(text);
            }

            var title = ExcelPresentationSpecValidator.SanitizeTitle(spec?.Title)
                ?? ExcelPresentationSpecValidator.SanitizeTitle(fallbackTitle);

            // Only add a Title row when no line already carries the report's name, so a
            // subtitle like "Account Summary Report (01/02/2026) To (28/02/2026)" is not
            // preceded by a redundant "Account Summary Report" (Contract §2, §2 step 1).
            if (title != null && !existing.Any(line => line.Contains(title, StringComparison.Ordinal)))
            {
                block.Insert(0, ExcelHeaderLine.Title(title));
            }

            if (request != null)
            {
                block.AddRange(ExcelRequestDates.Describe(request));
            }

            block.Add(ExcelRequestDates.ExportedLine(exportedAt));

            return layout.With(headerBlock: block);
        }

        private static IReadOnlyList<ExcelReportSection> BuildSections(
            ExcelPresentationSpec spec,
            Type? summaryType,
            ILogger? logger)
        {
            if (spec.Sections is not { Count: > 0 })
            {
                return Array.Empty<ExcelReportSection>();
            }

            var summaryMap = summaryType == null ? null : ExcelRowPropertyMap.For(summaryType);
            var sections = new List<ExcelReportSection>(spec.Sections.Count);

            foreach (var section in spec.Sections)
            {
                var elementType = ResolveSectionRowType(summaryMap, section.DataPath);
                var map = elementType == null ? null : ExcelRowPropertyMap.For(elementType);

                if (map == null)
                {
                    logger?.LogWarning(
                        "Excel export for '{Controller}': section '{Section}' has no row list at '{DataPath}' "
                            + "on the summary type — its columns will export blank.",
                        spec.ControllerName,
                        section.Title,
                        section.DataPath);
                }

                var columns = new List<ExcelColumn>(section.Columns.Count + 1);
                if (section.ShowRowNumber)
                {
                    columns.Add(ExcelColumn.RowNumber(
                        string.IsNullOrWhiteSpace(section.RowNumberTitle) ? "No" : section.RowNumberTitle));
                }

                foreach (var column in section.Columns)
                {
                    columns.Add(BuildColumn(column, map, spec.ControllerName, logger));
                }

                sections.Add(new ExcelReportSection { Title = section.Title, Columns = columns });
            }

            return sections;
        }

        private static Type? ResolveSectionRowType(ExcelRowPropertyMap? summaryMap, string dataPath)
        {
            var declared = summaryMap?.PropertyType(dataPath);
            if (declared == null)
            {
                return null;
            }

            if (declared.IsArray)
            {
                return declared.GetElementType();
            }

            var enumerable = declared.GetInterfaces()
                .Concat(new[] { declared })
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return enumerable?.GetGenericArguments()[0];
        }

        private static ExcelCurrencyTotalsColumns? ToCurrencyTotalsColumns(ExcelCurrencyTotalsPlacement? placement)
            => placement == null
                ? null
                : new ExcelCurrencyTotalsColumns(placement.LabelColumnKey, placement.ValueColumnKey);

        private static ExcelColumn BuildColumn(
            ExcelSpecColumn spec,
            ExcelRowPropertyMap? map,
            string controllerName,
            ILogger? logger)
        {
            var format = ResolveFormat(spec);
            var isNumeric = IsNumericDataType(spec.DataType);
            var width = ResolveWidth(spec, format);
            var key = string.IsNullOrEmpty(spec.Key) ? spec.DataIndex : spec.Key;

            // No row type (the warning was logged once by the caller): a blank column
            // reads as "no data", which is the truth, where "N/A" on every row would not.
            if (map == null)
            {
                return ExcelColumn.Blank(spec.Title, width).Bind(key, spec.DataIndex);
            }

            var primary = map.Find(spec.DataIndex);
            var fallbacks = (spec.FallbackDataIndexes ?? new List<string>())
                .Select(map.Find)
                .Where(accessor => accessor != null)
                .Select(accessor => accessor!)
                .ToArray();

            // The transactionAmount / mpuAmount / amountDiff columns are computed in the
            // grid, so the same siblings have to be reachable here.
            var mocAmount = map.Find("mocAmount");
            var imAmount = map.Find("imAmount");
            var transactionAmount = map.Find("transactionAmount");

            if (primary == null && fallbacks.Length == 0 && !IsComputed(spec.DataIndex))
            {
                logger?.LogWarning(
                    "Excel export for '{Controller}': column '{Title}' has no property '{DataIndex}' on row type {RowType} — exporting it blank.",
                    controllerName,
                    spec.Title,
                    spec.DataIndex,
                    map.RowType.Name);

                return ExcelColumn.Blank(spec.Title, width).Bind(key, spec.DataIndex);
            }

            return ExcelColumn.Untyped(
                spec.Title,
                format,
                (row, _) => Coerce(Raw(row), format, spec),
                width,
                includeInTotals: false,
                isNumeric: isNumeric,
                key: key,
                dataIndex: spec.DataIndex);

            object? Raw(object row)
            {
                var value = primary?.Invoke(row);

                if (string.Equals(spec.DataIndex, "mpuAmount", StringComparison.Ordinal) && !HasValue(value))
                {
                    return ToTransactionAmount(transactionAmount?.Invoke(row))
                        - ToMoneyNumber(mocAmount?.Invoke(row))
                        - ToMoneyNumber(imAmount?.Invoke(row));
                }

                if (string.Equals(spec.DataIndex, "amountDiff", StringComparison.Ordinal) && !HasValue(value))
                {
                    return ToTransactionAmount(transactionAmount?.Invoke(row))
                        - ToMoneyNumber(mocAmount?.Invoke(row));
                }

                if (HasValue(value) || fallbacks.Length == 0)
                {
                    return value;
                }

                // The grid joins every non-blank fallback with ", ".
                var joined = string.Join(
                    ", ",
                    fallbacks.Select(accessor => accessor(row)).Where(HasValue).Select(AsString));

                return joined.Length == 0 ? null : joined;
            }
        }

        private static bool IsComputed(string? dataIndex)
            => string.Equals(dataIndex, "mpuAmount", StringComparison.Ordinal)
                || string.Equals(dataIndex, "amountDiff", StringComparison.Ordinal);

        private static ExcelCellFormat ResolveFormat(ExcelSpecColumn spec) => spec.DataType switch
        {
            "number" => ExcelCellFormat.Number,
            "money" => string.Equals(spec.NumberFormat, Money4Format, StringComparison.Ordinal)
                ? ExcelCellFormat.Money4
                : ExcelCellFormat.Money,
            "date" => ExcelCellFormat.Date,
            "dateTime" => ExcelCellFormat.DateTime,
            _ => ExcelCellFormat.Text,   // 'string', 'boolean' and unset all render as text
        };

        private static bool IsNumericDataType(string? dataType)
            => dataType is "number" or "money";

        /// <summary>date 12, dateTime 20, money 16, number 12, text clamp(title+4, 12, 40).</summary>
        private static double ResolveWidth(ExcelSpecColumn spec, ExcelCellFormat format) => format switch
        {
            ExcelCellFormat.Date => 12,
            ExcelCellFormat.DateTime => 20,
            ExcelCellFormat.Money => 16,
            ExcelCellFormat.Money4 => 16,
            ExcelCellFormat.Number => 12,
            _ => Math.Clamp((spec.Title?.Length ?? 0) + 4, 12, 40),
        };

        private static object? Coerce(object? raw, ExcelCellFormat format, ExcelSpecColumn spec)
        {
            switch (format)
            {
                case ExcelCellFormat.Number:
                    return HasValue(raw) ? (ToDecimal(raw) ?? (object)AsString(raw)) : null;

                case ExcelCellFormat.Money:
                case ExcelCellFormat.Money4:
                    if (!HasValue(raw))
                    {
                        return null;
                    }

                    if (string.Equals(spec.DataIndex, "transactionAmount", StringComparison.Ordinal))
                    {
                        return ToTransactionAmount(raw);
                    }

                    return ToDecimal(raw) ?? (object)AsString(raw);

                case ExcelCellFormat.Date:
                case ExcelCellFormat.DateTime:
                    if (!HasValue(raw))
                    {
                        return null;
                    }

                    return ToDateTime(raw) ?? (object)AsString(raw);

                default:
                    var isBoolean = string.Equals(spec.DataType, "boolean", StringComparison.Ordinal);

                    // BasicTable prints `render ?? value?.toString() ?? 'N/A'`: only a
                    // MISSING value becomes "N/A" on a plain text column — an empty or
                    // whitespace string renders verbatim. The columns that DO carry a
                    // render (boolean here; date/dateTime/money/fallback columns are
                    // handled above, and a blank fallback join already arrives as null)
                    // run the value through hasValue, so they show "N/A" for both.
                    if (raw == null)
                    {
                        return NullText;
                    }

                    if (!HasValue(raw))
                    {
                        return isBoolean ? NullText : AsString(raw);
                    }

                    return isBoolean ? ToYesNo(raw) : AsString(raw);
            }
        }

        /// <summary>GenericReportPage's <c>hasValue</c>: not null and not whitespace-only.</summary>
        internal static bool HasValue(object? value)
            => value != null && AsString(value).Trim().Length > 0;

        internal static string AsString(object? value) => value switch
        {
            null => string.Empty,
            string s => s,
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "true" : "false",
            // The grid stringifies the JSON number, which JS has already normalised, so
            // a decimal(18,4) SUM of 1500 must read "1500" and not "1500.0000".
            decimal dec => TrimScale(dec),
            double dbl => dbl.ToString(CultureInfo.InvariantCulture),
            float flt => flt.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };

        /// <summary>A decimal without its SQL trailing zeros, InvariantCulture.</summary>
        private static string TrimScale(decimal value)
            => value == 0m
                ? "0"
                : value.ToString("0.############################", CultureInfo.InvariantCulture);

        private static string ToYesNo(object? value)
        {
            if (value is bool boolean)
            {
                return boolean ? "Yes" : "No";
            }

            var text = AsString(value).Trim();
            if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
            {
                return "Yes";
            }

            return string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) ? "No" : text;
        }

        private static decimal? ToDecimal(object? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case decimal d:
                    return d;
                case double dbl:
                    return double.IsFinite(dbl) ? (decimal)dbl : null;
                case float f:
                    return float.IsFinite(f) ? (decimal)f : null;
                case byte or sbyte or short or ushort or int or uint or long or ulong:
                    return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }

            var text = AsString(value).Replace(",", string.Empty).Trim();
            return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }

        /// <summary>Mirrors <c>toMoneyNumber</c>: commas stripped, unparsable → 0.</summary>
        private static decimal ToMoneyNumber(object? value) => ToDecimal(value) ?? 0m;

        /// <summary>
        /// Mirrors <c>toTransactionAmountNumber</c>: an integer string is minor units
        /// (kyat pyas) so it is divided by 100; a string carrying a "." is already a
        /// decimal amount. Commas are stripped either way; unparsable → 0.
        /// </summary>
        internal static decimal ToTransactionAmount(object? value)
        {
            if (value == null)
            {
                return 0m;
            }

            var raw = AsString(value).Replace(",", string.Empty).Trim();
            if (raw.Length == 0)
            {
                return 0m;
            }

            if (raw.Contains('.', StringComparison.Ordinal))
            {
                return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 0m;
            }

            return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var minorUnits)
                ? minorUnits / 100m
                : 0m;
        }

        private static DateTime? ToDateTime(object? value)
        {
            switch (value)
            {
                case DateTime dateTime:
                    return dateTime;
                case DateTimeOffset offset:
                    return offset.DateTime;
                case DateOnly dateOnly:
                    return dateOnly.ToDateTime(TimeOnly.MinValue);
            }

            var text = AsString(value).Trim();
            if (text.Length == 0)
            {
                return null;
            }

            return DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed)
                ? parsed
                : null;
        }
    }
}
