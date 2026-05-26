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

    private readonly IAdminSettingsService _settingsService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VideoSettingsProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VideoSettingsProvider"/> class.
    /// </summary>
    public VideoSettingsProvider(
        IAdminSettingsService settingsService,
        IConfiguration configuration,
        ILogger<VideoSettingsProvider> logger)
    {
        _settingsService = settingsService;
        _configuration = configuration;
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
        try
        {
            var setting = await _settingsService.GetSettingAsync(Module, key);
            if (setting is not null && !string.IsNullOrWhiteSpace(setting.Value))
            {
                return setting.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read Video setting {Key} from database, falling back to configuration", key);
        }

        return _configuration.GetValue<string>(configKey) ?? defaultValue;
    }
}
