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
        var ownedFolderId = Guid.CreateVersion7();
        var sharedFolderId = Guid.CreateVersion7();

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
        var userId = Guid.CreateVersion7();
        var folderId = Guid.CreateVersion7();
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
    public async Task LoadSourcesAsync_EmptyPersistedSourcesList_ReturnsEmptyWithoutLegacyFallback()
    {
        var userId = Guid.CreateVersion7();
        var settingsService = new Mock<IUserSettingsService>(MockBehavior.Strict);

        // The JSON-backed key exists but holds an empty list (user removed all sources).
        settingsService
            .Setup(service => service.GetSettingAsync(userId, MediaLibrarySourceSettings.SettingsModule, "music-sources"))
            .ReturnsAsync(new UserSettingDto
            {
                UserId = userId,
                Module = MediaLibrarySourceSettings.SettingsModule,
                Key = "music-sources",
                Value = MediaLibrarySourceSettings.Serialize([])
            });

        var sources = await MediaLibrarySourceSettings.LoadSourcesAsync(settingsService.Object, userId, "music");

        Assert.AreEqual(0, sources.Count);
    }

    [TestMethod]
    public async Task LoadSourcesAsync_PersistedSources_IgnoreLegacySinglePathSettings()
    {
        var userId = Guid.CreateVersion7();
        var folderId = Guid.CreateVersion7();
        var settingsService = new Mock<IUserSettingsService>(MockBehavior.Strict);

        settingsService
            .Setup(service => service.GetSettingAsync(userId, MediaLibrarySourceSettings.SettingsModule, "music-sources"))
            .ReturnsAsync(new UserSettingDto
            {
                UserId = userId,
                Module = MediaLibrarySourceSettings.SettingsModule,
                Key = "music-sources",
                Value = MediaLibrarySourceSettings.Serialize(
                [
                    new MediaLibrarySource
                    {
                        SourceKind = MediaLibrarySourceKind.OwnedFileNode,
                        FolderId = folderId,
                        DisplayPath = "/Music/New",
                        DisplayName = "New",
                        Enabled = true,
                    }
                ])
            });

        var sources = await MediaLibrarySourceSettings.LoadSourcesAsync(settingsService.Object, userId, "music");

        Assert.AreEqual(1, sources.Count);
        Assert.AreEqual(folderId, sources[0].FolderId);
        Assert.AreEqual("/Music/New", sources[0].DisplayPath);
    }

    [TestMethod]
    public async Task SaveSourcesAsync_EmptyListAfterRemovingLastSource_DoesNotResurrectLegacySource()
    {
        var userId = Guid.CreateVersion7();
        var legacyFolderId = Guid.CreateVersion7();
        var addedFolderId = Guid.CreateVersion7();
        var store = new Dictionary<string, UserSettingDto>
        {
            ["media-library:music-path"] = new UserSettingDto { UserId = userId, Module = MediaLibrarySourceSettings.SettingsModule, Key = "music-path", Value = "/Music/Legacy" },
            ["media-library:music-folder-id"] = new UserSettingDto { UserId = userId, Module = MediaLibrarySourceSettings.SettingsModule, Key = "music-folder-id", Value = legacyFolderId.ToString("D") }
        };
        var settingsService = CreateInMemorySettingsService(store);

        // Fresh user (no JSON sources key yet) → the legacy single-folder source is returned.
        var legacy = await MediaLibrarySourceSettings.LoadSourcesAsync(settingsService.Object, userId, "music");
        Assert.AreEqual(1, legacy.Count);
        Assert.AreEqual(legacyFolderId, legacy[0].FolderId);

        // Adding a source auto-saves the JSON-backed key.
        await MediaLibrarySourceSettings.SaveSourcesAsync(
            settingsService.Object,
            userId,
            "music",
            [
                new MediaLibrarySource
                {
                    SourceKind = MediaLibrarySourceKind.OwnedFileNode,
                    FolderId = addedFolderId,
                    DisplayPath = "/Music/Added",
                    DisplayName = "Added",
                    Enabled = true,
                }
            ]);

        // Removing the last source auto-saves an empty list.
        await MediaLibrarySourceSettings.SaveSourcesAsync(settingsService.Object, userId, "music", []);

        var afterRemoval = await MediaLibrarySourceSettings.LoadSourcesAsync(settingsService.Object, userId, "music");

        Assert.AreEqual(0, afterRemoval.Count);
    }

    [TestMethod]
    public async Task SaveSourcesAsync_ValidSources_PersistsJsonToModuleSourcesKey()
    {
        var userId = Guid.CreateVersion7();
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
                    SharedFolderId = Guid.CreateVersion7(),
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

    /// <summary>
    /// Builds a settings service backed by an in-memory dictionary so tests can exercise the full
    /// Save → Load round trip (including legacy-key fallback behavior) without a database.
    /// </summary>
    private static Mock<IUserSettingsService> CreateInMemorySettingsService(Dictionary<string, UserSettingDto> store)
    {
        var mock = new Mock<IUserSettingsService>(MockBehavior.Strict);
        mock.Setup(service => service.GetSettingAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((Guid _, string module, string key) =>
                store.TryGetValue($"{module}:{key}", out var dto) ? dto : null);
        mock.Setup(service => service.UpsertSettingAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UpsertUserSettingDto>()))
            .ReturnsAsync((Guid userId, string module, string key, UpsertUserSettingDto dto) =>
            {
                var saved = new UserSettingDto
                {
                    UserId = userId,
                    Module = module,
                    Key = key,
                    Value = dto.Value,
                    Description = dto.Description,
                    IsSensitive = dto.IsSensitive,
                };
                store[$"{module}:{key}"] = saved;
                return saved;
            });
        return mock;
    }
}
