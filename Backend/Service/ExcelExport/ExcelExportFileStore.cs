using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
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

        public ExcelExportFileStore(IOptions<ExcelExportOptions> options, IWebHostEnvironment env)
        {
            var configured = options.Value.StorageRoot;
            _root = Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(env.ContentRootPath, configured);
            Directory.CreateDirectory(_root);
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
            // Guard against path traversal in stored relative paths.
            var full = Path.GetFullPath(Path.Combine(_root, relativePath));
            if (!full.StartsWith(_root, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Resolved export path escapes the storage root.");
            }

            return full;
        }
    }
}
