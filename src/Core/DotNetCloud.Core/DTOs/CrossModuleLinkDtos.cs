namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Represents a resolved cross-module link with display-ready metadata.
/// Used by the cross-module link resolver to return enriched link information.
/// </summary>
public sealed record CrossModuleLinkDto
{
    /// <summary>
    /// The type of entity being linked to.
    /// </summary>
    public required CrossModuleLinkType LinkType { get; init; }

    /// <summary>
    /// The unique identifier of the target entity.
    /// </summary>
    public required Guid TargetId { get; init; }

    /// <summary>
    /// Human-readable display label (resolved from the target entity).
    /// </summary>
    public required string DisplayLabel { get; init; }

    /// <summary>
    /// Module-relative URL for navigating to this entity in the Blazor shell.
    /// </summary>
    public string? Href { get; init; }

    /// <summary>
    /// Whether the target entity still exists (false if deleted or inaccessible).
    /// </summary>
    public bool IsResolved { get; init; } = true;
}

/// <summary>
/// The type of entity referenced in a cross-module link.
/// </summary>
public enum CrossModuleLinkType
{
    /// <summary>Link to a file in the Files module.</summary>
    File,

    /// <summary>Link to a calendar event in the Calendar module.</summary>
    CalendarEvent,

    /// <summary>Link to a contact in the Contacts module.</summary>
    Contact,

    /// <summary>Link to a note in the Notes module.</summary>
    Note
}

/// <summary>
/// Request to resolve one or more cross-module links into display-ready metadata.
/// </summary>
public sealed record CrossModuleLinkRequest
{
    /// <summary>
    /// The type of entity to resolve.
    /// </summary>
    public required CrossModuleLinkType LinkType { get; init; }

    /// <summary>
    /// The unique identifier of the target entity.
    /// </summary>
    public required Guid TargetId { get; init; }
}
