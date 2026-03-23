namespace DotNetCloud.Modules.Notes.Models;

/// <summary>
/// Represents a historical version of a note for version history / undo.
/// </summary>
public sealed class NoteVersion
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The note this version belongs to.</summary>
    public Guid NoteId { get; set; }

    /// <summary>Sequential version number.</summary>
    public int VersionNumber { get; set; }

    /// <summary>Title at the time of this version.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Content body at the time of this version.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>ID of the user who created this version.</summary>
    public Guid EditedByUserId { get; set; }

    /// <summary>Timestamp when this version was saved (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation: the note.</summary>
    public Note? Note { get; set; }
}
