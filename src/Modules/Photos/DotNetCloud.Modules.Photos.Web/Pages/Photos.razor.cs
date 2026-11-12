using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Photos.Web.Services;
using DotNetCloud.Modules.Sdk.Web;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Photos.Web.Pages;

/// <summary>
/// Code-behind for the Photos module UI.
/// </summary>
/// <remarks>
/// <para>
/// The same single-document UI the module shipped as plain JavaScript, ported to Razor components: a
/// sidebar of library views and albums, a paged gallery with grid and list modes, a viewer overlay and
/// a new-album dialog.
/// </para>
/// <para>
/// Thumbnails and downloads are browser-built subresources, so their URLs are minted with the frame
/// ticket via <see cref="ModuleFrameBridge.MediaUrlAsync"/> before they reach an <c>img src</c>; a bare
/// module URL there would be refused.
/// </para>
/// </remarks>
public partial class Photos : ComponentBase
{
    private const int PageSize = 60;

    private readonly List<PhotoDto> _photos = [];
    private readonly List<AlbumDto> _albums = [];
    private readonly List<PhotoShareDto> _shares = [];
    private readonly Dictionary<Guid, string> _gridUrls = [];
    private readonly Dictionary<Guid, PhotoTransform> _previewTransforms = [];

    private PhotoView _view = PhotoView.All;
    private string _viewTitle = "All photos";
    private bool _listView;
    private bool _collapsed;

    private int _page;
    private bool _hasMore;
    private string _searchInput = string.Empty;
    private string? _searchQuery;
    private Guid? _selectedAlbumId;

    private string? _status;
    private bool _statusIsError;

    private int _viewerIndex = -1;
    private string? _detailUrl;
    private bool _confirmDelete;
    private bool _deleting;
    private Guid? _albumSelectId;

    private bool _albumDialogOpen;
    private string _albumTitle = string.Empty;
    private string _albumDescription = string.Empty;
    private bool _creatingAlbum;

    [Inject]
    private PhotosApi Api { get; set; } = default!;

    [Inject]
    private ModuleFrameBridge Bridge { get; set; } = default!;

    /// <summary>Gets or sets the photo the shell deep-linked to.</summary>
    [SupplyParameterFromQuery(Name = "photoId")]
    public string? PhotoId { get; set; }

    /// <summary>Gets or sets the album the shell deep-linked to.</summary>
    [SupplyParameterFromQuery(Name = "album")]
    public string? AlbumId { get; set; }

    private PhotoDto? _viewerPhoto =>
        _viewerIndex >= 0 && _viewerIndex < _photos.Count ? _photos[_viewerIndex] : null;

    private bool ShowPager =>
        _view is PhotoView.All or PhotoView.Favorites or PhotoView.Timeline or PhotoView.Search or PhotoView.Album
        && (_page > 0 || _hasMore);

    private bool IsQuarterTurn =>
        _viewerPhoto is { } photo && TransformOf(photo.Id).Rotate % 180 == 90;

    private string TransformStyle
    {
        get
        {
            var transform = _viewerPhoto is { } photo ? TransformOf(photo.Id) : PhotoTransform.Identity;
            var scaleX = transform.FlipX ? -1 : 1;
            var scaleY = transform.FlipY ? -1 : 1;
            return $"transform: rotate({transform.Rotate}deg) scaleX({scaleX}) scaleY({scaleY})";
        }
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await LoadAlbumsAsync();
        await LoadPhotosAsync(PhotoView.All, page: 0);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await Bridge.InitializeAsync();
        await HandleDeepLinkAsync();
    }

    // ── Loading ─────────────────────────────────────────────

