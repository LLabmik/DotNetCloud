namespace DotNetCloud.Core.Events.Search;

/// <summary>
/// Published by any module when searchable content changes and the search index needs updating.
/// The Search module subscribes to this event for real-time incremental indexing.
/// </summary>
public sealed record SearchIndexRequestEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The module that owns the changed entity.</summary>
    public required string ModuleId { get; init; }

    /// <summary>The entity identifier that changed.</summary>
    public required string EntityId { get; init; }

    /// <summary>The indexing action to perform.</summary>
    public required SearchIndexAction Action { get; init; }
}

/// <summary>
/// Specifies the indexing action for a <see cref="SearchIndexRequestEvent"/>.
/// </summary>
public enum SearchIndexAction
{
    /// <summary>Add or update the entity in the search index.</summary>
    Index,

    /// <summary>Remove the entity from the search index.</summary>
    Remove
}
