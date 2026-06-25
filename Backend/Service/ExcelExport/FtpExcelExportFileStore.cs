using System;
using System.IO;
using FluentFTP;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// <see cref="IExcelExportFileStore"/> backed by an FTP server (FluentFTP). Selected when
    /// ExcelExport:Storage = "Ftp". Writes are staged to a local temp file while generating and
    /// uploaded on <see cref="ICommittableStream.Commit"/> (success only); downloads are staged
    /// to a local temp file served with delete-on-close. Each FTP operation uses its own short
    /// connection, so a failure surfaces as a clean per-job error rather than a stuck job.
    /// </summary>
    public sealed class FtpExcelExportFileStore : IExcelExportFileStore
    {
        private readonly ExcelExportFtpOptions _ftp;
        private readonly string _basePath;
        private readonly string _stagingDir;
        private readonly ILogger<FtpExcelExportFileStore> _logger;

        public FtpExcelExportFileStore(IOptions<ExcelExportOptions> options, ILogger<FtpExcelExportFileStore> logger)
        {
            _ftp = options.Value.Ftp ?? new ExcelExportFtpOptions();
            _logger = logger;

            if (string.IsNullOrWhiteSpace(_ftp.Host))
            {
                throw new InvalidOperationException(
                    "ExcelExport:Storage is 'Ftp' but ExcelExport:Ftp:Host is not configured.");
            }

            _basePath = NormalizeFtpDir(_ftp.BasePath);
            _stagingDir = Path.Combine(Path.GetTempPath(), "TradenetAdminReport", "FtpStaging");

            _logger.LogInformation(
                "Excel export storage: FTP ftp://{Host}:{Port}{BasePath} (user '{User}', staging '{Staging}').",
                _ftp.Host, _ftp.Port, _basePath, _ftp.Username, _stagingDir);
        }

        public string BuildRelativePath(string fileName)
        {
            // Shard by date; FTP paths always use forward slashes.
            var dayFolder = DateTime.UtcNow.ToString("yyyyMMdd");
            return $"{dayFolder}/{SanitizeSegment(fileName)}";
        }

        public Stream OpenWrite(string relativePath)
        {
            // Stage to a local temp file; the real upload happens in Commit() (success path only),
            // so a failed/aborted generation never publishes a partial file to the server.
            Directory.CreateDirectory(_stagingDir);
            var localTemp = Path.Combine(_stagingDir, Guid.NewGuid().ToString("N") + ".tmp");
            var fs = new FileStream(localTemp, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            return new FtpUploadStream(this, fs, localTemp, RemotePath(relativePath));
        }

        public Stream OpenRead(string relativePath)
        {
            Directory.CreateDirectory(_stagingDir);
            var localTemp = Path.Combine(_stagingDir, Guid.NewGuid().ToString("N") + ".dl");

            try
            {
                using var client = CreateConnectedClient();
                var status = client.DownloadFile(localTemp, RemotePath(relativePath));
                if (status != FtpStatus.Success)
                {
                    throw new FileNotFoundException($"FTP download failed for '{relativePath}' (status {status}).");
                }
            }
            catch
            {
                SafeDeleteLocal(localTemp);
                throw;
            }

            // Delete-on-close: the temp file is removed once the HTTP response stream is disposed.
            return new FileStream(localTemp, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, 81920, FileOptions.DeleteOnClose);
        }

        public bool Exists(string? relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return false;
            }

            try
            {
                using var client = CreateConnectedClient();
                return client.FileExists(RemotePath(relativePath));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FTP Exists check failed for {Path}.", relativePath);
                return false;
            }
        }

        public long GetSize(string relativePath)
        {
            using var client = CreateConnectedClient();
            var size = client.GetFileSize(RemotePath(relativePath), -1);
            return size < 0 ? 0 : size;
        }

        public void Delete(string? relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return;
            }

            try
            {
                using var client = CreateConnectedClient();
                var remote = RemotePath(relativePath);
                if (client.FileExists(remote))
                {
                    client.DeleteFile(remote);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FTP delete failed for {Path}.", relativePath);
            }
        }

        // --- internals ---

        /// <summary>Uploads a fully-written local staging file to its remote path (creates dirs).</summary>
        private void Upload(string localPath, string remotePath)
        {
            using var client = CreateConnectedClient();
            var status = client.UploadFile(localPath, remotePath, FtpRemoteExists.Overwrite, createRemoteDir: true);
            if (status == FtpStatus.Failed)
            {
                throw new IOException($"FTP upload failed for '{remotePath}'.");
            }
        }

        private FtpClient CreateConnectedClient()
        {
            var client = new FtpClient(
                _ftp.Host,
                _ftp.Username,
                _ftp.Password,
                _ftp.Port <= 0 ? 21 : _ftp.Port);

            client.Config.EncryptionMode = FtpEncryptionMode.None; // plain ftp://
            client.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;

            var connectMs = Math.Max(5, _ftp.ConnectTimeoutSeconds) * 1000;
            var opMs = Math.Max(5, _ftp.OperationTimeoutSeconds) * 1000;
            client.Config.ConnectTimeout = connectMs;
            client.Config.ReadTimeout = opMs;
            client.Config.DataConnectionConnectTimeout = connectMs;
            client.Config.DataConnectionReadTimeout = opMs;

            try
            {
                client.Connect();
            }
            catch
            {
                client.Dispose();
                throw;
            }

            return client;
        }

        private string RemotePath(string relativePath)
        {
            var rel = relativePath.Replace('\\', '/').TrimStart('/');
            if (rel.Contains("..", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Resolved export path escapes the storage root.");
            }

            return _basePath.TrimEnd('/') + "/" + rel;
        }

        private static string NormalizeFtpDir(string? dir)
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                return "/";
            }

            var d = dir.Replace('\\', '/').Trim();
            return d.StartsWith('/') ? d : "/" + d;
        }

        private static string SanitizeSegment(string name)
            => name.Replace('\\', '_').Replace('/', '_');

        private static void SafeDeleteLocal(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch { /* best effort */ }
        }

        /// <summary>Buffers the export to a local temp file; uploads to FTP only on Commit().</summary>
        private sealed class FtpUploadStream : Stream, ICommittableStream
        {
            private readonly FtpExcelExportFileStore _store;
            private readonly FileStream _inner;
            private readonly string _localPath;
            private readonly string _remotePath;
            private bool _committed;
            private bool _disposed;

            public FtpUploadStream(FtpExcelExportFileStore store, FileStream inner, string localPath, string remotePath)
            {
                _store = store;
                _inner = inner;
                _localPath = localPath;
                _remotePath = remotePath;
            }

            public void Commit()
            {
                if (_committed)
                {
                    return;
                }

                _inner.Flush();
                _inner.Dispose(); // close so the file is fully on disk before upload
                _store.Upload(_localPath, _remotePath);
                _committed = true;
            }

            protected override void Dispose(bool disposing)
            {
                if (!_disposed && disposing)
                {
                    _disposed = true;
                    try { _inner.Dispose(); } catch { /* may already be closed by Commit() */ }
                    SafeDeleteLocal(_localPath); // discard staging; if not committed, nothing was uploaded
                }

                base.Dispose(disposing);
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => _inner.CanSeek;
            public override bool CanWrite => _inner.CanWrite;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => _inner.Position = value; }
            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void SetLength(long value) => _inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        }
    }
}
