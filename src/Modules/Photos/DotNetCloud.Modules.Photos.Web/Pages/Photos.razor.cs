using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Photos.Web.Services;
using DotNetCloud.Modules.Sdk.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

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

    // Tone and radius sliders. They are previews of the next operation, not the state of the stack:
    // pressing Apply records the value; the stack stays the source of truth.
    private int _brightnessInput;
    private int _contrastInput;
    private int _saturationInput;
    private double _sharpenInput;
    private double _blurInput;

    // Crop selection, normalized 0..1 against the rendered preview so the recorded rectangle is
    // independent of the viewer's size.
    private bool _cropActive;
    private bool _cropSelecting;
    private double _cropAnchorX;
    private double _cropAnchorY;
    private double _cropStartX;
    private double _cropStartY;
    private double _cropWidth;
    private double _cropHeight;
    private double _cropLayerWidth;
    private double _cropLayerHeight;
    private ElementReference _cropLayer;

    [Inject]
    private PhotosApi Api { get; set; } = default!;

    [Inject]
    private ModuleFrameBridge Bridge { get; set; } = default!;

    [Inject]
    private IJSRuntime Js { get; set; } = default!;

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

    private bool HasCropSelection => _cropWidth > 0.005 && _cropHeight > 0.005;

    private string CropSelectionStyle => string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"--crop-x: {_cropStartX * 100:0.###}%; --crop-y: {_cropStartY * 100:0.###}%; " +
        $"--crop-w: {_cropWidth * 100:0.###}%; --crop-h: {_cropHeight * 100:0.###}%");

    private string TransformStyle
    {
        get
        {
            var transform = _viewerPhoto is { } photo ? TransformOf(photo.Id) : PhotoTransform.Identity;
            var scaleX = transform.FlipX ? -1 : 1;
            var scaleY = transform.FlipY ? -1 : 1;

            // Rotate/flip are a CSS transform; tone and blur are a filter, so a queued edit is visible
            // before "Save edits" bakes it into the pixels.
            var filter = FormattableString.Invariant(
                $"brightness({transform.Brightness:0.###}) contrast({transform.Contrast:0.###}) saturate({transform.Saturate:0.###})");

            if (transform.Blur > 0)
            {
                filter += FormattableString.Invariant($" blur({transform.Blur:0.##}px)");
            }

            return $"transform: rotate({transform.Rotate}deg) scaleX({scaleX}) scaleY({scaleY}); filter: {filter}";
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
        => await ApplyOperationAsync(
            operationType,
            new Dictionary<string, string> { ["value"] = value });

    private async Task ApplyOperationAsync(PhotoEditType operationType, Dictionary<string, string> parameters)
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
                    Parameters = parameters,
                });

            await RefreshPreviewTransformAsync(photo.Id);
            SetStatus("Edit queued. Choose Save edits to apply it to the photo.");
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            await HandleExceptionAsync(ex);
        }
    }

    private async Task ApplyRadiusEditAsync(PhotoEditType operationType, double radius)
    {
        if (radius <= 0)
        {
            SetStatus("Choose a radius above zero first.", isError: true);
            return;
        }

        await ApplyOperationAsync(
            operationType,
            new Dictionary<string, string>
            {
                ["radius"] = radius.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            });
    }

    private async Task RevertAllEditsAsync()
    {
        if (_viewerPhoto is not { } photo)
        {
            return;
        }

        try
        {
            // The host drops the stack and re-renders the previews from the original, so the viewer
            // must forget its CSS transform and refetch the image.
            await Api.RevertAllEditsAsync(photo.Id);
            _previewTransforms.Remove(photo.Id);
            _cropActive = false;
            await ReloadPreviewAsync(photo.Id);
            ReplacePhoto(photo.Id, p => p with { HasEdits = false });
            SetStatus("All edits reverted.");
        }
        catch (Exception ex) when (ex is ModuleApiException or HttpRequestException)
        {
            await HandleExceptionAsync(ex);
        }
    }

    private void BeginCrop()
    {
        if (_viewerPhoto is not { } photo)
        {
            return;
        }

        if (IsQuarterTurn || _previewTransforms.TryGetValue(photo.Id, out var pending) && (pending.FlipX || pending.FlipY))
        {
            SetStatus("Save or undo the rotation and flip first — crop is taken in the photo's own orientation.", isError: true);
            return;
        }

        _cropActive = true;
        _cropSelecting = false;
        _cropWidth = 0;
        _cropHeight = 0;
        SetStatus("Drag across the photo to choose the crop area.");
    }

    private void CancelCrop()
    {
        _cropActive = false;
        _cropSelecting = false;
        _cropWidth = 0;
        _cropHeight = 0;
        SetStatus(null);
    }

    private async Task CropPointerDownAsync(PointerEventArgs args)
    {
        try
        {
            if (!_cropActive)
            {
                return;
            }

            var size = await Js.InvokeAsync<CropLayerSize>("dncPhotos.measure", _cropLayer);
            _cropLayerWidth = size.Width;
            _cropLayerHeight = size.Height;

            if (_cropLayerWidth <= 0 || _cropLayerHeight <= 0)
            {
                return;
            }

            _cropSelecting = true;
            _cropAnchorX = Math.Clamp(args.OffsetX / _cropLayerWidth, 0, 1);
            _cropAnchorY = Math.Clamp(args.OffsetY / _cropLayerHeight, 0, 1);
            _cropStartX = _cropAnchorX;
            _cropStartY = _cropAnchorY;
            _cropWidth = 0;
            _cropHeight = 0;
        }
        catch (JSException)
        {
            // The helper script did not load; cropping stays unavailable rather than corrupting state.
            _cropActive = false;
        }
    }

    private void CropPointerMove(PointerEventArgs args)
    {
        if (!_cropSelecting || _cropLayerWidth <= 0 || _cropLayerHeight <= 0)
        {
            return;
        }

        var currentX = Math.Clamp(args.OffsetX / _cropLayerWidth, 0, 1);
        var currentY = Math.Clamp(args.OffsetY / _cropLayerHeight, 0, 1);

        // The anchor is where the drag started; the rectangle is the box between anchor and pointer.
        _cropStartX = Math.Min(_cropAnchorX, currentX);
        _cropStartY = Math.Min(_cropAnchorY, currentY);
        _cropWidth = Math.Abs(currentX - _cropAnchorX);
        _cropHeight = Math.Abs(currentY - _cropAnchorY);
    }

    private void CropPointerUp(PointerEventArgs args)
    {
        if (!_cropSelecting)
        {
            return;
        }

        CropPointerMove(args);
        _cropSelecting = false;

        if (!HasCropSelection)
        {
            SetStatus("Drag a larger area to crop.", isError: true);
        }
    }

    private async Task ApplyCropAsync()
    {
        if (_viewerPhoto is not { } photo)
        {
            return;
        }

        if (!HasCropSelection)
        {
            SetStatus("Drag across the photo to choose the crop area first.", isError: true);
            return;
        }

        // The selection is a fraction of the preview, so it travels as fractions: the browser has no
        // reliable way to know the original pixel size (a photo uploaded before sizes were recorded
        // has none), and the render pipeline resolves fractions against the image it is holding.
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var normalized = new Dictionary<string, string>
        {
            ["nx"] = _cropStartX.ToString("0.####", culture),
            ["ny"] = _cropStartY.ToString("0.####", culture),
            ["nw"] = _cropWidth.ToString("0.####", culture),
            ["nh"] = _cropHeight.ToString("0.####", culture),
        };

        // Keep the pixel rectangle too when the size is known: it makes stored stacks readable and
        // keeps older readers working. Fractions stay authoritative.
        if (photo.Width is { } sourceWidth && photo.Height is { } sourceHeight
            && sourceWidth > 0 && sourceHeight > 0)
        {
            var x = Math.Clamp((int)Math.Round(_cropStartX * sourceWidth), 0, sourceWidth - 1);
            var y = Math.Clamp((int)Math.Round(_cropStartY * sourceHeight), 0, sourceHeight - 1);
            var width = Math.Clamp((int)Math.Round(_cropWidth * sourceWidth), 1, sourceWidth - x);
            var height = Math.Clamp((int)Math.Round(_cropHeight * sourceHeight), 1, sourceHeight - y);

            normalized["x"] = x.ToString(culture);
            normalized["y"] = y.ToString(culture);
            normalized["width"] = width.ToString(culture);
            normalized["height"] = height.ToString(culture);
        }

        _cropActive = false;
        _cropSelecting = false;

        await ApplyOperationAsync(PhotoEditType.Crop, normalized);

        _cropWidth = 0;
        _cropHeight = 0;
    }

    private async Task ReloadPreviewAsync(Guid photoId)
    {
        _detailUrl = await Bridge.MediaUrlAsync(
            PhotosApi.ThumbnailUrl(photoId, "detail") + "&t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
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
            await ReloadPreviewAsync(photo.Id);
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
        var brightness = 1.0;
        var contrast = 1.0;
        var saturate = 1.0;
        var blur = 0.0;

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
            else if (operation.Parameters.TryGetValue("value", out var raw)
                && double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var amount))
            {
                // A preview only: the stack is authoritative and the pipeline applies the real maths.
                var factor = 1.0 + (amount / 100.0);

                if (string.Equals(operation.OperationType, "Brightness", StringComparison.OrdinalIgnoreCase))
                {
                    brightness *= factor;
                }
                else if (string.Equals(operation.OperationType, "Contrast", StringComparison.OrdinalIgnoreCase))
                {
                    contrast *= factor;
                }
                else if (string.Equals(operation.OperationType, "Saturation", StringComparison.OrdinalIgnoreCase))
                {
                    saturate *= factor;
                }
            }

            if (string.Equals(operation.OperationType, "Blur", StringComparison.OrdinalIgnoreCase))
            {
                var radiusText = operation.Parameters.TryGetValue("radius", out var radius) ? radius : null;
                if (radiusText is not null
                    && double.TryParse(radiusText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var radiusValue))
                {
                    blur += radiusValue;
                }
            }
        }

        return new PhotoTransform(rotate, flipX, flipY, brightness, contrast, saturate, blur);
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

    private readonly record struct PhotoTransform(
        int Rotate,
        bool FlipX,
        bool FlipY,
        double Brightness = 1.0,
        double Contrast = 1.0,
        double Saturate = 1.0,
        double Blur = 0.0)
    {
        public static PhotoTransform Identity { get; } = new(0, false, false);
    }

    /// <summary>On-screen size of the crop layer, as reported by <c>dncPhotos.measure</c>.</summary>
    private sealed record CropLayerSize(double Width, double Height);

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
