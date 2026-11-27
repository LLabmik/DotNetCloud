using System.Net;
using System.Net.Http.Headers;
using DotNetCloud.Modules.Sdk.Web;

namespace DotNetCloud.Modules.Bookmarks.Web.Services;

/// <summary>
/// The Bookmarks module UI's client for its own host's REST API.
/// </summary>
/// <remarks>
/// <para>
/// URLs are relative: the core proxies <c>/apps/dotnetcloud.bookmarks/</c> and strips the prefix, so
/// <c>api/v1/bookmarks</c> resolves inside the module either way. The host answers the standard
/// success envelope, which <see cref="ModuleApiClient"/> unwraps.
/// </para>
/// <para>
/// Export is not a fetch: a download inside a sandboxed frame is blocked, so the UI opens the export
/// endpoint through the shell bridge — <see cref="BuildExportUrl"/> produces the relative path for
/// that navigation.
/// </para>
/// </remarks>
public sealed class BookmarksApi
{
    private const string BasePath = "api/v1/bookmarks";

    private readonly ModuleApiClient _api;

    /// <summary>Initializes a new instance of the <see cref="BookmarksApi"/> class.</summary>
    /// <param name="api">The shared frame API client.</param>
    public BookmarksApi(ModuleApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    /// <summary>Lists bookmarks, optionally limited to one folder.</summary>
    /// <param name="folderId">The folder to list, or <see langword="null"/> for every bookmark.</param>
    /// <param name="skip">Rows to skip.</param>
    /// <param name="take">Maximum rows to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bookmarks.</returns>
    public async Task<IReadOnlyList<BookmarkDto>> ListAsync(
        Guid? folderId = null,
        int skip = 0,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BasePath}?skip={skip}&take={take}";
        if (folderId is { } id)
        {
            url += $"&folderId={id}";
        }

        return await _api.SendAsync<List<BookmarkDto>>(HttpMethod.Get, url, cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? [];
    }

    /// <summary>Searches the caller's bookmarks.</summary>
    /// <param name="query">The search text.</param>
    /// <param name="skip">Rows to skip.</param>
    /// <param name="take">Maximum rows to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching bookmarks.</returns>
    public async Task<IReadOnlyList<BookmarkDto>> SearchAsync(
        string query,
        int skip = 0,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BasePath}/search?q={Uri.EscapeDataString(query)}&skip={skip}&take={take}";

        return await _api.SendAsync<List<BookmarkDto>>(HttpMethod.Get, url, cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? [];
    }

    /// <summary>Creates a bookmark.</summary>
    /// <param name="request">The bookmark data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created bookmark, when the host returned it.</returns>
    public Task<BookmarkDto?> CreateAsync(BookmarkSaveRequest request, CancellationToken cancellationToken = default) =>
        _api.SendAsync<BookmarkDto>(HttpMethod.Post, BasePath, request, cancellationToken);

    /// <summary>Updates a bookmark.</summary>
    /// <param name="id">The bookmark id.</param>
    /// <param name="request">The new bookmark data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated bookmark, when the host returned it.</returns>
    public Task<BookmarkDto?> UpdateAsync(
        Guid id,
        BookmarkSaveRequest request,
        CancellationToken cancellationToken = default) =>
        _api.SendAsync<BookmarkDto>(HttpMethod.Put, $"{BasePath}/{id}", request, cancellationToken);

    /// <summary>Sets or clears the favorite flag.</summary>
    /// <param name="id">The bookmark id.</param>
    /// <param name="isFavorite">The new flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated bookmark, when the host returned it.</returns>
    public Task<BookmarkDto?> SetFavoriteAsync(
        Guid id,
        bool isFavorite,
        CancellationToken cancellationToken = default) =>
        _api.SendAsync<BookmarkDto>(HttpMethod.Put, $"{BasePath}/{id}", new { isFavorite }, cancellationToken);

    /// <summary>Deletes a bookmark.</summary>
    /// <param name="id">The bookmark id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the deletion.</returns>
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Delete, $"{BasePath}/{id}", cancellationToken: cancellationToken);

