using System.Collections.Concurrent;

namespace DotNetCloud.Core.Server.Diagnostics;

/// <summary>
/// Process-wide traffic counters for client HTTP payload and WebSocket frames.
/// </summary>
/// <remarks>
/// Counters are cumulative since the core process started and reset on restart.
/// Counting uses <see cref="Interlocked"/> only — no locks and no allocation on the
/// request hot path. A short ring buffer of (timestamp, totals) samples is used to
/// derive bytes-per-second rates without doing math per request.
/// </remarks>
public sealed class TrafficAccounting
{
    private const int RateSampleCount = 10;

    /// <summary>Largest number of users tracked at once before the oldest entry is evicted.</summary>
    internal const int MaxTrackedUsers = 512;

    /// <summary>How long a quiet user is kept (and shown greyed) after becoming idle.</summary>
    internal static readonly TimeSpan IdleGrace = TimeSpan.FromMinutes(5);

    /// <summary>Idle threshold used before the configured value arrives.</summary>
    internal static readonly TimeSpan DefaultIdleThreshold = TimeSpan.FromMinutes(3);

    private readonly object _rateLock = new();
    private readonly LinkedList<RateSample> _samples = new();
    private readonly ConcurrentDictionary<Guid, UserTrafficEntry> _perUser = new();
    private readonly TimeProvider _timeProvider;
    private long _bytesIn;
    private long _bytesOut;
    private TimeSpan _idleThreshold = DefaultIdleThreshold;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrafficAccounting"/> class.
    /// </summary>
    /// <param name="timeProvider">Time source used for timestamps and idle pruning.</param>
    public TrafficAccounting(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
        StartedAtUtc = timeProvider.GetUtcNow();
    }

    /// <summary>Gets when counting started (core process start, UTC).</summary>
    public DateTimeOffset StartedAtUtc { get; }

    /// <summary>Gets the total bytes received from clients.</summary>
    public long BytesIn => Interlocked.Read(ref _bytesIn);

    /// <summary>Gets the total bytes sent to clients.</summary>
    public long BytesOut => Interlocked.Read(ref _bytesOut);

    /// <summary>Adds received bytes to the running total.</summary>
    /// <param name="count">Byte count; non-positive values are ignored.</param>
    public void AddIn(long count)
    {
        if (count <= 0)
        {
            return;
        }

        Interlocked.Add(ref _bytesIn, count);
    }

    /// <summary>Adds sent bytes to the running total.</summary>
    /// <param name="count">Byte count; non-positive values are ignored.</param>
    public void AddOut(long count)
    {
        if (count <= 0)
        {
            return;
        }

        Interlocked.Add(ref _bytesOut, count);
    }

    /// <summary>
    /// Adds received bytes to the running total and attributes them to a user.
    /// </summary>
    /// <param name="userId">The authenticated user the bytes belong to.</param>
    /// <param name="count">Byte count; non-positive values are ignored.</param>
    internal void AddIn(Guid userId, long count)
    {
        if (count <= 0)
        {
            return;
        }

        Interlocked.Add(ref _bytesIn, count);
        GetOrCreateEntry(userId).AddIn(count, _timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Adds sent bytes to the running total and attributes them to a user.
    /// </summary>
    /// <param name="userId">The authenticated user the bytes belong to.</param>
    /// <param name="count">Byte count; non-positive values are ignored.</param>
    internal void AddOut(Guid userId, long count)
    {
        if (count <= 0)
        {
            return;
        }

        Interlocked.Add(ref _bytesOut, count);
        GetOrCreateEntry(userId).AddOut(count, _timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Applies the admin-configured idle threshold (pushed by the presence activity monitor,
    /// which already re-reads <c>PresenceIdleTimeoutMinutes</c> every 30 s).
    /// </summary>
    /// <param name="threshold">The idle threshold; non-positive values fall back to the default.</param>
    internal void UpdateIdleThreshold(TimeSpan threshold) =>
        _idleThreshold = threshold > TimeSpan.Zero ? threshold : DefaultIdleThreshold;

    /// <summary>
    /// Removes users that have been quiet past the idle threshold plus the grace window, then
    /// enforces the tracked-user cap by evicting the least recently active entries.
    /// </summary>
    /// <returns>The number of entries removed.</returns>
    internal int PruneIdle()
    {
        var now = _timeProvider.GetUtcNow();
        var horizon = _idleThreshold + IdleGrace;
        var removed = 0;

        foreach (var (userId, entry) in _perUser)
        {
            if (now - entry.LastActivityUtc >= horizon && _perUser.TryRemove(userId, out _))
            {
                removed++;
            }
        }

        var overflow = _perUser.Count - MaxTrackedUsers;
        if (overflow > 0)
        {
            foreach (var userId in _perUser
                .OrderBy(pair => pair.Value.LastActivityUtc)
                .Take(overflow)
                .Select(pair => pair.Key)
                .ToList())
            {
                if (_perUser.TryRemove(userId, out _))
                {
                    removed++;
                }
            }
        }

        return removed;
    }

    /// <summary>
    /// Captures one view of every tracked user. <see cref="UserTrafficSample.IsIdle"/> uses the
    /// same threshold as <see cref="PruneIdle"/>, so grey-out and forgetting always agree.
    /// </summary>
    /// <returns>The tracked users, unsorted.</returns>
    internal IReadOnlyList<UserTrafficSample> GetPerUserSnapshot()
    {
        var now = _timeProvider.GetUtcNow();
        var snapshot = new List<UserTrafficSample>(_perUser.Count);

        foreach (var entry in _perUser.Values)
        {
            var lastActivity = entry.LastActivityUtc;
            snapshot.Add(new UserTrafficSample(
                entry.UserId,
                entry.BytesIn,
                entry.BytesOut,
                lastActivity,
                IsIdle: now - lastActivity >= _idleThreshold));
        }

        return snapshot;
    }

    private UserTrafficEntry GetOrCreateEntry(Guid userId) =>
        _perUser.TryGetValue(userId, out var entry)
            ? entry
            : _perUser.GetOrAdd(userId, id => new UserTrafficEntry(id, _timeProvider.GetUtcNow()));

    /// <summary>
    /// Records a rate sample with the supplied timestamp. Called once per second by
    /// the metrics sampler.
    /// </summary>
    /// <param name="at">Timestamp of the sample.</param>
    public void RecordSample(DateTimeOffset at)
    {
        lock (_rateLock)
        {
            _samples.AddLast(new RateSample(at, Interlocked.Read(ref _bytesIn), Interlocked.Read(ref _bytesOut)));
            while (_samples.Count > RateSampleCount)
            {
                _samples.RemoveFirst();
            }
        }
    }

    /// <summary>
    /// Computes average bytes-per-second over the buffered sample window.
    /// </summary>
    /// <returns>In/out rates; both zero when fewer than two samples exist.</returns>
    public (double InPerSecond, double OutPerSecond) GetRates()
    {
        lock (_rateLock)
        {
            if (_samples.Count < 2)
            {
                return (0, 0);
            }

            var first = _samples.First!.Value;
            var last = _samples.Last!.Value;
            var seconds = (last.At - first.At).TotalSeconds;
            if (seconds <= 0)
            {
                return (0, 0);
            }

            var inRate = (last.BytesIn - first.BytesIn) / seconds;
            var outRate = (last.BytesOut - first.BytesOut) / seconds;

            return (Math.Max(0, inRate), Math.Max(0, outRate));
        }
    }

    private readonly record struct RateSample(DateTimeOffset At, long BytesIn, long BytesOut);
}
