using System.Net;
using System.Net.Http.Json;
using DotNetCloud.Integration.Tests.Infrastructure;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Integration.Tests.Api;

/// <summary>
/// Integration tests for Files thumbnail API endpoint behavior.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class FilesThumbnailIntegrationTests
{
    private static readonly byte[] TinyGifBytes = Convert.FromBase64String(
        "R0lGODdhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==");

    private static FilesHostWebApplicationFactory _factory = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _factory = new FilesHostWebApplicationFactory();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _factory.Dispose();
    }

    [TestMethod]
    public async Task GetThumbnail_WhenGenerated_ReturnsJpegContent()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedApiClient(userId);

        var chunkHash = DotNetCloud.Modules.Files.Services.ContentHasher.ComputeHash(TinyGifBytes);

        var initiateResponse = await client.PostAsJsonAsync(
            "/api/v1/files/upload/initiate",
            new InitiateUploadDto
            {
                FileName = "tiny.gif",
                TotalSize = TinyGifBytes.Length,
                MimeType = "image/gif",
                ChunkHashes = [chunkHash]
            });

        var initiateRoot = await ApiAssert.SuccessAsync(initiateResponse, HttpStatusCode.Created);
        var sessionId = DataOrRoot(initiateRoot).GetProperty("sessionId").GetGuid();

        using var chunkContent = new ByteArrayContent(TinyGifBytes);
        var uploadChunkResponse = await client.PutAsync($"/api/v1/files/upload/{sessionId}/chunks/{chunkHash}", chunkContent);
        await ApiAssert.SuccessAsync(uploadChunkResponse, HttpStatusCode.OK);

        var completeResponse = await client.PostAsync($"/api/v1/files/upload/{sessionId}/complete", content: null);
        var completeRoot = await ApiAssert.SuccessAsync(completeResponse, HttpStatusCode.OK);
        var fileNodeId = DataOrRoot(completeRoot).GetProperty("id").GetGuid();

        // Seed thumbnail cache to validate endpoint wiring independently from upload pipeline generation hooks.
        using (var scope = _factory.Services.CreateScope())
        {
            var thumbnailService = scope.ServiceProvider.GetRequiredService<IThumbnailService>();
            var sourcePath = Path.Combine(Path.GetTempPath(), $"thumb-source-{Guid.NewGuid():N}.gif");

            try
            {
                await File.WriteAllBytesAsync(sourcePath, TinyGifBytes);
                await thumbnailService.GenerateThumbnailAsync(fileNodeId, sourcePath, "image/gif");
            }
            finally
            {
                if (File.Exists(sourcePath))
                    File.Delete(sourcePath);
            }
        }

        var thumbnailResponse = await client.GetAsync($"/api/v1/files/{fileNodeId}/thumbnail?size=small");
        Assert.AreEqual(HttpStatusCode.OK, thumbnailResponse.StatusCode);
        Assert.AreEqual("image/jpeg", thumbnailResponse.Content.Headers.ContentType?.MediaType);

        var thumbnailBytes = await thumbnailResponse.Content.ReadAsByteArrayAsync();
        Assert.IsTrue(thumbnailBytes.Length > 0);
    }

    [TestMethod]
    public async Task GetThumbnail_WithInvalidSize_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateAuthenticatedApiClient(userId);

        var response = await client.GetAsync($"/api/v1/files/{Guid.NewGuid()}/thumbnail?size=huge");
        await ApiAssert.ErrorAsync(response, HttpStatusCode.BadRequest);
    }

    private static System.Text.Json.JsonElement DataOrRoot(System.Text.Json.JsonElement root)
    {
        var current = root;

        while (current.ValueKind == System.Text.Json.JsonValueKind.Object &&
               current.TryGetProperty("data", out var nested))
        {
            current = nested;
        }

        return current;
    }
}
