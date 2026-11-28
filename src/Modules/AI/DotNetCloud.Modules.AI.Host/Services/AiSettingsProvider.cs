using DotNetCloud.Modules.AI.Services;
using DotNetCloud.Modules.Sdk.Api;

namespace DotNetCloud.Modules.AI.Host.Services;

/// <summary>
/// Resolves AI module settings from the core's module settings plane, falling back to
/// <see cref="IConfiguration"/> (the module's own <c>appsettings.json</c>).
/// </summary>
/// <remarks>
/// <para>
/// Before the module was sandboxed it read the core's <c>SystemSettings</c> table through
/// <c>IAdminSettingsService</c>. An isolated module has no core database access, so the values now
/// live in the module settings plane: an administrator writes them through the core admin API
/// (<c>PUT api/v1/core/admin/modules/dotnetcloud.ai/settings/{key}</c>) and the module reads them
/// through <c>GetSetting</c> on the canonical module API, which namespaces the key by the verified
/// module identity. Values marked secret (the API key) are encrypted at rest by the core and arrive
/// here as plain text over the authenticated channel.
/// </para>
/// <para>
/// Every read falls back to configuration so a standalone host (local development, tests) still runs
/// with the defaults in <c>appsettings.json</c>.
/// </para>
/// </remarks>
public sealed class AiSettingsProvider : IAiSettingsProvider
{
    private readonly IDncModuleApi _moduleApi;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiSettingsProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="AiSettingsProvider"/> class.</summary>
    /// <param name="moduleApi">The module API client (settings plane).</param>
    /// <param name="configuration">Configuration source for fallback.</param>
    /// <param name="logger">Logger instance.</param>
    public AiSettingsProvider(
        IDncModuleApi moduleApi,
        IConfiguration configuration,
        ILogger<AiSettingsProvider> logger)
    {
        _moduleApi = moduleApi;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GetProviderAsync(CancellationToken cancellationToken = default)
    {
        return await GetStringSettingAsync("Provider", "AI:Provider", "ollama", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GetApiBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        return await GetStringSettingAsync("ApiBaseUrl", "AI:Ollama:BaseUrl", "http://localhost:11434/", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        return await GetStringSettingAsync("ApiKey", "AI:ApiKey", string.Empty, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GetOrganizationIdAsync(CancellationToken cancellationToken = default)
    {
        return await GetStringSettingAsync("OrganizationId", "AI:OrganizationId", string.Empty, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GetDefaultModelAsync(CancellationToken cancellationToken = default)
    {
        return await GetStringSettingAsync("DefaultModel", "AI:Ollama:DefaultModel", "gpt-oss:20b", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> GetMaxTokensAsync(CancellationToken cancellationToken = default)
    {
        var value = await GetStringSettingAsync("MaxTokens", "AI:MaxTokens", "0", cancellationToken);
        return int.TryParse(value, out var result) ? result : 0;
    }

    /// <inheritdoc />
    public async Task<int> GetRequestTimeoutSecondsAsync(CancellationToken cancellationToken = default)
    {
        var value = await GetStringSettingAsync("RequestTimeoutSeconds", "AI:RequestTimeoutSeconds", "300", cancellationToken);
        return int.TryParse(value, out var result) && result >= 10 ? result : 300;
    }

    private async Task<string> GetStringSettingAsync(
        string key, string configKey, string defaultValue, CancellationToken cancellationToken)
    {
        try
        {
            var setting = await _moduleApi.GetSettingAsync(key, cancellationToken);
            if (!string.IsNullOrWhiteSpace(setting))
            {
                return setting;
            }
        }
        catch (Exception ex)
        {
            // A settings-plane outage must not break chat: fall back to configuration. Neither the
            // key nor its value is logged (the value may be an API key).
            _logger.LogWarning(
                ex,
                "Failed to read AI setting {Key} from the core settings plane; falling back to configuration",
                key);
        }

        return _configuration.GetValue<string>(configKey) ?? defaultValue;
    }
}
