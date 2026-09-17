namespace DotNetCloud.UI.Shared.Components.Editors;

/// <summary>
/// The JS entry points (and the wire shape they exchange) used by <see cref="MarkdownEditor"/> to
/// read and write the textarea. Keeping the names here means a rename in
/// <c>wwwroot/js/markdown-editor.js</c> cannot silently break the formatting toolbar.
/// </summary>
public static class MarkdownEditorInterop
{
    /// <summary>The global JS helper object exposing the editor's read/write helpers.</summary>
    public const string JsObject = "dotnetcloudMarkdownEditor";

    /// <summary>Method returning the textarea's text plus its current selection.</summary>
    public const string ReadStateMethod = "readState";

    /// <summary>Method writing formatted text back and restoring the selection.</summary>
    public const string WriteStateMethod = "writeState";

    /// <summary>Fully qualified identifier of <see cref="ReadStateMethod"/>.</summary>
    public const string ReadState = $"{JsObject}.{ReadStateMethod}";

    /// <summary>Fully qualified identifier of <see cref="WriteStateMethod"/>.</summary>
    public const string WriteState = $"{JsObject}.{WriteStateMethod}";
}

/// <summary>
/// A snapshot of the editor textarea: the current text and the selected range. Produced by
/// <see cref="MarkdownEditorInterop.ReadState"/> and consumed by
/// <see cref="MarkdownTextFormatter"/>.
/// </summary>
public sealed record MarkdownEditorState
{
    /// <summary>The textarea's current text.</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>Start of the selection, in UTF-16 code units.</summary>
    public int SelectionStart { get; init; }

    /// <summary>End of the selection, in UTF-16 code units.</summary>
    public int SelectionEnd { get; init; }

    /// <summary>The selected range.</summary>
    public MarkdownTextRange Selection => new(SelectionStart, SelectionEnd);
}
