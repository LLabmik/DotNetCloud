using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Shared rate limiter for TMDB API requests.
/// Enforces a configurable minimum delay between requests.
/// </summary>
public sealed class TmdbRateLimiter : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly int _delayMs;
    private readonly ILogger<TmdbRateLimiter> _logger;
    private DateTime _lastRequestTime = DateTime.MinValue;

    public TmdbRateLimiter(int delayMs = 300, ILogger<TmdbRateLimiter>? logger = null)
    {
        _delayMs = delayMs;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TmdbRateLimiter>.Instance;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        var elapsed = DateTime.UtcNow - _lastRequestTime;
        var remainingMs = _delayMs - (int)elapsed.TotalMilliseconds;
        if (remainingMs > 0)
        {
            _logger.LogDebug("TMDB rate limiter: waiting {DelayMs}ms before next request", remainingMs);
            await Task.Delay(remainingMs, cancellationToken);
        }
    }

    public void Release()
    {
        _lastRequestTime = DateTime.UtcNow;
        _semaphore.Release();
    }

    public void Dispose() => _semaphore.Dispose();
}
