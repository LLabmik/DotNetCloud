namespace DotNetCloud.Core.AI;

/// <summary>
/// Represents a complete response from an LLM provider.
/// </summary>
public sealed record LlmResponse
{
    /// <summary>The model that generated the response.</summary>
    public required string Model { get; init; }

    /// <summary>The assistant's response message.</summary>
    public required LlmMessage Message { get; init; }

    /// <summary>Whether the response generation is complete.</summary>
    public bool Done { get; init; }

    /// <summary>Total duration of the request in nanoseconds, if reported by the provider.</summary>
    public long? TotalDurationNs { get; init; }

    /// <summary>Number of tokens in the prompt evaluation.</summary>
    public int? PromptEvalCount { get; init; }

    /// <summary>Number of tokens generated in the response.</summary>
    public int? EvalCount { get; init; }
}
