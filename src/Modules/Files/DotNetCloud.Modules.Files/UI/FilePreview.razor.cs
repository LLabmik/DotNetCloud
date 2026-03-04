using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the full-screen file preview modal.
/// Supports images, video, audio, PDF, text, code, and markdown previews.
/// Keyboard shortcuts: Escape = close, ← = previous file, → = next file.
/// </summary>
public partial class FilePreview : ComponentBase
{
    /// <summary>The file node to preview (starting node; may change on navigation).</summary>
    [Parameter] public FileNodeViewModel? Node { get; set; }

    /// <summary>
    /// All nodes in the current context, used to enable prev/next navigation.
    /// Only file nodes (not folders) are navigable.
    /// </summary>
    [Parameter] public IReadOnlyList<FileNodeViewModel>? AllNodes { get; set; }

    /// <summary>
    /// Base URL of the Files API (e.g. <c>https://cloud.example.com</c>).
    /// Used to construct inline content URLs for media elements.
    /// When empty, relative API paths are used.
    /// </summary>
    [Parameter] public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Whether to show the Share button in the preview header.</summary>
    [Parameter] public bool ShowShareButton { get; set; } = true;

    /// <summary>Invoked when the user closes the preview.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Invoked when the user clicks the Download button.</summary>
    [Parameter] public EventCallback<FileNodeViewModel> OnDownload { get; set; }

    /// <summary>Invoked when the user clicks the Share button.</summary>
    [Parameter] public EventCallback<FileNodeViewModel> OnShare { get; set; }

    private ElementReference _overlayRef;

    // Tracks the currently displayed node when the user navigates away from the original Node.
    private FileNodeViewModel? _currentNode;

    /// <summary>The node currently displayed in the preview area.</summary>
    protected FileNodeViewModel? DisplayNode => _currentNode ?? Node;

