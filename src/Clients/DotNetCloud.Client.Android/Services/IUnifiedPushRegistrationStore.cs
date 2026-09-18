namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Persists UnifiedPush registration state (one entry per saved server connection) plus the
/// chosen distributor, so registrations survive process restarts and taps can be attributed
/// to the right server.
/// </summary>
/// <remarks>
/// Implementations must never throw: the registration state machine runs from broadcast
/// receivers and background services where a failure must not crash the app.
/// </remarks>
public interface IUnifiedPushRegistrationStore
{
    /// <summary>Returns every known registration.</summary>
    /// <returns>The registrations, possibly empty.</returns>
    IReadOnlyList<UnifiedPushRegistration> GetAll();

    /// <summary>Returns the registration for a server connection.</summary>
    /// <param name="serverBaseUrl">Saved server connection URL.</param>
    /// <returns>The registration, or null when the connection has never registered.</returns>
    UnifiedPushRegistration? Get(string serverBaseUrl);

    /// <summary>Finds the registration identified by a connection token.</summary>
    /// <param name="token">Connection token from a distributor broadcast.</param>
    /// <returns>The registration, or null when the token is unknown (and must be ignored).</returns>
    UnifiedPushRegistration? FindByToken(string token);

    /// <summary>Saves (or replaces) a registration.</summary>
    /// <param name="registration">The registration to store.</param>
    void Save(UnifiedPushRegistration registration);

    /// <summary>Removes the registration for a server connection.</summary>
    /// <param name="serverBaseUrl">Saved server connection URL.</param>
    void Remove(string serverBaseUrl);

    /// <summary>
    /// Package name of the distributor the user selected, or null to use the single installed
    /// distributor automatically (with several installed and none selected, the user must choose).
    /// </summary>
    string? SelectedDistributorPackage { get; set; }
}
