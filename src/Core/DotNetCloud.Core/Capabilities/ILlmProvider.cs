using DotNetCloud.Core.AI;
using DotNetCloud.Core.Authorization;

namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Capability interface for LLM (Large Language Model) integration.
/// 
/// Provides access to AI chat completion, streaming responses, and model discovery.
/// This is a <see cref="CapabilityTier.Restricted"/> capability — modules must
/// declare it in their manifest and an administrator must approve access.
/// </summary>
/// <remarks>
/// <para>
/// <b>Supported Providers:</b>
/// <list type="bullet">
///   <item><description>Ollama (local/LAN): Free, self-hosted, air-gapped capable</description></item>
///   <item><description>Anthropic Claude: Cloud-based, pay-per-token</description></item>
///   <item><description>OpenAI / Azure OpenAI: Cloud-based, pay-per-token</description></item>
/// </list>
/// </para>
/// <para>
/// Privacy: When using Ollama, no data leaves the server. Cloud providers are opt-in only.
/// All LLM requests are logged with user, module, provider, and token count (content is NOT logged).
/// </para>
/// </remarks>
public interface ILlmProvider : ICapabilityInterface
{
    /// <summary>
    /// Sends a chat completion request and returns the full response.
    /// </summary>
    /// <param name="caller">The caller context for authorization and auditing.</param>
    /// <param name="request">The LLM request containing model, messages, and parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete LLM response.</returns>
    Task<LlmResponse> CompleteAsync(
        CallerContext caller,
        LlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a chat completion request and streams the response token-by-token.
    /// </summary>
    /// <param name="caller">The caller context for authorization and auditing.</param>
    /// <param name="request">The LLM request. The <see cref="LlmRequest.Stream"/> property is ignored (always streams).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of response chunks.</returns>
    IAsyncEnumerable<LlmResponseChunk> CompleteStreamingAsync(
        CallerContext caller,
        LlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all available models from the configured provider(s).
    /// </summary>
    /// <param name="caller">The caller context for authorization and auditing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of available model information.</returns>
    Task<IReadOnlyList<LlmModelInfo>> ListModelsAsync(
        CallerContext caller,
        CancellationToken cancellationToken = default);
}
