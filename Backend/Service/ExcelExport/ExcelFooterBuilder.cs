using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// Builds the sheet's footer rows from the SAME totals the grid footer shows,
    /// mirroring BasicTable.tsx's tfoot exactly:
    ///
    ///   Total          | …            | 1,234.00              (columnTotals)
    ///   USD:3 licence(s)| …           | USD:1,234.0000        (one per currency)
    ///   TOTAL          | Total:7 licence(s)                   (grand row)
    ///
    /// Nothing at all when the export is empty or the report has no totals — the grid
    /// hides its footer in exactly those cases.
    /// </summary>
    public static class ExcelFooterBuilder
    {
        public static IReadOnlyList<ExcelFooterRow> Build(
            ExcelReportLayout layout,
            ReportFooterTotals? totals,
            long dataRowCount)
        {
            ArgumentNullException.ThrowIfNull(layout);

            if (totals == null || dataRowCount == 0 || layout.Columns.Count == 0)
            {
                return Array.Empty<ExcelFooterRow>();
            }

            var columns = layout.Columns;
            var rows = new List<ExcelFooterRow>(2);

            var totalRow = BuildColumnTotalsRow(columns, totals.ColumnTotals);
            if (totalRow != null)
            {
                rows.Add(totalRow);
            }

            var currencies = totals.CurrencyTotals?.Currencies;
            if (currencies is { Count: > 0 })
            {
                var (labelIndex, valueIndex) = ResolveCurrencyColumns(columns, layout.CurrencyTotalsColumns);

                foreach (var entry in currencies)
                {
                    var cells = NewRow(columns.Count);

                    // Value first, label second: BasicTable tests the label key before
                    // the value key, so if a spec ever pointed both at one column the
                    // label is what the grid shows.
                    if (valueIndex >= 0)
                    {
                        cells[valueIndex] = new ExcelFooterCell(
                            $"{entry.Currency}:{FormatN4(entry.TotalValue)}");
                    }

                    if (labelIndex >= 0)
                    {
                        cells[labelIndex] = new ExcelFooterCell(
                            $"{entry.Currency}:{entry.NoOfLicences.ToString(CultureInfo.InvariantCulture)} licence(s)");
                    }

                    rows.Add(new ExcelFooterRow(cells));
                }

                rows.Add(BuildGrandRow(columns, labelIndex, totals.CurrencyTotals!.GrandTotalLicences));
            }

            return rows;
        }

        private static ExcelFooterRow? BuildColumnTotalsRow(
            IReadOnlyList<ExcelColumn> columns,
            IReadOnlyDictionary<string, decimal>? columnTotals)
        {
            if (columnTotals == null || columnTotals.Count == 0)
            {
                return null;
            }

            // The grid keys columnTotals by dataIndex and ignores the row-number column.
            var totalled = new decimal?[columns.Count];
            var any = false;

            for (var i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                if (column.IsRowNumber)
                {
                    continue;
                }

                var dataIndex = column.DataIndex ?? column.Key;
                if (dataIndex != null && columnTotals.TryGetValue(dataIndex, out var total))
                {
                    totalled[i] = total;
                    any = true;
                }
            }

            if (!any)
            {
                return null;
            }

            // "Total" goes in the first data column that has no total of its own; the
            // row-number cell stays blank.
            var labelIndex = -1;
            for (var i = 0; i < columns.Count; i++)
            {
                if (columns[i].IsRowNumber || totalled[i] != null)
                {
                    continue;
                }

                labelIndex = i;
                break;
            }

            var cells = NewRow(columns.Count);
            for (var i = 0; i < columns.Count; i++)
            {
                if (totalled[i] is { } total)
                {
                    // A text-formatted total must be stringified the way the grid does
                    // (String(jsonNumber)), or SQL's trailing zeros leak in as "1500.0000".
                    var fmt = TotalFormat(columns[i]);
                    cells[i] = new ExcelFooterCell(
                        fmt == ExcelCellFormat.Text ? ExcelLayoutBuilder.AsString(total) : (object)total,
                        fmt);
                }
                else if (i == labelIndex)
                {
                    // Legacy RDLC grand-total label (ImportPermitBySectionReport.rdlc:841).
                    cells[i] = new ExcelFooterCell("TOTAL");
                }
            }

            return new ExcelFooterRow(cells);
        }

        /// <summary>
        /// "TOTAL" in the row-number cell, or — when the sheet has no row-number column —
        /// in the first data column unless that is already the currency label column.
        /// </summary>
        private static ExcelFooterRow BuildGrandRow(
            IReadOnlyList<ExcelColumn> columns,
            int labelIndex,
            int grandTotalLicences)
        {
            var cells = NewRow(columns.Count);

            var rowNumberIndex = -1;
            for (var i = 0; i < columns.Count; i++)
            {
                if (columns[i].IsRowNumber)
                {
                    rowNumberIndex = i;
                    break;
                }
            }

            if (rowNumberIndex >= 0)
            {
                cells[rowNumberIndex] = new ExcelFooterCell("TOTAL");
            }
            else if (columns.Count > 0 && labelIndex != 0)
            {
                cells[0] = new ExcelFooterCell("TOTAL");
            }

            if (labelIndex >= 0)
            {
                cells[labelIndex] = new ExcelFooterCell($"Total:{grandTotalLicences.ToString(CultureInfo.InvariantCulture)} licence(s)");
            }

            return new ExcelFooterRow(cells);
        }

        /// <summary>
        /// The configured label/value column keys, or the grid's fallback: the first
        /// non-numeric data column and the first numeric data column. Key comparison is
        /// Ordinal, exactly like the React key match.
        /// </summary>
        private static (int LabelIndex, int ValueIndex) ResolveCurrencyColumns(
            IReadOnlyList<ExcelColumn> columns,
            ExcelCurrencyTotalsColumns? configured)
        {
            return (
                Resolve(configured?.LabelColumnKey, numeric: false),
                Resolve(configured?.ValueColumnKey, numeric: true));

            // BasicTable's fallback is `configuredKey ?? firstColumn`, i.e. it fires only
            // when nothing was configured. A configured key that matches no column makes
            // the grid render no cell at all, so the sheet must not quietly put the total
            // somewhere else either.
            int Resolve(string? key, bool numeric)
                => string.IsNullOrEmpty(key)
                    ? FirstDataColumn(columns, numeric)
                    : IndexOfKey(columns, key);
        }

        private static int IndexOfKey(IReadOnlyList<ExcelColumn> columns, string? key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return -1;
            }

            for (var i = 0; i < columns.Count; i++)
            {
                if (!columns[i].IsRowNumber && string.Equals(columns[i].Key, key, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FirstDataColumn(IReadOnlyList<ExcelColumn> columns, bool numeric)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                if (columns[i].IsRowNumber)
                {
                    continue;
                }

                if (columns[i].IsNumeric == numeric)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Money → "#,##0.00", Number → general, anything else → text.</summary>
        private static ExcelCellFormat TotalFormat(ExcelColumn column) => column.Format switch
        {
            ExcelCellFormat.Money => ExcelCellFormat.Money,
            ExcelCellFormat.Money4 => ExcelCellFormat.Money,
            ExcelCellFormat.Number => ExcelCellFormat.Number,
            _ => ExcelCellFormat.Text,
        };

        /// <summary>The legacy RDLC FORMAT(Sum(Amount), "N4") — en-US grouping, 4 decimals.</summary>
        private static string FormatN4(decimal value)
            => value.ToString("#,##0.0000", CultureInfo.GetCultureInfo("en-US"));

        private static ExcelFooterCell?[] NewRow(int columnCount) => new ExcelFooterCell?[columnCount];
    }
}
