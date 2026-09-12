using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Text.RegularExpressions;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the full-screen file preview modal.
/// Supports images, video, audio, PDF, text, code, and markdown previews.
/// Keyboard shortcuts: Escape = close, ← = previous file, → = next file.
/// </summary>
public partial class FilePreview : ComponentBase, IAsyncDisposable
{
    private static readonly Regex HtmlTagRegex = new("<\\/?[a-zA-Z][^>]*>", RegexOptions.Compiled);

    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private IMarkdownRenderer MarkdownRenderer { get; set; } = default!;

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

    /// <summary>Whether to show the Comments button in the preview header.</summary>
    [Parameter] public bool ShowCommentsButton { get; set; } = true;

    /// <summary>When true, the preview starts in slideshow mode and auto-advances through images.</summary>
    [Parameter] public bool StartSlideshow { get; set; }

    /// <summary>Initial seconds between slideshow advances (clamped to 2–60 seconds).</summary>
    [Parameter] public int SlideshowIntervalSeconds { get; set; } = 5;

    /// <summary>Whether to show the Delete button in the preview header (for gallery images).</summary>
    [Parameter] public bool ShowDeleteButton { get; set; }

    /// <summary>
    /// True when a modal dialog (e.g. the delete confirmation) is displayed over the preview.
    /// Keyboard shortcuts are ignored and slideshow auto-advance is paused until it closes.
    /// </summary>
    [Parameter] public bool ModalOpen { get; set; }

    /// <summary>Invoked when the user requests deletion of the currently displayed file.</summary>
    [Parameter] public EventCallback<FileNodeViewModel> OnDelete { get; set; }

    /// <summary>Invoked when the user closes the preview.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Invoked when the user clicks the Download button.</summary>
    [Parameter] public EventCallback<FileNodeViewModel> OnDownload { get; set; }

    /// <summary>Invoked when the user clicks the Share button.</summary>
    [Parameter] public EventCallback<FileNodeViewModel> OnShare { get; set; }

    /// <summary>Invoked when the user clicks the Comments button.</summary>
    [Parameter] public EventCallback<FileNodeViewModel> OnComments { get; set; }

    private ElementReference _overlayRef;
    private ElementReference _codeRef;
    private DotNetObjectReference<FilePreview>? _gestureDotNetRef;
    private int _gestureHandlerId;
    private double _imageZoom = 1;

    // Slideshow playback state
    private bool _slideshowActive;
    private int _slideshowInterval;
    private System.Threading.Timer? _slideshowTimer;

    // Native text preview state
    private string? _textContent;
    private bool _isLoadingText;
    private bool _isEditingText;
    private string? _editableText;
    private bool _isSavingText;
    private bool _needsHighlight;

    protected bool MarkdownContainsInlineHtml
    {
        get
        {
            var content = _isEditingText ? _editableText : _textContent;
            return IsMarkdown && !string.IsNullOrWhiteSpace(content) && HtmlTagRegex.IsMatch(content);
        }
    }

    // Tracks the currently displayed node when the user navigates away from the original Node.
    private FileNodeViewModel? _currentNode;

    // Tracks the last Node parameter to detect actual changes vs. spurious re-renders.
    private Guid? _lastNodeId;

    /// <summary>The node currently displayed in the preview area.</summary>
    protected FileNodeViewModel? DisplayNode => _currentNode ?? Node;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Initialize the slideshow interval once from the parameter (guarded by <= 0 so a
        // re-render never resets a user-chosen interval).
        if (_slideshowInterval <= 0)
            _slideshowInterval = Math.Clamp(SlideshowIntervalSeconds, 2, 60);

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

            if (StartSlideshow)
                StartSlideshowPlayback();
        }

        // Load text content whenever it's needed but not yet loaded.
        // Handles both the initial render and SSR → WASM circuit handoff.
        if ((IsText || IsCode || IsMarkdown) && _textContent is null && !_isLoadingText)
        {
            await LoadTextContentAsync();
        }

        // Apply syntax highlighting after the <code> element has rendered with content.
        if (_needsHighlight && (IsText || IsCode) && _textContent is not null && !_isLoadingText && !_isEditingText)
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
        if (DisplayNode is null)
            return;
        _isLoadingText = true;
        _needsHighlight = !IsMarkdown;
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
        if (DisplayNode is null || _editableText is null)
            return;
        _isSavingText = true;
        StateHasChanged();

