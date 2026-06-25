using System;
using System.Threading.Channels;
using API.Model.Activity;
using Microsoft.Extensions.Options;

namespace API.Service.Activity
{
    /// <summary>
    /// In-memory, non-blocking hand-off between request threads (producers) and the
    /// background writer (single consumer). Enqueue never blocks the HTTP request: on
    /// overflow the oldest queued entry is dropped so a burst can't back-pressure users.
    /// </summary>
    public interface IActivityLogQueue
    {
        /// <summary>Queue an entry. Returns false only if the entry could not be accepted.</summary>
        bool TryEnqueue(ActivityLog entry);

        ChannelReader<ActivityLog> Reader { get; }
    }

    public sealed class ActivityLogQueue : IActivityLogQueue
    {
        private readonly Channel<ActivityLog> _channel;

        public ActivityLogQueue(IOptions<ActivityLogOptions> options)
        {
            var capacity = Math.Max(100, options.Value.QueueCapacity);
            _channel = Channel.CreateBounded<ActivityLog>(new BoundedChannelOptions(capacity)
            {
                // Audit logging must never slow a user's request; drop the oldest queued
                // entry rather than block the producer when the writer falls behind.
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
        }

        public bool TryEnqueue(ActivityLog entry) => _channel.Writer.TryWrite(entry);

        public ChannelReader<ActivityLog> Reader => _channel.Reader;
    }
}
