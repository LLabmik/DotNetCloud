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
    /// <summary>Creates a new conversation for the caller, using the admin-configured default model.</summary>
    Task<Conversation> CreateConversationAsync(CallerContext caller, string? title, string? systemPrompt, CancellationToken cancellationToken = default);

    /// <summary>Gets a conversation by ID, verifying ownership.</summary>
    Task<Conversation?> GetConversationAsync(CallerContext caller, Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary>Lists all conversations for the caller, most recent first.</summary>
    Task<IReadOnlyList<Conversation>> ListConversationsAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets conversation count and last activity time for the given user.</summary>
    Task<AiConversationStatsDto> GetConversationStatsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Deletes (soft-delete) a conversation.</summary>
    Task<bool> DeleteConversationAsync(CallerContext caller, Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary>Renames a conversation, allowing the user to override the auto-generated title.</summary>
    Task<bool> RenameConversationAsync(CallerContext caller, Guid conversationId, string newTitle, CancellationToken cancellationToken = default);

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

/// <summary>Conversation count and last activity for the AI widget.</summary>
public sealed record AiConversationStatsDto
{
    /// <summary>Total number of conversations.</summary>
    public int TotalConversations { get; init; }

    /// <summary>Timestamp of the most recent conversation activity (UTC), if any.</summary>
    public DateTime? LastActivityAt { get; init; }
}
