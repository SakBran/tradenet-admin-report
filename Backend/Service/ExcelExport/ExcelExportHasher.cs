using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// Builds the dedup key for an export request: report key + a canonical form
    /// of the request that ignores grid-only paging/sorting fields, so two
    /// requests that differ only in page/sort hash identically.
    /// </summary>
    public static class ExcelExportHasher
    {
        // Grid/transport-only fields from ReportQueryRequest — never affect the exported data set.
        private static readonly HashSet<string> IgnoredFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "pageIndex",
            "pageSize",
            "sortColumn",
            "sortOrder",
            "filterColumn",
            "filterQuery",
            "includeTotalCount"
        };

        public static string ComputeHash(string reportKey, string requestJson)
        {
            var canonical = Canonicalize(requestJson);
            var payload = reportKey + "|" + canonical;
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        /// <summary>
        /// Re-serializes the request object's top-level properties with keys
        /// lower-cased and sorted, dropping the ignored grid fields. Nested values
        /// are kept as-is (the report DTOs are flat in practice).
        /// </summary>
        private static string Canonicalize(string requestJson)
        {
            if (string.IsNullOrWhiteSpace(requestJson))
            {
                return string.Empty;
            }

            using var doc = JsonDocument.Parse(requestJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return requestJson;
            }

            var pairs = doc.RootElement
                .EnumerateObject()
                .Where(p => !IgnoredFields.Contains(p.Name))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.Name.ToLowerInvariant() + "=" + p.Value.GetRawText());

            return string.Join("&", pairs);
        }
    }
}
