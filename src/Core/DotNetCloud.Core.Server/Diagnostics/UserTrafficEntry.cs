namespace DotNetCloud.Core.Server.Diagnostics;

/// <summary>
/// Live traffic counters for one authenticated user, tracked while they are active.
/// </summary>
/// <remarks>
/// <para>
/// Counters are in-memory only and die with the entry: when a user goes quiet past the idle
/// grace window the entry is pruned and a returning user starts again at zero. That is
/// intentional — the admin System page answers "what is happening right now", not "how much
/// has this user transferred this month".
/// </para>
/// <para>
/// Activity is quantised: the idle clock is refreshed only once every
/// <see cref="ActivityQuantumBytes"/> of attributed traffic. SignalR and Blazor circuits send
/// keep-alive pings every ~15 s and those frames <b>are</b> counted as bytes, so refreshing on
/// every chunk would keep any open tab permanently "active" and defeat the purpose of the
/// feature.
/// </para>
/// <para>
/// All mutations use <see cref="Interlocked"/>; the entry is read and written concurrently by
/// many request threads.
/// </para>
/// </remarks>
internal sealed class UserTrafficEntry
{
    /// <summary>
    /// Bytes of attributed traffic that must accumulate before the activity clock is refreshed.
    /// </summary>
    internal const long ActivityQuantumBytes = 1024;

    private long _bytesIn;
    private long _bytesOut;
    private long _lastActivityUtcTicks;
    private long _unmarkedBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserTrafficEntry"/> class, stamped as
    /// active at <paramref name="now"/>.
    /// </summary>
    /// <param name="userId">The user the entry belongs to.</param>
    /// <param name="now">The current UTC time.</param>
    public UserTrafficEntry(Guid userId, DateTimeOffset now)
    {
        UserId = userId;
        _lastActivityUtcTicks = now.UtcTicks;
    }

    /// <summary>Gets the user the entry belongs to.</summary>
    public Guid UserId { get; }

    /// <summary>Gets the bytes received from this user.</summary>
    public long BytesIn => Interlocked.Read(ref _bytesIn);

    /// <summary>Gets the bytes sent to this user.</summary>
    public long BytesOut => Interlocked.Read(ref _bytesOut);

    /// <summary>Gets when this user last moved a quantum of traffic (UTC).</summary>
    public DateTimeOffset LastActivityUtc =>
        new(Interlocked.Read(ref _lastActivityUtcTicks), TimeSpan.Zero);

    /// <summary>Adds received bytes and possibly refreshes the activity clock.</summary>
    /// <param name="count">Byte count; non-positive values are ignored.</param>
    /// <param name="now">The current UTC time.</param>
    public void AddIn(long count, DateTimeOffset now)
    {
        if (count <= 0)
        {
            return;
        }

        Interlocked.Add(ref _bytesIn, count);
        MarkActivity(count, now);
    }

    /// <summary>Adds sent bytes and possibly refreshes the activity clock.</summary>
    /// <param name="count">Byte count; non-positive values are ignored.</param>
    /// <param name="now">The current UTC time.</param>
    public void AddOut(long count, DateTimeOffset now)
    {
        if (count <= 0)
        {
            return;
        }

        Interlocked.Add(ref _bytesOut, count);
        MarkActivity(count, now);
    }

    private void MarkActivity(long count, DateTimeOffset now)
    {
        if (Interlocked.Add(ref _unmarkedBytes, count) < ActivityQuantumBytes)
        {
            return;
        }

        // Reset before stamping so concurrent callers do not double-refresh.
        Interlocked.Exchange(ref _unmarkedBytes, 0);
        Interlocked.Exchange(ref _lastActivityUtcTicks, now.UtcTicks);
    }
}

/// <summary>
/// A point-in-time view of one user's live traffic counters.
/// </summary>
/// <param name="UserId">The user ID.</param>
/// <param name="BytesIn">Bytes received from the user.</param>
/// <param name="BytesOut">Bytes sent to the user.</param>
/// <param name="LastActivityUtc">When the user last moved a quantum of traffic.</param>
/// <param name="IsIdle">Whether the user has been quiet past the idle threshold.</param>
internal readonly record struct UserTrafficSample(
    Guid UserId,
    long BytesIn,
    long BytesOut,
    DateTimeOffset LastActivityUtc,
    bool IsIdle);
