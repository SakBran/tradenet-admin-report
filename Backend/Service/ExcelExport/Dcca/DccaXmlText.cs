using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace API.Service.ExcelExport.Dcca
{
    /// <summary>
    /// The string and number encoding EPPlus 4.1 used, reproduced exactly.
    ///
    /// These rules are NOT reverse-engineered from the output — they are the literals in the
    /// shipped assembly (<c>tradenet-2.0-admin/TradenetAdmin/bin/EPPlus.dll</c>, 1,250,304 bytes),
    /// whose string heap holds, contiguously:
    ///
    ///   &amp;  &amp;amp;  &lt;  &amp;lt;  &gt;  &amp;gt;  (_x[0-9A-F]{4,4}_)  _x005F  _x00{0}_
    ///
    /// and, in the shared-strings block:
    ///
    ///   count="{0}" uniqueCount="{0}"&gt;   &lt;si&gt;   &lt;/si&gt;   "  "   "\t"   "\n"
    ///   &lt;si&gt;&lt;t xml:space="preserve"&gt;   &lt;si&gt;&lt;t&gt;   &lt;/t&gt;&lt;/si&gt;   &lt;/sst&gt;
    ///
    /// Getting this wrong is a data bug, not a cosmetic one: 17 of the 498 strings in the real
    /// 2021 export carry <c>xml:space="preserve"</c> because company names arrive padded
    /// (<c>"Shwe Htut Khaung Co., Ltd.                    "</c>) or double-spaced
    /// (<c>"HONEYS  GARMENT  INDUSTRY  LIMITED"</c>). Dropping the attribute would silently trim
    /// them on import.
    /// </summary>
    internal static class DccaXmlText
    {
        // EPPlus: "(_x[0-9A-F]{4,4}_)" -> the match is prefixed with "_x005F" so a literal that
        // already looks like an escape survives a round trip.
        private static readonly Regex AlreadyEscaped = new(
            "(_x[0-9A-F]{4,4}_)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// EPPlus escapes <c>&amp;</c>, <c>&lt;</c> and <c>&gt;</c> and nothing else — notably NOT
        /// the apostrophe (the real export contains one raw <c>'</c> and zero <c>&amp;apos;</c>)
        /// and not the double quote. Control characters become <c>_x00NN_</c>.
        /// </summary>
        internal static string Encode(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // "_x005F" doubling runs FIRST, or it would also rewrite the escapes we emit below.
            var escaped = AlreadyEscaped.Replace(value, "_x005F$1");

            // Ampersand first: doing it later would re-escape the & of &lt; and &gt;.
            escaped = escaped
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);

            return EncodeControlCharacters(escaped);
        }

        private static string EncodeControlCharacters(string value)
        {
            var needed = false;
            foreach (var c in value)
            {
                if (IsEncodedControlCharacter(c))
                {
                    needed = true;
                    break;
                }
            }

            if (!needed)
            {
                return value;
            }

            var builder = new System.Text.StringBuilder(value.Length + 8);
            foreach (var c in value)
            {
                if (IsEncodedControlCharacter(c))
                {
                    // EPPlus's "_x00{0}_" with {0} = the two-digit upper-case hex code.
                    builder.Append("_x00").Append(((int)c).ToString("X2", CultureInfo.InvariantCulture)).Append('_');
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        // Tab, newline and carriage return are legal XML text and EPPlus leaves them alone —
        // they are also part of the preserve predicate below, which would be meaningless if they
        // were encoded away.
        private static bool IsEncodedControlCharacter(char c)
            => c < 0x20 && c != '\t' && c != '\n' && c != '\r';

        /// <summary>
        /// Whether the <c>&lt;t&gt;</c> element needs <c>xml:space="preserve"</c>.
        ///
        /// Verified against every one of the 498 strings in the real 2021 export: 17 need it,
        /// 481 do not, zero mismatches.
        /// </summary>
        internal static bool NeedsSpacePreserved(string value)
            => value.Length > 0
                && (value[0] == ' '
                    || value[^1] == ' '
                    || value.Contains("  ", StringComparison.Ordinal)
                    || value.Contains('\t', StringComparison.Ordinal)
                    || value.Contains('\n', StringComparison.Ordinal));

        /// <summary>
        /// The amount, as the old chain produced it.
        ///
        /// <c>sp_AccountSummaryReport_pagination.sql</c> declares <c>Amount float</c>. The old
        /// path was <c>float → .NET Framework double.ToString()</c> — which defaulted to <b>G15</b>,
        /// 15 significant digits — then <c>decimal.Parse</c> (<c>Business/Reports.cs:4770</c>),
        /// and EPPlus formatted a <c>decimal</c>. That G15 hop is load-bearing: a float artefact
        /// like <c>1234.5600000000001</c> reached the old file as <c>1234.56</c>.
        ///
        /// .NET Core's default <c>double.ToString()</c> is shortest-round-trippable, NOT G15, so
        /// it would emit <c>1234.5600000000001</c> and diverge. Reproduce the round trip rather
        /// than guessing a format string.
        /// </summary>
        internal static string Number(double value)
        {
            var g15 = value.ToString("G15", CultureInfo.InvariantCulture);

            // Out of decimal's range (or NaN/Infinity) — no legacy behaviour to match, and the
            // fee column has never held such a value. Fall back to the G15 text itself.
            return decimal.TryParse(
                g15,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var exact)
                ? exact.ToString(CultureInfo.InvariantCulture)
                : g15;
        }
    }
}
