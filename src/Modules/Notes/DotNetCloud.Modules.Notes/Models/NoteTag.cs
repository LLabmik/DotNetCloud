namespace DotNetCloud.Modules.Notes.Models;

/// <summary>
/// Represents a tag applied to a note.
/// </summary>
public sealed class NoteTag
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The note this tag is applied to.</summary>
    public Guid NoteId { get; set; }

    /// <summary>Tag value (e.g. "work", "todo").</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>Navigation: the note.</summary>
    public Note? Note { get; set; }
}
