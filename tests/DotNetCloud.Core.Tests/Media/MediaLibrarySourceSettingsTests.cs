using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DotNetCloud.Core.Tests.Media;

/// <summary>
/// Tests for media-library source settings serialization and legacy migration.
/// </summary>
[TestClass]
public sealed class MediaLibrarySourceSettingsTests
{
    [TestMethod]
    public void Normalize_DuplicateOwnedAndSharedSources_DeduplicatesAndNormalizesValues()
    {
        var ownedFolderId = Guid.NewGuid();
        var sharedFolderId = Guid.NewGuid();

        var normalized = MediaLibrarySourceSettings.Normalize(
        [
            new MediaLibrarySource
            {
                SourceKind = MediaLibrarySourceKind.OwnedFileNode,
                FolderId = ownedFolderId,
                DisplayPath = " /Music ",
                DisplayName = "",
                Enabled = true,
            },
            new MediaLibrarySource
            {
                SourceKind = MediaLibrarySourceKind.OwnedFileNode,
                FolderId = ownedFolderId,
                DisplayPath = "/Music",
                DisplayName = "Music",
                Enabled = true,
            },
            new MediaLibrarySource
            {
                SourceKind = MediaLibrarySourceKind.SharedMount,
                SharedFolderId = sharedFolderId,
                RelativePath = "/albums/live/",
                DisplayPath = "/_DotNetCloud/Live",
                DisplayName = "Live",
                Enabled = true,
            },
            new MediaLibrarySource
            {
                SourceKind = MediaLibrarySourceKind.SharedMount,
                SharedFolderId = sharedFolderId,
                RelativePath = "albums/live",
                DisplayPath = "/_DotNetCloud/Live",
                DisplayName = "Live",
                Enabled = true,
            }
        ]);

        Assert.AreEqual(2, normalized.Count);
        Assert.AreEqual("Music", normalized[0].DisplayName);
        Assert.AreEqual("/Music", normalized[0].DisplayPath);
        Assert.AreEqual("albums/live", normalized[1].RelativePath);
    }

    [TestMethod]
    public async Task LoadSourcesAsync_LegacyFolderSettings_ReturnsOwnedSource()
    {
        var userId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var settingsService = new Mock<IUserSettingsService>(MockBehavior.Strict);

        settingsService
            .Setup(service => service.GetSettingAsync(userId, MediaLibrarySourceSettings.SettingsModule, "music-sources"))
            .ReturnsAsync((UserSettingDto?)null);
        settingsService
            .Setup(service => service.GetSettingAsync(userId, MediaLibrarySourceSettings.SettingsModule, "music-path"))
            .ReturnsAsync(new UserSettingDto { UserId = userId, Module = MediaLibrarySourceSettings.SettingsModule, Key = "music-path", Value = "/Music/Albums" });
        settingsService
            .Setup(service => service.GetSettingAsync(userId, MediaLibrarySourceSettings.SettingsModule, "music-folder-id"))
            .ReturnsAsync(new UserSettingDto { UserId = userId, Module = MediaLibrarySourceSettings.SettingsModule, Key = "music-folder-id", Value = folderId.ToString("D") });

        var sources = await MediaLibrarySourceSettings.LoadSourcesAsync(settingsService.Object, userId, "music");

        Assert.AreEqual(1, sources.Count);
        Assert.AreEqual(MediaLibrarySourceKind.OwnedFileNode, sources[0].SourceKind);
        Assert.AreEqual(folderId, sources[0].FolderId);
        Assert.AreEqual("/Music/Albums", sources[0].DisplayPath);
        Assert.AreEqual("Albums", sources[0].DisplayName);
    }

    [TestMethod]
    public async Task SaveSourcesAsync_ValidSources_PersistsJsonToModuleSourcesKey()
    {
        var userId = Guid.NewGuid();
        var settingsService = new Mock<IUserSettingsService>(MockBehavior.Strict);
        UpsertUserSettingDto? capturedDto = null;

        settingsService
            .Setup(service => service.UpsertSettingAsync(userId, MediaLibrarySourceSettings.SettingsModule, "photos-sources", It.IsAny<UpsertUserSettingDto>()))
            .Callback<Guid, string, string, UpsertUserSettingDto>((_, _, _, dto) => capturedDto = dto)
            .ReturnsAsync(new UserSettingDto { UserId = userId, Module = MediaLibrarySourceSettings.SettingsModule, Key = "photos-sources", Value = string.Empty });

        await MediaLibrarySourceSettings.SaveSourcesAsync(
            settingsService.Object,
            userId,
            "photos",
            [
                new MediaLibrarySource
                {
                    SourceKind = MediaLibrarySourceKind.SharedMount,
                    SharedFolderId = Guid.NewGuid(),
                    RelativePath = "gallery",
                    DisplayPath = "/_DotNetCloud/Gallery",
                    DisplayName = "Gallery",
                    Enabled = true,
                }
            ],
            "Photos library scan sources");

        Assert.IsNotNull(capturedDto);
        Assert.AreEqual("Photos library scan sources", capturedDto.Description);
        Assert.IsTrue(capturedDto.Value.Contains("SharedMount", StringComparison.Ordinal));
        Assert.IsTrue(capturedDto.Value.Contains("gallery", StringComparison.Ordinal));
    }
}