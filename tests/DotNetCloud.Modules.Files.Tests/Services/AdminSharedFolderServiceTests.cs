using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Models;
using AdminSharedFolderDeletedEvent = DotNetCloud.Core.Events.AdminSharedFolderDeletedEvent;
using IEventBus = DotNetCloud.Core.Events.IEventBus;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Data.Services.Background;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using DotNetCloud.Modules.Search.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class AdminSharedFolderServiceTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.CreateVersion7().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static CallerContext AdminCaller(Guid userId)
        => new(userId, ["Admin"], CallerType.User);

    private static AdminSharedFolderService CreateService(
        FilesDbContext db,
        string rootPath,
        Mock<IUserOrganizationResolver>? userOrganizationResolver = null,
        Mock<IGroupDirectory>? groupDirectory = null,
        IAdminSharedFolderMaintenanceScheduler? maintenanceScheduler = null,
        Mock<ISearchFtsClient>? searchClient = null,
        Mock<IEventBus>? eventBus = null,
        Mock<ICoreCapabilitiesClient>? coreClient = null)
    {
        var validator = new AdminSharedFolderPathValidator(
            db,
            Microsoft.Extensions.Options.Options.Create(new AdminSharedFolderOptions
            {
                RootPath = rootPath,
            }));

        return new AdminSharedFolderService(
            db,
            validator,
            coreDb: null,
            userOrganizationResolver?.Object,
            groupDirectory?.Object,
            maintenanceScheduler,
            searchClient?.Object,
            eventBus?.Object,
            coreClient?.Object,
            NullLogger<AdminSharedFolderService>.Instance);
    }

    [TestMethod]
    public async Task CreateSharedFolderAsync_ValidDefinition_PersistsCanonicalPathAndGrants()
    {
        var rootPath = CreateTempDirectory();
        var callerId = Guid.CreateVersion7();
        var organizationId = Guid.CreateVersion7();
        var grantedGroupId = Guid.CreateVersion7();

        try
        {
            var canonicalPath = Directory.CreateDirectory(Path.Combine(rootPath, "media", "albums")).FullName;
            using var db = CreateContext();
            var orgResolver = new Mock<IUserOrganizationResolver>();
            orgResolver.Setup(resolver => resolver.GetOrganizationIdAsync(callerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(organizationId);

            var groupDirectory = new Mock<IGroupDirectory>();
            groupDirectory.Setup(directory => directory.GetGroupAsync(grantedGroupId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GroupInfo
                {
                    Id = grantedGroupId,
                    Name = "Media Team",
                    OrganizationId = organizationId,
                    MemberCount = 12,
                    CreatedAt = DateTime.UtcNow,
                });

            var service = CreateService(db, rootPath, orgResolver, groupDirectory);

            var scheduleLowerBound = DateTime.UtcNow.AddHours(24).AddMinutes(-2);

            var result = await service.CreateSharedFolderAsync(new CreateAdminSharedFolderDto
            {
                DisplayName = "Media",
                SourcePath = Path.Combine("media", "albums"),
                GroupIds = [grantedGroupId],
            }, AdminCaller(callerId));

            var scheduleUpperBound = DateTime.UtcNow.AddHours(24).AddMinutes(2);

            Assert.AreEqual("Media", result.DisplayName);
            Assert.AreEqual(Path.GetFullPath(canonicalPath), result.SourcePath);
            Assert.AreEqual(organizationId, result.OrganizationId);
            Assert.AreEqual(1, result.GrantedGroups.Count);
            Assert.AreEqual(grantedGroupId, result.GrantedGroups[0].GroupId);
            Assert.IsTrue(result.NextScheduledScanAt >= scheduleLowerBound);
            Assert.IsTrue(result.NextScheduledScanAt <= scheduleUpperBound);

            var definition = await db.AdminSharedFolders
                .Include(folder => folder.Grants)
                .SingleAsync();

            Assert.AreEqual(Path.GetFullPath(canonicalPath), definition.SourcePath);
            Assert.AreEqual(1, definition.Grants.Count);
            Assert.IsTrue(definition.NextScheduledScanAt >= scheduleLowerBound);
            Assert.IsTrue(definition.NextScheduledScanAt <= scheduleUpperBound);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task CreateSharedFolderAsync_GroupOutsideCallerOrganization_ThrowsValidationException()
    {
        var rootPath = CreateTempDirectory();
        var callerId = Guid.CreateVersion7();
        var callerOrganizationId = Guid.CreateVersion7();
        var otherOrganizationId = Guid.CreateVersion7();
        var grantedGroupId = Guid.CreateVersion7();

        try
        {
            Directory.CreateDirectory(Path.Combine(rootPath, "media"));
            using var db = CreateContext();
            var orgResolver = new Mock<IUserOrganizationResolver>();
            orgResolver.Setup(resolver => resolver.GetOrganizationIdAsync(callerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(callerOrganizationId);

            var groupDirectory = new Mock<IGroupDirectory>();
            groupDirectory.Setup(directory => directory.GetGroupAsync(grantedGroupId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GroupInfo
                {
                    Id = grantedGroupId,
                    Name = "Other Org",
                    OrganizationId = otherOrganizationId,
                    MemberCount = 3,
                    CreatedAt = DateTime.UtcNow,
                });

            var service = CreateService(db, rootPath, orgResolver, groupDirectory);

            await Assert.ThrowsExactlyAsync<ValidationException>(() => service.CreateSharedFolderAsync(new CreateAdminSharedFolderDto
            {
                DisplayName = "Media",
                SourcePath = "media",
                GroupIds = [grantedGroupId],
            }, AdminCaller(callerId)));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task UpdateSharedFolderAsync_ReplacesGrantsAndMutableFields()
    {
        var rootPath = CreateTempDirectory();
        var callerId = Guid.CreateVersion7();
        var organizationId = Guid.CreateVersion7();
        var existingGroupId = Guid.CreateVersion7();
        var replacementGroupId = Guid.CreateVersion7();

        try
        {
            var initialPath = Directory.CreateDirectory(Path.Combine(rootPath, "media")).FullName;
            var updatedPath = Directory.CreateDirectory(Path.Combine(rootPath, "photos")).FullName;
            using var db = CreateContext();
            var definition = new AdminSharedFolderDefinition
            {
                OrganizationId = organizationId,
                DisplayName = "Media",
                SourcePath = Path.GetFullPath(initialPath),
                CreatedByUserId = callerId,
                Grants =
                [
                    new AdminSharedFolderGrant { GroupId = existingGroupId },
                ],
            };
            db.AdminSharedFolders.Add(definition);
            await db.SaveChangesAsync();

            var orgResolver = new Mock<IUserOrganizationResolver>();
            orgResolver.Setup(resolver => resolver.GetOrganizationIdAsync(callerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(organizationId);

            var groupDirectory = new Mock<IGroupDirectory>();
            groupDirectory.Setup(directory => directory.GetGroupAsync(replacementGroupId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GroupInfo
                {
                    Id = replacementGroupId,
                    Name = "Photos",
                    OrganizationId = organizationId,
                    MemberCount = 8,
                    CreatedAt = DateTime.UtcNow,
                });

            var service = CreateService(db, rootPath, orgResolver, groupDirectory);

            var result = await service.UpdateSharedFolderAsync(definition.Id, new UpdateAdminSharedFolderDto
            {
                DisplayName = "Photos",
                SourcePath = "photos",
                IsEnabled = false,
                CrawlMode = "Manual",
                GroupIds = [replacementGroupId],
            }, AdminCaller(callerId));

            Assert.AreEqual("Photos", result.DisplayName);
            Assert.AreEqual(Path.GetFullPath(updatedPath), result.SourcePath);
            Assert.IsFalse(result.IsEnabled);
            Assert.AreEqual("Manual", result.CrawlMode);
            Assert.IsNull(result.NextScheduledScanAt);
            Assert.AreEqual(1, result.GrantedGroups.Count);
            Assert.AreEqual(replacementGroupId, result.GrantedGroups[0].GroupId);

            var persisted = await db.AdminSharedFolders
                .Include(folder => folder.Grants)
                .SingleAsync(folder => folder.Id == definition.Id);

            Assert.AreEqual("Photos", persisted.DisplayName);
            Assert.AreEqual(Path.GetFullPath(updatedPath), persisted.SourcePath);
            Assert.AreEqual(AdminSharedFolderCrawlMode.Manual, persisted.CrawlMode);
            Assert.IsNull(persisted.NextScheduledScanAt);
            Assert.AreEqual(1, persisted.Grants.Count);
            Assert.AreEqual(replacementGroupId, persisted.Grants.Single().GroupId);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task BrowseDirectoriesAsync_ValidPath_ReturnsImmediateSubdirectories()
    {
        var rootPath = CreateTempDirectory();
        var callerId = Guid.CreateVersion7();

        try
        {
            var teamDirectory = Directory.CreateDirectory(Path.Combine(rootPath, "team")).FullName;
            var designDirectory = Directory.CreateDirectory(Path.Combine(teamDirectory, "design-assets")).FullName;
            Directory.CreateDirectory(Path.Combine(designDirectory, "archive"));
            var qaDirectory = Directory.CreateDirectory(Path.Combine(teamDirectory, "qa")).FullName;

            using var db = CreateContext();
            var service = CreateService(db, rootPath);

            var result = await service.BrowseDirectoriesAsync("team", AdminCaller(callerId));

            Assert.AreEqual(Path.GetFullPath(rootPath), result.RootPath);
            Assert.AreEqual(Path.GetFullPath(teamDirectory), result.CurrentPath);
            Assert.AreEqual("team", result.RelativePath);
            CollectionAssert.AreEqual(
                new[] { "design-assets", "qa" },
                result.Directories.Select(directory => directory.Name).ToArray());
            Assert.AreEqual(Path.GetFullPath(designDirectory), result.Directories[0].SourcePath);
            Assert.AreEqual("team/design-assets", result.Directories[0].RelativePath);
            Assert.AreEqual(Path.GetFullPath(qaDirectory), result.Directories[1].SourcePath);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task RequestReindexAsync_MarksDefinitionRequestedAndSchedulesImmediateScan()
    {
        var rootPath = CreateTempDirectory();
        using var db = CreateContext();
        var callerId = Guid.CreateVersion7();
        var scheduler = new RecordingMaintenanceScheduler();
        var definition = new AdminSharedFolderDefinition
        {
            DisplayName = "Media",
            SourcePath = "/mnt/media",
            CreatedByUserId = callerId,
            NextScheduledScanAt = DateTime.UtcNow.AddDays(3),
        };
        db.AdminSharedFolders.Add(definition);
        await db.SaveChangesAsync();

        try
        {
            var service = CreateService(db, rootPath, maintenanceScheduler: scheduler);
            var result = await service.RequestReindexAsync(definition.Id, AdminCaller(callerId));

            Assert.AreEqual("Requested", result.ReindexState);
            Assert.IsTrue(result.NextScheduledScanAt <= DateTime.UtcNow.AddSeconds(5));
            Assert.AreEqual(1, scheduler.TriggerCount);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task ScheduleRescanAsync_TriggersImmediateMaintenanceProcessing()
    {
        var rootPath = CreateTempDirectory();
        using var db = CreateContext();
        var callerId = Guid.CreateVersion7();
        var scheduler = new RecordingMaintenanceScheduler();
        var requestedScanAt = DateTime.UtcNow.AddMinutes(5);
        var definition = new AdminSharedFolderDefinition
        {
            DisplayName = "Media",
            SourcePath = "/mnt/media",
            CreatedByUserId = callerId,
        };
        db.AdminSharedFolders.Add(definition);
        await db.SaveChangesAsync();

        try
        {
            var service = CreateService(db, rootPath, maintenanceScheduler: scheduler);
            var result = await service.ScheduleRescanAsync(definition.Id, requestedScanAt, AdminCaller(callerId));

            Assert.IsTrue(result.NextScheduledScanAt >= requestedScanAt.AddSeconds(-5));
            Assert.IsTrue(result.NextScheduledScanAt <= requestedScanAt.AddSeconds(5));
            Assert.AreEqual(1, scheduler.TriggerCount);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task DeleteSharedFolderAsync_RemovesDefinitionAndGrants()
    {
        var rootPath = CreateTempDirectory();
        using var db = CreateContext();
        var callerId = Guid.CreateVersion7();
        var definition = new AdminSharedFolderDefinition
        {
            DisplayName = "Media",
            SourcePath = "/mnt/media",
            CreatedByUserId = callerId,
            Grants =
            [
                new AdminSharedFolderGrant { GroupId = Guid.CreateVersion7() },
            ],
        };
        db.AdminSharedFolders.Add(definition);
        await db.SaveChangesAsync();

        try
        {
            var service = CreateService(db, rootPath);

            var result = await service.DeleteSharedFolderAsync(definition.Id, AdminCaller(callerId));

            Assert.IsTrue(result.Deleted);
            Assert.AreNotEqual(Guid.Empty, result.CleanupJobId);
            Assert.AreEqual(0, await db.AdminSharedFolders.CountAsync());
            Assert.AreEqual(0, await db.AdminSharedFolderGrants.CountAsync());
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task DeleteSharedFolderAsync_WithMountedEntries_GathersEntriesBeforeDelete()
    {
        var rootPath = CreateTempDirectory();
        using var db = CreateContext();
        var callerId = Guid.CreateVersion7();
        var sharedFolderId = Guid.CreateVersion7();
        var definition = new AdminSharedFolderDefinition
        {
            Id = sharedFolderId,
            DisplayName = "Media",
            SourcePath = "/mnt/media",
            CreatedByUserId = callerId,
            Grants =
            [
                new AdminSharedFolderGrant { GroupId = Guid.CreateVersion7() },
            ],
        };
        db.AdminSharedFolders.Add(definition);

        // Add mounted node entries (these will be cascade-deleted)
        db.MountedNodeEntries.AddRange(
            new MountedNodeEntry
            {
                Id = VirtualMountedNodeRegistry.GetMountedNodeId(sharedFolderId, "file1.txt", isDirectory: false),
                SharedFolderId = sharedFolderId,
                RelativePath = "file1.txt",
                IsDirectory = false,
            },
            new MountedNodeEntry
            {
                Id = VirtualMountedNodeRegistry.GetMountedNodeId(sharedFolderId, "subdir", isDirectory: true),
                SharedFolderId = sharedFolderId,
                RelativePath = "subdir",
                IsDirectory = true,
            },
            new MountedNodeEntry
            {
                Id = VirtualMountedNodeRegistry.GetMountedNodeId(sharedFolderId, "subdir/file2.txt", isDirectory: false),
                SharedFolderId = sharedFolderId,
                RelativePath = "subdir/file2.txt",
                IsDirectory = false,
            });
        await db.SaveChangesAsync();

        try
        {
            var searchClientMock = new Mock<ISearchFtsClient>();
            searchClientMock.Setup(client => client.IsAvailable).Returns(true);
            searchClientMock
                .Setup(client => client.RemoveDocumentAsync("files", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var service = CreateService(db, rootPath, searchClient: searchClientMock);

            var result = await service.DeleteSharedFolderAsync(sharedFolderId, AdminCaller(callerId));

            Assert.IsTrue(result.Deleted);
            Assert.AreEqual(4, result.PendingSearchRemovals); // Root + 3 mounted entries

            // Verify search client was called for each entity ID
            searchClientMock.Verify(
                client => client.RemoveDocumentAsync("files", It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Exactly(4));

            // Verify cleanup status record reflects search removals
            var status = await db.AdminSharedFolderCleanupStatuses
                .FirstOrDefaultAsync(s => s.CleanupJobId == result.CleanupJobId);
            Assert.IsNotNull(status);
            Assert.AreEqual(sharedFolderId, status.SharedFolderId);
            Assert.AreEqual("Media", status.DisplayName);
            Assert.AreEqual(4, status.SearchDocsRemoved); // All 4 removals succeeded
            Assert.AreEqual(4, status.SearchDocsTotal);
            Assert.IsTrue(status.Phase == CleanupPhase.CleaningMediaSources
                       || status.Phase == CleanupPhase.RemovingSearchDocs); // Depending on timing

            // Verify definition and mounted entries are deleted
            Assert.AreEqual(0, await db.AdminSharedFolders.CountAsync());
            Assert.AreEqual(0, await db.MountedNodeEntries.CountAsync());
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task DeleteSharedFolderAsync_WithoutSearchClient_StillDeletesDefinition()
    {
        var rootPath = CreateTempDirectory();
        using var db = CreateContext();
        var callerId = Guid.CreateVersion7();
        var definition = new AdminSharedFolderDefinition
        {
            DisplayName = "Media",
            SourcePath = "/mnt/media",
            CreatedByUserId = callerId,
        };
        db.AdminSharedFolders.Add(definition);
        await db.SaveChangesAsync();

        try
        {
            var service = CreateService(db, rootPath);

            var result = await service.DeleteSharedFolderAsync(definition.Id, AdminCaller(callerId));

            Assert.IsTrue(result.Deleted);
            Assert.AreEqual(1, result.PendingSearchRemovals); // Just the root folder
            Assert.AreEqual(0, result.SearchDocsRemoved); // No search client available
            Assert.AreEqual(0, await db.AdminSharedFolders.CountAsync());
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task DeleteSharedFolderAsync_CreatesCleanupStatusRecord()
    {
        var rootPath = CreateTempDirectory();
        using var db = CreateContext();
        var callerId = Guid.CreateVersion7();
        var definition = new AdminSharedFolderDefinition
        {
            DisplayName = "Media",
            SourcePath = "/mnt/media",
            CreatedByUserId = callerId,
        };
        db.AdminSharedFolders.Add(definition);
        await db.SaveChangesAsync();

        try
        {
            var service = CreateService(db, rootPath);

            var result = await service.DeleteSharedFolderAsync(definition.Id, AdminCaller(callerId));

            // Verify cleanup status record was created
            var status = await db.AdminSharedFolderCleanupStatuses
                .FirstOrDefaultAsync(s => s.CleanupJobId == result.CleanupJobId);

            Assert.IsNotNull(status);
            Assert.AreEqual(definition.Id, status.SharedFolderId);
            Assert.AreEqual("Media", status.DisplayName);
            Assert.AreEqual(CleanupPhase.CleaningMediaSources, status.Phase); // Past search cleanup
            Assert.IsTrue(status.StartedAt <= DateTime.UtcNow);
            Assert.AreEqual(1, status.SearchDocsTotal);
            Assert.AreEqual(0, status.SearchDocsRemoved);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task DeleteSharedFolderAsync_PublishesEvent()
    {
        var rootPath = CreateTempDirectory();
        using var db = CreateContext();
        var callerId = Guid.CreateVersion7();
        var definition = new AdminSharedFolderDefinition
        {
            DisplayName = "Media",
            SourcePath = "/mnt/media",
            CreatedByUserId = callerId,
            Grants =
            [
                new AdminSharedFolderGrant { GroupId = Guid.CreateVersion7() },
            ],
        };
        db.AdminSharedFolders.Add(definition);
        await db.SaveChangesAsync();

        AdminSharedFolderDeletedEvent? publishedEvent = null;
        var eventBusMock = new Mock<IEventBus>();
        eventBusMock
            .Setup(bus => bus.PublishAsync(It.IsAny<AdminSharedFolderDeletedEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
            .Callback<AdminSharedFolderDeletedEvent, CallerContext, CancellationToken>((evt, _, _) => publishedEvent = evt)
            .Returns(Task.CompletedTask);

        try
        {
            var service = CreateService(db, rootPath, eventBus: eventBusMock);

            var result = await service.DeleteSharedFolderAsync(definition.Id, AdminCaller(callerId));

            Assert.IsTrue(result.Deleted);
            Assert.IsNotNull(publishedEvent);
            Assert.AreEqual(definition.Id, publishedEvent.SharedFolderId);
            Assert.AreEqual("Media", publishedEvent.DisplayName);
            Assert.AreEqual(0, publishedEvent.MountedEntries.Count); // No mounted entries

            eventBusMock.Verify(
                bus => bus.PublishAsync(It.IsAny<AdminSharedFolderDeletedEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dotnetcloud-admin-service-{Guid.CreateVersion7():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingMaintenanceScheduler : IAdminSharedFolderMaintenanceScheduler
    {
        public int TriggerCount { get; private set; }

        public void TriggerProcessing()
        {
            TriggerCount++;
        }
    }
}
