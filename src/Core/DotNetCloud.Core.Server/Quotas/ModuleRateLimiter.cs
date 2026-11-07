using System.Collections.Concurrent;

namespace DotNetCloud.Core.Server.Quotas;

/// <summary>Categories of per-module rate limits (F10, §4.11).</summary>
internal enum ModuleRateLimitKind
{
    /// <summary>Every capability RPC.</summary>
    Request,

    /// <summary>Published events (<c>PublishEvent</c>).</summary>
    Event,

    /// <summary>Sent notifications (<c>SendNotification</c>).</summary>
    Notification,

    /// <summary>Audit entries (<c>LogAudit</c>).</summary>
    Audit,
}

/// <summary>Outcome of a rate-limit acquire attempt.</summary>
internal enum RateLimitOutcome
{
    /// <summary>The call is within the module's quota.</summary>
    Allowed,

    /// <summary>The module is over quota for this window.</summary>
    Limited,

    /// <summary>The module just crossed its quota for this window (log this one only).</summary>
    LimitedFirstInWindow,
}

/// <summary>
/// Identity-keyed, in-memory fixed-window rate limiter for module capability calls.
/// </summary>
/// <remarks>
/// Counters are keyed by module id and category and reset at the top of every UTC minute. The
/// limiter is intentionally local to the process: it exists to keep one misbehaving module from
/// exhausting core CPU, database connections or audit storage, not to meter billing. Rejected
/// calls are counted but never audited, so a runaway module cannot amplify load through the audit
/// path.
/// </remarks>
internal sealed class ModuleRateLimiter
{
    private const int MaxTrackedCounters = 2048;
    private const int PruneInterval = 512;

    private readonly ConcurrentDictionary<string, WindowCounter> _counters = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private int _operations;

    /// <summary>Initializes a new instance of the <see cref="ModuleRateLimiter"/> class.</summary>
    /// <param name="timeProvider">Clock used to derive the current one-minute window.</param>
    public ModuleRateLimiter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Attempts to consume one unit of a module's quota for a category.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="kind">The rate-limit category.</param>
    /// <param name="limitPerMinute">The effective limit; values below 1 mean unlimited.</param>
    /// <returns>The outcome of the attempt.</returns>
    public RateLimitOutcome TryAcquire(string moduleId, ModuleRateLimitKind kind, int limitPerMinute)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (limitPerMinute <= 0)
        {
            return RateLimitOutcome.Allowed;
        }

        var window = _timeProvider.GetUtcNow().ToUnixTimeSeconds() / 60;
        var counter = _counters.GetOrAdd($"{moduleId}\u001f{kind}", static _ => new WindowCounter());
        var outcome = counter.TryIncrement(window, limitPerMinute);

        if (Interlocked.Increment(ref _operations) % PruneInterval == 0)
        {
            Prune(currentWindow: window);
        }

        return outcome;
    }

    /// <summary>Returns how many units have been consumed in the current window for tests and diagnostics.</summary>
    internal int GetCurrentCount(string moduleId, ModuleRateLimitKind kind) =>
        _counters.TryGetValue($"{moduleId}\u001f{kind}", out var counter) ? counter.CurrentCount : 0;

    private void Prune(long currentWindow)
    {
        if (_counters.Count <= MaxTrackedCounters)
        {
            return;
        }

        foreach (var pair in _counters)
        {
            if (pair.Value.Window < currentWindow)
            {
                _counters.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed class WindowCounter
    {
        private readonly object _gate = new();
        private long _window = long.MinValue;
        private int _count;

        public long Window
        {
            get
            {
                lock (_gate)
                {
                    return _window;
                }
            }
        }

        public int CurrentCount
        {
            get
            {
                lock (_gate)
                {
                    return _count;
                }
            }
        }

        public RateLimitOutcome TryIncrement(long window, int limit)
        {
            lock (_gate)
            {
                if (_window != window)
                {
                    _window = window;
                    _count = 0;
                }

                if (_count < limit)
                {
                    _count++;
                    return RateLimitOutcome.Allowed;
                }

                var firstRejection = _count == limit;

                // Saturate instead of overflowing when a module keeps hammering the limit.
                if (_count < limit + 1)
                {
                    _count = limit + 1;
                }

                return firstRejection ? RateLimitOutcome.LimitedFirstInWindow : RateLimitOutcome.Limited;
            }
        }
    }
}
