using System.Text.Json;
using DotNetCloud.Modules.Bookmarks.Web.Services;
using DotNetCloud.Modules.Sdk.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace DotNetCloud.Modules.Bookmarks.Web.Pages;

/// <summary>
/// Code-behind for the Bookmarks module UI.
/// </summary>
/// <remarks>
/// <para>
/// The page is the same single-document UI the module shipped as plain JavaScript, ported to Razor
/// components: a sidebar of views, folders and tools, and one main panel at a time (library, detail,
/// bookmark form, folder form, import, help).
/// </para>
/// <para>
/// Every string that comes from the server is user data and is rendered as text — Razor escapes it, so
/// there is no innerHTML equivalent anywhere.
/// </para>
/// </remarks>
public partial class Bookmarks : ComponentBase
{
    private const int PageSize = 200;
    private const long MaxImportBytes = 10L * 1024 * 1024;

    private readonly List<BookmarkFolderDto> _folders = [];
    private readonly List<BookmarkDto> _bookmarks = [];
    private readonly List<BookmarkDto> _searchResults = [];

    private Panel _panel = Panel.Library;
    private string _activeNav = "nav-all";
    private bool _collapsed;

    private string? _status;
    private string? _searchQuery;
    private string _searchInput = string.Empty;
    private bool _showFavorites;
    private Guid? _selectedFolderId;
    private bool _listView;

    private BookmarkDto? _selected;
    private BookmarkPreviewDto? _preview;

    private BookmarkDto? _editing;
    private string _editUrl = string.Empty;
    private string _editTitle = string.Empty;
    private string _editDescription = string.Empty;
    private string _editNotes = string.Empty;
    private string _editTags = string.Empty;
    private Guid? _editFolderId;
    private string? _editError;
    private bool _savingBookmark;

    private BookmarkFolderDto? _editingFolder;
    private string _folderName = string.Empty;
    private string _folderColor = "#6b7280";
    private string? _folderError;
    private bool _savingFolder;
    private bool _deletingFolder;

    private IBrowserFile? _importFile;
    private Guid? _importFolderId;
    private string? _importError;
    private BookmarkImportResultDto? _importResult;
    private bool _importing;

    [Inject]
    private BookmarksApi Api { get; set; } = default!;

