using Android.Content;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android;

/// <summary>
/// Android implementation of the UnifiedPush connector: it owns the connection tokens, talks to
/// the user's distributor app, and reports endpoints to the DotNetCloud server.
/// </summary>
/// <remarks>
/// All entry points are reachable from broadcast receivers and background services, so nothing
/// here may throw; failures are recorded in the registration state and surfaced in Settings.
/// </remarks>
internal sealed class UnifiedPushConnector : IUnifiedPushConnector
{
    private readonly IUnifiedPushRegistrationStore _store;
    private readonly IPushEndpointRegistrar _registrar;
    private readonly IServerConnectionStore _serverStore;
    private readonly ILogger<UnifiedPushConnector> _logger;

    // Serializes the state machine: register requests, distributor callbacks and acks can all
    // arrive concurrently (app start, broadcast, Settings tap).
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes a new <see cref="UnifiedPushConnector"/>.</summary>
    /// <param name="store">Registration store.</param>
    /// <param name="registrar">Reports endpoints to the server.</param>
    /// <param name="serverStore">Saved server connections.</param>
    /// <param name="logger">Logger.</param>
    public UnifiedPushConnector(
        IUnifiedPushRegistrationStore store,
        IPushEndpointRegistrar registrar,
        IServerConnectionStore serverStore,
        ILogger<UnifiedPushConnector> logger)
    {
        _store = store;
        _registrar = registrar;
        _serverStore = serverStore;
        _logger = logger;
    }

    private static Context? AppContext => global::Android.App.Application.Context;

