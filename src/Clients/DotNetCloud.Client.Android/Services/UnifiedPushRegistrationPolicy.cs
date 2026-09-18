namespace DotNetCloud.Client.Android.Services;

/// <summary>What the connector should do after applying a policy decision.</summary>
public enum UnifiedPushRegistrationAction
{
    /// <summary>Nothing to do.</summary>
    None = 0,

    /// <summary>Send <c>REGISTER</c> to the distributor.</summary>
    SendRegister,

    /// <summary>Send the endpoint to the server.</summary>
    ReportToServer,

    /// <summary>Retry registration immediately with the (already rotated) token.</summary>
    RetryNow,

    /// <summary>Retry at a later time (next launch, connectivity change, or the stored backoff).</summary>
    RetryLater,

    /// <summary>Retries exhausted or the distributor requires the user — surface it in Settings.</summary>
    NeedsUserAction,

    /// <summary>The registration is gone; remove it and tell the server to forget the endpoint.</summary>
    Drop,
}

/// <summary>The registration to persist and the action to take.</summary>
/// <param name="Registration">Updated registration state.</param>
/// <param name="Action">Action to perform.</param>
public sealed record UnifiedPushRegistrationDecision(
    UnifiedPushRegistration Registration,
    UnifiedPushRegistrationAction Action);

/// <summary>
/// The UnifiedPush registration state machine, kept free of Android types so it can be unit
/// tested. The connector is a thin adapter over this policy.
/// </summary>
public static class UnifiedPushRegistrationPolicy
{
    /// <summary>
    /// Whether a <c>REGISTRATION_FAILED</c> broadcast must be ignored because the distributor
    /// already supplied an endpoint for this token (spec §REGISTRATION_FAILED).
    /// </summary>
    /// <param name="registration">Current registration.</param>
    /// <returns>True when the failure is stale and must be ignored.</returns>
    public static bool ShouldIgnoreFailure(UnifiedPushRegistration registration) =>
        registration.State == UnifiedPushRegistrationState.Registered && registration.HasEndpoint;

    /// <summary>
    /// Applies a registration request: guarantees a token and moves to the pending state, or
    /// refuses to retry while the backoff window is open.
    /// </summary>
    /// <param name="registration">Current registration.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The decision.</returns>
    public static UnifiedPushRegistrationDecision OnRegisterRequested(
        UnifiedPushRegistration registration, DateTimeOffset now)
    {
        var withToken = string.IsNullOrWhiteSpace(registration.Token)
            ? registration.WithNewToken(UnifiedPushProtocol.CreateToken(), now)
            : registration;

        if (withToken.State == UnifiedPushRegistrationState.Registered && withToken.HasEndpoint)
            return new UnifiedPushRegistrationDecision(withToken, UnifiedPushRegistrationAction.ReportToServer);

        if (!UnifiedPushRetry.ShouldAttempt(withToken.FailedAttempts, withToken.NextAttemptAt, now))
        {
            return new UnifiedPushRegistrationDecision(
                withToken,
                withToken.FailedAttempts >= UnifiedPushRetry.MaxAttempts
                    ? UnifiedPushRegistrationAction.NeedsUserAction
                    : UnifiedPushRegistrationAction.RetryLater);
        }

        return new UnifiedPushRegistrationDecision(
            withToken.WithPending(now), UnifiedPushRegistrationAction.SendRegister);
    }

    /// <summary>Applies a newly supplied endpoint.</summary>
    /// <param name="registration">Current registration.</param>
    /// <param name="endpoint">Endpoint URL.</param>
    /// <param name="distributorPackage">Distributor that supplied it, when known.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The decision.</returns>
    public static UnifiedPushRegistrationDecision OnEndpointReceived(
        UnifiedPushRegistration registration, string endpoint, string? distributorPackage, DateTimeOffset now) =>
        new(registration.WithEndpoint(endpoint, distributorPackage, now), UnifiedPushRegistrationAction.ReportToServer);

    /// <summary>
    /// Applies a registration failure, rotating the connection token as the specification
    /// requires and applying the backoff policy.
    /// </summary>
    /// <param name="registration">Current registration.</param>
    /// <param name="reason">Reason reported by the distributor.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The decision.</returns>
    public static UnifiedPushRegistrationDecision OnRegistrationFailed(
        UnifiedPushRegistration registration, string? reason, DateTimeOffset now)
    {
        if (ShouldIgnoreFailure(registration))
            return new UnifiedPushRegistrationDecision(registration, UnifiedPushRegistrationAction.None);

        // The token is single-use after a failure: the next attempt must use a fresh one.
        var failed = registration
            .WithNewToken(UnifiedPushProtocol.CreateToken(), now)
            .WithFailure(reason, now);

        if (!UnifiedPushRetry.IsRetryable(reason))
            return new UnifiedPushRegistrationDecision(failed, UnifiedPushRegistrationAction.NeedsUserAction);

        if (failed.FailedAttempts >= UnifiedPushRetry.MaxAttempts)
            return new UnifiedPushRegistrationDecision(failed, UnifiedPushRegistrationAction.NeedsUserAction);

        // One immediate retry (INTERNAL_ERROR is documented as directly retryable); after that
        // respect the backoff so two misconfigured apps cannot ping-pong forever.
        var retryImmediately =
            failed.FailedAttempts == 1
            && reason?.Equals(UnifiedPushProtocol.ReasonInternalError, StringComparison.OrdinalIgnoreCase) == true;

        return new UnifiedPushRegistrationDecision(
            failed,
            retryImmediately ? UnifiedPushRegistrationAction.RetryNow : UnifiedPushRegistrationAction.RetryLater);
    }

    /// <summary>Applies an <c>UNREGISTERED</c> broadcast.</summary>
    /// <param name="registration">Current registration.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The decision (always <see cref="UnifiedPushRegistrationAction.Drop"/>).</returns>
    public static UnifiedPushRegistrationDecision OnUnregistered(
        UnifiedPushRegistration registration, DateTimeOffset now) =>
        new(
            registration with
            {
                Endpoint = null,
                State = UnifiedPushRegistrationState.Unregistered,
                FailedAttempts = 0,
                NextAttemptAt = null,
                UpdatedAt = now,
            },
            UnifiedPushRegistrationAction.Drop);

    /// <summary>Applies a <c>TEMP_UNAVAILABLE</c> broadcast.</summary>
    /// <param name="registration">Current registration.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The decision.</returns>
    public static UnifiedPushRegistrationDecision OnTempUnavailable(
        UnifiedPushRegistration registration, DateTimeOffset now) =>
        new(registration.WithTempUnavailable(now), UnifiedPushRegistrationAction.None);
}
