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

    private readonly object _rateLock = new();
    private readonly LinkedList<RateSample> _samples = new();
    private long _bytesIn;
    private long _bytesOut;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrafficAccounting"/> class.
    /// </summary>
    /// <param name="timeProvider">Time source used to stamp the creation time.</param>
    public TrafficAccounting(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
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
