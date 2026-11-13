using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Sdk.Web;

namespace DotNetCloud.Modules.Photos.Web.Services;

/// <summary>
/// The Photos module UI's client for its own host's REST API.
/// </summary>
/// <remarks>
/// <para>
/// URLs are relative: the core proxies <c>/apps/dotnetcloud.photos/</c> and strips the prefix, so
/// <c>api/v1/photos</c> resolves inside the module either way. The host answers the standard success
/// envelope, which <see cref="ModuleApiClient"/> unwraps.
/// </para>
/// <para>
/// Thumbnails and downloads are browser-built subresources, so their URLs bypass the bridge's
/// <c>fetch</c> patch: <see cref="ThumbnailUrl"/> must be passed through
/// <see cref="ModuleFrameBridge.MediaUrlAsync"/> before it goes into an <c>img src</c>.
/// </para>
/// </remarks>
public sealed class PhotosApi
{
    private const string PhotosPath = "api/v1/photos";
    private const string AlbumsPath = "api/v1/albums";

    private readonly ModuleApiClient _api;

    /// <summary>Initializes a new instance of the <see cref="PhotosApi"/> class.</summary>
    /// <param name="api">The shared frame API client.</param>
    public PhotosApi(ModuleApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    /// <summary>Lists the caller's photos, newest first.</summary>
    /// <param name="skip">Rows to skip.</param>
    /// <param name="take">Maximum rows to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The photos.</returns>
    public async Task<IReadOnlyList<PhotoDto>> ListAsync(
        int skip = 0,
        int take = 60,
        CancellationToken cancellationToken = default) =>
        await _api.SendAsync<List<PhotoDto>>(
            HttpMethod.Get,
            $"{PhotosPath}?skip={skip}&take={take}",
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];

    /// <summary>Lists the caller's favorite photos.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The photos.</returns>
    public async Task<IReadOnlyList<PhotoDto>> ListFavoritesAsync(CancellationToken cancellationToken = default) =>
        await _api.SendAsync<List<PhotoDto>>(
            HttpMethod.Get,
            $"{PhotosPath}/favorites",
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];

    /// <summary>Searches the caller's photos.</summary>
    /// <param name="query">The search text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching photos.</returns>
    public async Task<IReadOnlyList<PhotoDto>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default) =>
        await _api.SendAsync<List<PhotoDto>>(
            HttpMethod.Get,
            $"{PhotosPath}/search?q={Uri.EscapeDataString(query)}",
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];

    /// <summary>Lists photos taken within a date range.</summary>
    /// <param name="from">Range start.</param>
    /// <param name="to">Range end.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The photos.</returns>
    public async Task<IReadOnlyList<PhotoDto>> ListTimelineAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        await _api.SendAsync<List<PhotoDto>>(
            HttpMethod.Get,
            $"{PhotosPath}/timeline?from={Uri.EscapeDataString(from.UtcDateTime.ToString("O"))}&to={Uri.EscapeDataString(to.UtcDateTime.ToString("O"))}",
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];

    /// <summary>Gets one photo.</summary>
    /// <param name="photoId">The photo id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The photo, when the host returned it.</returns>
    public Task<PhotoDto?> GetAsync(Guid photoId, CancellationToken cancellationToken = default) =>
        _api.SendAsync<PhotoDto>(HttpMethod.Get, $"{PhotosPath}/{photoId}", cancellationToken: cancellationToken);

    /// <summary>Toggles the favorite flag on a photo.</summary>
    /// <param name="photoId">The photo id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated photo, when the host returned it.</returns>
    public Task<PhotoDto?> ToggleFavoriteAsync(Guid photoId, CancellationToken cancellationToken = default) =>
        _api.SendAsync<PhotoDto>(HttpMethod.Post, $"{PhotosPath}/{photoId}/favorite", cancellationToken: cancellationToken);

    /// <summary>Removes a photo from the library (the file itself stays in the caller's files).</summary>
    /// <param name="photoId">The photo id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the removal.</returns>
    public Task DeleteAsync(Guid photoId, CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Delete, $"{PhotosPath}/{photoId}", cancellationToken: cancellationToken);

    /// <summary>Gets the recorded (unsaved) edit stack for a photo.</summary>
    /// <param name="photoId">The photo id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The edit stack, oldest first.</returns>
    public async Task<IReadOnlyList<PhotoEditRecordDto>> GetEditsAsync(
        Guid photoId,
        CancellationToken cancellationToken = default) =>
        await _api.SendAsync<List<PhotoEditRecordDto>>(
            HttpMethod.Get,
            $"{PhotosPath}/{photoId}/edits",
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];