    /// <inheritdoc />
    public async Task<bool> RegisterAsync(string serverBaseUrl, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await RegisterCoreAsync(serverBaseUrl, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> UnregisterAsync(string serverBaseUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serverBaseUrl))
            return false;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var registration = _store.Get(serverBaseUrl);
            if (registration is null)
                return true;

            UnifiedPushIntents.Send(
                AppContext,
                UnifiedPushProtocol.BuildUnregisterIntent(registration.Token),
                registration.DistributorPackage);

            if (registration.HasEndpoint)
                await _registrar.UnregisterAsync(registration.ServerBaseUrl, registration.Endpoint!, ct)
                    .ConfigureAwait(false);

            _store.Remove(registration.ServerBaseUrl);
            _logger.LogInformation("UnifiedPush registration removed for the active server connection.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UnifiedPush unregistration failed.");
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> SelectDistributorAsync(CancellationToken ct = default)
    {
        try
        {
            var host = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (host is null)
            {
                _logger.LogWarning("Cannot choose a distributor: no current activity.");
                return false;
            }

            var package = await DistributorLinkActivity.SelectAsync(host, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(package))
            {
                _logger.LogInformation("Distributor selection was cancelled or returned no package.");
                return false;
            }

            _store.SelectedDistributorPackage = package;
            _logger.LogInformation("Distributor selected: {Package}.", package);

            // Re-register every known connection with the newly chosen distributor.
            foreach (var registration in _store.GetAll())
                await RegisterAsync(registration.ServerBaseUrl, ct).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributor selection failed.");
            return false;
        }
    }

    /// <inheritdoc />
    public Task<UnifiedPushStatus> GetStatusAsync(string serverBaseUrl, CancellationToken ct = default)
    {
        var registration = string.IsNullOrWhiteSpace(serverBaseUrl) ? null : _store.Get(serverBaseUrl);
        var installed = UnifiedPushIntents.FindDistributors(AppContext);
        var package = registration?.DistributorPackage
            ?? _store.SelectedDistributorPackage
            ?? (installed.Count == 1 ? installed[0].PackageName : null);

        return Task.FromResult(new UnifiedPushStatus(
            registration?.State ?? UnifiedPushRegistrationState.Unregistered,
            package,
            UnifiedPushIntents.TryGetDistributorLabel(AppContext, package),
            UnifiedPushProtocol.SafeEndpointHost(registration?.Endpoint),
            registration?.LastReason,
            installed.Count));
    }

    /// <inheritdoc />
    public Task AcknowledgeAsync(string token, string? id, CancellationToken ct = default)
    {
        var descriptor = UnifiedPushProtocol.BuildAckIntent(token, id);
        if (descriptor is null)
            return Task.CompletedTask;

        var package = _store.FindByToken(token)?.DistributorPackage;
        var sent = UnifiedPushIntents.Send(AppContext, descriptor, package);

        // Info level on purpose: the plan's acceptance check reads the whole lifecycle out of
        // logcat (registered → endpoint → delivery → ACK).
        _logger.LogInformation("UnifiedPush MESSAGE_ACK for {Id} sent: {Sent}.", id, sent);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task HandleNewEndpointAsync(string token, string endpoint, string? id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var registration = _store.FindByToken(token);
            if (registration is null)
            {
                // The specification requires unknown tokens to be ignored.
                _logger.LogWarning("Ignoring NEW_ENDPOINT for an unknown connection token.");
                return;
            }

            // Acknowledge first: distributors may drop an endpoint that has not been acked.
            await AcknowledgeAsync(token, id, ct).ConfigureAwait(false);

            var decision = UnifiedPushRegistrationPolicy.OnEndpointReceived(
                registration, endpoint, _store.SelectedDistributorPackage, DateTimeOffset.UtcNow);

            _store.Save(decision.Registration);
            _logger.LogInformation(
                "UnifiedPush NEW_ENDPOINT received (host {Host}); registering with the server.",
                UnifiedPushProtocol.SafeEndpointHost(endpoint) ?? "(unparsable)");

            await _registrar.RegisterAsync(decision.Registration.ServerBaseUrl, endpoint, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Handling NEW_ENDPOINT failed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task HandleRegistrationFailedAsync(string token, string? reason, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var registration = _store.FindByToken(token);
            if (registration is null)
            {
                _logger.LogWarning("Ignoring REGISTRATION_FAILED for an unknown connection token.");
                return;
            }

            var decision = UnifiedPushRegistrationPolicy.OnRegistrationFailed(
                registration, reason, DateTimeOffset.UtcNow);

            if (decision.Action == UnifiedPushRegistrationAction.None)
            {
                _logger.LogInformation(
                    "Ignoring a stale REGISTRATION_FAILED ({Reason}); an endpoint is already in place.", reason);
                return;
            }

            _store.Save(decision.Registration);
            _logger.LogWarning(
                "UnifiedPush registration failed ({Reason}); next step {Action}.",
                reason ?? "(none)", decision.Action);

            if (decision.Action == UnifiedPushRegistrationAction.RetryNow)
                await RegisterCoreAsync(decision.Registration.ServerBaseUrl, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Handling REGISTRATION_FAILED failed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task HandleUnregisteredAsync(string token, string? useDistributor, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var registration = _store.FindByToken(token);
            if (registration is null)
            {
                _logger.LogWarning("Ignoring UNREGISTERED for an unknown connection token.");
                return;
            }

            var decision = UnifiedPushRegistrationPolicy.OnUnregistered(registration, DateTimeOffset.UtcNow);
            if (decision.Action != UnifiedPushRegistrationAction.Drop)
                return;

            // The specification requires the connection token to be discarded; the next launch
            // creates a fresh registration with a new token.
            _store.Remove(registration.ServerBaseUrl);

            if (registration.HasEndpoint)
                await _registrar.UnregisterAsync(registration.ServerBaseUrl, registration.Endpoint!, ct)
                    .ConfigureAwait(false);

            _logger.LogInformation("UnifiedPush distributor unregistered this device.");

            if (!string.IsNullOrWhiteSpace(useDistributor))
            {
                _store.SelectedDistributorPackage = useDistributor;
                _logger.LogInformation("Switching to distributor {Package} as requested.", useDistributor);
                await RegisterCoreAsync(registration.ServerBaseUrl, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Handling UNREGISTERED failed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task HandleTempUnavailableAsync(string token, string? useDistributor, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var registration = _store.FindByToken(token);
            if (registration is null)
            {
                _logger.LogWarning("Ignoring TEMP_UNAVAILABLE for an unknown connection token.");
                return;
            }

            // The optional useDistributor fallback chain is deliberately not followed: this app
            // supports a single user-chosen distributor (§9.5 keeps the UX to a Settings card).
            if (!string.IsNullOrWhiteSpace(useDistributor))
                _logger.LogInformation("Ignoring the suggested fallback distributor {Package}.", useDistributor);

            var decision = UnifiedPushRegistrationPolicy.OnTempUnavailable(registration, DateTimeOffset.UtcNow);
            _store.Save(decision.Registration);
            _logger.LogWarning("UnifiedPush push server is temporarily unavailable; awaiting a new endpoint.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Handling TEMP_UNAVAILABLE failed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    // ── Internals (callers hold the gate) ───────────────────────────────────

    private async Task<bool> RegisterCoreAsync(string serverBaseUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(serverBaseUrl))
            return false;

        try
        {
            var registration = _store.Get(serverBaseUrl)
                ?? new UnifiedPushRegistration
                {
                    ServerBaseUrl = serverBaseUrl,
                    Token = UnifiedPushProtocol.CreateToken(),
                };

            var decision = UnifiedPushRegistrationPolicy.OnRegisterRequested(registration, DateTimeOffset.UtcNow);
            var updated = decision.Registration;

            switch (decision.Action)
            {
                case UnifiedPushRegistrationAction.ReportToServer:
                    _store.Save(updated);
                    return await _registrar.RegisterAsync(
                        updated.ServerBaseUrl, updated.Endpoint!, ct).ConfigureAwait(false);

                case UnifiedPushRegistrationAction.RetryLater:
                    _store.Save(updated);
                    _logger.LogInformation(
                        "UnifiedPush registration retry deferred until {NextAttempt}.",
                        updated.NextAttemptAt);
                    return false;

                case UnifiedPushRegistrationAction.NeedsUserAction:
                    _store.Save(updated);
                    _logger.LogWarning(
                        "UnifiedPush registration needs a user action in the distributor app (reason {Reason}).",
                        updated.LastReason ?? "(none)");
                    return false;
            }

            return SendRegister(updated);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UnifiedPush registration failed.");
            return false;
        }
    }

    private bool SendRegister(UnifiedPushRegistration registration)
    {
        var package = ResolveDistributorPackage();
        if (package is null)
        {
            _logger.LogWarning(
                "No UnifiedPush distributor selected; install one and choose it in Settings.");
            _store.Save(registration);
            return false;
        }

        var withPackage = registration with { DistributorPackage = package };
        _store.Save(withPackage);

        var description = BuildRegistrationDescription(withPackage.ServerBaseUrl);
        var sent = UnifiedPushIntents.Send(
            AppContext,
            UnifiedPushProtocol.BuildRegisterIntent(withPackage.Token, description),
            package);

        if (sent)
            _logger.LogInformation("UnifiedPush REGISTER sent to {Package}.", package);
        else
            _logger.LogWarning("UnifiedPush REGISTER could not be sent to {Package}.", package);

        return sent;
    }

    /// <summary>
    /// Uses the user's chosen distributor; when none is chosen and exactly one is installed it is
    /// selected automatically, otherwise the user has to pick one in Settings.
    /// </summary>
    private string? ResolveDistributorPackage()
    {
        var selected = _store.SelectedDistributorPackage;
        if (!string.IsNullOrWhiteSpace(selected))
            return selected;

        var candidates = UnifiedPushIntents.FindDistributors(AppContext);
        if (candidates.Count == 1)
        {
            _store.SelectedDistributorPackage = candidates[0].PackageName;
            _logger.LogInformation(
                "Automatically selected the only installed distributor: {Package}.", candidates[0].PackageName);
            return candidates[0].PackageName;
        }

        if (candidates.Count == 0)
            _logger.LogWarning("No UnifiedPush distributor is installed on this device.");
        else
            _logger.LogWarning("{Count} distributors are installed; the user must choose one.", candidates.Count);

        return null;
    }

    private string? BuildRegistrationDescription(string serverBaseUrl)
    {
        try
        {
            var connection = _serverStore.GetAll().FirstOrDefault(
                item => string.Equals(
                    item.ServerBaseUrl.TrimEnd('/'), serverBaseUrl.TrimEnd('/'),
                    StringComparison.OrdinalIgnoreCase));

            var account = connection?.AccountEmail;
            return string.IsNullOrWhiteSpace(account) ? "DotNetCloud" : $"DotNetCloud ({account})";
        }
        catch (Exception)
        {
            return "DotNetCloud";
        }
    }
}
