namespace DotNetCloud.Client.Android.Ai;

/// <summary>REST API client for the AI module (base path /api/v1/ai).</summary>
public interface IAiRestClient
{
    /// <summary>Lists the LLM models available on the server.</summary>
    Task<IReadOnlyList<AiModelDto>> ListModelsAsync(string serverBaseUrl, string accessToken, CancellationToken ct = default);

    /// <summary>Lists all conversations for the current user (summaries, no messages).</summary>
    Task<IReadOnlyList<AiConversationDto>> ListConversationsAsync(string serverBaseUrl, string accessToken, CancellationToken ct = default);

    /// <summary>Gets a single conversation including its messages.</summary>
    Task<AiConversationDto?> GetConversationAsync(string serverBaseUrl, string accessToken, Guid conversationId, CancellationToken ct = default);

    /// <summary>Creates a new conversation with the given title and model.</summary>
    Task<AiConversationDto?> CreateConversationAsync(string serverBaseUrl, string accessToken, string? title, string model, CancellationToken ct = default);

    /// <summary>Deletes a conversation. Returns <c>false</c> if it does not exist.</summary>
    Task<bool> DeleteConversationAsync(string serverBaseUrl, string accessToken, Guid conversationId, CancellationToken ct = default);

    /// <summary>Renames a conversation. Returns <c>false</c> if it does not exist.</summary>
    Task<bool> RenameConversationAsync(string serverBaseUrl, string accessToken, Guid conversationId, string newTitle, CancellationToken ct = default);

    /// <summary>Sends a message and streams the assistant reply via Server-Sent Events.</summary>
    IAsyncEnumerable<AiStreamChunk> SendMessageStreamingAsync(string serverBaseUrl, string accessToken, Guid conversationId, string message, CancellationToken ct = default);

    /// <summary>Checks whether the configured Ollama backend is healthy.</summary>
    Task<bool> GetOllamaHealthAsync(string serverBaseUrl, string accessToken, CancellationToken ct = default);
}
