using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video.Data.Services;

/// <summary>
/// Resolves Video module settings from the system settings DB with fallback to IConfiguration.
/// </summary>
public sealed class VideoSettingsProvider : IVideoSettingsProvider
{
    private const string Module = "dotnetcloud.video";

    private IAdminSettingsService? SettingsService
    {
        get
        {
            if (_lazySettingsService is null && _serviceProvider.GetService(typeof(IAdminSettingsService)) is IAdminSettingsService svc)
                _lazySettingsService = svc;
            return _lazySettingsService;
        }
    }
    private readonly IConfiguration _configuration;
    private readonly ILogger<VideoSettingsProvider> _logger;
    private IAdminSettingsService? _lazySettingsService;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoSettingsProvider"/> class.
    /// </summary>
    /// <param name="configuration">Configuration source for fallback.</param>
    /// <param name="serviceProvider">Service provider for optionally resolving IAdminSettingsService.</param>
    /// <param name="logger">Logger instance.</param>
    public VideoSettingsProvider(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<VideoSettingsProvider> logger)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GetTmdbApiKeyAsync(CancellationToken cancellationToken = default)
    {
        return await GetStringSettingAsync("TmdbApiKey", "Video:Enrichment:TmdbApiKey", string.Empty, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsTmdbAvailableAsync(CancellationToken cancellationToken = default)
    {
        var key = await GetTmdbApiKeyAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(key);
    }

    private async Task<string> GetStringSettingAsync(string key, string configKey, string defaultValue, CancellationToken cancellationToken)
    {
        // Try admin settings service first (may be null in module host standalone mode)
        var svc = SettingsService;
        if (svc is not null)
        {
            try
            {
                var setting = await svc.GetSettingAsync(Module, key);
                if (setting is not null && !string.IsNullOrWhiteSpace(setting.Value))
                    return setting.Value;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read Video setting {Key} from database, falling back to configuration", key);
            }
        }

        return _configuration.GetValue<string>(configKey) ?? defaultValue;
    }
}
