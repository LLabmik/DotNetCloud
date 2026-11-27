namespace DotNetCloud.Modules.AI.Web.Services;

/// <summary>
/// A conversation as the AI module's own REST API returns it.
/// </summary>
public sealed record AiConversationDto
{
    /// <summary>Gets the conversation id.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the conversation title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the model the conversation uses.</summary>
    public string? Model { get; init; }

    /// <summary>Gets the optional system prompt.</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>Gets when the conversation was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Gets when the conversation was last changed.</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Gets the conversation's messages; empty for the list endpoint.</summary>
    public IReadOnlyList<AiMessageDto> Messages { get; init; } = [];
}

/// <summary>
/// One message in a conversation.
/// </summary>
public sealed record AiMessageDto
{
    /// <summary>Gets the message id.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the author role (<c>user</c> or <c>assistant</c>).</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Gets the message text. Assistant text is Markdown.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Gets when the message was created.</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// The AI settings the module exposes for display.
/// </summary>
public sealed record AiSettingsDto
{
    /// <summary>Gets the default model.</summary>
    public string? DefaultModel { get; init; }

    /// <summary>Gets the provider name.</summary>
    public string? Provider { get; init; }
}

/// <summary>
/// One Server-Sent-Events payload from the streaming chat endpoint.
/// </summary>
public sealed record AiStreamChunk
{
    /// <summary>Gets the generation status (<c>queued</c>, <c>generating</c> or <c>done</c>).</summary>
    public string? Status { get; init; }

    /// <summary>Gets the queue position, when the request is queued.</summary>
    public int? Position { get; init; }

    /// <summary>Gets the queue size, when the request is queued.</summary>
    public int? Total { get; init; }

    /// <summary>Gets the answer text produced by this chunk.</summary>
    public string? Content { get; init; }

    /// <summary>Gets the reasoning text produced by this chunk.</summary>
    public string? Thinking { get; init; }

    /// <summary>Gets a value indicating whether the model reported completion.</summary>
    public bool Done { get; init; }
}
