using DotNetCloud.Core.AI;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.AI.Models;

namespace DotNetCloud.Modules.AI.Services;

/// <summary>
/// Service interface for AI chat operations — managing conversations
/// and routing completion requests to the configured LLM provider.
/// </summary>
public interface IAiChatService
{
    /// <summary>Creates a new conversation for the caller.</summary>
    Task<Conversation> CreateConversationAsync(CallerContext caller, string? title, string model, string? systemPrompt, CancellationToken cancellationToken = default);

    /// <summary>Gets a conversation by ID, verifying ownership.</summary>
    Task<Conversation?> GetConversationAsync(CallerContext caller, Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary>Lists all conversations for the caller, most recent first.</summary>
    Task<IReadOnlyList<Conversation>> ListConversationsAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes (soft-delete) a conversation.</summary>
    Task<bool> DeleteConversationAsync(CallerContext caller, Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a user message to the conversation and returns the full assistant response.
    /// The message and response are persisted to the conversation history.
    /// </summary>
    Task<LlmResponse> SendMessageAsync(CallerContext caller, Guid conversationId, string userMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a user message and streams the assistant response token-by-token.
    /// The message and final response are persisted to the conversation history.
    /// </summary>
    IAsyncEnumerable<LlmResponseChunk> SendMessageStreamingAsync(CallerContext caller, Guid conversationId, string userMessage, CancellationToken cancellationToken = default);

    /// <summary>Lists available models from the configured provider(s).</summary>
    Task<IReadOnlyList<LlmModelInfo>> ListModelsAsync(CallerContext caller, CancellationToken cancellationToken = default);
}
