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

/// <summary>Code-behind for the <see cref="BookmarksPage"/> component.</summary>
public partial class BookmarksPage : ComponentBase
{
    private enum BookmarkViewMode { Grid, List }

    private bool _sidebarCollapsed;
    private bool _isLoading = true;
    private string? _errorMessage;
    private BookmarkViewMode _viewMode = BookmarkViewMode.Grid;

    private List<BookmarkItem> _bookmarks = [];
    private List<BookmarkFolder> _folders = [];
    private BookmarkItem? _selectedBookmark;
    private BookmarkPreview? _selectedPreview;
    private Guid? _selectedFolderId;
    private bool _showFavorites;

    // Edit state
    private BookmarkItem? _editingBookmark;
    private BookmarkFolder? _editingFolder;
    private string _searchInput = string.Empty;
    private string? _searchQuery;
    private List<BookmarkItem> _searchResults = [];

    // Import state
    private bool _importing;
    private BookmarkImportResult? _importResult;
    private Stream? _importFileStream;
    private string _importFileName = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var collapsed = await JSRuntime.InvokeAsync<string>("localStorage.getItem", new object?[] {
"dotnetcloud.sidebar:bookmarks" });
            if (bool.TryParse(collapsed ?? "false", out var parsed))
                _sidebarCollapsed = parsed;
        }
        catch { }

        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            _folders = (await ApiClient.ListFoldersAsync()).ToList();
            _bookmarks = (await ApiClient.ListAsync(folderId: _selectedFolderId)).ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load bookmarks: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ToggleSidebar()
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        try
        {
            _ = JSRuntime.InvokeVoidAsync("localStorage.setItem",
                new object?[] { "dotnetcloud.sidebar:bookmarks", _sidebarCollapsed.ToString().ToLowerInvariant() });
        }
        catch { }
    }

    private async Task SelectFolder(Guid? folderId)
    {
        _selectedFolderId = folderId;
        _showFavorites = false;
        _selectedBookmark = null;
        _editingBookmark = null;
        _searchQuery = null;
        await ReloadAsync();
    }

    private async Task SelectBookmark(BookmarkItem item)
    {
        _selectedBookmark = item;
        _editingBookmark = null;
        _selectedPreview = null;
        StateHasChanged();
        // Load preview
        try
        {
            _selectedPreview = await ApiClient.GetPreviewAsync(item.Id);
            StateHasChanged();
        }
        catch { _selectedPreview = null; }
    }

    private void BeginAdd()
    {
        _editingBookmark = new BookmarkItem { Id = Guid.Empty, FolderId = _selectedFolderId };
        _selectedBookmark = null;
        StateHasChanged();
    }

    private void BeginEdit(BookmarkItem item)
    {
        _editingBookmark = item;
        _selectedBookmark = null;
    }

    private void BeginAddFolder()
    {
        _editingFolder = new BookmarkFolder { Color = "#6b7280" };
    }

    private async Task SaveBookmarkAsync(BookmarkItem item)
    {
        if (item.Id == Guid.Empty)
        {
            var request = new CreateBookmarkRequest
            {
                Url = item.Url,
                Title = !string.IsNullOrWhiteSpace(item.Title) ? item.Title : null,
                Description = item.Description,
                Notes = item.Notes,
                Tags = !string.IsNullOrWhiteSpace(item.TagsJson)
                    ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(item.TagsJson)
                    : null,
                FolderId = item.FolderId
            };
            await ApiClient.CreateAsync(request);
        }
        else
        {
            var request = new UpdateBookmarkRequest
            {
                Url = item.Url,
                Title = item.Title,
                Description = item.Description,
                Notes = item.Notes,
                Tags = !string.IsNullOrWhiteSpace(item.TagsJson)
                    ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(item.TagsJson)
                    : null,
                FolderId = item.FolderId
            };
            await ApiClient.UpdateAsync(item.Id, request);
        }
        CancelEdit();
        await ReloadAsync();
        StateHasChanged();
    }

    private async Task SaveFolderAsync()
    {
        if (_editingFolder is null) return;

        try
        {
            if (_editingFolder.Id == Guid.Empty)
            {
                await ApiClient.CreateFolderAsync(new CreateBookmarkFolderRequest
                {
                    Name = _editingFolder.Name,
                    Color = _editingFolder.Color
                });
            }
            else
            {
                await ApiClient.UpdateFolderAsync(_editingFolder.Id, new UpdateBookmarkFolderRequest
                {
                    Name = _editingFolder.Name,
                    Color = _editingFolder.Color
                });
            }
            CancelEdit();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to save folder: {ex.Message}";
        }
    }

    private async Task ToggleFavorite(BookmarkItem item)
    {
        try
        {
            await ApiClient.UpdateAsync(item.Id, new UpdateBookmarkRequest { IsFavorite = !item.IsFavorite });
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to update bookmark: {ex.Message}";
        }
    }

    private async Task DeleteBookmark(BookmarkItem item)
    {
        try
        {
            await ApiClient.DeleteAsync(item.Id);
            if (_selectedBookmark?.Id == item.Id)
                _selectedBookmark = null;
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to delete bookmark: {ex.Message}";
        }
    }

    private async Task FetchPreview(BookmarkItem item)
    {
        try
        {
            await ApiClient.FetchPreviewAsync(item.Id);
            if (_selectedBookmark?.Id == item.Id)
                _selectedPreview = await ApiClient.GetPreviewAsync(item.Id);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to fetch preview: {ex.Message}";
        }
    }

    private void CancelEdit()
    {
        _editingBookmark = null;
        _editingFolder = null;
        _selectedBookmark = null;
        StateHasChanged();
    }

    private async Task DoSearch()
    {
        if (string.IsNullOrWhiteSpace(_searchInput)) return;
        _searchQuery = _searchInput;
        try
        {
            _searchResults = (await ApiClient.SearchAsync(_searchQuery)).ToList();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Search failed: {ex.Message}";
        }
    }

    private void BeginImport()
    {
        _importing = true;
        _importResult = null;
    }

    private async Task HandleFileSelected(InputFileChangeEventArgs e)
    {
        if (e.File is null) return;

        try
        {
            _importFileStream = e.File.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
            _importFileName = e.File.Name;
            _importResult = await ApiClient.ImportAsync(_importFileStream, _importFileName, _selectedFolderId);
        }
        catch (Exception ex)
        {
            _importResult = new BookmarkImportResult
            {
                Errors = { $"Import failed: {ex.Message}" }
            };
        }
    }

    private async Task ExportBookmarks()
    {
        try
        {
            var bytes = await ApiClient.ExportAsync(_selectedFolderId);
            var base64 = Convert.ToBase64String(bytes);
            await JSRuntime.InvokeVoidAsync("downloadFile",
                new object[] { "bookmarks.html", "text/html", base64 });
        }
        catch (Exception ex)
        {
            _errorMessage = $"Export failed: {ex.Message}";
        }
    }
}
