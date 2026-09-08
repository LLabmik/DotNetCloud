using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.SharedWithMe;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using FilesFileShare = DotNetCloud.Modules.Files.Models.FileShare;

namespace DotNetCloud.Modules.Files.Tests.Services;

/// <summary>
/// Tests for the virtual per-module folders under the Files "_DotNetCloud/SharedWithMe" tree:
/// module folders (Files/Notes), deep-link virtual entries from module providers, and the
/// no-provider file-share regression path.
/// </summary>
[TestClass]
public class FileServiceSharedWithMeModuleTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.CreateVersion7().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static FileService CreateService(
        FilesDbContext db,
        IShareAccessMembershipResolver? shareAccessMembershipResolver = null,
        ISharedWithMeModuleRegistry? sharedWithMe = null) =>
        new(db,
            Mock.Of<IEventBus>(),
            NullLoggerFactory.Instance.CreateLogger<FileService>(),
            new PermissionService(db),
            new DeviceContext(),
            Mock.Of<IQuotaService>(),
            Microsoft.Extensions.Options.Options.Create(new FileSystemOptions()),
            Mock.Of<ISyncChangeNotifier>(),
            shareAccessMembershipResolver,
            sharedWithMe);

    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    /// <summary>Seeds a "Shared File.txt" owned by someone else, shared with a group; returns its node and group id.</summary>
    private static async Task<(Guid NodeId, Guid GroupId)> SeedGroupSharedFileAsync(
        FilesDbContext db, string fileName = "Shared File.txt")
    {
        var ownerId = Guid.CreateVersion7();
        var groupId = Guid.CreateVersion7();
        var sharedNode = new FileNode { Name = fileName, NodeType = FileNodeType.File, OwnerId = ownerId };
        db.FileNodes.Add(sharedNode);
        db.FileShares.Add(new FilesFileShare
        {
            FileNodeId = sharedNode.Id,
            ShareType = ShareType.Group,
            SharedWithGroupId = groupId,
            CreatedByUserId = ownerId,
        });
        await db.SaveChangesAsync();
        return (sharedNode.Id, groupId);
    }

    private static Mock<IShareAccessMembershipResolver> ResolverFor(Guid callerUserId, Guid groupId)
    {
        var mock = new Mock<IShareAccessMembershipResolver>();
        mock.Setup(resolver => resolver.ResolveAsync(callerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShareAccessMembership { GroupIds = [groupId] });
        return mock;
    }

    private static SharedWithMeModuleItem SharedNoteItem(Guid noteId) => new(
        ModuleId: "notes",
        DisplayName: "Notes",
        EntityId: noteId,
        EntityType: "Note",
        Title: "Shared Meeting Notes",
        Subtitle: null,
        DeepLink: $"/apps/notes?noteId={noteId}",
        IconName: "edit_note",
        UpdatedAt: DateTime.UtcNow.AddHours(-1));

    /// <summary>Navigates the virtual tree to the "Shared With Me" folder's child list.</summary>
    private static async Task<IReadOnlyList<FileNodeDto>> ListSharedWithMeChildrenAsync(
        FileService service, CallerContext caller)
    {
        var dotNetCloudRoot = (await service.ListRootAsync(caller)).First(node => node.Name == "_DotNetCloud");
        var sharedWithMe = (await service.ListChildrenAsync(dotNetCloudRoot.Id, caller)).First(node => node.Name == "Shared With Me");
        return await service.ListChildrenAsync(sharedWithMe.Id, caller);
    }

    [TestMethod]
    public async Task ListSharedWithMeChildrenAsync_FileShareAndNotesProvider_ReturnsFilesAndNotesFolders()
    {
        using var db = CreateContext();
        var callerUserId = Guid.CreateVersion7();
        var (_, groupId) = await SeedGroupSharedFileAsync(db);

        var noteId = Guid.CreateVersion7();
        var registry = new FakeSharedWithMeModuleRegistry(
            modules: [new SharedWithMeModule("notes", "Notes", "edit_note")],
            items: [("notes", SharedNoteItem(noteId))]);

        var service = CreateService(db, ResolverFor(callerUserId, groupId).Object, registry);
        var children = await ListSharedWithMeChildrenAsync(service, UserCaller(callerUserId));

        CollectionAssert.AreEquivalent(
            new[] { "Files", "Notes" },
            children.Select(node => node.Name).ToArray());

        var filesFolder = children.Single(node => node.Name == "Files");
        Assert.AreEqual("Folder", filesFolder.NodeType);
        Assert.IsTrue(filesFolder.IsVirtual);
        Assert.IsTrue(filesFolder.IsReadOnly);
        Assert.AreEqual("SharedWithMeModule", filesFolder.VirtualSourceKind);
        Assert.AreEqual("files", filesFolder.ModuleId);
        Assert.AreEqual(1, filesFolder.ChildCount);

        var notesFolder = children.Single(node => node.Name == "Notes");
        Assert.AreEqual("Folder", notesFolder.NodeType);
        Assert.IsTrue(notesFolder.IsVirtual);
        Assert.IsTrue(notesFolder.IsReadOnly);
        Assert.AreEqual("SharedWithMeModule", notesFolder.VirtualSourceKind);
        Assert.AreEqual("notes", notesFolder.ModuleId);
        Assert.AreEqual("edit_note", notesFolder.IconName);
        Assert.AreEqual(1, notesFolder.ChildCount);
    }

    [TestMethod]
    public async Task ListChildrenAsync_NotesModuleFolder_ReturnsProviderItemsMappedToVirtualDeepLinkEntries()
    {
        using var db = CreateContext();
        var callerUserId = Guid.CreateVersion7();
        var noteId = Guid.CreateVersion7();

        var registry = new FakeSharedWithMeModuleRegistry(
            modules: [new SharedWithMeModule("notes", "Notes", "edit_note")],
            items: [("notes", SharedNoteItem(noteId))]);

        var service = CreateService(db, sharedWithMe: registry);
        var notesFolderId = VirtualMountedNodeRegistry.GetSharedWithMeModuleFolderId("notes");

        var children = await service.ListChildrenAsync(notesFolderId, UserCaller(callerUserId));

        var onlyChild = children.SingleOrDefault();
        Assert.IsNotNull(onlyChild);
        var entry = onlyChild!;
        Assert.AreEqual("Shared Meeting Notes", entry.Name);
        Assert.AreEqual("File", entry.NodeType);
        Assert.IsTrue(entry.IsVirtual);
        Assert.IsTrue(entry.IsReadOnly);
        Assert.AreEqual("SharedWithMeModule", entry.VirtualSourceKind);
        Assert.AreEqual("notes", entry.ModuleId);
        Assert.AreEqual("Note", entry.EntityType);
        Assert.AreEqual(noteId, entry.SourceEntityId);
        Assert.AreEqual($"/apps/notes?noteId={noteId}", entry.DeepLinkUrl);
        Assert.AreEqual("edit_note", entry.IconName);
        Assert.AreEqual(notesFolderId, entry.ParentId);
        Assert.AreEqual(VirtualMountedNodeRegistry.GetSharedWithMeItemId("notes", "Note", noteId), entry.Id);
    }

    [TestMethod]
    public async Task GetNodeAsync_NotesModuleFolderId_ReturnsReadOnlyVirtualFolderNode()
    {
        using var db = CreateContext();
        var callerUserId = Guid.CreateVersion7();

        var registry = new FakeSharedWithMeModuleRegistry(
            modules: [new SharedWithMeModule("notes", "Notes", "edit_note")],
            items: [("notes", SharedNoteItem(Guid.CreateVersion7()))]);

        var service = CreateService(db, sharedWithMe: registry);
        var notesFolderId = VirtualMountedNodeRegistry.GetSharedWithMeModuleFolderId("notes");

        var folder = await service.GetNodeAsync(notesFolderId, UserCaller(callerUserId));

        Assert.IsNotNull(folder);
        Assert.AreEqual("Notes", folder!.Name);
        Assert.AreEqual("Folder", folder.NodeType);
        Assert.IsTrue(folder.IsVirtual);
        Assert.IsTrue(folder.IsReadOnly);
        Assert.AreEqual("SharedWithMeModule", folder.VirtualSourceKind);
        Assert.AreEqual("notes", folder.ModuleId);
        Assert.AreEqual("edit_note", folder.IconName);
    }

    [TestMethod]
    public async Task ListSharedWithMeChildrenAsync_RegistryPresentWithFileShareButNoProviders_ReturnsOnlyFilesFolder()
    {
        using var db = CreateContext();
        var callerUserId = Guid.CreateVersion7();
        var (_, groupId) = await SeedGroupSharedFileAsync(db);

        var registry = new FakeSharedWithMeModuleRegistry(modules: []);

        var service = CreateService(db, ResolverFor(callerUserId, groupId).Object, registry);
        var children = await ListSharedWithMeChildrenAsync(service, UserCaller(callerUserId));

        Assert.AreEqual(1, children.Count);
        Assert.AreEqual("Files", children[0].Name);
        Assert.AreEqual("files", children[0].ModuleId);
    }

    [TestMethod]
    public async Task ListChildrenAsync_FilesModuleFolder_ReturnsMountedFileShareItemsUnderIt()
    {
        using var db = CreateContext();
        var callerUserId = Guid.CreateVersion7();
        var (_, groupId) = await SeedGroupSharedFileAsync(db);

        var registry = new FakeSharedWithMeModuleRegistry(modules: []);

        var service = CreateService(db, ResolverFor(callerUserId, groupId).Object, registry);
        var filesFolderId = VirtualMountedNodeRegistry.GetSharedWithMeModuleFolderId("files");

        var children = await service.ListChildrenAsync(filesFolderId, UserCaller(callerUserId));

        // Mounted file-share items are real (permission-gated) nodes surfaced under the Files folder —
        // the same DTO shape as the legacy direct listing, only re-parented under the module folder.
        var onlyChild = children.SingleOrDefault();
        Assert.IsNotNull(onlyChild);
        var entry = onlyChild!;
        Assert.AreEqual("Shared File.txt", entry.Name);
        Assert.AreEqual("SharedWithMe", entry.VirtualSourceKind);
        Assert.AreEqual(filesFolderId, entry.ParentId);
        Assert.IsFalse(entry.IsVirtual);
    }

    [TestMethod]
    public async Task ListSharedWithMeChildrenAsync_NoRegistry_ReturnsFileShareItemsDirectly_Regression()
    {
        using var db = CreateContext();
        var callerUserId = Guid.CreateVersion7();
        var (_, groupId) = await SeedGroupSharedFileAsync(db);

        // No registry → legacy file-share-only layout (used by Files.Host and unit tests).
        var service = CreateService(db, ResolverFor(callerUserId, groupId).Object, sharedWithMe: null);
        var children = await ListSharedWithMeChildrenAsync(service, UserCaller(callerUserId));

        var onlyChild = children.SingleOrDefault();
        Assert.IsNotNull(onlyChild);
        var entry = onlyChild!;
        Assert.AreEqual("Shared File.txt", entry.Name);
        Assert.AreEqual("SharedWithMe", entry.VirtualSourceKind);
        Assert.AreEqual(VirtualMountedNodeRegistry.SharedWithMeRootId, entry.ParentId);
        Assert.IsFalse(entry.IsVirtual);
    }

    [TestMethod]
    public async Task ListSharedWithMeChildrenAsync_NoSharesNoProviderItems_ReturnsEmpty()
    {
        using var db = CreateContext();
        var callerUserId = Guid.CreateVersion7();
        var registry = new FakeSharedWithMeModuleRegistry(modules: []);

        var service = CreateService(db, sharedWithMe: registry);
        var children = await ListSharedWithMeChildrenAsync(service, UserCaller(callerUserId));

        Assert.AreEqual(0, children.Count);
    }

    /// <summary>In-memory registry stub returning configured modules/items without any real provider.</summary>
    private sealed class FakeSharedWithMeModuleRegistry : ISharedWithMeModuleRegistry
    {
        private readonly IReadOnlyList<SharedWithMeModule> _modules;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<SharedWithMeModuleItem>> _itemsByModule;

        public FakeSharedWithMeModuleRegistry(
            IEnumerable<SharedWithMeModule>? modules = null,
            IEnumerable<(string ModuleId, SharedWithMeModuleItem Item)>? items = null)
        {
            _modules = modules?.ToList() ?? [];
            _itemsByModule = (items ?? [])
                .GroupBy(entry => entry.ModuleId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(entry => entry.Item).ToList() as IReadOnlyList<SharedWithMeModuleItem>,
                    StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<SharedWithMeModule> Modules => _modules;

        public Task<int> CountAsync(string moduleId, Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_itemsByModule.TryGetValue(moduleId, out var items) ? items.Count : 0);

        public Task<IReadOnlyList<SharedWithMeModuleItem>> ListAsync(string moduleId, Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_itemsByModule.TryGetValue(moduleId, out var items)
                ? items
                : (IReadOnlyList<SharedWithMeModuleItem>)[]);
    }
}
