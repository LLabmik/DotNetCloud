using DotNetCloud.Core.Auth.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Auth.Services;

/// <summary>
/// Background service that periodically rotates OpenIddict signing and encryption keys.
/// Generates new RSA keys at the configured interval and cleans up keys past the retention period.
/// New keys take effect on the next server restart (when <c>AddSigningKey</c> re-registers all keys).
/// </summary>
public sealed class OidcKeyRotationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<OidcKeyRotationOptions> _options;
    private readonly ILogger<OidcKeyRotationService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="OidcKeyRotationService"/>.
    /// </summary>
    public OidcKeyRotationService(
        IServiceScopeFactory scopeFactory,
        IOptions<OidcKeyRotationOptions> options,
        ILogger<OidcKeyRotationService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run initial rotation check on startup
        await RotateKeysIfNeededAsync(stoppingToken);

        // Then check periodically
        using var timer = new PeriodicTimer(_options.Value.CheckInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RotateKeysIfNeededAsync(stoppingToken);
        }
    }

    private async Task RotateKeysIfNeededAsync(CancellationToken stoppingToken)
    {
        try
        {
            var opts = _options.Value;
            var oidcKeysDir = GetOidcKeysDirectory();

            if (!Directory.Exists(oidcKeysDir))
            {
                Directory.CreateDirectory(oidcKeysDir);
                _logger.LogInformation("Created oidc-keys directory at {OidcKeysDir}.", oidcKeysDir);
            }

            // Check signing key
            if (OidcKeyManager.ShouldRotate(oidcKeysDir, OidcKeyManager.SigningKeyPrefix, opts.RotationInterval))
            {
                _logger.LogInformation("Signing key rotation interval reached. Generating new key.");
                OidcKeyManager.GenerateRotatedKey(oidcKeysDir, OidcKeyManager.SigningKeyPrefix, _logger);
            }

            // Check encryption key
            if (OidcKeyManager.ShouldRotate(oidcKeysDir, OidcKeyManager.EncryptionKeyPrefix, opts.RotationInterval))
            {
                _logger.LogInformation("Encryption key rotation interval reached. Generating new key.");
                OidcKeyManager.GenerateRotatedKey(oidcKeysDir, OidcKeyManager.EncryptionKeyPrefix, _logger);
            }

            // Clean up old keys past retention
            OidcKeyManager.CleanupOldKeys(oidcKeysDir, OidcKeyManager.SigningKeyPrefix, opts.KeyRetentionPeriod, _logger);
            OidcKeyManager.CleanupOldKeys(oidcKeysDir, OidcKeyManager.EncryptionKeyPrefix, opts.KeyRetentionPeriod, _logger);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to rotate OpenIddict keys.");
        }

        await Task.CompletedTask;
    }

    private static string GetOidcKeysDirectory()
    {
        var dataRoot = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR");
        return Path.Combine(
            !string.IsNullOrWhiteSpace(dataRoot) ? dataRoot : AppContext.BaseDirectory,
            "oidc-keys");
    }
}

/// <summary>
/// Configuration options for OpenIddict key rotation.
/// </summary>
public class OidcKeyRotationOptions
{
    /// <summary>
    /// How often to generate a new signing/encryption key. Default: 90 days.
    /// </summary>
    public TimeSpan RotationInterval { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// How long to retain old keys after rotation so existing tokens remain verifiable.
    /// Must be greater than the maximum token lifetime (access token + refresh token + buffer).
    /// Default: 120 days.
    /// </summary>
    public TimeSpan KeyRetentionPeriod { get; set; } = TimeSpan.FromDays(120);

    /// <summary>
    /// How often the background service checks whether rotation is needed. Default: 24 hours.
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// The directory where OpenIddict keys are stored. If null, defaults to
    /// <c>{DOTNETCLOUD_DATA_DIR}/oidc-keys</c> or <c>{AppContext.BaseDirectory}/oidc-keys</c>.
    /// </summary>
    public string? KeysDirectory { get; set; }
}