    /// <summary>Lists the caller's folders.</summary>
    /// <param name="parentId">Optional parent folder to list below.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The folders.</returns>
    public async Task<IReadOnlyList<BookmarkFolderDto>> ListFoldersAsync(
        Guid? parentId = null,
        CancellationToken cancellationToken = default)
    {
        var url = parentId is { } id ? $"{BasePath}/folders?parentId={id}" : $"{BasePath}/folders";

        return await _api.SendAsync<List<BookmarkFolderDto>>(HttpMethod.Get, url, cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? [];
    }

    /// <summary>Creates a folder.</summary>
    /// <param name="request">The folder data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created folder, when the host returned it.</returns>
    public Task<BookmarkFolderDto?> CreateFolderAsync(
        BookmarkFolderSaveRequest request,
        CancellationToken cancellationToken = default) =>
        _api.SendAsync<BookmarkFolderDto>(HttpMethod.Post, $"{BasePath}/folders", request, cancellationToken);

    /// <summary>Updates a folder.</summary>
    /// <param name="id">The folder id.</param>
    /// <param name="request">The new folder data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated folder, when the host returned it.</returns>
    public Task<BookmarkFolderDto?> UpdateFolderAsync(
        Guid id,
        BookmarkFolderSaveRequest request,
        CancellationToken cancellationToken = default) =>
        _api.SendAsync<BookmarkFolderDto>(HttpMethod.Put, $"{BasePath}/folders/{id}", request, cancellationToken);

    /// <summary>Deletes a folder.</summary>
    /// <param name="id">The folder id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the host accepted the deletion.</returns>
    public Task DeleteFolderAsync(Guid id, CancellationToken cancellationToken = default) =>
        _api.SendAsync(HttpMethod.Delete, $"{BasePath}/folders/{id}", cancellationToken: cancellationToken);

    /// <summary>Gets the stored preview for a bookmark, if one exists.</summary>
    /// <param name="id">The bookmark id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The preview, or <see langword="null"/> when the host has none.</returns>
    public async Task<BookmarkPreviewDto?> GetPreviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _api
                .SendAsync<BookmarkPreviewDto>(HttpMethod.Get, $"{BasePath}/{id}/preview", cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ModuleApiException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            // "No preview yet" is a normal state, not a failure worth reporting.
            return null;
        }
    }

    /// <summary>Asks the host to fetch a fresh preview.</summary>
    /// <param name="id">The bookmark id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The fetched preview, when the host returned it.</returns>
    public Task<BookmarkPreviewDto?> FetchPreviewAsync(Guid id, CancellationToken cancellationToken = default) =>
        _api.SendAsync<BookmarkPreviewDto>(HttpMethod.Post, $"{BasePath}/{id}/preview/fetch", cancellationToken: cancellationToken);

    /// <summary>Imports a browser bookmarks HTML export.</summary>
    /// <param name="content">The file content.</param>
    /// <param name="fileName">The file name sent in the form part.</param>
    /// <param name="folderId">Optional folder to import into.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The import summary.</returns>
    public async Task<BookmarkImportResultDto> ImportAsync(
        Stream content,
        string fileName,
        Guid? folderId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        form.Add(file, "file", fileName);

        var url = folderId is { } id ? $"{BasePath}/import?folderId={id}" : $"{BasePath}/import";

        return await _api.SendContentAsync<BookmarkImportResultDto>(HttpMethod.Post, url, form, cancellationToken)
            .ConfigureAwait(false) ?? new BookmarkImportResultDto();
    }

    /// <summary>Builds the relative export URL the shell opens in its own tab.</summary>
    /// <param name="folderId">Optional folder to export.</param>
    /// <returns>A URL relative to the frame document.</returns>
    public static string BuildExportUrl(Guid? folderId = null) =>
        folderId is { } id ? $"{BasePath}/export?folderId={id}" : $"{BasePath}/export";
}
