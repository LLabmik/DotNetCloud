using System.Net;
using DotNetCloud.Modules.Bookmarks.Web.Services;
using DotNetCloud.Modules.Sdk.Web;

namespace DotNetCloud.Modules.Bookmarks.Tests;

/// <summary>
/// Covers the browser-side API client the Bookmarks module UI runs on.
/// </summary>
/// <remarks>
/// The host answers the standard success envelope for everything except a missing preview, which is a
/// 404 the UI treats as "no preview yet" rather than a failure.
/// </remarks>
[TestClass]
public class BookmarksApiTests
{
    [TestMethod]
    public async Task ListAsync_FolderFilter_AppendsTheFolderToTheQuery()
    {
        var folderId = Guid.Parse("8a1f0b5f-6c33-4a1f-9c4e-3c7db7f2a8b1");
        var handler = StubHttpMessageHandler.Json(
            """
            { "success": true, "data": [ { "id": "0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d", "url": "https://example.com", "title": "Example", "tagsJson": "[\"a\",\"b\"]", "isFavorite": true } ] }
            """);

        var items = await CreateApi(handler).ListAsync(folderId);

        Assert.AreEqual(1, items.Count);
        Assert.AreEqual("Example", items[0].Title);
        Assert.IsTrue(items[0].IsFavorite);
        Assert.AreEqual($"/api/v1/bookmarks?skip=0&take=200&folderId={folderId}", handler.Requests[0]);
    }

    [TestMethod]
    public async Task SearchAsync_EscapesTheQuery()
    {
        var handler = StubHttpMessageHandler.Json("""{ "success": true, "data": [] }""");

        await CreateApi(handler).SearchAsync("a b&c");

        StringAssert.Contains(handler.Requests[0], "q=a%20b%26c");
    }

    [TestMethod]
    public async Task SetFavoriteAsync_PutsTheFlagToTheBookmarkUrl()
    {
        var id = Guid.Parse("0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d");
        var handler = StubHttpMessageHandler.Json("""{ "success": true, "data": { "id": "0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d", "isFavorite": true } }""");

        var updated = await CreateApi(handler).SetFavoriteAsync(id, true);

        Assert.IsTrue(updated!.IsFavorite);
        Assert.AreEqual(HttpMethod.Put, handler.Methods[0]);
        Assert.AreEqual($"/api/v1/bookmarks/{id}", handler.Requests[0]);
    }

    [TestMethod]
    public async Task GetPreviewAsync_NotFound_ReturnsNullInsteadOfThrowing()
    {
        var handler = StubHttpMessageHandler.Json(
            """{ "success": false, "error": { "code": "BOOKMARK_NOT_FOUND", "message": "Preview not found." } }""",
            HttpStatusCode.NotFound);

        var preview = await CreateApi(handler).GetPreviewAsync(Guid.NewGuid());

        Assert.IsNull(preview, "'no preview yet' is a normal state, not a UI error");
    }

    [TestMethod]
    public async Task GetPreviewAsync_NumericStatus_IsReadAsTheEnum()
    {
        // The host serialises enums as numbers (MVC defaults); the shared client reads names and ints.
        var handler = StubHttpMessageHandler.Json(
            """{ "success": true, "data": { "status": 1, "resolvedTitle": "Resolved" } }""");

        var preview = await CreateApi(handler).GetPreviewAsync(Guid.NewGuid());

        Assert.IsNotNull(preview);
        Assert.AreEqual(BookmarkPreviewStatus.Ok, preview.Status);
        Assert.AreEqual("Resolved", preview.ResolvedTitle);
    }

    [TestMethod]
    public async Task ImportAsync_PostsMultipartToTheImportEndpoint()
    {
        var handler = StubHttpMessageHandler.Json(
            """{ "success": true, "data": { "importedCount": 3, "folderCount": 1, "skippedCount": 0, "errors": [] } }""");

        using var stream = new MemoryStream("<html></html>"u8.ToArray());
        var result = await CreateApi(handler).ImportAsync(stream, "bookmarks.html");

        Assert.AreEqual(3, result.ImportedCount);
        Assert.AreEqual(HttpMethod.Post, handler.Methods[0]);
        Assert.AreEqual("/api/v1/bookmarks/import", handler.Requests[0]);
        Assert.AreEqual("multipart/form-data", handler.ContentTypes[0]);
    }

    [TestMethod]
    public void BuildExportUrl_WithFolder_IncludesTheFolderQuery()
    {
        var folderId = Guid.Parse("8a1f0b5f-6c33-4a1f-9c4e-3c7db7f2a8b1");

        Assert.AreEqual("api/v1/bookmarks/export", BookmarksApi.BuildExportUrl());
        Assert.AreEqual($"api/v1/bookmarks/export?folderId={folderId}", BookmarksApi.BuildExportUrl(folderId));
    }

    [TestMethod]
    public async Task ListFoldersAsync_ReturnsTheFolders()
    {
        var handler = StubHttpMessageHandler.Json(
            """{ "success": true, "data": [ { "id": "8a1f0b5f-6c33-4a1f-9c4e-3c7db7f2a8b1", "name": "Work", "color": "#123456" } ] }""");

        var folders = await CreateApi(handler).ListFoldersAsync();

        Assert.AreEqual(1, folders.Count);
        Assert.AreEqual("Work", folders[0].Name);
        Assert.AreEqual("/api/v1/bookmarks/folders", handler.Requests[0]);
    }

    private static BookmarksApi CreateApi(StubHttpMessageHandler handler) =>
        new(new ModuleApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://module.local/") }));
}