        try
        {
            var url = string.IsNullOrEmpty(ApiBaseUrl)
                ? $"/api/v1/files/{DisplayNode.Id}/content"
                : $"{ApiBaseUrl.TrimEnd('/')}/api/v1/files/{DisplayNode.Id}/content";

            var success = await Js.InvokeAsync<bool>("dotnetcloudFilePreview.saveTextContent", url);
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

    /// <summary>Handles image load errors gracefully (broken image fallback handled via CSS).</summary>
    protected void HandleImageError() { /* Graceful fallback — CSS hides broken image icon */ }

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

    /// <summary>True when the file is source code, another text/* sub-type, or a structured text format (JSON, XML).</summary>
    protected bool IsCode =>
        (DisplayNode?.MimeType?.StartsWith("text/") == true && !IsText && !IsMarkdown) ||
        DisplayNode?.MimeType is "application/json" or "application/xml";

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
        DisplayNode is null ? -1 : IndexOf(NavigableFiles, DisplayNode.Id);

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
            await ShowNodeAsync(NavigableFiles[idx - 1]);
    }

    /// <summary>Navigates to the next file in the list.</summary>
    protected async Task GoNext()
    {
        var idx = CurrentIndex;
        if (idx >= 0 && idx < NavigableFiles.Count - 1)
            await ShowNodeAsync(NavigableFiles[idx + 1]);
    }

    /// <summary>Switches the preview to the given node, resetting per-file state and loading content.</summary>
    private async Task ShowNodeAsync(FileNodeViewModel node)
    {
        _currentNode = node;
        _imageZoom = 1;
        _textContent = null;
        _isLoadingText = false;
        _isEditingText = false;
        _editableText = null;
        StateHasChanged();

        if (IsText || IsCode || IsMarkdown)
            await LoadTextContentAsync();
    }

