using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Photos.Services;
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
    // ── State ────────────────────────────────────────────────

    private enum Section { Gallery, Albums, Timeline, Favorites, Map, Shared }
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

    // Slideshow
    private bool _slideshowActive;
    private bool _slideshowPaused;
    private bool _slideshowTransitioning;
    private int _slideshowIndex;
    private List<PhotoDto> _slideshowPhotos = [];
    private System.Threading.Timer? _slideshowTimer;

    // Share
    private bool _showShareDialog;
    private Guid? _shareTargetPhotoId;
    private string _shareUserId = string.Empty;
    private PhotoSharePermission _sharePermission = PhotoSharePermission.ReadOnly;

    // Auth
    private CallerContext? _caller;

    // ── Lifecycle ────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _caller = await GetCallerContextAsync();
            await LoadCurrentSectionAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize Photos page");
            _errorMessage = "Failed to load photos. Please try again.";
            _loading = false;
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
        if (_caller is null) return;
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
                        if (photo is not null) _currentPhotos.Add(photo);
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

    private void HandlePhotoClick(PhotoDto photo, MouseEventArgs e)
    {
        if (e.CtrlKey || e.MetaKey)
        {
            if (!_selectedPhotoIds.Add(photo.Id))
                _selectedPhotoIds.Remove(photo.Id);
        }
        else
        {
            _selectedPhotoIds.Clear();
            _selectedPhotoIds.Add(photo.Id);
        }
    }

    private async Task ToggleFavoriteAsync(PhotoDto photo)
    {
        if (_caller is null) return;
        try
        {
            var updated = await PhotoService.ToggleFavoriteAsync(photo.Id, _caller);
            // Update in-place
            var idx = _currentPhotos.FindIndex(p => p.Id == photo.Id);
            if (idx >= 0) _currentPhotos[idx] = updated;
            if (_lightboxPhoto?.Id == photo.Id) _lightboxPhoto = updated;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to toggle favorite for photo {PhotoId}", photo.Id);
        }
    }

    private async Task DeletePhotoAsync(PhotoDto photo)
    {
        if (_caller is null) return;
        try
        {
            await PhotoService.DeletePhotoAsync(photo.Id, _caller);
            _currentPhotos.RemoveAll(p => p.Id == photo.Id);
            if (_lightboxPhoto?.Id == photo.Id) CloseLightbox();
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
        if (_caller is null || string.IsNullOrWhiteSpace(_albumTitle)) return;
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

    private void OpenLightbox(PhotoDto photo)
    {
        _lightboxPhoto = photo;
        _lightboxIndex = _currentPhotos.IndexOf(photo);
        _showInfoPanel = false;
        _showEditPanel = false;
    }

    private void CloseLightbox()
    {
        _lightboxPhoto = null;
        _showInfoPanel = false;
        _showEditPanel = false;
    }

    private void LightboxPrev()
    {
        if (_lightboxIndex > 0)
        {
            _lightboxIndex--;
            _lightboxPhoto = _currentPhotos[_lightboxIndex];
        }
    }

    private void LightboxNext()
    {
        if (_lightboxIndex < _currentPhotos.Count - 1)
        {
            _lightboxIndex++;
            _lightboxPhoto = _currentPhotos[_lightboxIndex];
        }
    }

    private bool CanLightboxPrev => _lightboxIndex > 0;
    private bool CanLightboxNext => _lightboxIndex < _currentPhotos.Count - 1;

    private void HandleLightboxKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "ArrowLeft": LightboxPrev(); break;
            case "ArrowRight": LightboxNext(); break;
            case "Escape": CloseLightbox(); break;
        }
    }

    // ── Editing ──────────────────────────────────────────────

    private async Task ApplyEditAsync(PhotoEditType editType, int value)
    {
        if (_caller is null || _lightboxPhoto is null) return;
        try
        {
            var operation = new PhotoEditOperationDto
            {
                OperationType = editType,
                Parameters = new Dictionary<string, string> { ["value"] = value.ToString() }
            };
            await EditService.ApplyEditAsync(_lightboxPhoto.Id, operation, _caller);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to apply edit to photo {PhotoId}", _lightboxPhoto.Id);
        }
    }

    private async Task UndoEditAsync()
    {
        if (_caller is null || _lightboxPhoto is null) return;
        try
        {
            await EditService.UndoLastEditAsync(_lightboxPhoto.Id, _caller);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to undo edit");
        }
    }

    private async Task RevertAllEditsAsync()
    {
        if (_caller is null || _lightboxPhoto is null) return;
        try
        {
            await EditService.RevertAllAsync(_lightboxPhoto.Id, _caller);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to revert edits");
        }
    }

    // ── Slideshow ────────────────────────────────────────────

    private void BeginSlideshow()
    {
        _slideshowPhotos = _currentPhotos.Count > 0 ? [.. _currentPhotos] : [];
        if (_slideshowPhotos.Count == 0) return;

        _slideshowIndex = _lightboxPhoto is not null ? _currentPhotos.IndexOf(_lightboxPhoto) : 0;
        if (_slideshowIndex < 0) _slideshowIndex = 0;

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
            if (_slideshowPaused) return;
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
        if (_slideshowIndex > 0) _slideshowIndex--;
    }

    private void SlideshowNext()
    {
        if (_slideshowIndex < _slideshowPhotos.Count - 1) _slideshowIndex++;
    }

    private void HandleSlideshowKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "ArrowLeft": SlideshowPrev(); break;
            case "ArrowRight": SlideshowNext(); break;
            case " ": ToggleSlideshowPause(); break;
            case "Escape": StopSlideshow(); break;
        }
    }

    // ── Sharing ──────────────────────────────────────────────

    private void BeginShare(PhotoDto photo)
    {
        _shareTargetPhotoId = photo.Id;
        _shareUserId = string.Empty;
        _sharePermission = PhotoSharePermission.ReadOnly;
        _showShareDialog = true;
    }

    private async Task SaveShareAsync()
    {
        if (_caller is null || !_shareTargetPhotoId.HasValue) return;
        if (!Guid.TryParse(_shareUserId, out var userId)) return;

        try
        {
            await ShareService.SharePhotoAsync(_shareTargetPhotoId.Value, userId, _sharePermission, _caller);
            _showShareDialog = false;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to share photo");
        }
    }

    // ── Pagination ───────────────────────────────────────────

    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)_totalPhotos / _pageSize));

    private async Task PrevPage()
    {
        if (_page > 0) { _page--; await LoadCurrentSectionAsync(); }
    }

    private async Task NextPage()
    {
        if (_page < TotalPages - 1) { _page++; await LoadCurrentSectionAsync(); }
    }

    // ── Helpers ──────────────────────────────────────────────

    private string GetThumbnailUrl(Guid photoId, string size)
        => $"/api/v1/photos/{photoId}/thumbnail?size={size}";

    private static string FormatDate(DateTime dt)
        => dt.ToString("MMM d, yyyy");

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
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

    public async ValueTask DisposeAsync()
    {
        _slideshowTimer?.Dispose();
        _slideshowTimer = null;
        GC.SuppressFinalize(this);
        await ValueTask.CompletedTask;
    }
}
