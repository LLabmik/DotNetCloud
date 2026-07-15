using DotNetCloud.Client.Android.ViewModels;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Views;

/// <summary>Create/edit page for notes with markdown formatting toolbar.</summary>
public partial class NoteEditPage : ContentPage
{
    private readonly NoteEditViewModel _vm;
    private string _lastContent = string.Empty;

    /// <summary>Initializes a new <see cref="NoteEditPage"/>.</summary>
    public NoteEditPage(NoteEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NoteEditViewModel.PreviewHtml))
            {
                PreviewWebView.Source = new HtmlWebViewSource { Html = _vm.PreviewHtml };
            }
        };
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _lastContent = _vm.Content;
        _vm.LoadCommand.Execute(null);
    }

    // ── Markdown Toolbar Button Handlers ────────────────────────────

    private void OnBoldClicked(object? sender, EventArgs e)
        => InsertMarkdownSyntax("**", "**", "bold text");

    private void OnItalicClicked(object? sender, EventArgs e)
        => InsertMarkdownSyntax("*", "*", "italic text");

    private void OnHeadingClicked(object? sender, EventArgs e)
        => PrefixLine("## ");

    private void OnBulletListClicked(object? sender, EventArgs e)
        => PrefixLine("- ");

    private void OnNumberedListClicked(object? sender, EventArgs e)
        => PrefixLine("1. ");

    private void OnLinkClicked(object? sender, EventArgs e)
        => InsertMarkdownSyntax("[", "](url)", "link text");

    private void OnCodeClicked(object? sender, EventArgs e)
        => InsertMarkdownSyntax("`", "`", "code");

    private void OnBlockquoteClicked(object? sender, EventArgs e)
        => PrefixLine("> ");

    private void OnTogglePreviewClicked(object? sender, EventArgs e)
    {
        _vm.TogglePreviewCommand.Execute(null);
    }

    // ── Smart Continuation on Enter ─────────────────────────────────

    private void OnContentTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (e.NewTextValue is null || e.OldTextValue is null)
            return;

        // Guard: skip if we already processed this exact text (prevents re-entrant processing)
        if (string.Equals(e.NewTextValue, _lastContent, StringComparison.Ordinal))
            return;

        // Only process if the new text ends with \n that the old text didn't have
        if (!e.NewTextValue.EndsWith("\n") ||
            e.OldTextValue.EndsWith("\n") ||
            e.NewTextValue.Length != e.OldTextValue.Length + 1)
        {
            _lastContent = e.NewTextValue;
            return;
        }

        string newText = e.NewTextValue;
        int cursorPos = newText.Length;

        // Find start of the just-completed line (last \n before the final one)
        int lineStart = newText.LastIndexOf('\n', cursorPos - 2);
        if (lineStart < 0)
            lineStart = 0;
        else
            lineStart++; // skip the \n char

        string previousLine = newText[lineStart..(cursorPos - 1)]; // exclude the new \n

        string? prefix = GetContinuationPrefix(previousLine);
        if (prefix is null)
        {
            _lastContent = e.NewTextValue;
            return;
        }

        // ── CRITICAL: Defer content modification ─────────────────
        // Modifying _vm.Content inside TextChanged causes re-entrant calls
        // to the native Android EditText while it's still processing the
        // current text change. This corrupts the internal Editable buffer
        // and crashes the app. Using Dispatcher.Dispatch defers the
        // modification until after the current event cycle completes.
        // ──────────────────────────────────────────────────────────

        _lastContent = e.NewTextValue; // snapshot what we're processing now

        // If the line is just the prefix (empty content after it), remove the prefix to end the list
        if (previousLine.TrimEnd().Equals(prefix.TrimEnd(), StringComparison.Ordinal))
        {
            string before = newText[..lineStart];
            string after = newText[cursorPos..];
            Dispatcher.Dispatch(() =>
            {
                _vm.Content = before + after;
                _lastContent = _vm.Content;
            });
            return;
        }

        // Insert the continuation prefix
        string modified = newText + prefix;
        Dispatcher.Dispatch(() =>
        {
            _vm.Content = modified;
            _lastContent = _vm.Content;
        });
    }

    /// <summary>
    /// Returns the continuation prefix for the given line, or null if no continuation needed.
    /// </summary>
    private static string? GetContinuationPrefix(string line)
    {
        string trimmed = line.TrimEnd();

        // Blockquote: "> text" → continue with "> "
        if (trimmed.StartsWith("> ", StringComparison.Ordinal) && trimmed.Length > 2)
            return "> ";

        // Unordered list: "- text", "* text", "+ text" → continue with same prefix
        foreach (var bullet in new[] { "- ", "* ", "+ " })
        {
            if (trimmed.StartsWith(bullet, StringComparison.Ordinal) && trimmed.Length > 2)
                return bullet;
        }

        // Numbered list: "1. text", "2. text" → increment the number
        var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(\d+)\.\s");
        if (match.Success && trimmed.Length > match.Length)
        {
            if (int.TryParse(match.Groups[1].Value, out int num))
                return $"{num + 1}. ";
        }

        return null;
    }

    // ── Markdown Insertion Helpers ──────────────────────────────────

    private void InsertMarkdownSyntax(string prefix, string suffix, string placeholder)
    {
        string content = _vm.Content;
        int cursorPos = ContentEditor.CursorPosition;
        int selectionLen = ContentEditor.SelectionLength;

        if (selectionLen > 0)
        {
            string selected = content.Substring(cursorPos, selectionLen);
            _vm.Content = content[..cursorPos] + prefix + selected + suffix + content[(cursorPos + selectionLen)..];
        }
        else
        {
            _vm.Content = content[..cursorPos] + prefix + placeholder + suffix + content[cursorPos..];
        }
    }

    private void PrefixLine(string prefix)
    {
        string content = _vm.Content;
        int cursorPos = ContentEditor.CursorPosition;

        // Find start of current line
        int lineStart = content.LastIndexOf('\n', cursorPos > 0 ? cursorPos - 1 : 0);
        if (lineStart < 0)
            lineStart = 0;
        else
            lineStart++;

        // Don't double-prefix
        string linePrefix = content[lineStart..cursorPos];
        if (linePrefix.StartsWith(prefix, StringComparison.Ordinal))
            return;

        _vm.Content = content[..lineStart] + prefix + content[lineStart..];
    }
}
