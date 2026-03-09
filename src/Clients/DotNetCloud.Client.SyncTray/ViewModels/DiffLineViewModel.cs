namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>Type of change for a single diff line.</summary>
public enum DiffLineType
{
    /// <summary>Line is unchanged from the base/other version.</summary>
    Unchanged,

    /// <summary>Line was inserted (present in this version, not in the other).</summary>
    Inserted,

    /// <summary>Line was deleted (present in the other version, not in this one).</summary>
    Deleted,

    /// <summary>Line was modified (content differs between versions).</summary>
    Modified,

    /// <summary>Placeholder for an empty line to keep alignment.</summary>
    Filler,
}

/// <summary>
/// Represents a single line in one pane of a side-by-side diff view.
/// </summary>
public sealed class DiffLineViewModel
{
    /// <summary>1-based line number in the source text, or null for filler lines.</summary>
    public int? LineNumber { get; init; }

    /// <summary>Text content of this line.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Change classification.</summary>
    public DiffLineType Type { get; init; }

    /// <summary>Zero-based index of the hunk/block this line belongs to, or -1 for unchanged.</summary>
    public int HunkIndex { get; init; } = -1;
}
