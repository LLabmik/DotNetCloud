using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Photos.Events;
using DotNetCloud.Modules.Photos.Services;
using DotNetCloud.UI.Shared.Components.Dialogs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Photos.UI;

/// <summary>
/// Code-behind for the Photos page component.
/// Manages gallery, albums, timeline, map, lightbox, slideshow, and editing.
/// </summary>
public partial class PhotosPage : ComponentBase, IAsyncDisposable
{
    /// <summary>
    /// Optional photo ID to open in the lightbox when the page loads (deep-link from the home widget).
    /// </summary>
    [Parameter]
    public string? PhotoId { get; set; }

    /// <summary>
    /// Optional album ID to open when the page loads (deep-link from cross-module surfaces such as
    /// Files' shared-with-me tree). The album may be owned by or shared with the caller.
    /// </summary>
    [Parameter]
    public string? AlbumId { get; set; }

    private Guid? _lastHandledPhotoId;
    private Guid? _lastHandledAlbumId;

    // ── State ────────────────────────────────────────────────

    private enum Section { Gallery, Albums, Timeline, Favorites, Map, Shared, Settings }
    private enum ViewMode { Grid, List }

    private Section _section = Section.Gallery;
    private ViewMode _viewMode = ViewMode.Grid;
    private bool _sidebarCollapsed;
    private bool _loading = true;
    private string? _errorMessage;

    // Photos
    private List<PhotoDto> _currentPhotos = [];
    private List<PhotoDto>? _searchResults;
    private string _searchQuery = string.Empty;
    private int _page;
    private int _totalPhotos;
    private const int _pageSize = 60;
    private HashSet<Guid> _selectedPhotoIds = [];

    // Albums
    private List<AlbumDto> _albums = [];
    private Guid? _selectedAlbumId;
    private AlbumDto? _selectedAlbum;
    private bool _showAlbumDialog;
    private Guid? _editingAlbumId;
    private string _albumTitle = string.Empty;
    private string _albumDescription = string.Empty;

    // Timeline
    private SortedDictionary<string, List<PhotoDto>> _timelineGroups = new(Comparer<string>.Create((a, b) => b.CompareTo(a)));

    // Map
    private List<GeoClusterDto> _geoClusters = [];

    // Lightbox
    private PhotoDto? _lightboxPhoto;
    private int _lightboxIndex;
    private bool _showInfoPanel;
    private bool _showEditPanel;

    // Edit visual state (applied via CSS transform/filter)
    private readonly PhotoEditState _editState = new();
    private bool _editSaving;
    private string? _editSaveMessage;
    private bool _editSaveSuccess;

    // Slideshow
    private bool _slideshowActive;
    private bool _slideshowPaused;
    private bool _slideshowTransitioning;
    private int _slideshowIndex;
    private List<PhotoDto> _slideshowPhotos = [];
    private System.Threading.Timer? _slideshowTimer;

    // Share (unified DncShareDialog)
    private bool _showShareDialog;
    private string _shareDialogItemName = string.Empty;
    private Guid? _shareDialogPhotoId;
    private Guid? _shareDialogAlbumId;
    private List<DncShareEntry> _shareDialogShares = [];
    private bool _isLoadingShareDialogShares;

    /// <summary>Permission choices for photo/album shares (ReadOnly / Download).</summary>
    private static readonly IReadOnlyList<DncSharePermissionOption> PhotoPermissionOptions =
    [
        new() { Value = "Read", Label = "View only" },
        new() { Value = "ReadWrite", Label = "Can download" },
    ];

    // Auth
    private CallerContext? _caller;

    // Library Settings
    private List<MediaLibrarySource> _librarySources = [];
    private bool _settingsSaving;
    private bool _settingsScanning;
    private bool _settingsResetting;
    private bool _showResetConfirm;
    private string? _settingsError;
    private string? _settingsSuccess;
    private MediaScanResult? _scanResult;

    // Source removal (confirmation + library prune)
    private MediaLibrarySource? _sourceRemovePending;
    private int _sourceRemoveCount;
    private bool _removingSource;

    // Directory Browser
    private bool _showDirBrowser;
    private Guid? _dirBrowserFolderId;
    private List<(Guid Id, string Name)> _dirBrowserFolders = [];
    private List<(Guid Id, string Name)> _dirBrowserBreadcrumbs = [];
    private string? _dirBrowserError;

    // ── Lifecycle ────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var collapsed = await Js.InvokeAsync<string>("localStorage.getItem", new object?[] { "dotnetcloud.sidebar:photos" });
            if (bool.TryParse(collapsed ?? "false", out var parsed))
            {
                _sidebarCollapsed = parsed;
            }

