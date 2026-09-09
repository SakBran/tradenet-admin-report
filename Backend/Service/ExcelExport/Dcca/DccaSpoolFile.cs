using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace API.Service.ExcelExport.Dcca
{
    /// <summary>
    /// A write-then-read-once temp file, deflate-compressed on the way in.
    ///
    /// Two things force staging rather than streaming straight into the archive:
    /// <c>&lt;dimension ref="A1:I{last}"/&gt;</c> sits at the TOP of the sheet but the extent is
    /// only known once every row has arrived, and shared-string indices are assigned as rows
    /// stream. Pre-counting is not an option — that <c>COUNT(*)</c> is exactly what times this
    /// report out, which is why <c>WriteRowsAsync</c> passes <c>includeTotalCount: false</c>.
    ///
    /// Compressing the spool keeps the tail case boring: the sheet body measures ~343 bytes/row,
    /// so a million-row export is ~343 MB raw but ~38 MB spooled. <see cref="FileOptions.DeleteOnClose"/>
    /// means a crash or a cancelled job leaves nothing behind. Temp-file staging is already the
    /// house pattern — <c>FtpExcelExportFileStore</c> stages uploads the same way.
    /// </summary>
    internal sealed class DccaSpoolFile : IAsyncDisposable
    {
        private readonly FileStream _file;
        private DeflateStream? _compressor;

        internal DccaSpoolFile()
        {
            _file = new FileStream(
                Path.Combine(Path.GetTempPath(), $"dcca-{Guid.NewGuid():N}.tmp"),
                new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.ReadWrite,
                    Share = FileShare.None,
                    Options = FileOptions.DeleteOnClose | FileOptions.Asynchronous,
                });

            _compressor = new DeflateStream(_file, CompressionLevel.Fastest, leaveOpen: true);
        }

        internal async ValueTask WriteAsync(byte[] bytes, CancellationToken cancellationToken)
        {
            if (_compressor == null)
            {
                throw new InvalidOperationException("The DCCA spool has already been sealed for reading.");
            }

            await _compressor.WriteAsync(bytes, cancellationToken);
        }

        /// <summary>Flushes the deflate stream and rewinds, so the content can be copied out once.</summary>
        internal async ValueTask<Stream> SealAsync(CancellationToken cancellationToken)
        {
            if (_compressor != null)
            {
                await _compressor.FlushAsync(cancellationToken);
                await _compressor.DisposeAsync();
                _compressor = null;
            }

            _file.Seek(0, SeekOrigin.Begin);
            return new DeflateStream(_file, CompressionMode.Decompress, leaveOpen: true);
        }

        public async ValueTask DisposeAsync()
        {
            if (_compressor != null)
            {
                await _compressor.DisposeAsync();
                _compressor = null;
            }

            await _file.DisposeAsync();
        }
    }
}
