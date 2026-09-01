namespace DotNetCloud.Core.AI;

/// <summary>
/// Lifecycle status of a streamed LLM response chunk.
/// </summary>
public enum LlmStreamStatus
{
    /// <summary>No status (default).</summary>
    Unknown = 0,

    /// <summary>The request is waiting in the inference queue.</summary>
    Queued = 1,

    /// <summary>The model is actively generating tokens.</summary>
    Generating = 2,

    /// <summary>Generation finished.</summary>
    Done = 3
}
