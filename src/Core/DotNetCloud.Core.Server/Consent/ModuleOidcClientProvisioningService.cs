using DotNetCloud.Core.Server.ModuleLoading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Consent;

/// <summary>
/// Keeps one OIDC client per consent-aware module in sync with what the module declared.
/// </summary>
/// <remarks>
/// <para>
/// Module manifests are read by the supervisor as modules are discovered, so the declared scopes
/// are only known after discovery. This service reconciles the OpenIddict application store against
/// them: it runs shortly after startup and then periodically, so a manifest change (a new version
/// adding a scope) is reflected without a restart.
/// </para>
/// <para>
/// The consent prompt also provisions on demand, from the authorize endpoint, so a module that
/// starts between two reconcile passes can still prompt its first user immediately.
/// </para>
/// </remarks>
internal sealed class ModuleOidcClientProvisioningService : BackgroundService
{
    /// <summary>How often the client store is reconciled against the recorded manifests.</summary>
    internal static readonly TimeSpan ReconcileInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IModuleManifestRegistry _manifests;
    private readonly ILogger<ModuleOidcClientProvisioningService> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleOidcClientProvisioningService"/> class.</summary>
    /// <param name="scopeFactory">Scope factory (OpenIddict managers are scoped).</param>
    /// <param name="manifests">Declared manifest data per module.</param>
    /// <param name="logger">Logger.</param>
    public ModuleOidcClientProvisioningService(
        IServiceScopeFactory scopeFactory,
        IModuleManifestRegistry manifests,
        ILogger<ModuleOidcClientProvisioningService> logger)
    {
        _scopeFactory = scopeFactory;
        _manifests = manifests;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the host finish starting (and give discovery a moment) before the first pass.
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Module OIDC client reconciliation failed; it will be retried.");
            }

            try
            {
                await Task.Delay(ReconcileInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Runs one reconcile pass.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of module clients that were (re)written.</returns>
    internal async Task<int> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var written = 0;

        using var scope = _scopeFactory.CreateScope();
        var registrar = scope.ServiceProvider.GetRequiredService<ModuleOidcClientRegistrar>();

        foreach (var moduleId in _manifests.GetModuleIds())
        {
            var manifest = _manifests.GetManifest(moduleId);
            if (manifest is null || manifest.UserConsentScopes.Count == 0)
            {
                // A module that declares no scopes never prompts, so it needs no client.
                continue;
            }

            await registrar.EnsureClientAsync(
                moduleId,
                manifest.Name,
                manifest.UserConsentScopes,
                cancellationToken);

            written++;
        }

        if (written > 0)
        {
            _logger.LogDebug("Reconciled {Count} module OIDC client(s).", written);
        }

        return written;
    }
}
