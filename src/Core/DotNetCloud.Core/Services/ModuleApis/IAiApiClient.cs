using DotNetCloud.Core.AI;

namespace DotNetCloud.Core.Services.ModuleApis;

/// <summary>
/// gRPC API client interface for the AI module.
/// Provides LLM-powered chat, conversation management, model listing, and settings.
/// </summary>
public interface IAiApiClient
{
    /// <summary>Creates a new conversation (uses the admin-configured default model).</summary>
    Task<ConversationDto?> CreateConversationAsync(Guid userId, string? title, string? systemPrompt, CancellationToken ct = default);

    /// <summary>Gets a conversation by ID with all messages.</summary>
    Task<ConversationDetailDto?> GetConversationAsync(Guid userId, Guid conversationId, CancellationToken ct = default);

    /// <summary>Lists all conversations for the current user.</summary>
    Task<IReadOnlyList<ConversationSummaryDto>> ListConversationsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Deletes a conversation (soft-delete).</summary>
    Task<bool> DeleteConversationAsync(Guid userId, Guid conversationId, CancellationToken ct = default);

    /// <summary>Renames a conversation.</summary>
    Task<bool> RenameConversationAsync(Guid userId, Guid conversationId, string newTitle, CancellationToken ct = default);

    /// <summary>Sends a message and gets the full response.</summary>
    Task<ChatResponseDto?> SendMessageAsync(Guid userId, Guid conversationId, string message, CancellationToken ct = default);

    /// <summary>Sends a message and streams the response.</summary>
    IAsyncEnumerable<MessageChunkDto> SendMessageStreamingAsync(Guid userId, Guid conversationId, string message, CancellationToken ct = default);

    /// <summary>Checks whether the configured Ollama backend is healthy.</summary>
    Task<bool> IsOllamaHealthyAsync(CancellationToken ct = default);

    /// <summary>Lists available models.</summary>
    Task<IReadOnlyList<ModelInfoDto>> ListModelsAsync(CancellationToken ct = default);

    /// <summary>Gets AI settings.</summary>
    Task<SettingsDto?> GetSettingsAsync(CancellationToken ct = default);

    /// <summary>Updates AI settings.</summary>
    Task<SettingsDto?> UpdateSettingsAsync(SettingsDto dto, CancellationToken ct = default);
}

/// <summary>Conversation DTO with messages.</summary>
public record ConversationDto
{
    /// <summary>Conversation ID.</summary>
    public Guid Id { get; init; }
    /// <summary>Conversation title.</summary>
    public string Title { get; init; } = "";
    /// <summary>AI model name.</summary>
    public string Model { get; init; } = "";
    /// <summary>System prompt.</summary>
    public string? SystemPrompt { get; init; }
    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; init; }
    /// <summary>Last update timestamp.</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>Conversation detail DTO with messages.</summary>
public sealed record ConversationDetailDto : ConversationDto
{
    /// <summary>Messages in the conversation.</summary>
    public IReadOnlyList<MessageDto> Messages { get; init; } = [];
}

/// <summary>Conversation summary DTO.</summary>
public sealed record ConversationSummaryDto
{
    /// <summary>Conversation ID.</summary>
    public Guid Id { get; init; }
    /// <summary>Conversation title.</summary>
    public string Title { get; init; } = "";
    /// <summary>AI model name.</summary>
    public string Model { get; init; } = "";
    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; init; }
    /// <summary>Last update timestamp.</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>Message DTO.</summary>
public sealed record MessageDto
{
    /// <summary>Message ID.</summary>
    public Guid Id { get; init; }
    /// <summary>Message role (user, assistant, system).</summary>
    public string Role { get; init; } = "";
    /// <summary>Message content.</summary>
    public string Content { get; init; } = "";
    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>Chat response DTO.</summary>
public sealed record ChatResponseDto
{
    /// <summary>Model name.</summary>
    public string Model { get; init; } = "";
    /// <summary>Response content.</summary>
    public string Content { get; init; } = "";
    /// <summary>Whether the response is complete.</summary>
    public bool Done { get; init; }
    /// <summary>Prompt evaluation count.</summary>
    public int PromptEvalCount { get; init; }
    /// <summary>Response evaluation count.</summary>
    public int EvalCount { get; init; }
}

/// <summary>Streaming message chunk DTO.</summary>
public sealed record MessageChunkDto
{
    /// <summary>Content chunk.</summary>
    public string Content { get; init; } = "";
    /// <summary>Whether the response is complete.</summary>
    public bool Done { get; init; }
    /// <summary>Evaluation count.</summary>
    public int EvalCount { get; init; }
    /// <summary>The model's reasoning text (present during the thinking phase).</summary>
    public string Thinking { get; init; } = "";
    /// <summary>Stream lifecycle status (Queued / Generating / Done).</summary>
    public LlmStreamStatus Status { get; init; } = LlmStreamStatus.Generating;
    /// <summary>1-based queue position (only on Queued status chunks).</summary>
    public int? QueuedPosition { get; init; }
    /// <summary>Total items in the queue (only on Queued status chunks).</summary>
    public int? QueueTotal { get; init; }
}

/// <summary>Model info DTO.</summary>
public sealed record ModelInfoDto
{
    /// <summary>Model name.</summary>
    public string Name { get; init; } = "";
    /// <summary>Provider name.</summary>
    public string Provider { get; init; } = "";
}

/// <summary>Settings DTO.</summary>
public sealed record SettingsDto
{
    /// <summary>AI provider name.</summary>
    public string Provider { get; init; } = "";
    /// <summary>API base URL.</summary>
    public string ApiBaseUrl { get; init; } = "";
    /// <summary>Default model name.</summary>
    public string DefaultModel { get; init; } = "";
    /// <summary>Maximum tokens.</summary>
    public int MaxTokens { get; init; }
    /// <summary>Request timeout in seconds.</summary>
    public int RequestTimeoutSeconds { get; init; }
}
