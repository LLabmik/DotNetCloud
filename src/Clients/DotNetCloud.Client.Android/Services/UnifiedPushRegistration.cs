namespace DotNetCloud.Client.Android.Services;

/// <summary>Lifecycle state of one UnifiedPush registration (one per server connection).</summary>
public enum UnifiedPushRegistrationState
{
    /// <summary>No connection token has been handed to a distributor yet.</summary>
    Unregistered = 0,

    /// <summary><c>REGISTER</c> was sent; waiting for the distributor to answer.</summary>
    Pending,

    /// <summary>A distributor supplied an endpoint and the server has been told about it.</summary>
    Registered,

    /// <summary>The distributor reports its push server as unavailable; awaiting a new endpoint.</summary>
    TempUnavailable,

    /// <summary>Registration failed: either it needs a user action, or retries are exhausted.</summary>
    Failed,
}

/// <summary>
/// The state of the UnifiedPush registration for a single saved server connection.
/// </summary>
/// <remarks>
/// The connection token is per server connection (§9.7): each connection registers separately,
/// so an endpoint can always be attributed to the server that should receive it — a tap can
/// never open a channel on the wrong instance.
/// </remarks>
public sealed record UnifiedPushRegistration
{
    /// <summary>Saved server connection this registration belongs to.</summary>
    public required string ServerBaseUrl { get; init; }

    /// <summary>Connection token currently in use with the distributor.</summary>
    public required string Token { get; init; }

    /// <summary>Push endpoint supplied by the distributor, when one is known.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Package name of the distributor that owns the registration.</summary>
    public string? DistributorPackage { get; init; }

    /// <summary>Current lifecycle state.</summary>
    public UnifiedPushRegistrationState State { get; init; } = UnifiedPushRegistrationState.Unregistered;

    /// <summary>Last failure reason reported by a distributor, when any.</summary>
    public string? LastReason { get; init; }

    /// <summary>Number of consecutive failed registration attempts.</summary>
    public int FailedAttempts { get; init; }

    /// <summary>When the next registration attempt is allowed.</summary>
    public DateTimeOffset? NextAttemptAt { get; init; }

    /// <summary>When the registration was last changed.</summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Whether an endpoint is known and can be sent to the server.</summary>
    public bool HasEndpoint => !string.IsNullOrWhiteSpace(Endpoint);

    /// <summary>Records the endpoint supplied by a distributor.</summary>
    /// <param name="endpoint">Endpoint URL.</param>
    /// <param name="distributorPackage">Distributor that owns it, when known.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The updated registration.</returns>
    public UnifiedPushRegistration WithEndpoint(string endpoint, string? distributorPackage, DateTimeOffset now) =>
        this with
        {
            Endpoint = endpoint,
            DistributorPackage = distributorPackage ?? DistributorPackage,
            State = UnifiedPushRegistrationState.Registered,
            LastReason = null,
            FailedAttempts = 0,
            NextAttemptAt = null,
            UpdatedAt = now,
        };

    /// <summary>Records a registration failure and schedules the next attempt.</summary>
    /// <param name="reason">Failure reason reported by the distributor.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The updated registration.</returns>
    public UnifiedPushRegistration WithFailure(string? reason, DateTimeOffset now) =>
        this with
        {
            Endpoint = null,
            State = UnifiedPushRegistrationState.Failed,
            LastReason = reason,
            FailedAttempts = FailedAttempts + 1,
            NextAttemptAt = UnifiedPushRetry.NextAttemptAt(FailedAttempts + 1, now),
            UpdatedAt = now,
        };

    /// <summary>
    /// Replaces the connection token, as the specification requires for the next registration
    /// attempt after a failure.
    /// </summary>
    /// <param name="token">The new connection token.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The updated registration.</returns>
    public UnifiedPushRegistration WithNewToken(string token, DateTimeOffset now) =>
        this with
        {
            Token = token,
            UpdatedAt = now,
        };

    /// <summary>Marks the registration as tentatively registered (REGISTER sent).</summary>
    /// <param name="now">Current time.</param>
    /// <returns>The updated registration.</returns>
    public UnifiedPushRegistration WithPending(DateTimeOffset now) =>
        this with
        {
            State = UnifiedPushRegistrationState.Pending,
            UpdatedAt = now,
        };

    /// <summary>Marks the distributor's push server as unavailable.</summary>
    /// <param name="now">Current time.</param>
    /// <returns>The updated registration.</returns>
    public UnifiedPushRegistration WithTempUnavailable(DateTimeOffset now) =>
        this with
        {
            State = UnifiedPushRegistrationState.TempUnavailable,
            UpdatedAt = now,
        };
}

/// <summary>
/// Retry policy for failed UnifiedPush registrations (spec §Registration failure reasons).
/// </summary>
public static class UnifiedPushRetry
{
    /// <summary>How many consecutive failures are retried before the user must intervene.</summary>
    public const int MaxAttempts = 5;

    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(60),
    ];

    /// <summary>Whether a failure reason is worth retrying automatically.</summary>
    /// <param name="reason">Reason reported by the distributor.</param>
    /// <returns>True for INTERNAL_ERROR, NETWORK and a missing reason.</returns>
    public static bool IsRetryable(string? reason) =>
        reason is null
        || reason.Equals(UnifiedPushProtocol.ReasonInternalError, StringComparison.OrdinalIgnoreCase)
        || reason.Equals(UnifiedPushProtocol.ReasonNetwork, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a failure reason needs the user to act in the distributor app.</summary>
    /// <param name="reason">Reason reported by the distributor.</param>
    /// <returns>True for ACTION_REQUIRED and VAPID_REQUIRED.</returns>
    public static bool RequiresUserAction(string? reason) =>
        reason is not null
        && (reason.Equals(UnifiedPushProtocol.ReasonActionRequired, StringComparison.OrdinalIgnoreCase)
            || reason.Equals(UnifiedPushProtocol.ReasonVapidRequired, StringComparison.OrdinalIgnoreCase));

    /// <summary>Backoff before the attempt after <paramref name="failedAttempts"/> failures.</summary>
    /// <param name="failedAttempts">Number of consecutive failures so far.</param>
    /// <returns>The delay to wait.</returns>
    public static TimeSpan NextDelay(int failedAttempts)
    {
        if (failedAttempts <= 0)
            return Delays[0];

        return Delays[Math.Min(failedAttempts - 1, Delays.Length - 1)];
    }

    /// <summary>When the next attempt may run, or null when retries are exhausted.</summary>
    /// <param name="failedAttempts">Number of consecutive failures so far.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The earliest allowed attempt time, or null.</returns>
    public static DateTimeOffset? NextAttemptAt(int failedAttempts, DateTimeOffset now) =>
        failedAttempts >= MaxAttempts ? null : now + NextDelay(failedAttempts);

    /// <summary>Whether a new attempt is allowed now.</summary>
    /// <param name="failedAttempts">Number of consecutive failures so far.</param>
    /// <param name="nextAttemptAt">Earliest allowed attempt time, when known.</param>
    /// <param name="now">Current time.</param>
    /// <returns>True when an attempt may be made.</returns>
    public static bool ShouldAttempt(int failedAttempts, DateTimeOffset? nextAttemptAt, DateTimeOffset now) =>
        failedAttempts < MaxAttempts && (nextAttemptAt is null || nextAttemptAt <= now);
}