            _caller = await GetCallerContextAsync();
            await LoadLibraryPathAsync();
            await LoadCurrentSectionAsync();
            await HandlePhotoDeepLinkAsync();
            await HandleAlbumDeepLinkAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize Photos page");
            _errorMessage = "Failed to load photos. Please try again.";
            _loading = false;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        // Handle photoId/albumId changes when already on the page (same-page navigation).
        await HandlePhotoDeepLinkAsync();
        await HandleAlbumDeepLinkAsync();
    }

    /// <summary>
    /// Opens the photo referenced by the <c>PhotoId</c> deep-link parameter in the lightbox.
    /// Prefers a photo already present in the current gallery; otherwise the photo is fetched
    /// individually. Repeat handling of the same id is guarded, and failures never break the page.
    /// </summary>
    private async Task HandlePhotoDeepLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(PhotoId) || !Guid.TryParse(PhotoId, out var photoId) || photoId == _lastHandledPhotoId)
        {
            return;
        }

        _lastHandledPhotoId = photoId;
        try
        {
            var photo = _currentPhotos.FirstOrDefault(p => p.Id == photoId);
            if (photo is null)
            {
                if (_caller is null)
                {
                    return;
                }

                photo = await PhotoService.GetPhotoAsync(photoId, _caller);
                if (photo is null)
                {
                    return;
                }

                // The photo isn't part of the current gallery — show it on its own so the
                // lightbox index/prev/next logic stays consistent.
                _currentPhotos = [photo];
            }

            await OpenLightboxAsync(photo);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to open deep-linked photo {PhotoId}", photoId);
        }
    }

    /// <summary>
    /// Opens the album referenced by the <c>AlbumId</c> deep-link parameter. Switches to the
    /// Albums section and selects the album (the album may be shared with the caller, so it is
    /// resolved by id rather than requiring it in the owned-albums list). Repeat handling of the
    /// same id is guarded, and failures never break the page.
    /// </summary>
    private async Task HandleAlbumDeepLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(AlbumId) || !Guid.TryParse(AlbumId, out var albumId) || albumId == _lastHandledAlbumId)
        {
            return;
        }

        _lastHandledAlbumId = albumId;
        try
        {
            if (_caller is null)
            {
                return;
            }

            // Verify the caller can actually open this album (owner or shared) before switching.
            var album = await AlbumService.GetAlbumAsync(albumId, _caller);
            if (album is null)
            {
                return;
            }

            _section = Section.Albums;
            _searchResults = null;
            _searchQuery = string.Empty;
            _page = 0;
            _selectedAlbumId = albumId;
            _selectedAlbum = null;
            await LoadCurrentSectionAsync();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to open deep-linked album {AlbumId}", albumId);
        }
    }

    // ── Navigation ───────────────────────────────────────────

    private async void SwitchSection(Section section)
    {
        _section = section;
        _searchResults = null;
        _searchQuery = string.Empty;
        _selectedAlbumId = null;
        _selectedAlbum = null;
        _page = 0;
        await LoadCurrentSectionAsync();
        StateHasChanged();
    }

    private async Task LoadCurrentSectionAsync()
    {
        if (_caller is null)
            return;
        _loading = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            switch (_section)
            {
                case Section.Gallery:
                    var photos = await PhotoService.ListPhotosAsync(_caller, _page * _pageSize, _pageSize);
                    _currentPhotos = [.. photos];
                    // Estimate total (if we have full page, there might be more)
                    _totalPhotos = photos.Count == _pageSize ? (_page + 2) * _pageSize : _page * _pageSize + photos.Count;
                    break;

                case Section.Albums:
                    _albums = [.. await AlbumService.ListAlbumsAsync(_caller)];
                    if (_selectedAlbumId.HasValue)
                    {
                        _selectedAlbum = await AlbumService.GetAlbumAsync(_selectedAlbumId.Value, _caller);
                        _currentPhotos = [.. await AlbumService.GetAlbumPhotosAsync(_selectedAlbumId.Value, _caller)];
                    }
                    break;

                case Section.Timeline:
                    var from = DateTime.UtcNow.AddYears(-5);
                    var to = DateTime.UtcNow;
                    var timelinePhotos = await PhotoService.GetTimelineAsync(_caller, from, to);
                    _timelineGroups.Clear();
                    foreach (var p in timelinePhotos)
                    {
                        var key = p.TakenAt.ToString("yyyy-MM-dd");
                        if (!_timelineGroups.ContainsKey(key))
                            _timelineGroups[key] = [];
                        _timelineGroups[key].Add(p);
                    }
                    break;

                case Section.Favorites:
                    _currentPhotos = [.. await PhotoService.GetFavoritesAsync(_caller)];
                    break;

                case Section.Map:
                    _geoClusters = [.. await GeoService.GetGeoClustersAsync(_caller.UserId)];
                    break;

                case Section.Shared:
                    var shares = await ShareService.GetSharedWithMeAsync(_caller);
                    // Load shared photo details
                    _currentPhotos = [];
                    foreach (var share in shares.Where(s => s.PhotoId.HasValue))
                    {
                        var photo = await PhotoService.GetPhotoAsync(share.PhotoId!.Value, _caller);
                        if (photo is not null)
                            _currentPhotos.Add(photo);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load section {Section}", _section);
            _errorMessage = $"Failed to load {_section}. Please try again.";
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    // ── Search ───────────────────────────────────────────────

    private async Task HandleSearchKeyUp(KeyboardEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            _searchResults = null;
            return;
        }

        if (e.Key == "Enter" && _caller is not null)
        {
            _searchResults = [.. await PhotoService.SearchAsync(_caller, _searchQuery)];
            StateHasChanged();
        }
    }

    // ── Photo Selection & Actions ────────────────────────────

    private async Task HandlePhotoClick(PhotoDto photo, MouseEventArgs e)
    {
        if (e.CtrlKey || e.MetaKey)
        {
            // Ctrl/Cmd+Click: toggle multi-selection
            if (!_selectedPhotoIds.Add(photo.Id))
                _selectedPhotoIds.Remove(photo.Id);
            StateHasChanged();
        }
        else
        {
            // Single click: open lightbox
            await OpenLightboxAsync(photo);
        }
    }

    private async Task ToggleFavoriteAsync(PhotoDto photo)
    {
        if (_caller is null)
            return;
        try
        {
            var updated = await PhotoService.ToggleFavoriteAsync(photo.Id, _caller);
            // Update in-place
            var idx = _currentPhotos.FindIndex(p => p.Id == photo.Id);
            if (idx >= 0)
                _currentPhotos[idx] = updated;
            if (_lightboxPhoto?.Id == photo.Id)
                _lightboxPhoto = updated;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to toggle favorite for photo {PhotoId}", photo.Id);
        }
    }

    private async Task DeletePhotoAsync(PhotoDto photo)
    {
        if (_caller is null)
            return;
        try
        {
            await PhotoService.DeletePhotoAsync(photo.Id, _caller);
            _currentPhotos.RemoveAll(p => p.Id == photo.Id);
            if (_lightboxPhoto?.Id == photo.Id)
                CloseLightbox();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete photo {PhotoId}", photo.Id);
        }
    }

    // ── Albums ───────────────────────────────────────────────

    private async Task SelectAlbumAsync(Guid albumId)
    {
        _selectedAlbumId = albumId;
        _selectedAlbum = null;
        await LoadCurrentSectionAsync();
    }

    private void BeginCreateAlbum()
    {
        _editingAlbumId = null;
        _albumTitle = string.Empty;
        _albumDescription = string.Empty;
        _showAlbumDialog = true;
    }

    private async Task SaveAlbumAsync()
    {
        if (_caller is null || string.IsNullOrWhiteSpace(_albumTitle))
            return;
        try
        {
            if (_editingAlbumId.HasValue)
            {
                await AlbumService.UpdateAlbumAsync(_editingAlbumId.Value,
                    new UpdateAlbumDto { Title = _albumTitle, Description = _albumDescription }, _caller);
            }
            else
            {
                await AlbumService.CreateAlbumAsync(
                    new CreateAlbumDto { Title = _albumTitle, Description = _albumDescription }, _caller);
            }
            _showAlbumDialog = false;
            _albums = [.. await AlbumService.ListAlbumsAsync(_caller)];
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save album");
        }
    }

    // ── Lightbox ─────────────────────────────────────────────

    private async Task OpenLightboxAsync(PhotoDto photo)
    {
        _lightboxPhoto = photo;
        _lightboxIndex = _currentPhotos.IndexOf(photo);
        _showInfoPanel = false;
        _showEditPanel = false;
        await RebuildEditStateFromStackAsync();
    }

    private void CloseLightbox()
    {
        _lightboxPhoto = null;
        _showInfoPanel = false;
        _showEditPanel = false;
        _editState.Reset();
    }

    private async Task LightboxPrevAsync()
    {
        if (_lightboxIndex > 0)
        {
            _lightboxIndex--;
            _lightboxPhoto = _currentPhotos[_lightboxIndex];
            await RebuildEditStateFromStackAsync();
        }
    }

    private async Task LightboxNextAsync()
    {
        if (_lightboxIndex < _currentPhotos.Count - 1)
        {
            _lightboxIndex++;
            _lightboxPhoto = _currentPhotos[_lightboxIndex];
            await RebuildEditStateFromStackAsync();
        }
    }

    private bool CanLightboxPrev => _lightboxIndex > 0;
    private bool CanLightboxNext => _lightboxIndex < _currentPhotos.Count - 1;

    private async Task HandleLightboxKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "ArrowLeft":
                await LightboxPrevAsync();
                break;
            case "ArrowRight":
                await LightboxNextAsync();
                break;
            case "Escape":
                CloseLightbox();
                break;
        }
    }

    // ── Editing ──────────────────────────────────────────────

    private async Task ApplyEditAsync(PhotoEditType editType, int value)
    {
        if (_caller is null || _lightboxPhoto is null)
            return;
        try
        {
            var operation = new PhotoEditOperationDto
            {
                OperationType = editType,
                Parameters = new Dictionary<string, string> { ["value"] = value.ToString() }
            };
            await EditService.ApplyEditAsync(_lightboxPhoto.Id, operation, _caller);
            _editState.Apply(editType, value);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to apply edit to photo {PhotoId}", _lightboxPhoto.Id);
        }
    }

    private async Task UndoEditAsync()
    {
        if (_caller is null || _lightboxPhoto is null)
            return;
        try
        {
            await EditService.UndoLastEditAsync(_lightboxPhoto.Id, _caller);
            await RebuildEditStateFromStackAsync();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to undo edit");
        }
    }

    private async Task RevertAllEditsAsync()
    {
        if (_caller is null || _lightboxPhoto is null)
            return;
        try
        {
            await EditService.RevertAllAsync(_lightboxPhoto.Id, _caller);
            _editState.Reset();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to revert edits");
        }
    }

    private async Task RebuildEditStateFromStackAsync()
    {
        _editState.Reset();
        if (_lightboxPhoto is null)
            return;
        try
        {
            var stack = await EditService.GetEditStackAsync(_lightboxPhoto.Id);
            _editState.Rebuild(stack);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load edit stack for photo {PhotoId}", _lightboxPhoto.Id);
        }
    }

    private string GetEditImageStyle() => _editState.GetImageStyle();

    private async Task SaveEditsToThumbnailsAsync()
    {
        if (_caller is null || _lightboxPhoto is null)
            return;
        _editSaving = true;
        _editSaveMessage = null;
        StateHasChanged();
        try
        {
            var success = await ThumbnailService.SaveEditsAsync(_lightboxPhoto.Id);
            _editSaveSuccess = success;
            _editSaveMessage = success ? "Edits saved to thumbnails." : "Failed to save edits.";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save edits for photo {PhotoId}", _lightboxPhoto.Id);
            _editSaveSuccess = false;
            _editSaveMessage = "Error saving edits.";
        }
        finally
        {
            _editSaving = false;
            StateHasChanged();
        }
    }

    // ── Slideshow ────────────────────────────────────────────

    private void BeginSlideshow()
    {
        _slideshowPhotos = _currentPhotos.Count > 0 ? [.. _currentPhotos] : [];
        if (_slideshowPhotos.Count == 0)
            return;

        _slideshowIndex = _lightboxPhoto is not null ? _currentPhotos.IndexOf(_lightboxPhoto) : 0;
        if (_slideshowIndex < 0)
            _slideshowIndex = 0;

        _slideshowActive = true;
        _slideshowPaused = false;
        CloseLightbox();
        StartSlideshowTimer();
        StateHasChanged();
    }

    private void StartSlideshowTimer()
    {
        _slideshowTimer?.Dispose();
        _slideshowTimer = new System.Threading.Timer(_ =>
        {
            if (_slideshowPaused)
                return;
            InvokeAsync(() =>
            {
                _slideshowTransitioning = true;
                StateHasChanged();
                Task.Delay(300).ContinueWith(_ => InvokeAsync(() =>
                {
                    if (_slideshowIndex < _slideshowPhotos.Count - 1)
                        _slideshowIndex++;
                    else
                        _slideshowIndex = 0;
                    _slideshowTransitioning = false;
                    StateHasChanged();
                }));
            });
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    private void StopSlideshow()
    {
        _slideshowActive = false;
        _slideshowTimer?.Dispose();
        _slideshowTimer = null;
    }

    private void ToggleSlideshowPause() => _slideshowPaused = !_slideshowPaused;

    private void SlideshowPrev()
    {
        if (_slideshowIndex > 0)
            _slideshowIndex--;
    }

    private void SlideshowNext()
    {
        if (_slideshowIndex < _slideshowPhotos.Count - 1)
            _slideshowIndex++;
    }

    private void HandleSlideshowKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "ArrowLeft":
                SlideshowPrev();
                break;
            case "ArrowRight":
                SlideshowNext();
                break;
            case " ":
                ToggleSlideshowPause();
                break;
            case "Escape":
                StopSlideshow();
                break;
        }
    }

    // ── Sharing ──────────────────────────────────────────────

    /// <summary>Opens the share dialog for a photo owned by the caller.</summary>
    private async Task BeginShare(PhotoDto photo)
    {
        _shareDialogPhotoId = photo.Id;
        _shareDialogAlbumId = null;
        _shareDialogItemName = photo.FileName;
        _showShareDialog = true;
        _shareDialogShares = [];
        _isLoadingShareDialogShares = true;
        StateHasChanged();

        try
        {
            var shares = await ShareService.GetPhotoSharesAsync(photo.Id, _caller!);
            _shareDialogShares = await MapShareDtosToEntriesAsync(shares);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load shares for photo {PhotoId}", photo.Id);
            _shareDialogShares = [];
        }
        finally
        {
            _isLoadingShareDialogShares = false;
        }
    }

    /// <summary>Opens the share dialog for the selected album (owner only).</summary>
    private async Task BeginAlbumShare()
    {
        if (_caller is null || _selectedAlbum is null)
            return;

        _shareDialogAlbumId = _selectedAlbum.Id;
        _shareDialogPhotoId = null;
        _shareDialogItemName = _selectedAlbum.Title;
        _showShareDialog = true;
        _shareDialogShares = [];
        _isLoadingShareDialogShares = true;
        StateHasChanged();

        try
        {
            var shares = await ShareService.GetAlbumSharesAsync(_selectedAlbum.Id, _caller);
            _shareDialogShares = await MapShareDtosToEntriesAsync(shares);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load shares for album {AlbumId}", _selectedAlbum.Id);
            _shareDialogShares = [];
        }
        finally
        {
            _isLoadingShareDialogShares = false;
        }
    }

    private void CloseShareDialog()
    {
        _showShareDialog = false;
    }

    private async Task HandleShareCreatedAsync(DncShareCreatedEventArgs args)
    {
        if (_caller is null)
            return;

        var perm = args.Permission == "ReadWrite" ? PhotoSharePermission.Download : PhotoSharePermission.ReadOnly;
        Guid? targetUserId = args.ShareType == "Team" ? null : args.TargetId;
        Guid? targetTeamId = args.ShareType == "Team" ? args.TargetId : null;

        if (_shareDialogPhotoId is { } photoId)
        {
            await ShareService.SharePhotoAsync(photoId, targetUserId, targetTeamId, perm, _caller);
        }
        else if (_shareDialogAlbumId is { } albumId)
        {
            await ShareService.ShareAlbumAsync(albumId, targetUserId, targetTeamId, perm, _caller);
        }
        else
        {
            return;
        }

        await ReloadShareDialogEntriesAsync();
    }

    private async Task HandleShareRemovedAsync(Guid shareId)
    {
        await ShareService.RemoveShareAsync(shareId, _caller!);
        _shareDialogShares = [.. _shareDialogShares.Where(e => e.ShareId != shareId)];
    }

    /// <summary>Reloads the current target's shares and re-supplies the dialog list.</summary>
    private async Task ReloadShareDialogEntriesAsync()
    {
        if (_caller is null)
            return;

        IReadOnlyList<PhotoShareDto> shares;
        if (_shareDialogPhotoId is { } photoId)
        {
            shares = await ShareService.GetPhotoSharesAsync(photoId, _caller);
        }
        else if (_shareDialogAlbumId is { } albumId)
        {
            shares = await ShareService.GetAlbumSharesAsync(albumId, _caller);
        }
        else
        {
            return;
        }

        _shareDialogShares = await MapShareDtosToEntriesAsync(shares);
        StateHasChanged();
    }

    /// <summary>Maps server shares to dialog entries, resolving recipient display names.</summary>
    private async Task<List<DncShareEntry>> MapShareDtosToEntriesAsync(IReadOnlyList<PhotoShareDto> shares)
    {
        var userNames = await ResolveUserNamesAsync(shares);
        var entries = new List<DncShareEntry>(shares.Count);

        foreach (var share in shares)
        {
            var isTeam = share.SharedWithTeamId is not null;
            string recipientName;

            if (isTeam)
            {
                recipientName = await ResolveTeamNameAsync(share.SharedWithTeamId!.Value)
                    ?? $"{share.SharedWithTeamId.Value:N}"[..8];
            }
            else if (share.SharedWithUserId is { } userId && userNames.TryGetValue(userId, out var name))
            {
                recipientName = name;
            }
            else
            {
                recipientName = share.SharedWithUserId is { } fallback ? $"{fallback:N}"[..8] : "Unknown";
            }

            entries.Add(new DncShareEntry
            {
                ShareId = share.Id,
                RecipientName = recipientName,
                RecipientType = isTeam ? "Team" : "User",
                Permission = share.Permission is PhotoSharePermission.Download or PhotoSharePermission.Contribute
                    ? "ReadWrite"
                    : "Read",
                CreatedAt = share.CreatedAt
            });
        }

        return entries;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveUserNamesAsync(IReadOnlyList<PhotoShareDto> shares)
    {
        var ids = shares
            .Where(s => s.SharedWithTeamId is null && s.SharedWithUserId is not null)
            .Select(s => s.SharedWithUserId!.Value)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        try
        {
            return await UserDirectory.GetDisplayNamesAsync(ids);
        }
        catch
        {
            return new Dictionary<Guid, string>();
        }
    }

    private async Task<string?> ResolveTeamNameAsync(Guid teamId)
    {
        try
        {
            var team = await TeamDirectory.GetTeamAsync(teamId);
            return team?.Name;
        }
        catch
        {
            return null;
        }
    }

    // ── Pagination ───────────────────────────────────────────

    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)_totalPhotos / _pageSize));

    private async Task PrevPage()
    {
        if (_page > 0)
        { _page--; await LoadCurrentSectionAsync(); }
    }

    private async Task NextPage()
    {
        if (_page < TotalPages - 1)
        { _page++; await LoadCurrentSectionAsync(); }
    }

    // ── Helpers ──────────────────────────────────────────────

    private string GetThumbnailUrl(Guid photoId, string size)
        => $"/api/v1/photos/{photoId}/thumbnail?size={size}";

    private string GetPhotoDownloadUrl(Guid photoId)
        => $"/api/v1/photos/{photoId}/download";

    private static string FormatDate(DateTime dt)
        => dt.ToString("MMM d, yyyy");

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    private string GetSectionTitle() => _section switch
    {
        Section.Gallery => "All Photos",
        Section.Albums => "Albums",
        Section.Timeline => "Timeline",
        Section.Favorites => "Favorites",
        Section.Map => "Map",
        Section.Shared => "Shared with Me",
        _ => "Photos"
    };

    private string GetEmptyMessage() => _section switch
    {
        Section.Favorites => "No favorite photos yet",
        Section.Shared => "No photos shared with you yet",
        _ => "No photos yet"
    };

    private void HandleImageError() { /* graceful fallback handled in CSS */ }

    private async Task<CallerContext> GetCallerContextAsync()
    {
        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = state.User;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
            throw new InvalidOperationException("Authenticated user id claim is missing or invalid.");

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return new CallerContext(userId, roles, CallerType.User);
    }

    // ── Library Settings Methods ─────────────────────────────

    private async Task LoadLibraryPathAsync()
    {
        if (_caller is null)
            return;
        try
        {
            _librarySources = (await MediaLibrarySourceSettings.LoadSourcesAsync(UserSettingsService, _caller.UserId, "photos")).ToList();
        }
        catch { /* ignore load failures */ }
    }

    private Task SaveLibraryPathAsync()
        => PersistLibrarySourcesAsync(showSuccessMessage: true);

    private async Task PersistLibrarySourcesAsync(bool showSuccessMessage)
    {
        if (_caller is null)
            return;
        _settingsSaving = true;
        _settingsError = null;
        if (showSuccessMessage)
        {
            _settingsSuccess = null;
        }

        try
        {
            _librarySources = MediaLibrarySourceSettings.Normalize(_librarySources).ToList();
            await MediaLibrarySourceSettings.SaveSourcesAsync(
                UserSettingsService,
                _caller.UserId,
                "photos",
                _librarySources,
                "Photos library scan sources");

            if (showSuccessMessage)
            {
                _settingsSuccess = "Sources saved.";
            }
        }
        catch (Exception ex)
        {
            _settingsError = $"Save failed: {ex.Message}";
        }
        finally
        {
            _settingsSaving = false;
        }
    }

    private async Task ScanLibraryAsync()
    {
        if (_caller is null || _librarySources.Count == 0)
            return;
        await PersistLibrarySourcesAsync(showSuccessMessage: false);
        if (_settingsError is not null)
            return;

        _settingsScanning = true;
        _settingsError = null;
        _settingsSuccess = null;
        _scanResult = null;
        StateHasChanged();
        try
        {
            _scanResult = await MediaLibraryScanner.ScanSourcesAsync(_librarySources, _caller.UserId, "Photos");
            _settingsSuccess = $"Scan complete: {_scanResult.Imported} imported, {_scanResult.Skipped} already up to date.";
        }
        catch (Exception ex)
        {
            _settingsError = $"Scan failed: {ex.Message}";
        }
        finally
        {
            _settingsScanning = false;
        }
    }

    private async Task ResetCollectionAsync()
    {
        if (_caller is null)
            return;
        _settingsResetting = true;
        _settingsError = null;
        _settingsSuccess = null;
        _scanResult = null;
        StateHasChanged();
        try
        {
            await PhotoIndexingCallback.ResetCollectionAsync(_caller.UserId);
            _settingsSuccess = "Photo collection reset. Click Scan Now to rebuild your library.";
            _showResetConfirm = false;

            // Clear displayed data
            _currentPhotos.Clear();
            _searchResults = null;
            _albums.Clear();
            _timelineGroups.Clear();
            _geoClusters.Clear();
            _selectedPhotoIds.Clear();
            _selectedAlbum = null;
            _selectedAlbumId = null;
            _lightboxPhoto = null;
            if (_slideshowActive)
            {
                _slideshowActive = false;
                _slideshowTimer?.Dispose();
                _slideshowTimer = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to reset photo collection");
            _settingsError = $"Reset failed: {ex.Message}";
        }
        finally
        {
            _settingsResetting = false;
        }
    }

    // ── Directory Browser Methods ────────────────────────────

    private async Task OpenDirectoryBrowser()
    {
        _dirBrowserError = null;
        _dirBrowserFolderId = null;
        _dirBrowserBreadcrumbs.Clear();
        await LoadDirBrowserFoldersAsync();
        _showDirBrowser = true;
    }

    private void HideDirectoryBrowser() => _showDirBrowser = false;

    private async Task DirBrowserNavigateToRoot()
    {
        _dirBrowserFolderId = null;
        _dirBrowserBreadcrumbs.Clear();
        await LoadDirBrowserFoldersAsync();
    }

    private async Task LoadDirBrowserFoldersAsync()
    {
        _dirBrowserError = null;
        _dirBrowserFolders.Clear();
        try
        {
            if (_caller is null)
                return;
            var nodes = _dirBrowserFolderId.HasValue
                ? await FileService.ListChildrenAsync(_dirBrowserFolderId.Value, _caller)
                : await FileService.ListRootAsync(_caller);

            _dirBrowserFolders = nodes
                .Where(n => n.NodeType == "Folder")
                .OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .Select(n => (n.Id, n.Name))
                .ToList();
        }
        catch (Exception ex)
        {
            _dirBrowserError = ex.Message;
        }
    }

    private async Task DirBrowserNavigate(Guid folderId, string folderName)
    {
        _dirBrowserBreadcrumbs.Add((folderId, folderName));
        _dirBrowserFolderId = folderId;
        await LoadDirBrowserFoldersAsync();
    }

    private async Task DirBrowserGoUp()
    {
        if (_dirBrowserBreadcrumbs.Count > 0)
        {
            _dirBrowserBreadcrumbs.RemoveAt(_dirBrowserBreadcrumbs.Count - 1);
            _dirBrowserFolderId = _dirBrowserBreadcrumbs.Count > 0
                ? _dirBrowserBreadcrumbs[^1].Id
                : null;
            await LoadDirBrowserFoldersAsync();
        }
    }

    private async Task DirBrowserNavigateToCrumb(int index)
    {
        if (index < _dirBrowserBreadcrumbs.Count - 1)
        {
            _dirBrowserBreadcrumbs.RemoveRange(index + 1, _dirBrowserBreadcrumbs.Count - index - 1);
        }
        _dirBrowserFolderId = _dirBrowserBreadcrumbs[index].Id;
        await LoadDirBrowserFoldersAsync();
    }

    private string GetDirBrowserPath()
    {
        if (_dirBrowserBreadcrumbs.Count == 0)
            return "/";
        return "/" + string.Join('/', _dirBrowserBreadcrumbs.Select(b => b.Name));
    }

    private async Task ConfirmDirectoryBrowserAsync()
    {
        _dirBrowserError = null;

        var source = await CreateLibrarySourceFromBrowserAsync();
        if (source is null)
        {
            return;
        }

        var sourceKey = MediaLibrarySourceSettings.GetSourceKey(source);
        if (_librarySources.Any(existing => string.Equals(MediaLibrarySourceSettings.GetSourceKey(existing), sourceKey, StringComparison.OrdinalIgnoreCase)))
        {
            _dirBrowserError = "This folder is already selected.";
            return;
        }

        _librarySources.Add(source);
        _librarySources = MediaLibrarySourceSettings.Normalize(_librarySources).ToList();
        _settingsError = null;
        _settingsSuccess = null;
        _showDirBrowser = false;

        // Persist immediately so the source is not lost if the user navigates away without
        // pressing Scan Now / Save Sources.
        await PersistLibrarySourcesAsync(showSuccessMessage: false);
        if (_settingsError is null)
        {
            _settingsSuccess = "Source added.";
        }
    }

    /// <summary>
    /// Entry point for the source "Remove" button. When removing a source can orphan library items
    /// (files reachable only through that folder), a confirmation modal is shown with the affected
    /// item count before anything destructive happens.
    /// </summary>
    private async Task RequestRemoveLibrarySourceAsync(MediaLibrarySource source)
    {
        if (_caller is null)
            return;

        _removingSource = true;
        _settingsError = null;
        _settingsSuccess = null;
        try
        {
            var remainingSources = _librarySources
                .Where(existing => !string.Equals(
                    MediaLibrarySourceSettings.GetSourceKey(existing),
                    MediaLibrarySourceSettings.GetSourceKey(source),
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            var count = await MediaLibraryScanner.CountLibraryItemsNotInSourcesAsync(remainingSources, _caller.UserId, "Photos");
            if (count == 0)
            {
                // Nothing indexed would be lost — remove the source without a destructive prompt.
                await ApplyLibrarySourceRemovalAsync(source, remainingSources, attemptPrune: false);
            }
            else
            {
                _sourceRemovePending = source;
                _sourceRemoveCount = count;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Photos source removal preview failed for user {UserId}", _caller.UserId);
            // Files module unreachable — fall back to a generic confirmation so removal still works.
            _sourceRemovePending = source;
            _sourceRemoveCount = -1;
            StateHasChanged();
        }
        finally
        {
            _removingSource = false;
        }
    }

    /// <summary>Confirms the pending source removal and prunes orphaned library items.</summary>
    private async Task ConfirmRemoveLibrarySourceAsync()
    {
        if (_sourceRemovePending is null || _caller is null)
            return;

        var source = _sourceRemovePending;
        var remainingSources = _librarySources
            .Where(existing => !string.Equals(
                MediaLibrarySourceSettings.GetSourceKey(existing),
                MediaLibrarySourceSettings.GetSourceKey(source),
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        _sourceRemovePending = null;
        _sourceRemoveCount = 0;
        _settingsError = null;
        _settingsSuccess = null;
        _removingSource = true;
        try
        {
            await ApplyLibrarySourceRemovalAsync(source, remainingSources, attemptPrune: true);
        }
        finally
        {
            _removingSource = false;
        }
    }

    /// <summary>Cancels the pending source removal confirmation.</summary>
    private void CancelRemoveLibrarySource()
    {
        _sourceRemovePending = null;
        _sourceRemoveCount = 0;
    }

    /// <summary>
    /// Applies the source removal: removes the source from the persisted list and, when
    /// <paramref name="attemptPrune"/> is set, immediately removes library items whose files are only
    /// reachable through the removed source.
    /// </summary>
    private async Task ApplyLibrarySourceRemovalAsync(
        MediaLibrarySource source,
        IReadOnlyCollection<MediaLibrarySource> remainingSources,
        bool attemptPrune)
    {
        if (_caller is null)
            return;

        _librarySources = remainingSources.ToList();

        // Persist immediately so the removal is not lost if the user navigates away.
        await PersistLibrarySourcesAsync(showSuccessMessage: false);
        if (_settingsError is not null)
            return;

        if (!attemptPrune)
        {
            _settingsSuccess = "Source removed.";
            return;
        }

        try
        {
            var removed = await MediaLibraryScanner.RemoveLibraryItemsNotInSourcesAsync(remainingSources, _caller.UserId, "Photos");
            _settingsSuccess = removed > 0
                ? $"Source removed. {removed} item(s) removed from your library."
                : "Source removed.";
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Photos library prune after source removal failed for user {UserId}", _caller.UserId);
            _settingsSuccess = "Source removed, but some items remain until the next scan (cleanup could not run just now).";
        }
    }

    private async Task<MediaLibrarySource?> CreateLibrarySourceFromBrowserAsync()
    {
        if (_caller is null)
        {
            return null;
        }

        var displayPath = GetDirBrowserPath();
        if (!_dirBrowserFolderId.HasValue)
        {
            return new MediaLibrarySource
            {
                SourceKind = MediaLibrarySourceKind.OwnedFileNode,
                FolderId = null,
                DisplayPath = displayPath,
                DisplayName = "Home",
                Enabled = true,
            };
        }

        var node = await FileService.GetNodeAsync(_dirBrowserFolderId.Value, _caller);
        if (node is null)
        {
            _dirBrowserError = "The selected folder is no longer available.";
            return null;
        }

        if (!string.Equals(node.NodeType, "Folder", StringComparison.OrdinalIgnoreCase))
        {
            _dirBrowserError = "Select a folder source.";
            return null;
        }

        if (!node.IsVirtual)
        {
            return new MediaLibrarySource
            {
                SourceKind = MediaLibrarySourceKind.OwnedFileNode,
                FolderId = node.Id,
                DisplayPath = displayPath,
                DisplayName = node.Name,
                Enabled = true,
            };
        }

        if (string.Equals(node.VirtualSourceKind, "AdminSharedFolder", StringComparison.OrdinalIgnoreCase) && node.VirtualSourceId.HasValue)
        {
            return new MediaLibrarySource
            {
                SourceKind = MediaLibrarySourceKind.SharedMount,
                SharedFolderId = node.VirtualSourceId.Value,
                RelativePath = node.VirtualRelativePath,
                DisplayPath = displayPath,
                DisplayName = node.Name,
                Enabled = true,
            };
        }

        _dirBrowserError = "Only folders from your library or _DotNetCloud admin shared folders can be added.";
        return null;
    }

    private static string GetLibrarySourceKindLabel(MediaLibrarySource source)
        => source.SourceKind == MediaLibrarySourceKind.SharedMount ? "Shared" : "Owned";

    private async Task ToggleSidebar()
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        await SaveSidebarCollapsedStateAsync();
        StateHasChanged();
    }

    private async Task SaveSidebarCollapsedStateAsync()
    {
        try
        {
            await Js.InvokeAsync<object?>("localStorage.setItem", new object?[] { "dotnetcloud.sidebar:photos", _sidebarCollapsed.ToString().ToLowerInvariant() });
        }
        catch { /* localStorage unavailable */ }
    }

    public async ValueTask DisposeAsync()
    {
        _slideshowTimer?.Dispose();
        _slideshowTimer = null;
        GC.SuppressFinalize(this);
        await ValueTask.CompletedTask;
    }
}
