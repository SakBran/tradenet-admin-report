using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace API.Service.ExcelExport
{
    public static class AsyncChunking
    {
        /// <summary>
        /// Batches a streamed source into fixed-size lists, so rows are written to
        /// the workbook (and reference lookups resolved) a chunk at a time while
        /// the DB cursor streams — the full set never sits in memory.
        /// </summary>
        public static async IAsyncEnumerable<List<T>> ChunkAsync<T>(
            this IAsyncEnumerable<T> source,
            int chunkSize,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var buffer = new List<T>(chunkSize);
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                buffer.Add(item);
                if (buffer.Count >= chunkSize)
                {
                    yield return buffer;
                    buffer = new List<T>(chunkSize);
                }
            }

            if (buffer.Count > 0)
            {
                yield return buffer;
            }
        }
    }
}
