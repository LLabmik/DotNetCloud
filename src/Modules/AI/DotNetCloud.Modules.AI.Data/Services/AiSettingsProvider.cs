using DotNetCloud.Core.Services;
using DotNetCloud.Modules.AI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.AI.Data.Services;

/// <summary>
/// Resolves AI module settings from the system settings DB with fallback to IConfiguration.
/// </summary>
public sealed class AiSettingsProvider : IAiSettingsProvider
{
    private const string Module = "dotnetcloud.ai";

    private readonly IAdminSettingsService _settingsService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiSettingsProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiSettingsProvider"/> class.
    /// </summary>
    public AiSettingsProvider(
        IAdminSettingsService settingsService,
        IConfiguration configuration,
        ILogger<AiSettingsProvider> logger)
    {
        _settingsService = settingsService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GetProviderAsync(CancellationToken cancellationToken)
    {
        return await GetStringSettingAsync("Provider", "AI:Provider", "ollama");
    }

    /// <inheritdoc />
    public async Task<string> GetApiBaseUrlAsync(CancellationToken cancellationToken)
    {
        return await GetStringSettingAsync("ApiBaseUrl", "AI:Ollama:BaseUrl", "http://localhost:11434/");
    }

    /// <inheritdoc />
    public async Task<string> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        return await GetStringSettingAsync("ApiKey", "AI:ApiKey", string.Empty);
    }

    /// <inheritdoc />
    public async Task<string> GetOrganizationIdAsync(CancellationToken cancellationToken)
    {
        return await GetStringSettingAsync("OrganizationId", "AI:OrganizationId", string.Empty);
    }

    /// <inheritdoc />
    public async Task<string> GetDefaultModelAsync(CancellationToken cancellationToken)
    {
        return await GetStringSettingAsync("DefaultModel", "AI:Ollama:DefaultModel", "gpt-oss:20b");
    }

    /// <inheritdoc />
    public async Task<int> GetMaxTokensAsync(CancellationToken cancellationToken)
    {
        var value = await GetStringSettingAsync("MaxTokens", "AI:MaxTokens", "0");
        return int.TryParse(value, out var result) ? result : 0;
    }

    /// <inheritdoc />
    public async Task<int> GetRequestTimeoutSecondsAsync(CancellationToken cancellationToken)
    {
        var value = await GetStringSettingAsync("RequestTimeoutSeconds", "AI:RequestTimeoutSeconds", "300");
        return int.TryParse(value, out var result) && result >= 10 ? result : 300;
    }

    private async Task<string> GetStringSettingAsync(string key, string configKey, string defaultValue)
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
            _logger.LogWarning(ex, "Failed to read AI setting {Key} from database, falling back to configuration", key);
        }

        // Fall back to IConfiguration (appsettings.json)
        return _configuration.GetValue<string>(configKey) ?? defaultValue;
    }
}
