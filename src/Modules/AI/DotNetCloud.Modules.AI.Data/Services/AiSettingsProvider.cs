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
    private readonly ILogger<AiSettingsProvider> _logger;
    private IAdminSettingsService? _lazySettingsService;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiSettingsProvider"/> class.
    /// </summary>
    /// <param name="configuration">Configuration source for fallback.</param>
    /// <param name="serviceProvider">Service provider for optionally resolving <see cref="IAdminSettingsService"/>.</param>
    /// <param name="logger">Logger instance.</param>
    public AiSettingsProvider(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<AiSettingsProvider> logger)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
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
        // Try the admin settings service first (may be null in module host standalone mode,
        // where IAdminSettingsService is not registered — mirroring VideoSettingsProvider).
        var svc = SettingsService;
        if (svc is not null)
        {
            try
            {
                var setting = await svc.GetSettingAsync(Module, key);
                if (setting is not null && !string.IsNullOrWhiteSpace(setting.Value))
                {
                    return setting.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read AI setting {Key} from database, falling back to configuration", key);
            }
        }

        // Fall back to IConfiguration (appsettings.json)
        return _configuration.GetValue<string>(configKey) ?? defaultValue;
    }
}
