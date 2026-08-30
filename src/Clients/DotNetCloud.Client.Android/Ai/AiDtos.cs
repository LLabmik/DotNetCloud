namespace DotNetCloud.Client.Android.Ai;

/// <summary>An AI chat conversation and its metadata.</summary>
public sealed record AiConversationDto
{
    /// <summary>Unique conversation ID.</summary>
    public Guid Id { get; init; }

    /// <summary>Display title of the conversation.</summary>
    public string Title { get; init; } = "";

    /// <summary>Model used for this conversation.</summary>
    public string Model { get; init; } = "";

    /// <summary>Optional system prompt.</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>When the conversation was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the conversation was last updated.</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Messages in the conversation (only present on detail responses).</summary>
    public IReadOnlyList<AiMessageDto>? Messages { get; init; }
}

/// <summary>A single message within an AI conversation.</summary>
public sealed record AiMessageDto
{
    /// <summary>Unique message ID.</summary>
    public Guid Id { get; init; }

    /// <summary>Role: "user", "assistant", or "system".</summary>
    public string Role { get; init; } = "";

    /// <summary>Raw message content (Markdown for assistant messages).</summary>
    public string Content { get; init; } = "";

    /// <summary>When the message was created.</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>An LLM model available on the server.</summary>
public sealed record AiModelDto
{
    /// <summary>Model identifier used in API requests (e.g., "gpt-oss:20b").</summary>
    public string Id { get; init; } = "";

    /// <summary>Human-readable display name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Provider that serves the model (e.g., "ollama").</summary>
    public string Provider { get; init; } = "";

    /// <summary>Size of the model in bytes, if known.</summary>
    public long? SizeBytes { get; init; }

    /// <summary>Model parameter count description (e.g., "20B").</summary>
    public string? ParameterSize { get; init; }

    /// <summary>When the model was last modified/pulled.</summary>
    public DateTime? ModifiedAt { get; init; }
}

/// <summary>A single chunk of a streamed assistant reply.</summary>
public sealed record AiStreamChunk
{
    /// <summary>Text produced in this chunk.</summary>
    public string Content { get; init; } = "";

    /// <summary>Whether generation is complete.</summary>
    public bool Done { get; init; }

    /// <summary>Response tokens generated so far (present on the final chunk).</summary>
    public int? EvalCount { get; init; }
}
