namespace DotNetCloud.Client.Core.Tests.TestSupport;

/// <summary>
/// A <see cref="TimeProvider"/> whose timers fire immediately.
/// </summary>
/// <remarks>
/// Retry/backoff paths in the client call <see cref="Task.Delay(TimeSpan, TimeProvider, CancellationToken)"/>.
/// Using this provider keeps those unit tests fast (and deterministic) instead of sleeping for the
/// real exponential-backoff schedule, which used to take minutes.
/// </remarks>
internal sealed class NoWaitTimeProvider : TimeProvider
{
    /// <summary>Shared instance.</summary>
    public static readonly NoWaitTimeProvider Instance = new();

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ImmediateTimer(callback, state, period);
        timer.Schedule();
        return timer;
    }

    private sealed class ImmediateTimer : ITimer
    {
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private int _disposed;

        public ImmediateTimer(TimerCallback callback, object? state, TimeSpan period)
        {
            _callback = callback;
            _state = state;
            _ = period;
        }

        public void Schedule() =>
            ThreadPool.UnsafeQueueUserWorkItem(static t => ((ImmediateTimer)t!).Fire(), this, preferLocal: false);

        private void Fire()
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;

            // One-shot timers (the only kind Task.Delay uses) fire exactly once.
            _callback(_state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _ = dueTime;
            _ = period;
            if (Volatile.Read(ref _disposed) == 0)
                Schedule();
            return true;
        }

        public void Dispose() => Interlocked.Exchange(ref _disposed, 1);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
