using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Media;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Music.Events;
using DotNetCloud.Modules.Photos.Events;
using DotNetCloud.Modules.Video.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="AdminSharedFolderCleanupService"/>.
/// </summary>
[TestClass]
public sealed class AdminSharedFolderCleanupServiceTests
{
    private static readonly string[] MediaTypes = ["photos", "music", "video"];

    [TestMethod]
    public async Task HandleAsync_NoMountedEntries_CompletesSuccessfully()
    {
        // ---- ARRANGE ----
        var (provider, _, _) = CreateTestFixture();
        var service = provider.GetRequiredService<AdminSharedFolderCleanupService>();

        var evt = new AdminSharedFolderDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SharedFolderId = Guid.CreateVersion7(),
            DisplayName = "Test Share",
            MountedEntries = [],
        };

        // ---- ACT ----
        await service.HandleAsync(evt);

        // ---- ASSERT ----
        // Should complete without exceptions when there are no mounted entries
    }

    [TestMethod]
    public async Task HandleAsync_WithMountedEntries_ProcessesWithoutCallbacks_Completes()
    {
        // ---- ARRANGE ----
        var (provider, _, _) = CreateTestFixture();
        var service = provider.GetRequiredService<AdminSharedFolderCleanupService>();

        var sharedFolderId = Guid.CreateVersion7();
        var evt = new AdminSharedFolderDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SharedFolderId = sharedFolderId,
            DisplayName = "Music Share",
            MountedEntries =
            [
                new MountedEntryInfo { RelativePath = "song1.mp3", IsDirectory = false },
                new MountedEntryInfo { RelativePath = "song2.mp3", IsDirectory = false },
                new MountedEntryInfo { RelativePath = "album-art", IsDirectory = true },
            ],
        };

        // ---- ACT ----
        await service.HandleAsync(evt);

        // ---- ASSERT ----
        // Should complete without exceptions even when no callbacks are registered
    }

    [TestMethod]
    public async Task HandleAsync_WithMusicCallback_RemovesDeletedTracks()
    {
        // ---- ARRANGE ----
        var sharedFolderId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var fileNodeId = Guid.CreateVersion7();

        var musicCallbackMock = new Mock<IMusicIndexingCallback>();
        musicCallbackMock
            .Setup(callback => callback.RemoveDeletedTracksAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var (provider, _, _) = CreateTestFixture(musicCallback: musicCallbackMock.Object);

        // Seed a user setting with a media source referencing the shared folder
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            db.UserSettings.Add(new Core.Data.Entities.Settings.UserSetting
            {
                UserId = userId,
                Module = MediaLibrarySourceSettings.SettingsModule,
                Key = "music-sources",
                Value = MediaLibrarySourceSettings.Serialize([
                    new MediaLibrarySource
                    {
                        SourceKind = MediaLibrarySourceKind.SharedMount,
                        SharedFolderId = sharedFolderId,
                        DisplayPath = "/_DotNetCloud/Music",
                        DisplayName = "Music",
                        Enabled = true,
                    },
                ]),
            });
            await db.SaveChangesAsync();
        }

        var service = provider.GetRequiredService<AdminSharedFolderCleanupService>();
        var evt = new AdminSharedFolderDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SharedFolderId = sharedFolderId,
            DisplayName = "Music Share",
            MountedEntries =
            [
                new MountedEntryInfo { RelativePath = $"file::{fileNodeId}", IsDirectory = false },
            ],
        };

        // ---- ACT ----
        await service.HandleAsync(evt);

        // ---- ASSERT ----
        musicCallbackMock.Verify(
            callback => callback.RemoveDeletedTracksAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                userId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_WithVideoCallback_RemovesDeletedVideos()
    {
        // ---- ARRANGE ----
        var sharedFolderId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var videoCallbackMock = new Mock<IVideoIndexingCallback>();
        videoCallbackMock
            .Setup(callback => callback.RemoveDeletedVideosAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var (provider, _, _) = CreateTestFixture(videoCallback: videoCallbackMock.Object);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            db.UserSettings.Add(new Core.Data.Entities.Settings.UserSetting
            {
                UserId = userId,
                Module = MediaLibrarySourceSettings.SettingsModule,
                Key = "video-sources",
                Value = MediaLibrarySourceSettings.Serialize([
                    new MediaLibrarySource
                    {
                        SourceKind = MediaLibrarySourceKind.SharedMount,
                        SharedFolderId = sharedFolderId,
                        DisplayPath = "/_DotNetCloud/Videos",
                        DisplayName = "Videos",
                        Enabled = true,
                    },
                ]),
            });
            await db.SaveChangesAsync();
        }

        var service = provider.GetRequiredService<AdminSharedFolderCleanupService>();
        var evt = new AdminSharedFolderDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SharedFolderId = sharedFolderId,
            DisplayName = "Video Share",
            MountedEntries =
            [
                new MountedEntryInfo { RelativePath = "movie1.mp4", IsDirectory = false },
            ],
        };

        // ---- ACT ----
        await service.HandleAsync(evt);

        // ---- ASSERT ----
        videoCallbackMock.Verify(
            callback => callback.RemoveDeletedVideosAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                userId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_WithPhotoCallback_RemovesDeletedPhotos()
    {
        // ---- ARRANGE ----
        var sharedFolderId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var photoCallbackMock = new Mock<IPhotoIndexingCallback>();
        photoCallbackMock
            .Setup(callback => callback.RemoveDeletedPhotosAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var (provider, _, _) = CreateTestFixture(photoCallback: photoCallbackMock.Object);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            db.UserSettings.Add(new Core.Data.Entities.Settings.UserSetting
            {
                UserId = userId,
                Module = MediaLibrarySourceSettings.SettingsModule,
                Key = "photos-sources",
                Value = MediaLibrarySourceSettings.Serialize([
                    new MediaLibrarySource
                    {
                        SourceKind = MediaLibrarySourceKind.SharedMount,
                        SharedFolderId = sharedFolderId,
                        DisplayPath = "/_DotNetCloud/Photos",
                        DisplayName = "Photos",
                        Enabled = true,
                    },
                ]),
            });
            await db.SaveChangesAsync();
        }

        var service = provider.GetRequiredService<AdminSharedFolderCleanupService>();
        var evt = new AdminSharedFolderDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SharedFolderId = sharedFolderId,
            DisplayName = "Photo Share",
            MountedEntries =
            [
                new MountedEntryInfo { RelativePath = "photo1.jpg", IsDirectory = false },
            ],
        };

        // ---- ACT ----
        await service.HandleAsync(evt);

        // ---- ASSERT ----
        photoCallbackMock.Verify(
            callback => callback.RemoveDeletedPhotosAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                userId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_MultipleAffectedUsers_CleansEachUser()
    {
        // ---- ARRANGE ----
        var sharedFolderId = Guid.CreateVersion7();
        var user1Id = Guid.CreateVersion7();
        var user2Id = Guid.CreateVersion7();

        var musicCallbackMock = new Mock<IMusicIndexingCallback>();
        musicCallbackMock
            .Setup(callback => callback.RemoveDeletedTracksAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var (provider, settingsServiceMock, _) = CreateTestFixture(musicCallback: musicCallbackMock.Object);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            // User1 has both a shared mount source (matching) and an owned source (should be preserved)
            db.UserSettings.Add(new Core.Data.Entities.Settings.UserSetting
            {
                UserId = user1Id,
                Module = MediaLibrarySourceSettings.SettingsModule,
                Key = "music-sources",
                Value = MediaLibrarySourceSettings.Serialize([
                    new MediaLibrarySource
                    {
                        SourceKind = MediaLibrarySourceKind.SharedMount,
                        SharedFolderId = sharedFolderId,
                        DisplayPath = "/_DotNetCloud/Music",
                        DisplayName = "Music",
                        Enabled = true,
                    },
                    new MediaLibrarySource
                    {
                        SourceKind = MediaLibrarySourceKind.OwnedFileNode,
                        FolderId = Guid.CreateVersion7(),
                        DisplayPath = "/My Music",
                        DisplayName = "My Music",
                        Enabled = true,
                    },
                ]),
            });
            // User2 only has a shared mount source (should be fully removed)
            db.UserSettings.Add(new Core.Data.Entities.Settings.UserSetting
            {
                UserId = user2Id,
                Module = MediaLibrarySourceSettings.SettingsModule,
                Key = "music-sources",
                Value = MediaLibrarySourceSettings.Serialize([
                    new MediaLibrarySource
                    {
                        SourceKind = MediaLibrarySourceKind.SharedMount,
                        SharedFolderId = sharedFolderId,
                        DisplayPath = "/_DotNetCloud/Music",
                        DisplayName = "Music",
                        Enabled = true,
                    },
                ]),
            });
            await db.SaveChangesAsync();
        }

        var service = provider.GetRequiredService<AdminSharedFolderCleanupService>();
        var evt = new AdminSharedFolderDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SharedFolderId = sharedFolderId,
            DisplayName = "Music Share",
            MountedEntries =
            [
                new MountedEntryInfo { RelativePath = "song.mp3", IsDirectory = false },
            ],
        };

        // ---- ACT ----
        await service.HandleAsync(evt);

        // ---- ASSERT ----
        // Both users should have tracks removed once each
        musicCallbackMock.Verify(
            callback => callback.RemoveDeletedTracksAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        // Verify that UpsertSettingAsync was called for user1's settings
        // (the owned source should still be there, shared mount removed)
        settingsServiceMock.Verify(
            s => s.UpsertSettingAsync(
                user1Id,
                MediaLibrarySourceSettings.SettingsModule,
                "music-sources",
                It.Is<UpsertUserSettingDto>(dto =>
                    dto.Value.Contains("OwnedFileNode") &&
                    !dto.Value.Contains(sharedFolderId.ToString()))),
            Times.Once);

        // Verify that UpsertSettingAsync was called for user2's settings
        // (the shared mount was removed, setting saved with empty sources)
        settingsServiceMock.Verify(
            s => s.UpsertSettingAsync(
                user2Id,
                MediaLibrarySourceSettings.SettingsModule,
                "music-sources",
                It.IsAny<UpsertUserSettingDto>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_StatusReporter_MarksCompletedOnSuccess()
    {
        // ---- ARRANGE ----
        var statusReporterMock = new Mock<ICleanupStatusReporter>();
        var (provider, settingsService, dbContext) = CreateTestFixture(statusReporter: statusReporterMock.Object);

        var sharedFolderId = Guid.CreateVersion7();
        var evt = new AdminSharedFolderDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SharedFolderId = sharedFolderId,
            DisplayName = "Test",
            MountedEntries = [],
        };

        var service = provider.GetRequiredService<AdminSharedFolderCleanupService>();

        // ---- ACT ----
        await service.HandleAsync(evt);

        // ---- ASSERT ----
        statusReporterMock.Verify(
            reporter => reporter.MarkCompletedAsync(evt.EventId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_StatusReporter_MarksFailedOnException()
    {
        // ---- ARRANGE ----
        var sharedFolderId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var statusReporterMock = new Mock<ICleanupStatusReporter>();
        var (provider, settingsServiceMock, _) = CreateTestFixture(statusReporter: statusReporterMock.Object);

        // Configure the settings service to throw when saving — this simulates a
        // DB failure during the media source cleanup phase
        settingsServiceMock
            .Setup(s => s.UpsertSettingAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<UpsertUserSettingDto>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Seed a user setting that references the shared folder
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            db.UserSettings.Add(new Core.Data.Entities.Settings.UserSetting
            {
                UserId = userId,
                Module = MediaLibrarySourceSettings.SettingsModule,
                Key = "music-sources",
                Value = MediaLibrarySourceSettings.Serialize([
                    new MediaLibrarySource
                    {
                        SourceKind = MediaLibrarySourceKind.SharedMount,
                        SharedFolderId = sharedFolderId,
                        DisplayPath = "/_DotNetCloud/Music",
                        DisplayName = "Music",
                        Enabled = true,
                    },
                ]),
            });
            await db.SaveChangesAsync();
        }

        var evt = new AdminSharedFolderDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SharedFolderId = sharedFolderId,
            DisplayName = "Fail Share",
            MountedEntries =
            [
                new MountedEntryInfo { RelativePath = "song.mp3", IsDirectory = false },
            ],
        };

        var service = provider.GetRequiredService<AdminSharedFolderCleanupService>();

        // ---- ACT & ASSERT ----
        // The SaveSourcesAsync calls UpsertSettingAsync which is configured to throw
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.HandleAsync(evt));

        // ---- ASSERT ----
        // The status reporter should be called with MarkFailedAsync when an exception occurs
        statusReporterMock.Verify(
            reporter => reporter.MarkFailedAsync(evt.EventId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Creates a test fixture with CoreDbContext, mocked IUserSettingsService, and
    /// optional mocked callbacks registered in the DI container.
    /// </summary>
    private static (ServiceProvider Provider, Mock<IUserSettingsService> SettingsServiceMock, CoreDbContext DbContext) CreateTestFixture(
        IMusicIndexingCallback? musicCallback = null,
        IVideoIndexingCallback? videoCallback = null,
        IPhotoIndexingCallback? photoCallback = null,
        ICleanupStatusReporter? statusReporter = null)
    {
        var dbName = Guid.CreateVersion7().ToString();
        var services = new ServiceCollection();

        // Register logging infrastructure
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Register ITableNamingStrategy (required by CoreDbContext constructor)
        services.AddSingleton<DotNetCloud.Core.Data.Naming.ITableNamingStrategy>(
            new DotNetCloud.Core.Data.Naming.PostgreSqlNamingStrategy());

        // Register CoreDbContext with in-memory provider using explicit factory
        services.AddSingleton<CoreDbContext>(sp =>
        {
            var options = new DbContextOptionsBuilder<CoreDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            var namingStrategy = sp.GetRequiredService<DotNetCloud.Core.Data.Naming.ITableNamingStrategy>();
            return new CoreDbContext(options, namingStrategy);
        });

        // Register mocked IUserSettingsService
        var settingsServiceMock = new Mock<IUserSettingsService>();
        services.AddScoped(_ => settingsServiceMock.Object);

        // Register optional callbacks
        if (musicCallback is not null)
            services.AddScoped(_ => musicCallback);
        if (videoCallback is not null)
            services.AddScoped(_ => videoCallback);
        if (photoCallback is not null)
            services.AddScoped(_ => photoCallback);

        // Register optional status reporter
        if (statusReporter is not null)
            services.AddScoped(_ => statusReporter);

        // Register the cleanup service itself
        services.AddSingleton<AdminSharedFolderCleanupService>();

        var provider = services.BuildServiceProvider();
        var dbContext = provider.GetRequiredService<CoreDbContext>();

        return (provider, settingsServiceMock, dbContext);
    }
}
