namespace DotNetCloud.Modules.AI.UI;

/// <summary>
/// View model for the AI module admin settings page.
/// Covers LLM provider configuration, API authentication, and default model selection.
/// </summary>
public sealed class AiAdminSettingsViewModel
{
    /// <summary>
    /// The LLM API provider type.
    /// Determines how requests are routed and authenticated.
    /// </summary>
    public string Provider { get; set; } = "ollama";

    /// <summary>
    /// Base URL of the LLM API endpoint.
    /// For Ollama: <c>http://localhost:11434/</c>.
    /// For OpenAI: <c>https://api.openai.com/</c>.
    /// For Anthropic: <c>https://api.anthropic.com/</c>.
    /// </summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:11434/";

    /// <summary>
    /// API key for authenticated providers (OpenAI, Anthropic).
    /// Not required for local Ollama.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional organization ID for providers that support it (e.g., OpenAI).
    /// </summary>
    public string OrganizationId { get; set; } = string.Empty;

    /// <summary>
    /// The default LLM model to use for new conversations.
    /// </summary>
    public string DefaultModel { get; set; } = "gpt-oss:20b";

    /// <summary>
    /// Maximum tokens allowed per completion request.
    /// Set to 0 for provider default.
    /// </summary>
    public int MaxTokens { get; set; }

    /// <summary>
    /// Request timeout in seconds for LLM API calls.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 300;
}
