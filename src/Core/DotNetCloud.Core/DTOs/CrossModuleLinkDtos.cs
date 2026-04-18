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
    Note,

    /// <summary>Link to a photo in the Photos module.</summary>
    Photo,

    /// <summary>Link to a photo album in the Photos module.</summary>
    PhotoAlbum,

    /// <summary>Link to a music track in the Music module.</summary>
    MusicTrack,

    /// <summary>Link to a music album in the Music module.</summary>
    MusicAlbum,

    /// <summary>Link to a music artist in the Music module.</summary>
    MusicArtist,

    /// <summary>Link to a playlist in the Music module.</summary>
    Playlist,

    /// <summary>Link to a video in the Video module.</summary>
    Video,

    /// <summary>Link to a video collection in the Video module.</summary>
    VideoCollection
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
