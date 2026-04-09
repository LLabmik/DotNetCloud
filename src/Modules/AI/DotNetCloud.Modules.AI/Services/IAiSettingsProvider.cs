namespace DotNetCloud.Modules.AI.Services;

/// <summary>
/// Provides resolved AI module settings (API URL, model, auth) from the system settings store.
/// Falls back to <c>IConfiguration</c> (appsettings.json) when a DB setting is not present.
/// </summary>
public interface IAiSettingsProvider
{
    /// <summary>Gets the LLM provider type (ollama, openai, anthropic).</summary>
    Task<string> GetProviderAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the base URL of the LLM API.</summary>
    Task<string> GetApiBaseUrlAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the API key (empty for providers that don't require one).</summary>
    Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the optional organization ID.</summary>
    Task<string> GetOrganizationIdAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the default LLM model name.</summary>
    Task<string> GetDefaultModelAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the max tokens per response (0 = provider default).</summary>
    Task<int> GetMaxTokensAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the request timeout in seconds.</summary>
    Task<int> GetRequestTimeoutSecondsAsync(CancellationToken cancellationToken = default);
}
