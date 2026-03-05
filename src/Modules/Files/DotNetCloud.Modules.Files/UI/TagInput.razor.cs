using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Tag autocomplete input component. Suggests existing user tags while typing
/// and allows the user to specify a color before adding.
/// </summary>
public partial class TagInput : ComponentBase
{
    /// <summary>All user tags available for autocomplete suggestions.</summary>
    [Parameter] public IReadOnlyList<FileTagViewModel> ExistingTags { get; set; } = [];

    /// <summary>Raised when the user adds a tag (name, color).</summary>
    [Parameter] public EventCallback<(string Name, string? Color)> OnTagAdded { get; set; }

    private string _tagName = string.Empty;
    private string _tagColor = "#3b82f6"; // default blue
    private bool _showSuggestions;

    /// <summary>Tags that match the current input text.</summary>
    protected IReadOnlyList<FileTagViewModel> FilteredSuggestions
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_tagName))
                return ExistingTags;

            return ExistingTags
                .Where(t => t.Name.Contains(_tagName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    private async Task AddTag()
    {
        if (string.IsNullOrWhiteSpace(_tagName)) return;

        var name = _tagName.Trim();
        var color = _tagColor == "#3b82f6" ? null : _tagColor; // null means "use existing color or default"
        _tagName = string.Empty;
        _showSuggestions = false;

        await OnTagAdded.InvokeAsync((name, color));
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await AddTag();
        if (e.Key == "Escape") { _showSuggestions = false; _tagName = string.Empty; }
    }

    private async Task SelectSuggestion(FileTagViewModel suggestion)
    {
        _tagName = suggestion.Name;
        _tagColor = suggestion.Color ?? _tagColor;
        _showSuggestions = false;
        await AddTag();
    }

    private void ShowSuggestions() => _showSuggestions = true;
}
