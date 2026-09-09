using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace API.Service.ExcelExport.Dcca
{
    /// <summary>
    /// Writes the DCCA import workbook: the old Tradenet 2.0 export's ten OOXML parts, with the
    /// eight data-independent ones copied byte-for-byte out of the embedded skeleton and only
    /// <c>sheet1.xml</c> and <c>sharedStrings.xml</c> generated.
    ///
    /// The row layout reproduces the old EPPlus loop exactly
    /// (<c>ReportsController.AccountSummaryReport</c>, POST):
    ///
    ///   int row = 2; foreach (item) { row++; … }
    ///
    /// so row 1 is the header, **row 2 has no &lt;row&gt; element at all**, and data starts at row 3.
    /// Every string cell is a shared string, including the dates and the empty Remark.
    /// </summary>
    internal sealed class DccaWorkbookWriter : IAsyncDisposable
    {
        /// <summary>Excel's hard limit, less the header and the skipped row 2.</summary>
        internal const int MaxDataRows = 1_048_576 - 2;

        // Every entry is stamped with this instead of "now", so two exports of the same data
        // produce an identical file. It is also what the original template zip carries.
        private static readonly DateTimeOffset FixedTimestamp =
            new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly DccaSpoolFile _sheetRows = new();
        private readonly DccaSpoolFile _stringTable = new();

        // First-use order, exactly as EPPlus rebuilt it: the nine headers occupy 0-8 (they are
        // the first strings the sheet references, left to right), then each new value as it is
        // encountered scanning every data row left to right.
        private readonly Dictionary<string, int> _stringIndexes = new(StringComparer.Ordinal);

        private long _dataRows;
        private bool _sealed;

        internal DccaWorkbookWriter()
        {
            for (var i = 0; i < DccaWorkbookSkeleton.HeaderStrings.Length; i++)
            {
                // Seeded, not written: the header <si> elements are already in the skeleton's
                // string table, so only their indices are needed here.
                _stringIndexes[DccaWorkbookSkeleton.HeaderStrings[i]] = i;
            }
        }

        internal long DataRows => _dataRows;

        /// <summary>One output row per fee line, in the order the rows arrive.</summary>
        internal async ValueTask AppendRowAsync(DccaRow row, CancellationToken cancellationToken)
        {
            if (_sealed)
            {
                throw new InvalidOperationException("The DCCA workbook has already been written.");
            }

            if (_dataRows >= MaxDataRows)
            {
                throw new InvalidOperationException(
                    $"The DCCA export exceeded Excel's {MaxDataRows:N0}-row limit. The old report " +
                    "never produced a file this large; narrow the date range.");
            }

            _dataRows++;
            var rowNumber = _dataRows + 2; // row 1 = header, row 2 deliberately empty

            var builder = new StringBuilder(320);
            builder.Append("<row r=\"").Append(rowNumber.ToString(CultureInfo.InvariantCulture)).Append("\">");

            // A: the serial. Numeric, unstyled — its value is the position in the row order.
            Number(builder, 'A', rowNumber, DccaXmlText.Number(_dataRows));

            // B: the date, as TEXT in MM/dd/yyyy, on the column's own Text style (s="3").
            // Month-first is what DCCA has always received; a real date serial would arrive as a
            // number and 4 March would read as 3 April if the order flipped.
            await SharedAsync(builder, 'B', rowNumber, 3, row.EntryDate, cancellationToken);

            await SharedAsync(builder, 'C', rowNumber, 0, row.HtaThaKaNo, cancellationToken);
            await SharedAsync(builder, 'D', rowNumber, 0, row.CompanyName, cancellationToken);
            await SharedAsync(builder, 'E', rowNumber, 0, row.TransactionTitle, cancellationToken);

            // F: the fee. Numeric, General format, no thousands separator.
            Number(builder, 'F', rowNumber, DccaXmlText.Number(row.Amount));

            // G: Remark. The old code wrote a literal "" every row, which EPPlus interned as a
            // shared string — so this is <t></t>, not an omitted cell.
            await SharedAsync(builder, 'G', rowNumber, 0, string.Empty, cancellationToken);

            await SharedAsync(builder, 'H', rowNumber, 0, row.AccountCode, cancellationToken);
            await SharedAsync(builder, 'I', rowNumber, 0, row.LocationCode, cancellationToken);

            builder.Append("</row>");

            await _sheetRows.WriteAsync(Encoding.UTF8.GetBytes(builder.ToString()), cancellationToken);
        }

        private static void Number(StringBuilder builder, char column, long rowNumber, string value)
            => builder
                .Append("<c r=\"").Append(column).Append(rowNumber.ToString(CultureInfo.InvariantCulture))
                .Append("\" s=\"0\"><v>").Append(value).Append("</v></c>");

        private async ValueTask SharedAsync(
            StringBuilder builder,
            char column,
            long rowNumber,
            int style,
            string? value,
            CancellationToken cancellationToken)
        {
            // The old system read every string column as DataRow[...].ToString(), which yields ""
            // for DBNull and never null — so coercing null to "" is exact legacy reproduction,
            // not a divergence.
            var index = await InternAsync(value ?? string.Empty, cancellationToken);

            builder
                .Append("<c r=\"").Append(column).Append(rowNumber.ToString(CultureInfo.InvariantCulture))
                .Append("\" s=\"").Append(style.ToString(CultureInfo.InvariantCulture))
                .Append("\" t=\"s\"><v>").Append(index.ToString(CultureInfo.InvariantCulture))
                .Append("</v></c>");
        }

        private async ValueTask<int> InternAsync(string value, CancellationToken cancellationToken)
        {
            if (_stringIndexes.TryGetValue(value, out var existing))
            {
                return existing;
            }

            var index = _stringIndexes.Count;
            _stringIndexes[value] = index;

            var element = DccaXmlText.NeedsSpacePreserved(value)
                ? $"<si><t xml:space=\"preserve\">{DccaXmlText.Encode(value)}</t></si>"
                : $"<si><t>{DccaXmlText.Encode(value)}</t></si>";

            await _stringTable.WriteAsync(Encoding.UTF8.GetBytes(element), cancellationToken);
            return index;
        }

        /// <summary>
        /// Assembles the archive. The <see cref="ZipArchive"/> is created HERE and nowhere else:
        /// a constructor-opened archive would, on cancellation, write a central directory into
        /// the output during <c>Dispose</c> — and <c>ExcelExportWorker</c> deliberately skips
        /// deleting the file on shutdown-cancellation, so that would orphan a half-written
        /// workbook the cleanup worker can never reclaim (it has no <c>FilePath</c> recorded).
        /// </summary>
        internal async Task FinishAsync(Stream output, CancellationToken cancellationToken)
        {
            _sealed = true;

            var skeleton = DccaWorkbookSkeleton.Current;
            var lastRow = _dataRows == 0 ? 1 : _dataRows + 2;

            await using var sheetRows = await _sheetRows.SealAsync(cancellationToken);
            await using var stringTable = await _stringTable.SealAsync(cancellationToken);

            // leaveOpen: the worker owns `output` and commits/closes it itself.
            using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

            foreach (var name in DccaWorkbookSkeleton.EntryOrder)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                entry.LastWriteTime = FixedTimestamp;

                await using var entryStream = entry.Open();

                if (name == DccaWorkbookSkeleton.Sheet)
                {
                    await entryStream.WriteAsync(skeleton.SheetBeforeDimensionRef, cancellationToken);
                    await entryStream.WriteAsync(
                        Encoding.UTF8.GetBytes($"A1:I{lastRow.ToString(CultureInfo.InvariantCulture)}"),
                        cancellationToken);
                    await entryStream.WriteAsync(
                        skeleton.SheetAfterDimensionRefThroughHeaderRow, cancellationToken);
                    await sheetRows.CopyToAsync(entryStream, cancellationToken);
                    await entryStream.WriteAsync(skeleton.SheetAfterRows, cancellationToken);
                }
                else if (name == DccaWorkbookSkeleton.SharedStrings)
                {
                    // count == uniqueCount, as EPPlus wrote it (both are the unique total).
                    // This part is emitted even for a zero-row export: workbook.xml.rels has an
                    // rId pointing at it and [Content_Types].xml an Override, so omitting it
                    // would leave a dangling relationship and Excel would repair the file.
                    var count = Encoding.UTF8.GetBytes(
                        _stringIndexes.Count.ToString(CultureInfo.InvariantCulture));

                    await entryStream.WriteAsync(skeleton.SharedStringsBeforeCount, cancellationToken);
                    await entryStream.WriteAsync(count, cancellationToken);
                    await entryStream.WriteAsync(skeleton.SharedStringsBetweenCounts, cancellationToken);
                    await entryStream.WriteAsync(count, cancellationToken);
                    await entryStream.WriteAsync(
                        skeleton.SharedStringsAfterCountThroughHeaders, cancellationToken);
                    await stringTable.CopyToAsync(entryStream, cancellationToken);
                    await entryStream.WriteAsync(skeleton.SharedStringsSuffix, cancellationToken);
                }
                else
                {
                    await entryStream.WriteAsync(skeleton.StaticParts[name], cancellationToken);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _sheetRows.DisposeAsync();
            await _stringTable.DisposeAsync();
        }
    }

    /// <summary>One output row, already reduced to the nine cell values the old loop wrote.</summary>
    internal readonly record struct DccaRow(
        string EntryDate,
        string HtaThaKaNo,
        string? CompanyName,
        string? TransactionTitle,
        double Amount,
        string? AccountCode,
        string? LocationCode);
}
