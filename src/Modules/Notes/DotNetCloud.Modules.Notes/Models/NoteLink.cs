using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Notes.Models;

/// <summary>
/// Represents a link from a note to another entity (file, calendar event, contact, or another note).
/// </summary>
public sealed class NoteLink
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The note this link belongs to.</summary>
    public Guid NoteId { get; set; }

    /// <summary>Type of the linked entity.</summary>
    public NoteLinkType LinkType { get; set; }

    /// <summary>ID of the linked entity.</summary>
    public Guid TargetId { get; set; }

    /// <summary>Optional display label for the link.</summary>
    public string? DisplayLabel { get; set; }

    /// <summary>Navigation: the note.</summary>
    public Note? Note { get; set; }
}
