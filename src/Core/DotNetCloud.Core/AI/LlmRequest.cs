namespace DotNetCloud.Core.AI;

/// <summary>
/// Represents a request to an LLM provider for chat completion.
/// </summary>
public sealed record LlmRequest
{
    /// <summary>The model identifier to use (e.g., "gpt-oss:20b").</summary>
    public required string Model { get; init; }

    /// <summary>The conversation messages to send to the model.</summary>
    public required IReadOnlyList<LlmMessage> Messages { get; init; }

    /// <summary>Whether to stream the response token-by-token. Defaults to false.</summary>
    public bool Stream { get; init; }

    /// <summary>Optional temperature parameter (0.0-2.0). Higher values increase randomness.</summary>
    public double? Temperature { get; init; }

    /// <summary>Optional maximum number of tokens to generate.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>Optional system prompt prepended to the conversation.</summary>
    public string? SystemPrompt { get; init; }
}
