namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Shared rate limiter for MusicBrainz and Cover Art Archive API requests.
/// MusicBrainz mandates max 1 request per second; this enforces that constraint
/// across all clients sharing this instance.
/// </summary>
public sealed class MusicBrainzRateLimiter : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly int _delayMs;
    private DateTime _lastRequestTime = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicBrainzRateLimiter"/> class.
    /// </summary>
    /// <param name="delayMs">Minimum delay between requests in milliseconds (default: 1100ms).</param>
    public MusicBrainzRateLimiter(int delayMs = 1100)
    {
        _delayMs = delayMs;
    }

    /// <summary>
    /// Waits until it is safe to make the next API request, ensuring the minimum delay
    /// between requests is respected. Acquires a semaphore to serialize concurrent calls.
    /// Caller MUST call <see cref="Release"/> after the request completes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        var elapsed = DateTime.UtcNow - _lastRequestTime;
        var remainingMs = _delayMs - (int)elapsed.TotalMilliseconds;
        if (remainingMs > 0)
        {
            await Task.Delay(remainingMs, cancellationToken);
        }
    }

    /// <summary>
    /// Records the request timestamp and releases the semaphore.
    /// Must be called after every <see cref="WaitAsync"/> call.
    /// </summary>
    public void Release()
    {
        _lastRequestTime = DateTime.UtcNow;
        _semaphore.Release();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