    private async Task LoadPhotosAsync(PhotoView view, int page, string? query = null, bool append = false)
    {
        try
        {
            IReadOnlyList<PhotoDto> photos;

            switch (view)
            {
                case PhotoView.Favorites:
                    photos = await Api.ListFavoritesAsync();
                    break;
                case PhotoView.Search when !string.IsNullOrWhiteSpace(query):
                    photos = await Api.SearchAsync(query);
                    break;
                case PhotoView.Timeline:
                    photos = await Api.ListTimelineAsync(
                        new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero),
                        DateTimeOffset.UtcNow.AddDays(1));
                    break;
                case PhotoView.Album when _selectedAlbumId is { } albumId:
                    photos = await Api.ListAlbumPhotosAsync(albumId);
                    break;
                default:
                    photos = await Api.ListAsync(page * PageSize, PageSize);
                    _hasMore = photos.Count == PageSize;
                    break;
            }

            if (view is not PhotoView.All)
            {
                _hasMore = false;
            }

            if (view is PhotoView.Timeline)
            {
                photos = photos.OrderByDescending(photo => photo.TakenAt).ToArray();
            }

            _page = page;

            if (!append)
            {
                _photos.Clear();
            }

            _photos.AddRange(photos);
            await UpdateMediaUrlsAsync();
            SetStatus(null);
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            await HandleExceptionAsync(ex);
        }
    }

    private async Task LoadAlbumsAsync()
    {
        try
        {
            _albums.Clear();
            _albums.AddRange(await Api.ListAlbumsAsync());

            foreach (var album in _albums)
            {
                if (album.CoverPhotoId is { } coverId && !_gridUrls.ContainsKey(coverId))
                {
                    _gridUrls[coverId] = await Bridge.MediaUrlAsync(PhotosApi.ThumbnailUrl(coverId));
                }
            }
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            await HandleExceptionAsync(ex);
        }
    }

    private async Task LoadSharesAsync()
    {
        try
        {
            _shares.Clear();
            _shares.AddRange(await Api.ListSharedWithMeAsync());
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            await HandleExceptionAsync(ex);
        }
    }

    /// <summary>
    /// Mints ticketed thumbnail URLs for the loaded photos and the open viewer image.
    /// </summary>
    /// <remarks>
    /// The bridge patches <c>fetch</c>, which covers every API call but not the resource loads a
    /// browser issues itself, so the ticket has to be in the URL.
    /// </remarks>
    private async Task UpdateMediaUrlsAsync()
    {
        foreach (var photo in _photos)
        {
            if (!_gridUrls.ContainsKey(photo.Id))
            {
                _gridUrls[photo.Id] = await Bridge.MediaUrlAsync(PhotosApi.ThumbnailUrl(photo.Id));
            }
        }

        if (_viewerPhoto is { } current)
        {
            _detailUrl = await Bridge.MediaUrlAsync(PhotosApi.ThumbnailUrl(current.Id, "detail"));
        }
    }

    // ── Views ───────────────────────────────────────────────

    private Task ShowAllAsync() => ShowLibraryAsync(PhotoView.All, "All photos");

    private Task ShowFavoritesAsync() => ShowLibraryAsync(PhotoView.Favorites, "Favorites");

    private Task ShowTimelineAsync() => ShowLibraryAsync(PhotoView.Timeline, "Timeline");

    private async Task ShowLibraryAsync(PhotoView view, string title)
    {
        _view = view;
        _viewTitle = title;
        _selectedAlbumId = null;
        _searchQuery = null;
        _searchInput = string.Empty;
        _page = 0;
        _photos.Clear();

        await LoadPhotosAsync(view, page: 0);
    }

    private async Task ShowSharedAsync()
    {
        _view = PhotoView.Shared;
        _viewTitle = "Shared with me";
        _selectedAlbumId = null;
        await LoadSharesAsync();
    }

    private Task ShowHelpAsync()
    {
        _view = PhotoView.Help;
        _viewTitle = "Help";
        _selectedAlbumId = null;
        return Task.CompletedTask;
    }

    private async Task OpenAlbumAsync(Guid albumId, string? title)
    {
        _view = PhotoView.Album;
        _selectedAlbumId = albumId;
        _page = 0;
        _viewTitle = string.IsNullOrWhiteSpace(title)
            ? _albums.FirstOrDefault(album => album.Id == albumId)?.Title ?? "Album"
            : title;

        await LoadPhotosAsync(PhotoView.Album, page: 0);
    }

    private async Task RefreshAsync()
    {
        await LoadAlbumsAsync();

        switch (_view)
        {
            case PhotoView.Shared:
                await LoadSharesAsync();
                break;
            case PhotoView.Albums:
            case PhotoView.Help:
                break;
            default:
                await LoadPhotosAsync(_view, page: 0);
                break;
        }

        SetStatus("Refreshed.");
    }

    private async Task RunSearchAsync()
    {
        var query = _searchInput.Trim();
        if (query.Length == 0)
        {
            await ShowAllAsync();
            return;
        }

        _view = PhotoView.Search;
        _viewTitle = "Search: " + query;
        _searchQuery = query;
        _page = 0;
        _selectedAlbumId = null;

        await LoadPhotosAsync(PhotoView.Search, page: 0, query);
    }

    private async Task PreviousPageAsync()
    {
        if (_page == 0)
        {
            return;
        }

        await LoadPhotosAsync(_view, _page - 1, _searchQuery);
    }

    private async Task NextPageAsync()
    {
        if (!_hasMore)
        {
            return;
        }

        await LoadPhotosAsync(_view, _page + 1, _searchQuery, append: true);
    }

    private void ToggleViewMode()
    {
        _listView = !_listView;
    }

    private void ToggleSidebar() => _collapsed = !_collapsed;

    private bool IsNavActive(string navId) =>
        (_view, navId) switch
        {
            (PhotoView.All, "nav-all") => true,
            (PhotoView.Favorites, "nav-favorites") => true,
            (PhotoView.Timeline, "nav-timeline") => true,
            (PhotoView.Shared, "nav-shared") => true,
            (PhotoView.Help, "nav-help") => true,
            _ => false,
        };

    // ── Deep links ──────────────────────────────────────────

    private async Task HandleDeepLinkAsync()
    {
        if (!string.IsNullOrWhiteSpace(AlbumId) && Guid.TryParse(AlbumId, out var albumId))
        {
            await OpenAlbumAsync(albumId, title: null);
            return;
        }

        if (!string.IsNullOrWhiteSpace(PhotoId) && Guid.TryParse(PhotoId, out var photoId))
        {
            await OpenSharedPhotoAsync(photoId);
        }
    }

    private async Task OpenSharedPhotoAsync(Guid photoId)
    {
        try
        {
            var photo = await Api.GetAsync(photoId);
            if (photo is null)
            {
                SetStatus("That photo is no longer shared with you.", isError: true);
                return;
            }

            _photos.Clear();
            _photos.Add(photo);
            await OpenViewerAsync(0);
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            await HandleExceptionAsync(ex);
        }
    }

    // ── Viewer ──────────────────────────────────────────────

    private async Task OpenViewerAsync(int index)
    {
        _viewerIndex = index;
        _confirmDelete = false;
        _albumSelectId = null;

        if (_viewerPhoto is { } photo)
        {
            _detailUrl = await Bridge.MediaUrlAsync(PhotosApi.ThumbnailUrl(photo.Id, "detail"));
            await RefreshPreviewTransformAsync(photo.Id);
        }
    }

    private Task OpenViewer(int index) => OpenViewerAsync(index);

    private async Task StepViewerAsync(int delta)
    {
        var next = _viewerIndex + delta;
        if (next < 0 || next >= _photos.Count)
        {
            return;
        }

        await OpenViewerAsync(next);
    }

    private Task StepViewer(int delta) => StepViewerAsync(delta);

    private void CloseViewer()
    {
        _viewerIndex = -1;
        _detailUrl = null;
        _confirmDelete = false;
    }

    private async Task ToggleFavoriteAsync()
    {
        if (_viewerPhoto is not { } photo)
        {
            return;
        }

        try
        {
            var updated = await Api.ToggleFavoriteAsync(photo.Id);
            ApplyFavorite(photo.Id, updated?.IsFavorite ?? !photo.IsFavorite);
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            await HandleExceptionAsync(ex);
        }
    }

    private async Task DeleteCurrentPhotoAsync()
    {
        if (_viewerPhoto is not { } photo)
        {
            return;
        }

        _deleting = true;

        try
        {
            await Api.DeleteAsync(photo.Id);
            _photos.RemoveAt(_viewerIndex);
            _confirmDelete = false;

            if (_photos.Count == 0)
            {
                CloseViewer();
            }
            else
            {
                _viewerIndex = Math.Min(_viewerIndex, _photos.Count - 1);
                await OpenViewerAsync(_viewerIndex);
            }

            SetStatus("Removed from the library.");
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            await HandleExceptionAsync(ex);
        }
        finally
        {
            _deleting = false;
        }
    }

    private async Task DownloadCurrentPhotoAsync()
    {
        if (_viewerPhoto is not { } photo)
        {
            return;
        }

        // A same-frame navigation cannot download from an opaque origin, so the bridge opens a
        // core-controlled tab instead. The URL stays module-relative; the bridge prefixes the app path.
        await Bridge.OpenExternalAsync(PhotosApi.DownloadUrl(photo.Id));
    }

    // ── Edits ───────────────────────────────────────────────

    private async Task ApplyEditAsync(PhotoEditType operationType, string value)
    {
        if (_viewerPhoto is not { } photo)
        {
            return;
        }

        try
        {
            await Api.ApplyEditAsync(
                photo.Id,
                new PhotoEditOperationDto
                {
                    OperationType = operationType,
                    Parameters = new Dictionary<string, string> { ["value"] = value },
                });

            await RefreshPreviewTransformAsync(photo.Id);
            SetStatus("Edit queued. Choose Save edits to apply it to the photo.");
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            await HandleExceptionAsync(ex);
        }
    }

    private async Task UndoEditAsync()
    {
        if (_viewerPhoto is not { } photo)
        {
            return;
        }

        try
        {
            await Api.UndoLastEditAsync(photo.Id);
            await RefreshPreviewTransformAsync(photo.Id);
            SetStatus("Last edit undone.");
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            await HandleExceptionAsync(ex);
        }
    }

    private async Task SaveEditsAsync()
    {
        if (_viewerPhoto is not { } photo)
        {
            return;
        }

        try
        {
            await Api.SaveEditsAsync(photo.Id);
            ApplyHasEdits(photo.Id);

            // The edits are baked into the served thumbnails now, so drop the CSS preview and refetch
            // the image, or the viewer would show the transform on top of the rotated pixels.
            _previewTransforms.Remove(photo.Id);
            _detailUrl = await Bridge.MediaUrlAsync(
                PhotosApi.ThumbnailUrl(photo.Id, "detail") + "&t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            SetStatus("Saved. The photo now shows those edits everywhere.");
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            await HandleExceptionAsync(ex);
        }
    }

    private async Task RefreshPreviewTransformAsync(Guid photoId)
    {
        try
        {
            var stack = await Api.GetEditsAsync(photoId);
            _previewTransforms[photoId] = TransformOfStack(stack);
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            // A missing stack is not worth interrupting the user; the previous preview stays.
        }
    }

    private PhotoTransform TransformOf(Guid photoId) =>
        _previewTransforms.TryGetValue(photoId, out var transform) ? transform : PhotoTransform.Identity;

    private static PhotoTransform TransformOfStack(IReadOnlyList<PhotoEditRecordDto> stack)
    {
        var rotate = 0;
        var flipX = false;
        var flipY = false;

        foreach (var operation in stack)
        {
            if (string.Equals(operation.OperationType, "Rotate", StringComparison.OrdinalIgnoreCase))
            {
                if (operation.Parameters.TryGetValue("value", out var value)
                    && int.TryParse(value, out var degrees))
                {
                    rotate = ((rotate + degrees) % 360 + 360) % 360;
                }
            }
            else if (string.Equals(operation.OperationType, "Flip", StringComparison.OrdinalIgnoreCase))
            {
                if (operation.Parameters.TryGetValue("value", out var value) && value == "1")
                {
                    flipY = !flipY;
                }
                else
                {
                    flipX = !flipX;
                }
            }
        }

        return new PhotoTransform(rotate, flipX, flipY);
    }

    private void ApplyFavorite(Guid photoId, bool isFavorite)
    {
        ReplacePhoto(photoId, photo => photo with { IsFavorite = isFavorite });
    }

    private void ApplyHasEdits(Guid photoId)
    {
        ReplacePhoto(photoId, photo => photo with { HasEdits = true });
    }

    private void ReplacePhoto(Guid photoId, Func<PhotoDto, PhotoDto> update)
    {
        for (var index = 0; index < _photos.Count; index++)
        {
            if (_photos[index].Id == photoId)
            {
                _photos[index] = update(_photos[index]);
            }
        }
    }

    // ── Albums ──────────────────────────────────────────────

    private void OpenAlbumDialog()
    {
        _albumTitle = string.Empty;
        _albumDescription = string.Empty;
        _albumDialogOpen = true;
    }

    private void CloseAlbumDialog() => _albumDialogOpen = false;

    private async Task CreateAlbumAsync()
    {
        var title = _albumTitle.Trim();
        if (title.Length == 0)
        {
            return;
        }

        _creatingAlbum = true;

        try
        {
            await Api.CreateAlbumAsync(title, string.IsNullOrWhiteSpace(_albumDescription) ? null : _albumDescription.Trim());
            _albumDialogOpen = false;
            await LoadAlbumsAsync();
            SetStatus("Album created.");
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            await HandleExceptionAsync(ex);
        }
        finally
        {
            _creatingAlbum = false;
        }
    }

    private async Task AddToAlbumAsync()
    {
        if (_viewerPhoto is not { } photo || _albumSelectId is not { } albumId)
        {
            SetStatus("Choose an album first.", isError: true);
            return;
        }

        try
        {
            await Api.AddPhotoToAlbumAsync(albumId, photo.Id);
            await LoadAlbumsAsync();
            SetStatus("Added to the album.");
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            await HandleExceptionAsync(ex);
        }
    }

    private async Task RemoveFromAlbumAsync()
    {
        if (_viewerPhoto is not { } photo || _albumSelectId is not { } albumId)
        {
            SetStatus("Choose an album first.", isError: true);
            return;
        }

        try
        {
            await Api.RemovePhotoFromAlbumAsync(albumId, photo.Id);
            await LoadAlbumsAsync();

            if (_view is PhotoView.Album && _selectedAlbumId == albumId)
            {
                await LoadPhotosAsync(PhotoView.Album, page: 0);
            }

            SetStatus("Removed from the album.");
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            await HandleExceptionAsync(ex);
        }
    }

    // ── Formatting / helpers ────────────────────────────────

    private string? GridUrl(Guid photoId) => _gridUrls.TryGetValue(photoId, out var url) ? url : null;

    private static string DescribePhoto(PhotoDto photo)
    {
        var parts = new List<string>();

        if (photo.Width is > 0 && photo.Height is > 0)
        {
            parts.Add($"{photo.Width} × {photo.Height}");
        }

        if (photo.TakenAt != default)
        {
            parts.Add(FormatDate(photo.TakenAt));
        }

        return string.Join(" · ", parts);
    }

    private static string Caption(PhotoDto photo)
    {
        var prefix = photo.Tags.Count > 0
            ? "#" + string.Join(" #", photo.Tags) + " · "
            : string.Empty;

        var edited = photo.HasEdits ? "edited · " : string.Empty;
        return prefix + edited + FormatDate(photo.CreatedAt);
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

        return local.ToString("d", System.Globalization.CultureInfo.CurrentCulture);
    }

    private void SetStatus(string? message, bool isError = false)
    {
        _status = message;
        _statusIsError = isError;
    }

    /// <summary>
    /// Reports a failure inline, and turns a refused <c>files:read</c> consent into the shell's prompt.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns>A task that completes once the prompt request is posted.</returns>
    /// <remarks>
    /// Reading an original is brokered by the core under the user's <c>files:read</c> approval. The
    /// frame cannot approve anything itself, so the shell has to be asked; without this the user sees
    /// only "request failed" and never gets the prompt that would fix it.
    /// </remarks>
    private async Task HandleExceptionAsync(Exception exception)
    {
        if (exception is ModuleConsentRequiredException consent)
        {
            await Bridge.RequestConsentAsync(consent.Scopes.ToArray());
            SetStatus(
                "DotNetCloud needs your approval to read this photo's original. Approve the request shown above, then try again.",
                isError: true);
            return;
        }

        SetStatus(exception.Message, isError: true);
    }

    private readonly record struct PhotoTransform(int Rotate, bool FlipX, bool FlipY)
    {
        public static PhotoTransform Identity { get; } = new(0, false, false);
    }

    private enum PhotoView
    {
        All,
        Favorites,
        Timeline,
        Search,
        Album,
        Albums,
        Shared,
        Help,
    }
}
