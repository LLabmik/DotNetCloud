using DotNetCloud.Core.Localization;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Editors;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Layout;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Resources;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace DotNetCloud.UI.Shared.Components.Editors;

/// <summary>Code-behind for the <see cref="MarkdownEditor"/> component.</summary>
public partial class MarkdownEditor : ComponentBase
{
    private string _renderedHtml = string.Empty;
    private EditorView _activeView = EditorView.Split;
    private ElementReference _textareaRef;
    private bool _showCompactMenu;

    /// <summary>
    /// The raw Markdown content being edited.
    /// </summary>
    [Parameter]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Callback fired when the content changes.
    /// </summary>
    [Parameter]
    public EventCallback<string> ContentChanged { get; set; }

    /// <summary>
    /// The Markdown renderer service injected from DI.
    /// </summary>
    [Parameter]
    public IMarkdownRenderer? Renderer { get; set; }

    /// <summary>
    /// When true, hides the editor pane and toolbar and shows only the rendered preview.
    /// </summary>
    [Parameter]
    public bool PreviewOnly { get; set; }

    /// <summary>
    /// When true, initializes the editor in preview view instead of split view.
    /// Useful in contexts where rendered output should be emphasized first.
    /// </summary>
    [Parameter]
    public bool StartInPreview { get; set; }

    /// <summary>
    /// When true, renders a compact toolbar with essential formatting buttons only.
    /// Hides headings, tables, images, blockquotes, horizontal rules, and split view.
    /// </summary>
    [Parameter]
    public bool Compact { get; set; }

    /// <summary>
    /// When true, the editor fills the height its parent gives it and scrolls internally, so the
    /// formatting toolbar stays on screen and the page behind it does not grow.
    /// </summary>
    [Parameter]
    public bool FillHeight { get; set; }

    /// <summary>
    /// Custom placeholder text for the editor textarea.
    /// </summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>
    /// Number of visible rows in the editor textarea. Defaults to 0 (auto/flex).
    /// </summary>
    [Parameter]
    public int Rows { get; set; }

    /// <summary>
    /// Maximum content length in characters. Defaults to 100,000.
    /// Content beyond this limit is truncated. Set to 0 for unlimited.
    /// </summary>
    [Parameter]
    public int MaxLength { get; set; } = 100000;

    /// <summary>
    /// When true, shows a character count below the editor.
    /// </summary>
    [Parameter]
    public bool ShowCharacterCount { get; set; } = true;

    private bool _viewInitialized;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!_viewInitialized)
        {
            _activeView = PreviewOnly || StartInPreview
                ? EditorView.Preview
                : Compact ? EditorView.Edit : EditorView.Split;
            _viewInitialized = true;
        }
        else if (PreviewOnly)
        {
            _activeView = EditorView.Preview;
        }

        RenderPreview();
    }

    private void SetView(EditorView view)
    {
        if (PreviewOnly) return;
        _activeView = view;
        _showCompactMenu = false;
        if (view != EditorView.Edit)
        {
            RenderPreview();
        }
    }

    private void ToggleCompactMenu()
    {
        _showCompactMenu = !_showCompactMenu;
    }

    private async Task OnContentChanged()
    {
        EnforceMaxLength();
        RenderPreview();
        await ContentChanged.InvokeAsync(Content);
    }

    private void EnforceMaxLength()
    {
        if (MaxLength > 0 && Content.Length > MaxLength)
        {
            Content = Content[..MaxLength];
        }
    }

    private void RenderPreview()
    {
        if (Renderer is not null && !string.IsNullOrEmpty(Content))
        {
            _renderedHtml = Renderer.RenderToHtml(Content);
        }
        else
        {
            _renderedHtml = string.Empty;
        }
    }

    // ─── Toolbar actions ─────────────────────────────────────────────
    // Each action reads the textarea's live text + selection from the browser, applies a pure text
    // edit from MarkdownTextFormatter, and writes the result back with the new selection. Text the
    // user has highlighted is always formatted, never replaced.

    private Task InsertBold() =>
        ApplyWrapFormat("**", "**", "bold text");

    private Task InsertItalic() =>
        ApplyWrapFormat("*", "*", "italic text");

    private Task InsertStrikethrough() =>
        ApplyWrapFormat("~~", "~~", "strikethrough");

    private Task InsertHeading(int level) =>
        ApplyEditAsync(state => MarkdownTextFormatter.SetHeading(state.Value, state.Selection, level));

    private Task InsertUnorderedList() =>
        ApplyEditAsync(state => MarkdownTextFormatter.ToggleLinePrefix(state.Value, state.Selection, "- "));

    private Task InsertOrderedList() =>
        ApplyEditAsync(state => MarkdownTextFormatter.NumberLines(state.Value, state.Selection));

    private Task InsertTaskList() =>
        ApplyEditAsync(state => MarkdownTextFormatter.ToggleLinePrefix(state.Value, state.Selection, "- [ ] "));

    private Task InsertLink() =>
        ApplyWrapFormat("[", "](https://example.com)", "link text");

    private Task InsertImage() =>
        ApplyWrapFormat("![", "](image-url)", "alt text");

    private Task InsertCodeBlock() =>
        ApplyWrapFormat("\n```\n", "\n```\n", "code here");

    private Task InsertBlockquote() =>
        ApplyEditAsync(state => MarkdownTextFormatter.ToggleLinePrefix(state.Value, state.Selection, "> "));

    private Task InsertHorizontalRule() =>
        InsertBlock("\n---\n");

    private Task InsertTable() =>
        InsertBlock("\n| Header 1 | Header 2 | Header 3 |\n|----------|----------|----------|\n| Cell 1   | Cell 2   | Cell 3   |\n");

    private Task InsertBlock(string block) =>
        ApplyEditAsync(state => MarkdownTextFormatter.InsertBlock(state.Value, state.Selection, block));

    private Task ApplyWrapFormat(string prefix, string suffix, string placeholder) =>
        ApplyEditAsync(state => MarkdownTextFormatter.ToggleWrap(state.Value, state.Selection, prefix, suffix, placeholder));

    /// <summary>
    /// Reads the textarea state, applies <paramref name="edit"/>, writes the result back and
    /// notifies the parent component.
    /// </summary>
    private async Task ApplyEditAsync(Func<MarkdownEditorState, MarkdownEditorEdit> edit)
    {
        var state = await ReadStateAsync();
        if (state is null)
        {
            return;
        }

        var result = edit(state);

        try
        {
            await JS.InvokeVoidAsync(MarkdownEditorInterop.WriteState, _textareaRef,
                result.Value, result.Selection.Start, result.Selection.End);
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone — the edit still has to reach the parent below.
        }
        catch (JSException)
        {
            // Interop failed; keep the text edit rather than losing it.
        }

        Content = result.Value;
        EnforceMaxLength();
        RenderPreview();
        await ContentChanged.InvokeAsync(Content);
    }

    /// <summary>
    /// Reads the editor textarea's text and selection, or <see langword="null"/> when the element
    /// is no longer available (for example a stale reference after a re-render).
    /// </summary>
    private async Task<MarkdownEditorState?> ReadStateAsync()
    {
        try
        {
            return await JS.InvokeAsync<MarkdownEditorState?>(MarkdownEditorInterop.ReadState, _textareaRef);
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
        catch (JSException)
        {
            return null;
        }
    }

    private enum EditorView
    {
        Edit,
        Split,
        Preview
    }
}
