namespace DotNetCloud.Modules.Notes.Models;

/// <summary>
/// Represents a share of a note with another user.
/// </summary>
public sealed class NoteShare
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The note being shared.</summary>
    public Guid NoteId { get; set; }

    /// <summary>The user this note is shared with.</summary>
    public Guid SharedWithUserId { get; set; }

    /// <summary>Permission level granted.</summary>
    public NoteSharePermission Permission { get; set; } = NoteSharePermission.ReadOnly;

    /// <summary>Timestamp when the share was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation: the note.</summary>
    public Note? Note { get; set; }
}

/// <summary>
/// Permission level for a note share.
/// </summary>
public enum NoteSharePermission
{
    /// <summary>Can view but not edit.</summary>
    ReadOnly = 0,

    /// <summary>Can view and edit.</summary>
    ReadWrite = 1
}
