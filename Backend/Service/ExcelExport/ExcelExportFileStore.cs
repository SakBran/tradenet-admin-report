using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Service.ExcelExport
{
    /// <summary>Resolves, opens, checks and deletes generated export files on disk.</summary>
    public interface IExcelExportFileStore
    {
        /// <summary>Relative path (stored on the job) for a new file with the given name.</summary>
        string BuildRelativePath(string fileName);

        /// <summary>Opens a writable stream for a new file, creating folders as needed.</summary>
        Stream OpenWrite(string relativePath);

        /// <summary>Opens a read stream; throws FileNotFoundException if missing.</summary>
        Stream OpenRead(string relativePath);

        bool Exists(string? relativePath);

        long GetSize(string relativePath);

        void Delete(string? relativePath);
    }

    public sealed class ExcelExportFileStore : IExcelExportFileStore
    {
        private readonly string _root;

        public ExcelExportFileStore(
            IOptions<ExcelExportOptions> options,
            IWebHostEnvironment env,
            ILogger<ExcelExportFileStore> logger)
        {
            var configured = options.Value.StorageRoot;

            string resolved;
            if (string.IsNullOrWhiteSpace(configured))
            {
                // No explicit config: use a per-OS temp location the published process can
                // always write to. Avoids writing inside the deployed app folder, which on a
                // network share / under IIS is typically not writable by the app-pool identity.
                resolved = Path.Combine(Path.GetTempPath(), "TradenetAdminReport", "ExcelExports");
            }
            else if (Path.IsPathRooted(configured))
            {
                resolved = configured;
            }
            else
            {
                resolved = Path.Combine(env.ContentRootPath, configured);
            }

            // Normalize so the path uses a single, consistent separator. The FullPath() guard
            // compares the (normalized) candidate against _root with StartsWith, so a raw _root
            // with mixed separators (e.g. "P:\app\App_Data/ExcelExports" on Windows) would make
            // every write fail with "escapes the storage root". GetFullPath fixes that.
            _root = Path.GetFullPath(resolved);

            // Created lazily on first write (see OpenWrite), so a non-writable path surfaces as a
            // clean per-job failure inside the worker's try/catch rather than crashing construction.
            logger.LogInformation("Excel export storage root resolved to {StorageRoot}.", _root);
        }

        public string BuildRelativePath(string fileName)
        {
            // Shard by date so a single folder doesn't accumulate unbounded files.
            var dayFolder = DateTime.UtcNow.ToString("yyyyMMdd");
            return Path.Combine(dayFolder, fileName);
        }

        public Stream OpenWrite(string relativePath)
        {
            var full = FullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            return new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.None);
        }

        public Stream OpenRead(string relativePath)
        {
            return new FileStream(FullPath(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public bool Exists(string? relativePath)
        {
            return !string.IsNullOrEmpty(relativePath) && File.Exists(FullPath(relativePath));
        }

        public long GetSize(string relativePath) => new FileInfo(FullPath(relativePath)).Length;

        public void Delete(string? relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return;
            }

            var full = FullPath(relativePath);
            if (File.Exists(full))
            {
                File.Delete(full);
            }
        }

        private string FullPath(string relativePath)
        {
            // Guard against path traversal in stored relative paths. Compare against _root with a
            // trailing separator so (a) separators match (_root is normalized in the ctor) and
            // (b) a sibling folder sharing the prefix (e.g. "...ExcelExports2") can't slip through.
            var full = Path.GetFullPath(Path.Combine(_root, relativePath));
            var rootWithSep = _root.EndsWith(Path.DirectorySeparatorChar)
                ? _root
                : _root + Path.DirectorySeparatorChar;
            if (!full.StartsWith(rootWithSep, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Resolved export path escapes the storage root.");
            }

            return full;
        }
    }
}
