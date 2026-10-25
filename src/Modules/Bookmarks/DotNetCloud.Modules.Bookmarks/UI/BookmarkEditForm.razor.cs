using System.ComponentModel.DataAnnotations;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Bookmarks;
using DotNetCloud.Modules.Bookmarks.Models;
using DotNetCloud.Modules.Bookmarks.Services;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Bookmarks.UI;

/// <summary>Code-behind for the <see cref="BookmarkEditForm"/> component.</summary>
public partial class BookmarkEditForm : ComponentBase
{
    [Parameter, EditorRequired] public required BookmarkItem Bookmark { get; set; }
    [Parameter, EditorRequired] public required IReadOnlyList<BookmarkFolder> Folders { get; set; }
    [Parameter] public Func<BookmarkItem, Task>? OnSave { get; set; }
    [Parameter] public Action? OnCancel { get; set; }

    private string _tagsInput = string.Empty;
    private bool _submitting;
    private string? _errorMessage;

    protected override void OnParametersSet()
    {
        _submitting = false;
        _errorMessage = null;

        if (!string.IsNullOrWhiteSpace(Bookmark.TagsJson))
        {
            try
            {
                var tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(Bookmark.TagsJson);
                if (tags is not null)
                    _tagsInput = string.Join(", ", tags);
            }
            catch { }
        }
    }

    private async Task HandleSaveAsync()
    {
        _submitting = true;
        _errorMessage = null;

        try
        {
            // Serialize tags
            if (!string.IsNullOrWhiteSpace(_tagsInput))
            {
                var tags = _tagsInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(t => t.Length > 0).ToList();
                Bookmark.TagsJson = System.Text.Json.JsonSerializer.Serialize(tags);
            }
            else
            {
                Bookmark.TagsJson = null;
            }

            if (OnSave is not null)
                await OnSave.Invoke(Bookmark);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _submitting = false;
        }
    }

    private void HandleInvalidSubmit(EditContext context)
    {
        _errorMessage = "Please fix the validation errors before saving.";
    }

    private void Cancel()
    {
        OnCancel?.Invoke();
    }
}
