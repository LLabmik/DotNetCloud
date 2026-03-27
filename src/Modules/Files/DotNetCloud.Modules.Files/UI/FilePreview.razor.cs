using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the full-screen file preview modal.
/// Supports images, video, audio, PDF, text, code, and markdown previews.
/// Keyboard shortcuts: Escape = close, ← = previous file, → = next file.
/// </summary>
public partial class FilePreview : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = default!;

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
    private ElementReference _codeRef;
    private DotNetObjectReference<FilePreview>? _gestureDotNetRef;
    private int _gestureHandlerId;
    private double _imageZoom = 1;

    // Native text preview state
    private string? _textContent;
    private bool _isLoadingText;
    private bool _isEditingText;
    private string? _editableText;
    private bool _isSavingText;
    private bool _needsHighlight;

    // Tracks the currently displayed node when the user navigates away from the original Node.
    private FileNodeViewModel? _currentNode;

    // Tracks the last Node parameter to detect actual changes vs. spurious re-renders.
    private Guid? _lastNodeId;

    /// <summary>The node currently displayed in the preview area.</summary>
    protected FileNodeViewModel? DisplayNode => _currentNode ?? Node;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Only reset state when the Node parameter actually changes.
        // Prevents losing loaded text content on spurious parent re-renders
        // (e.g., SSR → WASM circuit handoff in InteractiveAuto mode).
        var newId = Node?.Id;
        if (newId == _lastNodeId)
            return;

        _lastNodeId = newId;
        _currentNode = null;
        _imageZoom = 1;
        _textContent = null;
        _isLoadingText = false;
        _isEditingText = false;
        _editableText = null;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await _overlayRef.FocusAsync();

            _gestureDotNetRef = DotNetObjectReference.Create(this);
            _gestureHandlerId = await Js.InvokeAsync<int>("dotnetcloudFilePreviewGestures.init", _overlayRef, _gestureDotNetRef);
        }

        // Load text content whenever it's needed but not yet loaded.
        // Handles both the initial render and SSR → WASM circuit handoff.
        if ((IsText || IsCode || IsMarkdown) && _textContent is null && !_isLoadingText)
        {
            await LoadTextContentAsync();
        }

        // Apply syntax highlighting after the <code> element has rendered with content.
        if (_needsHighlight && _textContent is not null && !_isLoadingText && !_isEditingText)
        {
            _needsHighlight = false;
            try
            {
                await Js.InvokeVoidAsync("dotnetcloudFilePreview.highlightCode", _codeRef);
            }
            catch (JSDisconnectedException)
            {
                // Circuit disconnected during teardown.
            }
        }
    }

    /// <summary>Fetches text file content via JS fetch and stores it for native rendering.</summary>
    private async Task LoadTextContentAsync()
    {
        if (DisplayNode is null) return;
        _isLoadingText = true;
        _needsHighlight = true;
        StateHasChanged();

        try
        {
            _textContent = await Js.InvokeAsync<string>("dotnetcloudFilePreview.fetchTextContent", GetContentUrl());
        }
        catch
        {
            _textContent = "Failed to load file content.";
            _needsHighlight = false;
        }
        finally
        {
            _isLoadingText = false;
            StateHasChanged();
        }
    }

    /// <summary>Toggles between view and edit mode for text files.</summary>
    protected void ToggleTextEdit()
    {
        _isEditingText = !_isEditingText;
        if (_isEditingText)
            _editableText = _textContent;
    }

    /// <summary>Saves the edited text content back to the server via PUT.</summary>
    protected async Task SaveTextAsync()
    {
        if (DisplayNode is null || _editableText is null) return;
        _isSavingText = true;
        StateHasChanged();

        try
        {
            var url = string.IsNullOrEmpty(ApiBaseUrl)
                ? $"/api/v1/files/{DisplayNode.Id}/content"
                : $"{ApiBaseUrl.TrimEnd('/')}/api/v1/files/{DisplayNode.Id}/content";

            var success = await Js.InvokeAsync<bool>("dotnetcloudFilePreview.saveTextContent", url, _editableText);
            if (success)
            {
                _textContent = _editableText;
                _isEditingText = false;
            }
        }
        catch
        {
            // Save failed — stay in edit mode so user doesn't lose work
        }
        finally
        {
            _isSavingText = false;
            StateHasChanged();
        }
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
    protected async Task GoPrev()
    {
        var idx = CurrentIndex;
        if (idx > 0)
        {
            _currentNode = NavigableFiles[idx - 1];
            _imageZoom = 1;
            _textContent = null;
            _isEditingText = false;
            StateHasChanged();
            if (IsText || IsCode || IsMarkdown)
                await LoadTextContentAsync();
        }
    }

    /// <summary>Navigates to the next file in the list.</summary>
    protected async Task GoNext()
    {
        var idx = CurrentIndex;
        if (idx >= 0 && idx < NavigableFiles.Count - 1)
        {
            _currentNode = NavigableFiles[idx + 1];
            _imageZoom = 1;
            _textContent = null;
            _isEditingText = false;
            StateHasChanged();
            if (IsText || IsCode || IsMarkdown)
                await LoadTextContentAsync();
        }
    }

    /// <summary>Handles a swipe-left gesture to navigate forward.</summary>
    [JSInvokable]
    public async Task OnSwipeLeft()
    {
        await GoNext();
    }

    /// <summary>Handles a swipe-right gesture to navigate backward.</summary>
    [JSInvokable]
    public async Task OnSwipeRight()
    {
        await GoPrev();
    }

    /// <summary>Handles pinch gesture scale deltas for image zoom.</summary>
    [JSInvokable]
    public Task OnPinchScale(double scaleDelta)
    {
        if (!IsImage)
        {
            return Task.CompletedTask;
        }

        _imageZoom = Math.Clamp(_imageZoom * scaleDelta, 1.0, 4.0);
        StateHasChanged();
        return Task.CompletedTask;
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
                await GoPrev();
                break;
            case "ArrowRight":
                await GoNext();
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

    /// <summary>Returns an emoji icon for the given MIME type.</summary>
    protected static string GetFileIcon(string? mimeType)
    {
        if (mimeType is null) return "📄";
        if (mimeType.StartsWith("image/")) return "🖼️";
        if (mimeType.StartsWith("video/")) return "🎬";
        if (mimeType.StartsWith("audio/")) return "🎵";
        if (mimeType == "application/pdf") return "📕";
        if (mimeType.StartsWith("text/")) return "📝";
        if (mimeType.Contains("spreadsheet") || mimeType.Contains("excel")) return "📊";
        if (mimeType.Contains("presentation") || mimeType.Contains("powerpoint")) return "📈";
        if (mimeType.Contains("document") || mimeType.Contains("word")) return "📘";
        if (mimeType.Contains("zip") || mimeType.Contains("compressed")) return "🗜️";
        return "📄";
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

    /// <summary>
    /// Returns the highlight.js CSS class for the current file's language.
    /// Uses specific language hints when known, otherwise falls back to auto-detection.
    /// </summary>
    protected string GetHighlightClass()
    {
        if (DisplayNode is null) return "hljs";
        if (IsText) return "language-plaintext";
        if (IsMarkdown) return "language-markdown";

        var ext = Path.GetExtension(DisplayNode.Name)?.TrimStart('.').ToLowerInvariant();
        var lang = ext switch
        {
            "js" or "jsx" or "mjs" or "cjs" => "javascript",
            "ts" or "tsx"                    => "typescript",
            "cs"                             => "csharp",
            "py"                             => "python",
            "go"                             => "go",
            "rs"                             => "rust",
            "java"                           => "java",
            "html" or "htm"                  => "xml",
            "css"                            => "css",
            "json"                           => "json",
            "xml" or "xaml" or "csproj" or "sln" or "props" or "targets" => "xml",
            "yaml" or "yml"                  => "yaml",
            "sh" or "bash" or "zsh"          => "bash",
            "ps1" or "psm1" or "psd1"        => "powershell",
            "sql"                            => "sql",
            "rb"                             => "ruby",
            "php"                            => "php",
            "swift"                          => "swift",
            "kt" or "kts"                    => "kotlin",
            "c" or "h"                       => "c",
            "cpp" or "hpp" or "cc" or "cxx"  => "cpp",
            "r"                              => "r",
            "lua"                            => "lua",
            "dockerfile"                     => "dockerfile",
            "toml"                           => "ini",
            "ini" or "cfg"                   => "ini",
            "md" or "markdown"               => "markdown",
            "razor" or "cshtml"              => "cshtml-razor",
            _                                => null,
        };

        return lang is not null ? $"language-{lang}" : "hljs";
    }

    /// <summary>Formats a byte count as a human-readable size string.</summary>
    protected static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    /// <summary>Inline style for image previews, enabling pinch zoom transform.</summary>
    protected string GetImageStyle() => $"transform: scale({_imageZoom:F2}); transform-origin: center center;";

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_gestureHandlerId != 0)
        {
            try
            {
                await Js.InvokeVoidAsync("dotnetcloudFilePreviewGestures.dispose", _gestureHandlerId);
            }
            catch (JSDisconnectedException)
            {
                // Blazor circuit already disconnected during teardown.
            }
        }

        _gestureDotNetRef?.Dispose();
    }
}
