using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Photos.Web.Services;
using DotNetCloud.Modules.Sdk.Web;

namespace DotNetCloud.Modules.Photos.Tests;

/// <summary>
/// Covers the browser-side API client the Photos module UI runs on.
/// </summary>
/// <remarks>
/// Thumbnails and downloads are browser-built subresources; their URLs must stay relative so the frame
/// ticket can be attached by the bridge.
/// </remarks>
[TestClass]
public class PhotosApiTests
{
    [TestMethod]
    public async Task ListAsync_PagesThroughTheLibrary()
    {
        var handler = StubHttpMessageHandler.Json(
            """
            { "success": true, "data": [ { "id": "0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d", "fileNodeId": "8a1f0b5f-6c33-4a1f-9c4e-3c7db7f2a8b1", "ownerId": "9b2f0b5f-6c33-4a1f-9c4e-3c7db7f2a8b2", "fileName": "a.jpg", "mimeType": "image/jpeg", "isFavorite": true, "width": 100, "height": 50, "takenAt": "2026-11-11T09:00:00Z", "createdAt": "2026-11-11T09:00:00Z" } ] }
            """);

        var photos = await CreateApi(handler).ListAsync(skip: 60, take: 60);

        Assert.AreEqual(1, photos.Count);
        Assert.AreEqual("a.jpg", photos[0].FileName);
        Assert.IsTrue(photos[0].IsFavorite);
        Assert.AreEqual("/api/v1/photos?skip=60&take=60", handler.Requests[0]);
    }

    [TestMethod]
    public async Task ListTimelineAsync_SendsAnIsoRange()
    {
        var handler = StubHttpMessageHandler.Json("""{ "success": true, "data": [] }""");

        await CreateApi(handler).ListTimelineAsync(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));

        StringAssert.StartsWith(handler.Requests[0], "/api/v1/photos/timeline?from=2026-01-01");
        StringAssert.Contains(handler.Requests[0], "to=2026-02-01");
    }

    [TestMethod]
    public async Task GetEditsAsync_ReadsTheParametersDictionary()
    {
        var handler = StubHttpMessageHandler.Json(
            """
            { "success": true, "data": [ { "operationType": "Rotate", "parameters": { "value": "90" }, "stackOrder": 0 } ] }
            """);

        var edits = await CreateApi(handler).GetEditsAsync(Guid.NewGuid());

        Assert.AreEqual(1, edits.Count);
        Assert.AreEqual("Rotate", edits[0].OperationType);
        Assert.AreEqual("90", edits[0].Parameters["value"]);
    }

    [TestMethod]
    public async Task ApplyEditAsync_WritesTheOperationTypeAsAName()
    {
        string? body = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "success": true, "data": {} }"""),
            };
        });

        await CreateApi(handler).ApplyEditAsync(
            Guid.NewGuid(),
            new PhotoEditOperationDto
            {
                OperationType = PhotoEditType.Rotate,
                Parameters = new Dictionary<string, string> { ["value"] = "90" },
            });

        StringAssert.Contains(body, "\"operationType\":\"Rotate\"");
        StringAssert.Contains(body, "\"value\":\"90\"");
    }

    [TestMethod]
    public async Task SetFavoriteAsync_PostsToTheFavoriteEndpoint()
    {
        var id = Guid.Parse("0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d");
        var handler = StubHttpMessageHandler.Json(
            """{ "success": true, "data": { "id": "0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d", "fileNodeId": "8a1f0b5f-6c33-4a1f-9c4e-3c7db7f2a8b1", "ownerId": "9b2f0b5f-6c33-4a1f-9c4e-3c7db7f2a8b2", "fileName": "a.jpg", "mimeType": "image/jpeg", "isFavorite": true, "takenAt": "2026-11-11T09:00:00Z", "createdAt": "2026-11-11T09:00:00Z" } }""");

        var updated = await CreateApi(handler).ToggleFavoriteAsync(id);

        Assert.IsTrue(updated!.IsFavorite);
        Assert.AreEqual("/api/v1/photos/0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d/favorite", handler.Requests[0]);
    }

    [TestMethod]
    public void ThumbnailAndDownloadUrls_StayRelative()
    {
        var id = Guid.Parse("0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d");

        Assert.AreEqual($"/api/v1/photos/0e6b8e26-1fd9-4bd5-9d55-a5d1f1ad0f0d/thumbnail?size=grid", "/" + PhotosApi.ThumbnailUrl(id));
        Assert.AreEqual($"api/v1/photos/{id}/download", PhotosApi.DownloadUrl(id));
    }

    private static PhotosApi CreateApi(StubHttpMessageHandler handler) =>
        new(new ModuleApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://module.local/") }));
}