    [Inject]
    private ModuleFrameBridge Bridge { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private IReadOnlyList<BookmarkDto> VisibleItems =>
        _searchQuery is not null
            ? _searchResults
            : _showFavorites
                ? _bookmarks.Where(item => item.IsFavorite).ToList()
                : _bookmarks;

    private string LibraryTitle
    {
        get
        {
            if (_searchQuery is not null)
            {
                return $"Results for “{_searchQuery}”";
            }

            if (_showFavorites)
            {
                return "Favorites";
            }

            return _selectedFolderId is { } folderId ? FolderName(folderId) : "All bookmarks";
        }
    }

    private string EmptyTitle =>
        _searchQuery is not null ? "No matching bookmarks"
        : _showFavorites ? "No favorites yet"
        : _selectedFolderId is not null ? "This folder is empty"
        : "No bookmarks yet";

    private string EmptyText =>
        _searchQuery is not null
            ? $"Nothing matched “{_searchQuery}”. Try a different search."
            : _showFavorites
                ? "Star a bookmark to keep it here."
                : _selectedFolderId is not null
                    ? "Add a bookmark to this folder, or move an existing one."
                    : "Add your first bookmark to get started, or import one from your browser.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync() => await RefreshAsync();

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Bridge.InitializeAsync();
        }
    }

    // ── Data ────────────────────────────────────────────────

    private async Task RefreshAsync()
    {
        _status = "Loading bookmarks…";

        try
        {
            _folders.Clear();
            _folders.AddRange(await Api.ListFoldersAsync());

            await LoadBookmarksAsync();
            _status = null;
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            ReportError(ex);
        }
    }

    private async Task LoadBookmarksAsync()
    {
        _bookmarks.Clear();
        _bookmarks.AddRange(await Api.ListAsync(_selectedFolderId, take: PageSize));

        if (_selected is not null)
        {
            _selected = _bookmarks.FirstOrDefault(item => item.Id == _selected.Id) ?? _selected;
        }
    }

    private async Task SelectAllAsync()
    {
        _selectedFolderId = null;
        _showFavorites = false;
        ClearSearchState();
        _activeNav = "nav-all";
        await ShowPanelAsync(Panel.Library, "Bookmarks");
        await RefreshAsync();
    }

    private async Task SelectFavoritesAsync()
    {
        _selectedFolderId = null;
        _showFavorites = true;
        ClearSearchState();
        _activeNav = "nav-favorites";
        await ShowPanelAsync(Panel.Library, "Bookmarks");
        await RefreshAsync();
    }

    private async Task SelectFolderAsync(Guid folderId)
    {
        _selectedFolderId = folderId;
        _showFavorites = false;
        ClearSearchState();
        _activeNav = $"folder:{folderId}";
        await ShowPanelAsync(Panel.Library, "Bookmarks");
        await RefreshAsync();
    }

    private async Task RunSearchAsync()
    {
        var query = _searchInput.Trim();
        if (query.Length == 0)
        {
            ClearSearch();
            return;
        }

        _searchQuery = query;
        _status = "Searching…";

        try
        {
            _searchResults.Clear();
            _searchResults.AddRange(await Api.SearchAsync(query, take: PageSize));

            await ShowPanelAsync(Panel.Library, "Bookmarks");
            _status = null;
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            ReportError(ex);
        }
    }

    private void ClearSearch()
    {
        ClearSearchState();
        _searchResults.Clear();
    }

    private void ClearSearchState()
    {
        _searchQuery = null;
        _searchInput = string.Empty;
    }

    // ── Detail ──────────────────────────────────────────────

    private async Task OpenDetailAsync(BookmarkDto item)
    {
        _selected = item;
        _preview = null;
        await ShowPanelAsync(Panel.Detail, "Bookmark");

        try
        {
            _preview = await Api.GetPreviewAsync(item.Id);
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            ReportError(ex);
        }
    }

    private async Task BackToLibraryAsync()
    {
        _selected = null;
        _preview = null;
        await ShowPanelAsync(Panel.Library, "Bookmarks");
    }

    private async Task OpenSelectedUrlAsync()
    {
        if (_selected is { Url.Length: > 0 } selected)
        {
            await Bridge.OpenExternalAsync(selected.Url);
        }
    }

    private async Task ToggleFavoriteAsync(BookmarkDto item)
    {
        try
        {
            var updated = await Api.SetFavoriteAsync(item.Id, !item.IsFavorite);
            ReplaceItem(updated?.Id == item.Id ? updated : item with { IsFavorite = !item.IsFavorite });

            if (_selected?.Id == item.Id)
            {
                _selected = FindItem(item.Id) ?? _selected;
            }
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            ReportError(ex);
        }
    }

    private async Task FetchPreviewAsync(BookmarkDto item)
    {
        _status = "Fetching preview…";

        try
        {
            var preview = await Api.FetchPreviewAsync(item.Id);

            if (_selected?.Id == item.Id)
            {
                _preview = preview;
            }

            _status = null;
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            ReportError(ex);
        }
    }

    private async Task DeleteBookmarkAsync(BookmarkDto item)
    {
        try
        {
            await Api.DeleteAsync(item.Id);

            _bookmarks.RemoveAll(candidate => candidate.Id == item.Id);
            _searchResults.RemoveAll(candidate => candidate.Id == item.Id);

            if (_selected?.Id == item.Id)
            {
                _selected = null;
                _preview = null;
                await ShowPanelAsync(Panel.Library, "Bookmarks");
            }

            await Bridge.NotifyAsync("Bookmark deleted");
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            ReportError(ex);
        }
    }

    private BookmarkDto? FindItem(Guid id) =>
        (_searchQuery is not null ? _searchResults : _bookmarks).FirstOrDefault(candidate => candidate.Id == id);

    private void ReplaceItem(BookmarkDto updated)
    {
        ReplaceIn(_bookmarks, updated);
        ReplaceIn(_searchResults, updated);

        static void ReplaceIn(List<BookmarkDto> list, BookmarkDto updated)
        {
            var index = list.FindIndex(candidate => candidate.Id == updated.Id);
            if (index >= 0)
            {
                list[index] = updated;
            }
        }
    }

    // ── Bookmark form ───────────────────────────────────────

    private async Task BeginCreate()
    {
        _editing = null;
        _editUrl = string.Empty;
        _editTitle = string.Empty;
        _editDescription = string.Empty;
        _editNotes = string.Empty;
        _editTags = string.Empty;
        _editFolderId = _selectedFolderId;
        _editError = null;

        await ShowPanelAsync(Panel.Edit, "Bookmark");
    }

    private async Task BeginEdit(BookmarkDto item)
    {
        _editing = item;
        _editUrl = item.Url;
        _editTitle = item.Title;
        _editDescription = item.Description ?? string.Empty;
        _editNotes = item.Notes ?? string.Empty;
        _editTags = string.Join(", ", ParseTags(item.TagsJson));
        _editFolderId = item.FolderId;
        _editError = null;

        await ShowPanelAsync(Panel.Edit, "Bookmark");
    }

    private async Task SaveBookmarkAsync()
    {
        var url = _editUrl.Trim();
        if (url.Length == 0)
        {
            _editError = "A URL is required.";
            return;
        }

        var request = new BookmarkSaveRequest
        {
            Url = url,
            Title = Optional(_editTitle),
            Description = Optional(_editDescription),
            Notes = Optional(_editNotes),
            Tags = ParseTags(_editTags),
            FolderId = _editFolderId,
        };

        var isCreate = _editing is null;
        _savingBookmark = true;

        try
        {
            if (isCreate)
            {
                await Api.CreateAsync(request);
            }
            else
            {
                await Api.UpdateAsync(_editing!.Id, request);
            }

            _editing = null;
            await RefreshAsync();
            await ShowPanelAsync(Panel.Library, "Bookmarks");
            await Bridge.NotifyAsync(isCreate ? "Bookmark created" : "Bookmark updated");
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            _editError = ex.Message;
        }
        finally
        {
            _savingBookmark = false;
        }
    }

    private async Task CancelEdit()
    {
        _editing = null;
        await ShowPanelAsync(Panel.Library, "Bookmarks");
    }

    // ── Folder form ─────────────────────────────────────────

    private async Task BeginFolderCreate()
    {
        _editingFolder = null;
        _folderName = string.Empty;
        _folderColor = "#6b7280";
        _folderError = null;

        await ShowPanelAsync(Panel.Folder, "Folder");
    }

    private async Task BeginFolderEdit(BookmarkFolderDto folder)
    {
        _editingFolder = folder;
        _folderName = folder.Name;
        _folderColor = folder.Color ?? "#6b7280";
        _folderError = null;

        await ShowPanelAsync(Panel.Folder, "Folder");
    }

    private async Task SaveFolderAsync()
    {
        var name = _folderName.Trim();
        if (name.Length == 0)
        {
            _folderError = "A folder name is required.";
            return;
        }

        var request = new BookmarkFolderSaveRequest
        {
            Name = name,
            Color = string.IsNullOrWhiteSpace(_folderColor) ? null : _folderColor,
        };

        _savingFolder = true;

        try
        {
            if (_editingFolder is not null)
            {
                await Api.UpdateFolderAsync(_editingFolder.Id, request);
            }
            else
            {
                await Api.CreateFolderAsync(request);
            }

            _editingFolder = null;
            await RefreshAsync();
            await ShowPanelAsync(Panel.Library, "Bookmarks");
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            _folderError = ex.Message;
        }
        finally
        {
            _savingFolder = false;
        }
    }

    private async Task DeleteFolderAsync()
    {
        if (_editingFolder is not { } folder)
        {
            return;
        }

        _deletingFolder = true;

        try
        {
            await Api.DeleteFolderAsync(folder.Id);

            _editingFolder = null;
            if (_selectedFolderId == folder.Id)
            {
                _selectedFolderId = null;
                _activeNav = "nav-all";
            }

            await RefreshAsync();
            await ShowPanelAsync(Panel.Library, "Bookmarks");
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            _folderError = ex.Message;
        }
        finally
        {
            _deletingFolder = false;
        }
    }

    private async Task CancelFolder()
    {
        _editingFolder = null;
        await ShowPanelAsync(Panel.Library, "Bookmarks");
    }

    // ── Import / export ─────────────────────────────────────

    private async Task OpenImport()
    {
        _importFile = null;
        _importFolderId = _selectedFolderId;
        _importResult = null;
        _importError = null;
        _activeNav = "nav-import";

        await ShowPanelAsync(Panel.Import, "Import bookmarks");
    }

    private void OnImportFileSelected(InputFileChangeEventArgs args)
    {
        _importFile = args.File;
        _importError = null;
    }

    private async Task ImportAsync()
    {
        if (_importFile is null)
        {
            _importError = "Select a bookmarks file to import.";
            return;
        }

        _importing = true;
        _importError = null;

        try
        {
            await using var stream = _importFile.OpenReadStream(MaxImportBytes);
            _importResult = await Api.ImportAsync(stream, _importFile.Name, _importFolderId);
            await RefreshAsync();
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException or IOException)
        {
            _importError = ex is ModuleApiException ? ex.Message : "Could not reach the module. Try again.";
        }
        finally
        {
            _importing = false;
        }
    }

    private async Task CloseImport()
    {
        _activeNav = _showFavorites
            ? "nav-favorites"
            : _selectedFolderId is { } folderId
                ? $"folder:{folderId}"
                : "nav-all";

        await ShowPanelAsync(Panel.Library, "Bookmarks");
    }

    private async Task ExportAsync()
    {
        // A download inside a sandboxed frame is blocked, so the export endpoint is opened in a
        // core-controlled tab: that navigation carries the session cookie, the proxy mints the
        // assertion, and the module answers with a text/html attachment.
        var absolute = new Uri(new Uri(Navigation.BaseUri), BookmarksApi.BuildExportUrl(_selectedFolderId));
        await Bridge.OpenExternalAsync(absolute.ToString());
    }

    // ── Shell ───────────────────────────────────────────────

    private Task ShowHelpAsync()
    {
        _activeNav = "nav-help";
        return ShowPanelAsync(Panel.Help, "Bookmarks help");
    }

    private async Task ShowPanelAsync(Panel panel, string title)
    {
        _panel = panel;
        await Bridge.SetTitleAsync(title);
    }

    private void ToggleSidebar() => _collapsed = !_collapsed;

    private Task SetViewAsync(bool listView)
    {
        _listView = listView;
        return Task.CompletedTask;
    }

    private bool IsNavActive(string navId) => string.Equals(_activeNav, navId, StringComparison.Ordinal);

    // ── Formatting ──────────────────────────────────────────

    private static string DisplayTitle(BookmarkDto item) =>
        !string.IsNullOrWhiteSpace(item.Title) ? item.Title.Trim()
        : !string.IsNullOrEmpty(item.Url) ? item.Url
        : "(untitled)";

    private string FolderName(Guid? folderId)
    {
        if (folderId is not { } id)
        {
            return "No folder";
        }

        var folder = _folders.FirstOrDefault(candidate => candidate.Id == id);
        return folder is null || string.IsNullOrWhiteSpace(folder.Name) ? "No folder" : folder.Name;
    }

    private string CardMeta(BookmarkDto item)
    {
        var parts = new List<string> { FolderName(item.FolderId) };
        var tags = ParseTags(item.TagsJson);
        if (tags.Count > 0)
        {
            parts.Add(string.Join(", ", tags));
        }

        return string.Join(" · ", parts);
    }

    private string DetailMeta(BookmarkDto item)
    {
        var parts = new List<string> { FolderName(item.FolderId) };

        var tags = ParseTags(item.TagsJson);
        if (tags.Count > 0)
        {
            parts.Add("Tags: " + string.Join(", ", tags));
        }

        if (item.UpdatedAt != default)
        {
            parts.Add("Updated " + FormatDate(item.UpdatedAt));
        }

        return string.Join(" · ", parts);
    }

    private static string FormatDate(DateTime value)
    {
        if (value == default)
        {
            return string.Empty;
        }

        var local = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime()
            : value.ToLocalTime();

        return local.ToString("g", System.Globalization.CultureInfo.CurrentCulture);
    }

    /// <summary>Reads the tags from the stored JSON array string, tolerating old or malformed data.</summary>
    /// <param name="tagsJson">The stored value.</param>
    /// <returns>The tag list; empty when the value is not a JSON string array.</returns>
    private static IReadOnlyList<string> ParseTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return [];
        }

        // The edit form keeps a comma-separated value in the same field, so both shapes are accepted.
        if (!tagsJson.TrimStart().StartsWith('['))
        {
            return SplitTags(tagsJson);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(tagsJson);
            return parsed?.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> SplitTags(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? Optional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static BookmarkPreviewStatus PreviewStatusOf(BookmarkPreviewDto preview) => preview.Status;

    private void ReportError(Exception exception)
    {
        if (exception is ModuleApiException { IsUnauthorized: true })
        {
            _status = "Sign in to DotNetCloud to use this module.";
            return;
        }

        _status = exception.Message;
    }

    private enum Panel
    {
        Library,
        Detail,
        Edit,
        Folder,
        Import,
        Help,
    }
}
