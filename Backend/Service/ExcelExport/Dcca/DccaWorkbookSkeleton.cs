using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace API.Service.ExcelExport.Dcca
{
    /// <summary>
    /// The embedded workbook the DCCA export is built from, and the pieces of it the writer
    /// splices.
    ///
    /// It is the REAL old Tradenet 2.0 DCCA export with its 543 data rows deleted — not the
    /// authoring template (<c>Content/excel-template/TransactionFees.xlsx</c>). That distinction
    /// is the whole point: EPPlus 4.1 re-serialized five of the eight static parts when it saved,
    /// so a template-derived skeleton would differ from the file DCCA actually imports
    /// (<c>styles.xml</c> 2383→2975 bytes, <c>workbook.xml</c> 1167→1294, and smaller changes to
    /// <c>[Content_Types].xml</c> and both <c>.rels</c>). Deriving the skeleton from the OUTPUT
    /// makes all eight byte-identical for free. <see cref="Validate"/> guards the mixup.
    ///
    /// Only <c>xl/worksheets/sheet1.xml</c> and <c>xl/sharedStrings.xml</c> depend on the data;
    /// the other eight parts are copied through untouched.
    /// </summary>
    internal static class DccaWorkbookSkeleton
    {
        private const string ResourceName = "API.Dcca.TransactionFeesSkeleton.xlsx";

        internal const string Sheet = "xl/worksheets/sheet1.xml";
        internal const string SharedStrings = "xl/sharedStrings.xml";

        /// <summary>
        /// Zip entry order, copied from the old export. Excel does not care, but the customer
        /// asked for the old file's structure and this is part of it. Note
        /// <c>xl/sharedStrings.xml</c> comes LAST, which is also convenient: the string table is
        /// only complete once every row has streamed.
        /// </summary>
        internal static readonly string[] EntryOrder =
        [
            "[Content_Types].xml",
            "_rels/.rels",
            "xl/workbook.xml",
            "xl/_rels/workbook.xml.rels",
            "xl/theme/theme1.xml",
            "xl/styles.xml",
            Sheet,
            "docProps/core.xml",
            "docProps/app.xml",
            SharedStrings,
        ];

        /// <summary>
        /// Parts the old export DROPPED from the template: the printer settings blob and the
        /// worksheet's relationship file. Dropping them is legal only because
        /// <c>&lt;pageSetup&gt;</c> also lost its <c>r:id</c> — otherwise the package would carry a
        /// dangling relationship and Excel would offer to repair the file. The two facts are
        /// checked together in <see cref="Validate"/>.
        /// </summary>
        internal static readonly string[] DroppedParts =
        [
            "xl/printerSettings/printerSettings1.bin",
            "xl/worksheets/_rels/sheet1.xml.rels",
        ];

        /// <summary>The nine header strings, which are always sst entries 0-8, in column order.</summary>
        internal static readonly string[] HeaderStrings =
        [
            "No", "Entry Date", "HtaThaKa No", "Company Name", "Transation Title",
            "Deducted Fees", "Remark", "Account Code", "Location Code",
        ];

        private static readonly Lazy<Parts> Loaded = new(Load, isThreadSafe: true);

        internal static Parts Current => Loaded.Value;

        /// <summary>
        /// The eight data-independent parts, plus the sheet and string-table fragments the writer
        /// splices its rows between.
        /// </summary>
        internal sealed class Parts
        {
            /// <summary>Part name → payload, for the eight parts copied through verbatim.</summary>
            public required IReadOnlyDictionary<string, byte[]> StaticParts { get; init; }

            /// <summary>Everything up to and including <c>&lt;dimension ref="</c>.</summary>
            public required byte[] SheetBeforeDimensionRef { get; init; }

            /// <summary>From <c>"/&gt;</c> after the dimension ref through <c>&lt;sheetData&gt;</c> and the header row.</summary>
            public required byte[] SheetAfterDimensionRefThroughHeaderRow { get; init; }

            /// <summary><c>&lt;/sheetData&gt;</c> to end of document.</summary>
            public required byte[] SheetAfterRows { get; init; }

            /// <summary>Declaration and <c>&lt;sst …</c> up to <c>count="</c>.</summary>
            public required byte[] SharedStringsBeforeCount { get; init; }

            /// <summary><c>" uniqueCount="</c> — the literal between the two counts.</summary>
            public required byte[] SharedStringsBetweenCounts { get; init; }

            /// <summary><c>"&gt;</c> closing the sst tag, through the nine header <c>&lt;si&gt;</c> elements.</summary>
            public required byte[] SharedStringsAfterCountThroughHeaders { get; init; }

            /// <summary><c>&lt;/sst&gt;</c>.</summary>
            public required byte[] SharedStringsSuffix { get; init; }
        }

        private static Parts Load()
        {
            using var stream = typeof(DccaWorkbookSkeleton).Assembly
                .GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException(
                    $"The DCCA skeleton workbook '{ResourceName}' is not embedded in " +
                    $"{typeof(DccaWorkbookSkeleton).Assembly.GetName().Name}. Check the " +
                    "EmbeddedResource item (and its LogicalName) in API.csproj.");

            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var entry in archive.Entries)
            {
                using var entryStream = entry.Open();
                using var buffer = new MemoryStream();
                entryStream.CopyTo(buffer);
                payloads[entry.FullName] = buffer.ToArray();
            }

            var sheet = Required(payloads, Sheet);
            var sst = Required(payloads, SharedStrings);

            var staticParts = payloads
                .Where(pair => pair.Key != Sheet && pair.Key != SharedStrings)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

            // The dimension ref is rewritten per export (A1:I{lastRow}), so split around its
            // value rather than reproducing the surrounding markup by hand. Splicing the
            // skeleton's own bytes is what keeps sheetViews, sheetFormatPr, all seven <col>
            // elements (with their re-serialized attribute order), the header row, pageMargins,
            // pageSetup and <headerFooter/> exact — none of it is retyped anywhere.
            var dimensionRef = Utf8("<dimension ref=\"");
            var dimensionStart = IndexOf(sheet, dimensionRef, 0);
            var dimensionValueStart = dimensionStart + dimensionRef.Length;
            var dimensionValueEnd = IndexOf(sheet, Utf8("\""), dimensionValueStart);

            // Rows are injected between the header row's </row> and </sheetData>. The skeleton
            // has exactly one row, so the first </row> is the header's.
            var headerRowEnd = IndexOf(sheet, Utf8("</row>"), 0) + "</row>".Length;
            var sheetDataEnd = IndexOf(sheet, Utf8("</sheetData>"), headerRowEnd);

            var countAttribute = Utf8(" count=\"");
            var countStart = IndexOf(sst, countAttribute, 0);
            var countValueStart = countStart + countAttribute.Length;
            var countValueEnd = IndexOf(sst, Utf8("\""), countValueStart);

            var uniqueAttribute = Utf8(" uniqueCount=\"");
            var uniqueStart = IndexOf(sst, uniqueAttribute, countValueEnd);
            var uniqueValueStart = uniqueStart + uniqueAttribute.Length;
            var uniqueValueEnd = IndexOf(sst, Utf8("\""), uniqueValueStart);

            var sstEnd = LastIndexOf(sst, Utf8("</sst>"));

            return new Parts
            {
                StaticParts = staticParts,
                SheetBeforeDimensionRef = sheet[..dimensionValueStart],
                SheetAfterDimensionRefThroughHeaderRow = sheet[dimensionValueEnd..headerRowEnd],
                SheetAfterRows = sheet[sheetDataEnd..],
                SharedStringsBeforeCount = sst[..countValueStart],
                SharedStringsBetweenCounts = sst[countValueEnd..uniqueValueStart],
                SharedStringsAfterCountThroughHeaders = sst[uniqueValueEnd..sstEnd],
                SharedStringsSuffix = sst[sstEnd..],
            };
        }

        /// <summary>
        /// Fails loudly if the embedded resource is not the workbook this writer expects.
        ///
        /// The hash-only version of this check is worthless against the one mistake that actually
        /// matters — someone regenerating the skeleton from the authoring TEMPLATE and updating
        /// the hashes in the same commit. So these are semantic markers the template provably
        /// cannot satisfy: it lacks <c>&lt;numFmts count="0" /&gt;</c> and <c>fullCalcOnLoad</c>,
        /// still carries <c>x14ac:knownFonts</c>, and its string table has "Deducted Fees" at
        /// index 4 where the export has "Transation Title".
        ///
        /// Callers LOG rather than throw: a bad resource must break the DCCA export only, not an
        /// API serving ~160 other reports.
        /// </summary>
        internal static IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            Parts parts;
            try
            {
                parts = Current;
            }
            catch (Exception ex)
            {
                return [$"The DCCA skeleton workbook could not be read: {ex.Message}"];
            }

            foreach (var name in EntryOrder)
            {
                if (name != Sheet && name != SharedStrings && !parts.StaticParts.ContainsKey(name))
                {
                    problems.Add($"The DCCA skeleton is missing the part '{name}'.");
                }
            }

            foreach (var dropped in DroppedParts)
            {
                if (parts.StaticParts.ContainsKey(dropped))
                {
                    problems.Add(
                        $"The DCCA skeleton still contains '{dropped}'. The old export dropped it, " +
                        "and keeping it while <pageSetup> has no r:id makes Excel repair the file.");
                }
            }

            var styles = Text(parts, "xl/styles.xml");
            if (styles != null)
            {
                if (!styles.Contains("<numFmts count=\"0\" />", StringComparison.Ordinal))
                {
                    problems.Add(
                        "xl/styles.xml has no '<numFmts count=\"0\" />' — this looks like the " +
                        "authoring template, not the old export. Regenerate the skeleton from " +
                        "uploads/AccountSummary.xlsx.");
                }

                if (styles.Contains("x14ac:knownFonts=\"1\"", StringComparison.Ordinal))
                {
                    problems.Add(
                        "xl/styles.xml still has 'x14ac:knownFonts=\"1\"' — the old export dropped " +
                        "it, so this is the authoring template.");
                }
            }

            var workbook = Text(parts, "xl/workbook.xml");
            if (workbook != null && !workbook.Contains("fullCalcOnLoad=\"1\"", StringComparison.Ordinal))
            {
                problems.Add(
                    "xl/workbook.xml has no 'fullCalcOnLoad=\"1\"' — the old export added it, so " +
                    "this is the authoring template.");
            }

            var sheetTail = Encoding.UTF8.GetString(parts.SheetAfterRows);
            if (sheetTail.Contains("r:id", StringComparison.Ordinal))
            {
                problems.Add(
                    "The DCCA skeleton's <pageSetup> still has an r:id, but the printer-settings " +
                    "relationship is dropped. Excel would repair the file.");
            }

            var headers = Encoding.UTF8.GetString(parts.SharedStringsAfterCountThroughHeaders);
            for (var i = 0; i < HeaderStrings.Length; i++)
            {
                if (!headers.Contains($"<si><t>{HeaderStrings[i]}</t></si>", StringComparison.Ordinal))
                {
                    problems.Add($"The DCCA skeleton's string table is missing header '{HeaderStrings[i]}'.");
                }
            }

            // The template's index 4 is "Deducted Fees"; the export rebuilt the table in
            // first-use order, so index 4 must be "Transation Title".
            var order = headers.Split("<si><t>", StringSplitOptions.RemoveEmptyEntries);
            if (order.Length > 5 && !order[5].StartsWith("Transation Title", StringComparison.Ordinal))
            {
                problems.Add(
                    "The DCCA skeleton's string table is not in first-use order (index 4 should be " +
                    "'Transation Title'). This looks like the authoring template.");
            }

            return problems;
        }

        private static string? Text(Parts parts, string name)
            => parts.StaticParts.TryGetValue(name, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;

        private static byte[] Required(IReadOnlyDictionary<string, byte[]> parts, string name)
            => parts.TryGetValue(name, out var value)
                ? value
                : throw new InvalidOperationException($"The DCCA skeleton workbook has no '{name}' part.");

        private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

        private static int IndexOf(byte[] haystack, byte[] needle, int start)
        {
            var index = haystack.AsSpan(start).IndexOf(needle);
            return index >= 0
                ? index + start
                : throw new InvalidOperationException(
                    $"The DCCA skeleton workbook does not contain '{Encoding.UTF8.GetString(needle)}'.");
        }

        private static int LastIndexOf(byte[] haystack, byte[] needle)
        {
            var index = haystack.AsSpan().LastIndexOf(needle);
            return index >= 0
                ? index
                : throw new InvalidOperationException(
                    $"The DCCA skeleton workbook does not contain '{Encoding.UTF8.GetString(needle)}'.");
        }
    }
}
