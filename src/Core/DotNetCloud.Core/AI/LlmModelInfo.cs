namespace DotNetCloud.Core.AI;

/// <summary>
/// Describes an available LLM model from a provider.
/// </summary>
public sealed record LlmModelInfo
{
    /// <summary>The model identifier used in API requests (e.g., "gpt-oss:20b").</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable display name for the model.</summary>
    public required string Name { get; init; }

    /// <summary>The provider that serves this model (e.g., "ollama", "anthropic").</summary>
    public required string Provider { get; init; }

    /// <summary>Size of the model in bytes, if known.</summary>
    public long? SizeBytes { get; init; }

    /// <summary>Model parameter count description (e.g., "20B").</summary>
    public string? ParameterSize { get; init; }

    /// <summary>When the model was last modified/pulled.</summary>
    public DateTime? ModifiedAt { get; init; }
}