    private static int IndexOf(IReadOnlyList<FileNodeViewModel> list, Guid id)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Id == id)
                return i;
        }

        return -1;
    }

    // ── Slideshow ───────────────────────────────────────────────────────────────

    /// <summary>Image files available for slideshow auto-advance.</summary>
    protected IReadOnlyList<FileNodeViewModel> SlideshowFiles => FilesImageHelper.Filter(NavigableFiles);

    /// <summary>True when there is more than one image to cycle through in a slideshow.</summary>
    protected bool IsSlideshowAvailable => SlideshowFiles.Count > 1;

    /// <summary>
    /// True when the delete action is available for the displayed file — any image that is a
    /// real (non-virtual), writable file, regardless of whether the viewer was opened from the gallery.
    /// </summary>
    protected bool CanDelete =>
        ShowDeleteButton
        && DisplayNode is { IsReadOnly: false }
        && FilesImageHelper.IsImage(DisplayNode);

    /// <summary>True while slideshow auto-advance is running.</summary>
    protected bool IsSlideshowActive => _slideshowActive;

    /// <summary>Starts or stops slideshow auto-advance.</summary>
    protected void ToggleSlideshow()
    {
        if (_slideshowActive)
            StopSlideshow();
        else
            StartSlideshowPlayback();
    }

    private void StartSlideshowPlayback()
    {
        if (!IsSlideshowAvailable)
            return;

        _slideshowActive = true;
        RestartSlideshowTimer();
        StateHasChanged();
    }

    private void StopSlideshow()
    {
        _slideshowActive = false;
        _slideshowTimer?.Dispose();
        _slideshowTimer = null;
        StateHasChanged();
    }

    private void RestartSlideshowTimer()
    {
        _slideshowTimer?.Dispose();
        var interval = TimeSpan.FromSeconds(Math.Clamp(_slideshowInterval, 2, 60));
        _slideshowTimer = new System.Threading.Timer(OnSlideshowTick, null, interval, interval);
    }

    private void OnSlideshowTick(object? state) => _ = RunSlideshowTickAsync();

    private async Task RunSlideshowTickAsync()
    {
        try
        {
            await InvokeAsync(async () =>
            {
                if (!_slideshowActive || _isEditingText || ModalOpen)
                    return;

                await AdvanceSlideshowAsync();
            });
        }
        catch (ObjectDisposedException)
        {
            // Component disposed while the timer callback was in flight.
        }
        catch (InvalidOperationException)
        {
            // Renderer already shut down during teardown.
        }
    }

    private async Task AdvanceSlideshowAsync()
    {
        var images = SlideshowFiles;
        if (images.Count < 2)
            return;

        var index = DisplayNode is null ? -1 : IndexOf(images, DisplayNode.Id);
        var next = index < 0 || index >= images.Count - 1 ? 0 : index + 1;
        await ShowNodeAsync(images[next]);
    }

    /// <summary>Restarts the slideshow timer at the newly selected interval.</summary>
    protected void OnSlideshowIntervalChanged()
    {
        if (_slideshowActive)
            RestartSlideshowTimer();
    }

    /// <summary>Invokes the delete callback for the currently displayed file.</summary>
    protected async Task Delete()
    {
        if (DisplayNode is not null)
            await OnDelete.InvokeAsync(DisplayNode);
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

    /// <summary>Handles keyboard shortcuts: Escape = close, ← = prev, → = next, Space = toggle slideshow, Delete = delete.</summary>
    protected async Task HandleKeyDown(KeyboardEventArgs e)
    {
        // A modal (e.g. delete confirmation) is on top — don't let keys reach the viewer underneath.
        if (ModalOpen)
            return;

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
            case " " when !_isEditingText && IsSlideshowAvailable:
                ToggleSlideshow();
                break;
            case "Delete" when !_isEditingText && CanDelete:
                await Delete();
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
    /// Includes a version query parameter for cache-busting when the file is updated.
    /// </summary>
    protected string GetContentUrl()
    {
        if (DisplayNode is null)
            return "#";
        var base_ = string.IsNullOrEmpty(ApiBaseUrl) ? string.Empty : ApiBaseUrl.TrimEnd('/');
        var version = DisplayNode.CurrentVersion > 0 ? DisplayNode.CurrentVersion : 1;
        return $"{base_}/api/v1/files/{DisplayNode.Id}/content?v={version}";
    }

    // ── Formatting ──────────────────────────────────────────────────────────────

    /// <summary>Returns an icon name for the given MIME type.</summary>
    protected static string GetFileIcon(string? mimeType)
    {
        if (mimeType is null)
            return "description";
        if (mimeType.StartsWith("image/"))
            return "image";
        if (mimeType.StartsWith("video/"))
            return "movie";
        if (mimeType.StartsWith("audio/"))
            return "music_note";
        if (mimeType == "application/pdf")
            return "picture_as_pdf";
        if (mimeType.StartsWith("text/"))
            return "text_snippet";
        if (mimeType.Contains("spreadsheet") || mimeType.Contains("excel"))
            return "table_chart";
        if (mimeType.Contains("presentation") || mimeType.Contains("powerpoint"))
            return "slideshow";
        if (mimeType.Contains("document") || mimeType.Contains("word"))
            return "article";
        if (mimeType.Contains("zip") || mimeType.Contains("compressed"))
            return "folder_zip";
        return "description";
    }

    /// <summary>
    /// Infers a human-readable code language label from the file extension.
    /// </summary>
    protected static string GetCodeLanguage(string? name)
    {
        var ext = Path.GetExtension(name)?.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "js" or "jsx" => "JavaScript",
            "ts" or "tsx" => "TypeScript",
            "cs" => "C#",
            "py" => "Python",
            "go" => "Go",
            "rs" => "Rust",
            "java" => "Java",
            "html" or "htm" => "HTML",
            "css" => "CSS",
            "json" => "JSON",
            "xml" or "xaml" => "XML",
            "yaml" or "yml" => "YAML",
            "sh" or "bash" => "Shell",
            "ps1" => "PowerShell",
            "sql" => "SQL",
            _ => "Code"
        };
    }

    /// <summary>
    /// Returns the highlight.js CSS class for the current file's language.
    /// Uses specific language hints when known, otherwise falls back to auto-detection.
    /// </summary>
    protected string GetHighlightClass()
    {
        if (DisplayNode is null)
            return "hljs";
        if (IsText)
            return "language-plaintext";
        if (IsMarkdown)
            return "language-markdown";

        var ext = Path.GetExtension(DisplayNode.Name)?.TrimStart('.').ToLowerInvariant();
        var lang = ext switch
        {
            "js" or "jsx" or "mjs" or "cjs" => "javascript",
            "ts" or "tsx" => "typescript",
            "cs" => "csharp",
            "py" => "python",
            "go" => "go",
            "rs" => "rust",
            "java" => "java",
            "html" or "htm" => "xml",
            "css" => "css",
            "json" => "json",
            "xml" or "xaml" or "csproj" or "sln" or "props" or "targets" => "xml",
            "yaml" or "yml" => "yaml",
            "sh" or "bash" or "zsh" => "bash",
            "ps1" or "psm1" or "psd1" => "powershell",
            "sql" => "sql",
            "rb" => "ruby",
            "php" => "php",
            "swift" => "swift",
            "kt" or "kts" => "kotlin",
            "c" or "h" => "c",
            "cpp" or "hpp" or "cc" or "cxx" => "cpp",
            "r" => "r",
            "lua" => "lua",
            "dockerfile" => "dockerfile",
            "toml" => "ini",
            "ini" or "cfg" => "ini",
            "md" or "markdown" => "markdown",
            "razor" or "cshtml" => "cshtml-razor",
            _ => null,
        };

        return lang is not null ? $"language-{lang}" : "hljs";
    }

    /// <summary>Formats a byte count as a human-readable size string.</summary>
    protected static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    /// <summary>Inline style for image previews, enabling pinch zoom transform.</summary>
    protected string GetImageStyle() => $"transform: scale({_imageZoom:F2}); transform-origin: center center;";

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _slideshowTimer?.Dispose();
        _slideshowTimer = null;

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
