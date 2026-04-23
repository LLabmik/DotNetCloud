using System.Security.Claims;
using System.Text.Json;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Server.Controllers;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Photos.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Controllers;

[TestClass]
public sealed class MediaLibraryControllerTests
{
    [TestMethod]
    public async Task GetLibraryPathsAsync_WithSharedMountSources_ReturnsConfiguredSources()
    {
        var userId = Guid.NewGuid();
        var sharedFolderId = Guid.NewGuid();
        var settingsMock = new Mock<IUserSettingsService>(MockBehavior.Strict);
        SetupSources(settingsMock, userId, "photos",
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
        ]);
        SetupNoSources(settingsMock, userId, "music");
        SetupNoSources(settingsMock, userId, "video");

        using var provider = CreateServiceProvider(Guid.NewGuid().ToString(), Mock.Of<IFileService>(), Mock.Of<IPhotoIndexingCallback>());
        var controller = CreateController(settingsMock.Object, CreateImportService(provider));
        SetCurrentUser(controller, userId);

        var result = await controller.GetLibraryPathsAsync();

        var ok = AssertResult<OkObjectResult>(result);
        var payload = ToJson(ok.Value);
        var data = payload.GetProperty("data");

        Assert.IsTrue(payload.GetProperty("success").GetBoolean());
        Assert.AreEqual("/_DotNetCloud/Gallery/Live", data.GetProperty("PhotosPath").GetString());
        Assert.AreEqual(string.Empty, data.GetProperty("MusicPath").GetString());
        Assert.AreEqual(1, data.GetProperty("PhotosSources").GetArrayLength());
        Assert.AreEqual("/_DotNetCloud/Gallery/Live", data.GetProperty("PhotosSources")[0].GetProperty("DisplayPath").GetString());
        Assert.AreEqual("Live", data.GetProperty("PhotosSources")[0].GetProperty("DisplayName").GetString());
        Assert.AreEqual(sharedFolderId.ToString("D"), data.GetProperty("PhotosSources")[0].GetProperty("SharedFolderId").GetString());
    }

    [TestMethod]
    public async Task ScanLibraryAsync_WithUnavailableSharedMount_ReturnsRemovalResult()
    {
        var userId = Guid.NewGuid();
        var sharedFolderId = Guid.NewGuid();
        var stalePhotoId = Guid.NewGuid();
        var settingsMock = new Mock<IUserSettingsService>(MockBehavior.Strict);
        SetupSources(settingsMock, userId, "photos",
        [
            new MediaLibrarySource
            {
                SourceKind = MediaLibrarySourceKind.SharedMount,
                SharedFolderId = sharedFolderId,
                DisplayPath = "/_DotNetCloud/Archive",
                DisplayName = "Archive",
                Enabled = true,
            }
        ]);

        var fileServiceMock = new Mock<IFileService>();
        fileServiceMock
            .Setup(service => service.ResolveMountedNodeAsync(
                sharedFolderId,
                It.Is<string?>(path => string.IsNullOrEmpty(path)),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileNodeDto?)null);

        var photoCallbackMock = new Mock<IPhotoIndexingCallback>();
        photoCallbackMock
            .Setup(callback => callback.GetIndexedFileNodeIdsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([stalePhotoId]);
        photoCallbackMock
            .Setup(callback => callback.RemoveDeletedPhotosAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(stalePhotoId)),
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        using var provider = CreateServiceProvider(Guid.NewGuid().ToString(), fileServiceMock.Object, photoCallbackMock.Object);
        var controller = CreateController(settingsMock.Object, CreateImportService(provider));
        SetCurrentUser(controller, userId);

        var result = await controller.ScanLibraryAsync(new MediaLibraryScanRequestDto { MediaType = "Photos" });

        var ok = AssertResult<OkObjectResult>(result);
        var payload = ToJson(ok.Value);
        var data = payload.GetProperty("data");

        Assert.IsTrue(payload.GetProperty("success").GetBoolean());
        Assert.AreEqual(0, data.GetProperty("TotalFound").GetInt32());
        Assert.AreEqual(0, data.GetProperty("Imported").GetInt32());
        Assert.AreEqual(1, data.GetProperty("Removed").GetInt32());
        Assert.AreEqual(1, data.GetProperty("Errors").GetArrayLength());
        StringAssert.Contains(data.GetProperty("Errors")[0].GetString() ?? string.Empty, "/_DotNetCloud/Archive");

        fileServiceMock.Verify(
            service => service.ResolveMountedNodeAsync(
                sharedFolderId,
                It.Is<string?>(path => string.IsNullOrEmpty(path)),
                It.IsAny<DotNetCloud.Core.Authorization.CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static void SetupSources(Mock<IUserSettingsService> settingsMock, Guid userId, string mediaType, IReadOnlyList<MediaLibrarySource> sources)
    {
        settingsMock
            .Setup(service => service.GetSettingAsync(userId, MediaLibrarySourceSettings.SettingsModule, MediaLibrarySourceSettings.GetSourcesKey(mediaType)))
            .ReturnsAsync(new UserSettingDto
            {
                UserId = userId,
                Module = MediaLibrarySourceSettings.SettingsModule,
                Key = MediaLibrarySourceSettings.GetSourcesKey(mediaType),
                Value = MediaLibrarySourceSettings.Serialize(sources),
            });
    }

    private static void SetupNoSources(Mock<IUserSettingsService> settingsMock, Guid userId, string mediaType)
    {
        settingsMock
            .Setup(service => service.GetSettingAsync(userId, MediaLibrarySourceSettings.SettingsModule, MediaLibrarySourceSettings.GetSourcesKey(mediaType)))
            .ReturnsAsync((UserSettingDto?)null);
        settingsMock
            .Setup(service => service.GetSettingAsync(userId, MediaLibrarySourceSettings.SettingsModule, MediaLibrarySourceSettings.GetLegacyPathKey(mediaType)))
            .ReturnsAsync((UserSettingDto?)null);
        settingsMock
            .Setup(service => service.GetSettingAsync(userId, MediaLibrarySourceSettings.SettingsModule, MediaLibrarySourceSettings.GetLegacyFolderIdKey(mediaType)))
            .ReturnsAsync((UserSettingDto?)null);
    }

    private static ServiceProvider CreateServiceProvider(string dbName, IFileService fileService, IPhotoIndexingCallback photoCallback)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FilesDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped(_ => fileService);
        services.AddScoped(_ => photoCallback);
        return services.BuildServiceProvider();
    }

    private static MediaFolderImportService CreateImportService(ServiceProvider provider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Files:Storage:RootPath"] = Path.GetTempPath(),
            })
            .Build();

        return new MediaFolderImportService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<IFileStorageEngine>(),
            Mock.Of<DotNetCloud.Core.Events.IEventBus>(),
            configuration,
            NullLogger<MediaFolderImportService>.Instance);
    }

    private static MediaLibraryController CreateController(IUserSettingsService settingsService, MediaFolderImportService importService)
    {
        return new MediaLibraryController(settingsService, importService, NullLogger<MediaLibraryController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };
    }

    private static void SetCurrentUser(MediaLibraryController controller, Guid userId)
    {
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        ],
        "TestAuth"));
    }

    private static T AssertResult<T>(IActionResult result)
        where T : IActionResult
    {
        Assert.IsInstanceOfType<T>(result);
        return (T)result;
    }

    private static JsonElement ToJson(object? value)
        => JsonSerializer.SerializeToElement(value);
}