    /// <inheritdoc />
    protected override void OnParametersSet() => _currentNode = null;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await _overlayRef.FocusAsync();
    }

    // ── MIME type checks ────────────────────────────────────────────────────────

    /// <summary>True when the file is a raster or vector image.</summary>
    protected bool IsImage => DisplayNode?.MimeType?.StartsWith("image/") == true;

    /// <summary>True when the file is a video stream.</summary>
    protected bool IsVideo => DisplayNode?.MimeType?.StartsWith("video/") == true;

    /// <summary>True when the file is an audio stream.</summary>
    protected bool IsAudio => DisplayNode?.MimeType?.StartsWith("audio/") == true;

    /// <summary>True when the file is a PDF document.</summary>
    protected bool IsPdf => DisplayNode?.MimeType == "application/pdf";

    /// <summary>True when the file is plain text.</summary>
    protected bool IsText => DisplayNode?.MimeType == "text/plain";

    /// <summary>True when the file is a Markdown document.</summary>
    protected bool IsMarkdown =>
        DisplayNode?.MimeType == "text/markdown" ||
        DisplayNode?.Name?.EndsWith(".md", StringComparison.OrdinalIgnoreCase) == true ||
        DisplayNode?.Name?.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>True when the file is source code or another text/* sub-type.</summary>
    protected bool IsCode =>
        DisplayNode?.MimeType?.StartsWith("text/") == true && !IsText && !IsMarkdown;

    /// <summary>True when the file is an editable office document (handled by Collabora).</summary>
    protected bool IsDocument =>
        DisplayNode?.MimeType is not null &&
        (DisplayNode.MimeType.Contains("document") || DisplayNode.MimeType.Contains("word") ||
         DisplayNode.MimeType.Contains("spreadsheet") || DisplayNode.MimeType.Contains("excel") ||
         DisplayNode.MimeType.Contains("presentation") || DisplayNode.MimeType.Contains("powerpoint") ||
         DisplayNode.MimeType.Contains("opendocument"));

    // ── Navigation ──────────────────────────────────────────────────────────────

    /// <summary>File-only nodes from <see cref="AllNodes"/>, used for prev/next navigation.</summary>
    protected IReadOnlyList<FileNodeViewModel> NavigableFiles =>
        AllNodes?.Where(n => n.NodeType == "File").ToList() ??
        (DisplayNode is not null ? [DisplayNode] : []);

    /// <summary>Zero-based index of <see cref="DisplayNode"/> within <see cref="NavigableFiles"/>.</summary>
    protected int CurrentIndex =>
        DisplayNode is null
            ? -1
            : NavigableFiles.Select((n, i) => (n, i))
                .FirstOrDefault(x => x.n.Id == DisplayNode.Id, (null!, -1)).i;

    /// <summary>True when there is a previous file to navigate to.</summary>
    protected bool CanGoPrev => CurrentIndex > 0;

    /// <summary>True when there is a next file to navigate to.</summary>
    protected bool CanGoNext
    {
        get
        {
            var idx = CurrentIndex;
            return idx >= 0 && idx < NavigableFiles.Count - 1;
        }
    }

    /// <summary>Navigates to the previous file in the list.</summary>
    protected void GoPrev()
    {
        var idx = CurrentIndex;
        if (idx > 0)
        {
            _currentNode = NavigableFiles[idx - 1];
            StateHasChanged();
        }
    }

    /// <summary>Navigates to the next file in the list.</summary>
    protected void GoNext()
    {
        var idx = CurrentIndex;
        if (idx >= 0 && idx < NavigableFiles.Count - 1)
        {
            _currentNode = NavigableFiles[idx + 1];
            StateHasChanged();
        }
    }

    // ── Keyboard ────────────────────────────────────────────────────────────────

    /// <summary>Handles keyboard shortcuts: Escape = close, ← = prev, → = next.</summary>
    protected async Task HandleKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Escape":
                await OnClose.InvokeAsync();
                break;
            case "ArrowLeft":
                GoPrev();
                break;
            case "ArrowRight":
                GoNext();
                break;
        }
    }

    // ── Actions ─────────────────────────────────────────────────────────────────

    /// <summary>Invokes the download callback for the currently displayed node.</summary>
    protected async Task Download()
    {
        if (DisplayNode is not null)
            await OnDownload.InvokeAsync(DisplayNode);
    }

    /// <summary>Invokes the share callback for the currently displayed node.</summary>
    protected async Task Share()
    {
        if (DisplayNode is not null)
            await OnShare.InvokeAsync(DisplayNode);
    }

    /// <summary>Opens the current document in the Collabora Online editor.</summary>
    protected void OpenInCollabora()
    {
        // In a full implementation, raises an event to open DocumentEditor for this node.
    }

    // ── URL helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Constructs the API URL for streaming the current file's content inline.
    /// </summary>
    protected string GetContentUrl()
    {
        if (DisplayNode is null) return "#";
        var base_ = string.IsNullOrEmpty(ApiBaseUrl) ? string.Empty : ApiBaseUrl.TrimEnd('/');
        return $"{base_}/api/v1/files/{DisplayNode.Id}/content";
    }

    // ── Formatting ──────────────────────────────────────────────────────────────

    /// <summary>Returns a text icon label for the given MIME type.</summary>
    protected static string GetFileIcon(string? mimeType)
    {
        if (mimeType is null) return "[File]";
        if (mimeType.StartsWith("image/"))  return "[Image]";
        if (mimeType.StartsWith("video/"))  return "[Video]";
        if (mimeType.StartsWith("audio/"))  return "[Audio]";
        if (mimeType == "application/pdf")  return "[PDF]";
        if (mimeType.StartsWith("text/"))   return "[Text]";
        if (mimeType.Contains("spreadsheet") || mimeType.Contains("excel")) return "[Sheet]";
        if (mimeType.Contains("presentation") || mimeType.Contains("powerpoint")) return "[Slides]";
        if (mimeType.Contains("document") || mimeType.Contains("word")) return "[Doc]";
        if (mimeType.Contains("zip") || mimeType.Contains("compressed")) return "[Archive]";
        return "[File]";
    }

    /// <summary>
    /// Infers a human-readable code language label from the file extension.
    /// </summary>
    protected static string GetCodeLanguage(string? name)
    {
        var ext = Path.GetExtension(name)?.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "js" or "jsx"       => "JavaScript",
            "ts" or "tsx"       => "TypeScript",
            "cs"                => "C#",
            "py"                => "Python",
            "go"                => "Go",
            "rs"                => "Rust",
            "java"              => "Java",
            "html" or "htm"     => "HTML",
            "css"               => "CSS",
            "json"              => "JSON",
            "xml" or "xaml"     => "XML",
            "yaml" or "yml"     => "YAML",
            "sh" or "bash"      => "Shell",
            "ps1"               => "PowerShell",
            "sql"               => "SQL",
            _                   => "Code"
        };
    }

    /// <summary>Formats a byte count as a human-readable size string.</summary>
    protected static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}
