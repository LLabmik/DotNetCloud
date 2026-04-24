using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Services;
using DotNetCloud.Integration.Tests.Infrastructure;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Photos.Events;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace DotNetCloud.Integration.Tests.Api;

[TestClass]
[TestCategory("Integration")]
public sealed class MediaLibraryEndpointIntegrationTests
{
    [TestMethod]
    public async Task GetLibraryPaths_WithSharedMountSources_ReturnsConfiguredSources()
    {
        var userId = Guid.NewGuid();
        var sharedFolderId = Guid.NewGuid();

        using var factory = new DotNetCloudWebApplicationFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            db.UserSettings.Add(new UserSetting
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Module = MediaLibrarySourceSettings.SettingsModule,
                Key = MediaLibrarySourceSettings.GetSourcesKey("photos"),
                Value = MediaLibrarySourceSettings.Serialize(
                [
                    new MediaLibrarySource
                    {
                        SourceKind = MediaLibrarySourceKind.SharedMount,
                        SharedFolderId = sharedFolderId,
                        RelativePath = "Gallery/Live",
                        DisplayPath = "/_DotNetCloud/Gallery/Live",
                        DisplayName = "Live",
                        Enabled = true,
                    }
                ]),
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateAuthenticatedApiClient(userId);

        var response = await client.GetAsync("/api/v1/media-library/paths");
        var root = await ApiAssert.SuccessAsync(response, HttpStatusCode.OK);
        var data = DataOrRoot(root);

        Assert.AreEqual("/_DotNetCloud/Gallery/Live", data.GetProperty("photosPath").GetString());
        Assert.AreEqual(1, data.GetProperty("photosSources").GetArrayLength());
        Assert.AreEqual("Live", data.GetProperty("photosSources")[0].GetProperty("displayName").GetString());
        Assert.AreEqual(sharedFolderId, data.GetProperty("photosSources")[0].GetProperty("sharedFolderId").GetGuid());
    }

    [TestMethod]
    public async Task ScanLibrary_WithUnavailableSharedMount_ReturnsRemovalResultOverCoreHost()
    {
        var userId = Guid.NewGuid();
        var sharedFolderId = Guid.NewGuid();
        var stalePhotoId = Guid.NewGuid();
        var fileServiceMock = new Mock<IFileService>();
        var photoCallbackMock = new Mock<IPhotoIndexingCallback>();

        fileServiceMock
            .Setup(service => service.ResolveMountedNodeAsync(
                sharedFolderId,
                It.Is<string?>(path => string.IsNullOrEmpty(path)),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileNodeDto?)null);

        photoCallbackMock
            .Setup(callback => callback.GetIndexedFileNodeIdsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([stalePhotoId]);
        photoCallbackMock
            .Setup(callback => callback.RemoveDeletedPhotosAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(stalePhotoId)),
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        using var baseFactory = new DotNetCloudWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFileService>();
                services.RemoveAll<IPhotoIndexingCallback>();
                services.AddScoped(_ => fileServiceMock.Object);
                services.AddScoped(_ => photoCallbackMock.Object);
            });
        });

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            db.UserSettings.Add(new UserSetting
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Module = MediaLibrarySourceSettings.SettingsModule,
                Key = MediaLibrarySourceSettings.GetSourcesKey("photos"),
                Value = MediaLibrarySourceSettings.Serialize(
                [
                    new MediaLibrarySource
                    {
                        SourceKind = MediaLibrarySourceKind.SharedMount,
                        SharedFolderId = sharedFolderId,
                        DisplayPath = "/_DotNetCloud/Archive",
                        DisplayName = "Archive",
                        Enabled = true,
                    }
                ]),
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        using var client = CreateAuthenticatedClient(factory, userId);

        var response = await client.PostAsJsonAsync("/api/v1/media-library/scan", new { mediaType = "Photos" });
        var root = await ApiAssert.SuccessAsync(response, HttpStatusCode.OK);
        var data = DataOrRoot(root);

        Assert.AreEqual(0, data.GetProperty("totalFound").GetInt32());
        Assert.AreEqual(0, data.GetProperty("imported").GetInt32());
        Assert.AreEqual(1, data.GetProperty("removed").GetInt32());
        Assert.AreEqual(1, data.GetProperty("errors").GetArrayLength());
        StringAssert.Contains(data.GetProperty("errors")[0].GetString() ?? string.Empty, "/_DotNetCloud/Archive");

        fileServiceMock.Verify(
            service => service.ResolveMountedNodeAsync(
                sharedFolderId,
                It.Is<string?>(path => string.IsNullOrEmpty(path)),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        photoCallbackMock.Verify(
            callback => callback.RemoveDeletedPhotosAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(stalePhotoId)),
                userId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<DotNetCloud.Core.Server.Program> factory, Guid userId)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("x-test-user-id", userId.ToString());
        return client;
    }

    private static JsonElement DataOrRoot(JsonElement root)
    {
        var current = root;

        while (current.ValueKind == JsonValueKind.Object &&
               current.TryGetProperty("data", out var nested))
        {
            current = nested;
        }

        return current;
    }
}