    /// <summary>Records an edit operation on the photo's stack.</summary>
    /// <param name="photoId">The photo id.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host recorded the edit.</returns>
    public Task ApplyEditAsync(
        Guid photoId,
        PhotoEditOperationDto operation,
        CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Post, $"{PhotosPath}/{photoId}/edits", operation, cancellationToken);

    /// <summary>Removes the most recent edit from the stack.</summary>
    /// <param name="photoId">The photo id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host removed it.</returns>
    public Task UndoLastEditAsync(Guid photoId, CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Delete, $"{PhotosPath}/{photoId}/edits/last", cancellationToken: cancellationToken);

    /// <summary>Clears the edit stack and re-renders the previews from the original.</summary>
    /// <param name="photoId">The photo id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host reverted the edits.</returns>
    public Task RevertAllEditsAsync(Guid photoId, CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Delete, $"{PhotosPath}/{photoId}/edits", cancellationToken: cancellationToken);

    /// <summary>Bakes the edit stack into the cached thumbnails.</summary>
    /// <param name="photoId">The photo id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the thumbnails were regenerated.</returns>
    public Task SaveEditsAsync(Guid photoId, CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Post, $"{PhotosPath}/{photoId}/edits/save", cancellationToken: cancellationToken);

    /// <summary>Lists the caller's albums.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The albums.</returns>
    public async Task<IReadOnlyList<AlbumDto>> ListAlbumsAsync(CancellationToken cancellationToken = default) =>
        await _api.SendAsync<List<AlbumDto>>(HttpMethod.Get, AlbumsPath, cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? [];

    /// <summary>Creates an album.</summary>
    /// <param name="title">The album title.</param>
    /// <param name="description">The optional description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created album, when the host returned it.</returns>
    public Task<AlbumDto?> CreateAlbumAsync(
        string title,
        string? description,
        CancellationToken cancellationToken = default) =>
        _api.SendAsync<AlbumDto>(
            HttpMethod.Post,
            AlbumsPath,
            new CreateAlbumDto { Title = title, Description = description },
            cancellationToken);

    /// <summary>Lists the photos in an album.</summary>
    /// <param name="albumId">The album id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The photos.</returns>
    public async Task<IReadOnlyList<PhotoDto>> ListAlbumPhotosAsync(
        Guid albumId,
        CancellationToken cancellationToken = default) =>
        await _api.SendAsync<List<PhotoDto>>(
            HttpMethod.Get,
            $"{AlbumsPath}/{albumId}/photos",
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];

    /// <summary>Adds a photo to an album.</summary>
    /// <param name="albumId">The album id.</param>
    /// <param name="photoId">The photo id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the change.</returns>
    public Task AddPhotoToAlbumAsync(Guid albumId, Guid photoId, CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Post, $"{AlbumsPath}/{albumId}/photos/{photoId}", cancellationToken: cancellationToken);

    /// <summary>Removes a photo from an album.</summary>
    /// <param name="albumId">The album id.</param>
    /// <param name="photoId">The photo id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the change.</returns>
    public Task RemovePhotoFromAlbumAsync(Guid albumId, Guid photoId, CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Delete, $"{AlbumsPath}/{albumId}/photos/{photoId}", cancellationToken: cancellationToken);

    /// <summary>Lists the photos and albums shared with the caller.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The shares.</returns>
    public async Task<IReadOnlyList<PhotoShareDto>> ListSharedWithMeAsync(CancellationToken cancellationToken = default) =>
        await _api.SendAsync<List<PhotoShareDto>>(
            HttpMethod.Get,
            $"{PhotosPath}/shared-with-me",
            cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];

    /// <summary>Builds the relative thumbnail URL for a photo.</summary>
    /// <param name="photoId">The photo id.</param>
    /// <param name="size">The cached size (<c>grid</c> or <c>detail</c>).</param>
    /// <returns>A URL relative to the frame document.</returns>
    public static string ThumbnailUrl(Guid photoId, string size = "grid") =>
        $"{PhotosPath}/{photoId}/thumbnail?size={Uri.EscapeDataString(size)}";

    /// <summary>Builds the relative download URL for a photo's original file.</summary>
    /// <param name="photoId">The photo id.</param>
    /// <returns>A URL relative to the frame document.</returns>
    public static string DownloadUrl(Guid photoId) => $"{PhotosPath}/{photoId}/download";
}
