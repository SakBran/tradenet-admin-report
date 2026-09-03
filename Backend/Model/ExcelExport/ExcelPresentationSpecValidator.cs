using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace API.Model.ExcelExport
{
    /// <summary>
    /// Raised when a posted <see cref="ExcelPresentationSpec"/> cannot be trusted. The
    /// action filter turns it into a 400 carrying <see cref="Errors"/>; it is never a 500.
    /// </summary>
    public sealed class ExcelPresentationSpecException : Exception
    {
        public ExcelPresentationSpecException(IReadOnlyList<string> errors)
            : base(errors.Count > 0 ? string.Join(" ", errors) : "The Excel presentation spec is invalid.")
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }
    }

    /// <summary>
    /// Sanitizes and bounds a client-supplied <see cref="ExcelPresentationSpec"/>. The
    /// spec drives file names, worksheet names and cell text, so every string is stripped
    /// of control characters, has its whitespace collapsed, and is length-bounded, and
    /// every identifier must look like a JSON property path.
    /// </summary>
    public static class ExcelPresentationSpecValidator
    {
        public const int MaxTitleLength = 200;
        public const int MaxStoredTitleLength = 256;   // ExcelExportJob.ReportTitle nvarchar(256)
        public const int MaxFileNameLength = 120;
        public const int MaxHeaderLines = 12;
        public const int MaxHeaderLineLength = 300;
        public const int MaxColumns = 100;
        public const int MaxColumnTitleLength = 200;
        public const int MaxIdentifierLength = 100;
        public const int MaxFallbackDataIndexes = 10;
        public const int MaxSections = 5;
        public const int MaxSummaryLines = 10;

        private static readonly Regex IdentifierPattern = new(@"^[A-Za-z0-9_.]+$", RegexOptions.Compiled);

        private static readonly HashSet<string> DataTypes = new(StringComparer.Ordinal)
        {
            "string", "number", "date", "dateTime", "boolean", "money",
        };

        /// <summary>Whitelisted <see cref="ExcelSpecColumn.DataType"/> values.</summary>
        public static IReadOnlyCollection<string> AllowedDataTypes => DataTypes;

        /// <summary>
        /// Sanitizes <paramref name="spec"/> in place and throws
        /// <see cref="ExcelPresentationSpecException"/> when it is not usable.
        /// </summary>
        /// <param name="requireColumns">
        /// False for a controller that supplies its own typed layout — the spec then only
        /// carries the title/header block.
        /// </param>
        public static void ValidateAndSanitize(ExcelPresentationSpec spec, bool requireColumns = true)
        {
            if (!TryValidateAndSanitize(spec, requireColumns, out var errors))
            {
                throw new ExcelPresentationSpecException(errors);
            }
        }

        public static bool TryValidateAndSanitize(
            ExcelPresentationSpec? spec,
            bool requireColumns,
            out IReadOnlyList<string> errors)
        {
            var problems = new List<string>();
            errors = problems;

            if (spec == null)
            {
                problems.Add("excel: the presentation spec is required.");
                return false;
            }

            if (spec.FormatVersion != ExcelPresentationSpec.CurrentFormatVersion)
            {
                problems.Add(
                    $"excel.formatVersion: expected {ExcelPresentationSpec.CurrentFormatVersion} but got {spec.FormatVersion}.");
            }

            spec.ConfigKey = Clean(spec.ConfigKey);
            spec.ControllerName = Clean(spec.ControllerName);
            spec.Title = Clean(spec.Title);
            spec.RowNumberTitle = Clean(spec.RowNumberTitle);

            if (spec.ConfigKey.Length == 0)
            {
                problems.Add("excel.configKey: required.");
            }

            if (spec.ControllerName.Length == 0)
            {
                problems.Add("excel.controllerName: required.");
            }

            if (spec.Title.Length == 0)
            {
                problems.Add("excel.title: required.");
            }
            else if (spec.Title.Length > MaxTitleLength)
            {
                problems.Add($"excel.title: must be {MaxTitleLength} characters or fewer.");
            }

            // CleanFileNameBase, not SanitizeFileNameBase: the public sanitizer truncates
            // at MaxFileNameLength (it is the enqueue path's last-resort net), which would
            // make the length check below unreachable and let an over-long name through.
            var fileName = CleanFileNameBase(spec.FileName);
            if (fileName == null)
            {
                problems.Add("excel.fileName: required.");
                spec.FileName = string.Empty;
            }
            else
            {
                if (fileName.Length > MaxFileNameLength)
                {
                    problems.Add($"excel.fileName: must be {MaxFileNameLength} characters or fewer.");
                    fileName = fileName[..MaxFileNameLength];
                }

                spec.FileName = fileName;
            }

            if (spec.RowNumberTitle.Length == 0)
            {
                spec.RowNumberTitle = "No";
            }
            else if (spec.RowNumberTitle.Length > 32)
            {
                problems.Add("excel.rowNumberTitle: must be 32 characters or fewer.");
            }

            spec.HeaderLines = SanitizeLines(spec.HeaderLines, "excel.headerLines", problems);

            spec.Columns = SanitizeColumns(spec.Columns, "excel.columns", problems);

            if (spec.Sections is { Count: > 0 })
            {
                if (spec.Sections.Count > MaxSections)
                {
                    problems.Add($"excel.sections: at most {MaxSections} sections are allowed.");
                    spec.Sections = spec.Sections.Take(MaxSections).ToList();
                }

                for (var i = 0; i < spec.Sections.Count; i++)
                {
                    var section = spec.Sections[i];
                    var path = $"excel.sections[{i}]";

                    section.Key = Clean(section.Key);
                    section.Title = Clean(section.Title);
                    section.DataPath = Clean(section.DataPath);
                    section.RowNumberTitle = Clean(section.RowNumberTitle);

                    if (section.Title.Length > MaxColumnTitleLength)
                    {
                        problems.Add($"{path}.title: must be {MaxColumnTitleLength} characters or fewer.");
                    }

                    RequireIdentifier(section.DataPath, $"{path}.dataPath", problems);

                    if (section.RowNumberTitle.Length == 0)
                    {
                        section.RowNumberTitle = "Sr.No.";
                    }

                    section.Columns = SanitizeColumns(section.Columns, $"{path}.columns", problems);
                    if (section.Columns.Count == 0)
                    {
                        problems.Add($"{path}.columns: at least one column is required.");
                    }
                }
            }
            else
            {
                spec.Sections = null;
            }

            if (spec.SummaryLines is { Count: > 0 })
            {
                if (spec.SummaryLines.Count > MaxSummaryLines)
                {
                    problems.Add($"excel.summaryLines: at most {MaxSummaryLines} lines are allowed.");
                    spec.SummaryLines = spec.SummaryLines.Take(MaxSummaryLines).ToList();
                }

                for (var i = 0; i < spec.SummaryLines.Count; i++)
                {
                    var line = spec.SummaryLines[i];
                    var path = $"excel.summaryLines[{i}]";

                    line.Label = Clean(line.Label);
                    line.DataPath = Clean(line.DataPath);
                    line.NumberFormat = CleanOrNull(line.NumberFormat);

                    if (line.Label.Length == 0)
                    {
                        problems.Add($"{path}.label: required.");
                    }
                    else if (line.Label.Length > MaxColumnTitleLength)
                    {
                        problems.Add($"{path}.label: must be {MaxColumnTitleLength} characters or fewer.");
                    }

                    RequireIdentifier(line.DataPath, $"{path}.dataPath", problems);
                }
            }
            else
            {
                spec.SummaryLines = null;
            }

            if (spec.CurrencyTotalsColumns != null)
            {
                var placement = spec.CurrencyTotalsColumns;
                placement.LabelColumnKey = Clean(placement.LabelColumnKey);
                placement.ValueColumnKey = Clean(placement.ValueColumnKey);

                if (placement.LabelColumnKey.Length == 0 && placement.ValueColumnKey.Length == 0)
                {
                    spec.CurrencyTotalsColumns = null;
                }
                else
                {
                    RequireIdentifier(
                        placement.LabelColumnKey, "excel.currencyTotalsColumns.labelColumnKey", problems);
                    RequireIdentifier(
                        placement.ValueColumnKey, "excel.currencyTotalsColumns.valueColumnKey", problems);

                    var keys = spec.Columns.Select(column => column.Key).ToHashSet(StringComparer.Ordinal);
                    if (placement.LabelColumnKey.Length > 0 && !keys.Contains(placement.LabelColumnKey))
                    {
                        problems.Add(
                            $"excel.currencyTotalsColumns.labelColumnKey: '{placement.LabelColumnKey}' is not one of the exported columns.");
                    }

                    if (placement.ValueColumnKey.Length > 0 && !keys.Contains(placement.ValueColumnKey))
                    {
                        problems.Add(
                            $"excel.currencyTotalsColumns.valueColumnKey: '{placement.ValueColumnKey}' is not one of the exported columns.");
                    }
                }
            }

            if (spec.Columns.Count == 0 && spec.Sections == null && requireColumns)
            {
                problems.Add("excel.columns: at least one column (or one section) is required.");
            }

            return problems.Count == 0;
        }

        /// <summary>
        /// Report title fit for the worksheet name, the job row and the sheet's title
        /// line. Null when there is nothing usable left.
        /// </summary>
        public static string? SanitizeTitle(string? value)
        {
            var cleaned = Clean(value);
            if (cleaned.Length == 0)
            {
                return null;
            }

            return cleaned.Length > MaxStoredTitleLength ? cleaned[..MaxStoredTitleLength] : cleaned;
        }

        /// <summary>
        /// Download file-name base: the ".xlsx" suffix dropped, every character outside
        /// <c>[A-Za-z0-9 _.-]</c> removed, truncated to <see cref="MaxFileNameLength"/>.
        /// Null when nothing usable is left. Used by the enqueue path on an already
        /// validated spec, so truncating (rather than failing) is the right last resort;
        /// <see cref="ValidateAndSanitize"/> rejects an over-long name before that.
        /// </summary>
        public static string? SanitizeFileNameBase(string? value)
        {
            var cleaned = CleanFileNameBase(value);
            if (cleaned == null)
            {
                return null;
            }

            return cleaned.Length > MaxFileNameLength ? cleaned[..MaxFileNameLength] : cleaned;
        }

        /// <summary>
        /// <see cref="SanitizeFileNameBase"/> without the length truncation, so the
        /// validator can see (and reject) a name that is too long.
        /// </summary>
        private static string? CleanFileNameBase(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var raw = value.Trim();
            if (raw.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                raw = raw[..^".xlsx".Length];
            }

            var sb = new StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or ' ' or '_' or '.' or '-')
                {
                    sb.Append(c);
                }
            }

            var cleaned = CollapseWhitespace(sb.ToString()).Trim(' ', '.', '-');
            return cleaned.Length == 0 ? null : cleaned;
        }

        private static List<string> SanitizeLines(List<string>? lines, string path, List<string> problems)
        {
            if (lines == null)
            {
                return new List<string>();
            }

            if (lines.Count > MaxHeaderLines)
            {
                problems.Add($"{path}: at most {MaxHeaderLines} lines are allowed.");
                lines = lines.Take(MaxHeaderLines).ToList();
            }

            var result = new List<string>(lines.Count);
            foreach (var line in lines)
            {
                var cleaned = Clean(line);
                if (cleaned.Length == 0)
                {
                    continue;
                }

                if (cleaned.Length > MaxHeaderLineLength)
                {
                    problems.Add($"{path}: each line must be {MaxHeaderLineLength} characters or fewer.");
                    cleaned = cleaned[..MaxHeaderLineLength];
                }

                result.Add(cleaned);
            }

            return result;
        }

        private static List<ExcelSpecColumn> SanitizeColumns(
            List<ExcelSpecColumn>? columns,
            string path,
            List<string> problems)
        {
            if (columns == null)
            {
                return new List<ExcelSpecColumn>();
            }

            if (columns.Count > MaxColumns)
            {
                problems.Add($"{path}: at most {MaxColumns} columns are allowed.");
                columns = columns.Take(MaxColumns).ToList();
            }

            for (var i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                var columnPath = $"{path}[{i}]";

                column.Key = Clean(column.Key);
                column.DataIndex = Clean(column.DataIndex);
                column.Title = Clean(column.Title);
                column.DataType = CleanOrNull(column.DataType);
                column.NumberFormat = CleanOrNull(column.NumberFormat);

                if (column.Key.Length == 0)
                {
                    column.Key = column.DataIndex;
                }

                RequireIdentifier(column.Key, $"{columnPath}.key", problems);
                RequireIdentifier(column.DataIndex, $"{columnPath}.dataIndex", problems);

                if (column.Title.Length > MaxColumnTitleLength)
                {
                    problems.Add($"{columnPath}.title: must be {MaxColumnTitleLength} characters or fewer.");
                    column.Title = column.Title[..MaxColumnTitleLength];
                }

                if (column.DataType != null && !DataTypes.Contains(column.DataType))
                {
                    problems.Add(
                        $"{columnPath}.dataType: '{column.DataType}' is not one of {string.Join(", ", DataTypes)}.");
                }

                if (column.FallbackDataIndexes is { Count: > 0 })
                {
                    if (column.FallbackDataIndexes.Count > MaxFallbackDataIndexes)
                    {
                        problems.Add($"{columnPath}.fallbackDataIndexes: at most {MaxFallbackDataIndexes} are allowed.");
                        column.FallbackDataIndexes = column.FallbackDataIndexes.Take(MaxFallbackDataIndexes).ToList();
                    }

                    var fallbacks = new List<string>(column.FallbackDataIndexes.Count);
                    foreach (var fallback in column.FallbackDataIndexes)
                    {
                        var cleaned = Clean(fallback);
                        if (cleaned.Length == 0)
                        {
                            continue;
                        }

                        RequireIdentifier(cleaned, $"{columnPath}.fallbackDataIndexes", problems);
                        fallbacks.Add(cleaned);
                    }

                    column.FallbackDataIndexes = fallbacks.Count > 0 ? fallbacks : null;
                }
                else
                {
                    column.FallbackDataIndexes = null;
                }

                if (column.NumberFormat is { Length: > 32 })
                {
                    problems.Add($"{columnPath}.numberFormat: must be 32 characters or fewer.");
                    column.NumberFormat = column.NumberFormat[..32];
                }
            }

            return columns;
        }

        private static void RequireIdentifier(string value, string path, List<string> problems)
        {
            if (value.Length == 0)
            {
                problems.Add($"{path}: required.");
                return;
            }

            if (value.Length > MaxIdentifierLength)
            {
                problems.Add($"{path}: must be {MaxIdentifierLength} characters or fewer.");
                return;
            }

            if (!IdentifierPattern.IsMatch(value))
            {
                problems.Add($"{path}: '{value}' is not a valid property path (letters, digits, '_' and '.' only).");
            }
        }

        private static string? CleanOrNull(string? value)
        {
            var cleaned = Clean(value);
            return cleaned.Length == 0 ? null : cleaned;
        }

        /// <summary>Strips control characters, collapses runs of whitespace, trims.</summary>
        private static string Clean(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (char.IsControl(c))
                {
                    // Newlines and tabs become a single space so wording survives.
                    if (c is '\n' or '\r' or '\t')
                    {
                        sb.Append(' ');
                    }

                    continue;
                }

                sb.Append(c);
            }

            return CollapseWhitespace(sb.ToString()).Trim();
        }

        private static string CollapseWhitespace(string value)
        {
            var sb = new StringBuilder(value.Length);
            var previousWasSpace = false;

            foreach (var c in value)
            {
                var isSpace = char.IsWhiteSpace(c);
                if (isSpace)
                {
                    if (!previousWasSpace)
                    {
                        sb.Append(' ');
                    }
                }
                else
                {
                    sb.Append(c);
                }

                previousWasSpace = isSpace;
            }

            return sb.ToString();
        }
    }
}
