namespace DotNetCloud.Core.AI;

/// <summary>
/// Represents a single chunk of a streaming LLM response.
/// </summary>
public sealed record LlmResponseChunk
{
    /// <summary>The model generating the response.</summary>
    public required string Model { get; init; }

    /// <summary>The partial content of the current chunk.</summary>
    public required string Content { get; init; }

    /// <summary>Whether this is the final chunk in the stream.</summary>
    public bool Done { get; init; }

    /// <summary>Total duration in nanoseconds (only present on the final chunk).</summary>
    public long? TotalDurationNs { get; init; }

    /// <summary>Number of tokens generated (only present on the final chunk).</summary>
    public int? EvalCount { get; init; }
}
