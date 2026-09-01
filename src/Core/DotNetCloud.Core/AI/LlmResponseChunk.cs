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

    /// <summary>The model's reasoning text (present on chunks produced during the thinking phase).</summary>
    public string Thinking { get; init; } = string.Empty;

    /// <summary>Stream lifecycle status. Defaults to Generating (existing content chunks).</summary>
    public LlmStreamStatus Status { get; init; } = LlmStreamStatus.Generating;

    /// <summary>1-based queue position (only on Queued status chunks).</summary>
    public int? QueuedPosition { get; init; }

    /// <summary>Total items in the queue (only on Queued status chunks).</summary>
    public int? QueueTotal { get; init; }
}
