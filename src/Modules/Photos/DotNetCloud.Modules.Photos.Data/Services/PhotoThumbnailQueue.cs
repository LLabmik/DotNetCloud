using System.Threading.Channels;

namespace DotNetCloud.Modules.Photos.Data.Services;

/// <summary>
/// A photo whose thumbnails still need to be generated.
/// </summary>
/// <param name="PhotoId">Photo record to generate thumbnails for.</param>
/// <param name="StoragePath">Content-addressable storage path of the original, when known.</param>
/// <param name="MimeType">Original MIME type.</param>
public sealed record PhotoThumbnailWorkItem(Guid PhotoId, string? StoragePath, string MimeType);

/// <summary>
/// Bounded in-process queue of pending thumbnail jobs.
/// Decoupling generation from indexing keeps the library import pass insert-only, so a scan
/// finishes quickly and thumbnails fill in afterwards instead of blocking the import loop.
/// </summary>
public interface IPhotoThumbnailQueue
{
    /// <summary>Number of jobs waiting to be processed.</summary>
    int PendingCount { get; }

    /// <summary>
    /// Attempts to enqueue a job without blocking.
    /// Returns <c>false</c> when the queue is full (caller should fall back to inline generation)
    /// or when the queue has been completed.
    /// </summary>
    /// <param name="item">Job to enqueue.</param>
    bool TryEnqueue(PhotoThumbnailWorkItem item);

    /// <summary>Waits for the next job.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<PhotoThumbnailWorkItem> DequeueAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Default <see cref="IPhotoThumbnailQueue"/> implementation backed by a bounded channel.
/// The bound keeps memory usage predictable; a full queue degrades to inline generation.
/// </summary>
public sealed class PhotoThumbnailQueue : IPhotoThumbnailQueue
{
    /// <summary>Default maximum number of queued jobs.</summary>
    public const int DefaultCapacity = 512;

    private readonly Channel<PhotoThumbnailWorkItem> _channel;

    /// <summary>Initializes a queue with <see cref="DefaultCapacity"/> slots.</summary>
    public PhotoThumbnailQueue()
        : this(DefaultCapacity)
    {
    }

    /// <summary>Initializes a queue with the given capacity.</summary>
    /// <param name="capacity">Maximum number of queued jobs (must be positive).</param>
    public PhotoThumbnailQueue(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _channel = Channel.CreateBounded<PhotoThumbnailWorkItem>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <inheritdoc />
    public int PendingCount => _channel.Reader.Count;

    /// <inheritdoc />
    public bool TryEnqueue(PhotoThumbnailWorkItem item) => _channel.Writer.TryWrite(item);

    /// <inheritdoc />
    public ValueTask<PhotoThumbnailWorkItem> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken);
}
