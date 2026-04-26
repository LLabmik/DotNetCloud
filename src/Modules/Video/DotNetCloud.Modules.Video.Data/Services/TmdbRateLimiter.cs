namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Shared rate limiter for TMDB API requests.
/// Enforces a configurable minimum delay between requests.
/// </summary>
public sealed class TmdbRateLimiter : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly int _delayMs;
    private DateTime _lastRequestTime = DateTime.MinValue;

    public TmdbRateLimiter(int delayMs = 300)
    {
        _delayMs = delayMs;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        var elapsed = DateTime.UtcNow - _lastRequestTime;
        var remainingMs = _delayMs - (int)elapsed.TotalMilliseconds;
        if (remainingMs > 0)
            await Task.Delay(remainingMs, cancellationToken);
    }

    public void Release()
    {
        _lastRequestTime = DateTime.UtcNow;
        _semaphore.Release();
    }

    public void Dispose() => _semaphore.Dispose();
}
