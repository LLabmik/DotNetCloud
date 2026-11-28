using DotNetCloud.Core.AI;

namespace DotNetCloud.Modules.AI.Services;

/// <summary>
/// HTTP client interface for communicating with an Ollama instance.
/// </summary>
public interface IOllamaClient
{
    /// <summary>Checks whether the Ollama instance is healthy and reachable.</summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a chat completion request and returns the full response.
    /// </summary>
    Task<LlmResponse> ChatAsync(LlmRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a chat completion request and streams the response.
    /// </summary>
    IAsyncEnumerable<LlmResponseChunk> ChatStreamingAsync(LlmRequest request, CancellationToken cancellationToken = default);

    /// <summary>Lists all models available on the Ollama instance.</summary>
    Task<IReadOnlyList<LlmModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default);
}
