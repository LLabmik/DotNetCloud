using System.IO.Compression;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class DefaultUserContentTests
{
    private string _templateRoot = null!;
    private string _storageRoot = null!;

    [TestInitialize]
    public void Initialize()
    {
        _templateRoot = Path.Combine(Path.GetTempPath(), $"dnc-template-{Guid.CreateVersion7():N}");
        _storageRoot = Path.Combine(Path.GetTempPath(), $"dnc-storage-{Guid.CreateVersion7():N}");
        Directory.CreateDirectory(_templateRoot);
        Directory.CreateDirectory(_storageRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        TryDelete(_templateRoot);
        TryDelete(_storageRoot);
    }

    private static FilesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options);

    private DefaultUserContentOptions Options() => new()
    {
        RootPath = _templateRoot,
        Enabled = true,
        Timeout = TimeSpan.FromMinutes(1)
    };

    private DefaultUserContentStore CreateStore(DefaultUserContentOptions options) =>
        new(NullLogger<DefaultUserContentStore>.Instance);

    private FileImportService CreateImport(FilesDbContext db) =>
        new(
            db,
            new LocalFileStorageEngine(_storageRoot, NullLogger<LocalFileStorageEngine>.Instance),
            new NoOpEventBus(),
            new NoOpSyncNotifier(),
            NullLogger<FileImportService>.Instance);

    private DefaultUserContentProvisioner CreateProvisioner(FilesDbContext db, DefaultUserContentOptions options)
    {
        var store = CreateStore(options);
        var import = CreateImport(db);
        var quota = new QuotaService(db, new NoOpEventBus(), MsOptions.Create(new QuotaOptions()), NullLogger<QuotaService>.Instance);
        var trash = new TrashService(db,
            new LocalFileStorageEngine(_storageRoot, NullLogger<LocalFileStorageEngine>.Instance),
            new NoOpEventBus(), new NoOpSyncNotifier(), NullLogger<TrashService>.Instance, quota);

        return new DefaultUserContentProvisioner(
            db,
            new StaticSettings(options),
            store,
            import,
            quota,
            NullLogger<DefaultUserContentProvisioner>.Instance,
            trash);
    }

    // ── Template store ────────────────────────────────────────────────────────

    [TestMethod]
    public void DefaultUserContentOptions_AdminOverlayDisabled_TurnsFeatureOff()
    {
        var options = new DefaultUserContentOptions { Enabled = true };

        DefaultUserContentOptions.Apply(options, new Dictionary<string, string>
        {
            [DefaultUserContentOptions.AdminSettings.EnabledKey] = "false"
        });

        Assert.IsFalse(options.Enabled);
    }

    [TestMethod]
    public async Task BuildManifestAsync_HiddenAndIllegalEntries_AreSkipped()
    {
        Directory.CreateDirectory(Path.Combine(_templateRoot, "Documents"));
        await File.WriteAllTextAsync(Path.Combine(_templateRoot, "Documents", "letter.txt"), "hello");
        await File.WriteAllTextAsync(Path.Combine(_templateRoot, ".hidden"), "x");
        await File.WriteAllTextAsync(Path.Combine(_templateRoot, "bad:name.txt"), "x");

        var store = CreateStore(Options());
        var result = await store.ValidateAsync(Options());

        Assert.IsTrue(result.Manifest.Entries.Any(e => e.RelativePath == "Documents/letter.txt"));
        Assert.IsFalse(result.Manifest.Entries.Any(e => e.RelativePath.Contains(".hidden", StringComparison.Ordinal)));
        Assert.IsTrue(result.Issues.Any(i => i.Code == "illegal-name"));
    }

    [TestMethod]
    public async Task BuildManifestAsync_SameContent_ProducesStableFingerprint()
    {
        Directory.CreateDirectory(Path.Combine(_templateRoot, "Documents"));
        await File.WriteAllTextAsync(Path.Combine(_templateRoot, "Documents", "letter.txt"), "hello");

        var store = CreateStore(Options());
        var first = await store.BuildManifestAsync(Options());
        var second = await store.BuildManifestAsync(Options());

        Assert.AreEqual(first.Fingerprint, second.Fingerprint);
        Assert.AreNotEqual(string.Empty, first.Fingerprint);
    }

    // ── Provisioner ───────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ReconcileAsync_NewUser_CreatesTemplateOnce()
    {
        Directory.CreateDirectory(Path.Combine(_templateRoot, "Documents"));
        await File.WriteAllTextAsync(Path.Combine(_templateRoot, "Documents", "letter.txt"), "hello");
        Directory.CreateDirectory(Path.Combine(_templateRoot, "Photos"));

        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var provisioner = CreateProvisioner(db, Options());

        var first = await provisioner.ReconcileAsync(userId, ReconcileMode.AddOnly);
        var second = await provisioner.ReconcileAsync(userId, ReconcileMode.AddOnly);

        Assert.AreEqual(UserContentProvisioningStatus.Completed, first.Status);
        Assert.AreEqual(3, first.ItemsCreated); // Documents, letter.txt, Photos
        Assert.AreEqual(0, second.ItemsCreated);
        Assert.AreEqual(3, await db.FileNodes.CountAsync(n => n.OwnerId == userId));
        Assert.AreEqual(1, await db.FileNodes.CountAsync(n => n.OwnerId == userId && n.Name == "letter.txt"));
    }

    [TestMethod]
    public async Task ReconcileAsync_DefaultFile_CreatesSingleVersionAndMarksReadOnly()
    {
        await File.WriteAllTextAsync(Path.Combine(_templateRoot, "letter.txt"), "hello");

        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var provisioner = CreateProvisioner(db, Options());

        await provisioner.ReconcileAsync(userId, ReconcileMode.AddOnly);

        var node = await db.FileNodes.SingleAsync(n => n.OwnerId == userId);
        var versions = await db.FileVersions.Where(v => v.FileNodeId == node.Id).ToListAsync();

        Assert.IsTrue(node.IsReadOnly);
        Assert.AreEqual(1, versions.Count);
        Assert.AreEqual(1, versions[0].VersionNumber);
        Assert.IsFalse(versions[0].IsChunked);
        Assert.AreEqual(1, node.CurrentVersion);
    }

    [TestMethod]
    public async Task ReconcileAsync_UserDeletedFile_IsTombstonedAndNotRecreated()
    {
        await File.WriteAllTextAsync(Path.Combine(_templateRoot, "letter.txt"), "hello");

        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var provisioner = CreateProvisioner(db, Options());

        await provisioner.ReconcileAsync(userId, ReconcileMode.AddOnly);

        var node = await db.FileNodes.SingleAsync(n => n.OwnerId == userId);
        node.IsDeleted = true;
        node.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var second = await provisioner.ReconcileAsync(userId, ReconcileMode.AddOnly);

        Assert.AreEqual(0, second.ItemsCreated);
        Assert.AreEqual(1, await db.FileNodes.IgnoreQueryFilters().CountAsync(n => n.OwnerId == userId));
        Assert.AreEqual(DefaultContentItemState.RemovedByUser,
            (await db.UserDefaultContentItems.SingleAsync(i => i.UserId == userId)).State);
    }

    [TestMethod]
    public async Task ReconcileAsync_TemplateEdit_IsOutdatedUntilPublishUpdates()
    {
        var templateFile = Path.Combine(_templateRoot, "letter.txt");
        await File.WriteAllTextAsync(templateFile, "v1");

        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var provisioner = CreateProvisioner(db, Options());

        await provisioner.ReconcileAsync(userId, ReconcileMode.AddOnly);

        await File.WriteAllTextAsync(templateFile, "v2 content");
        File.SetLastWriteTimeUtc(templateFile, DateTime.UtcNow);

        var addOnly = await provisioner.ReconcileAsync(userId, ReconcileMode.AddOnly);
        var node = await db.FileNodes.SingleAsync(n => n.OwnerId == userId);

        Assert.AreEqual(1, addOnly.PendingUpdates);
        Assert.AreEqual("v1", await ReadNodeContentAsync(db, node.Id));

        var publish = await provisioner.ReconcileAsync(userId, ReconcileMode.PublishUpdates);
        var updated = await db.FileNodes.SingleAsync(n => n.OwnerId == userId);
        var versions = await db.FileVersions.Where(v => v.FileNodeId == updated.Id).ToListAsync();

        Assert.AreEqual(0, publish.PendingUpdates);
        Assert.AreEqual("v2 content", await ReadNodeContentAsync(db, updated.Id));
        Assert.AreEqual(1, versions.Count, "default content must not accumulate version history");
        Assert.AreEqual(1, updated.CurrentVersion);
    }

    [TestMethod]
    public async Task ReconcileAsync_PresentDefaultFile_IsQuotaExempt()
    {
        await File.WriteAllTextAsync(Path.Combine(_templateRoot, "letter.txt"), "hello");

        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var provisioner = CreateProvisioner(db, Options());

        await provisioner.ReconcileAsync(userId, ReconcileMode.AddOnly);

        var quota = await db.FileQuotas.SingleAsync(q => q.UserId == userId);
        Assert.AreEqual(5, quota.ExemptBytes);
    }

    [TestMethod]
    public async Task ReconcileAsync_TemplateItemRemoved_RequiresConfirmedDeletion()
    {
        var templateFile = Path.Combine(_templateRoot, "letter.txt");
        await File.WriteAllTextAsync(templateFile, "hello");

        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var provisioner = CreateProvisioner(db, Options());

        await provisioner.ReconcileAsync(userId, ReconcileMode.AddOnly);

        File.Delete(templateFile);

        var addOnly = await provisioner.ReconcileAsync(userId, ReconcileMode.AddOnly);
        Assert.AreEqual(1, addOnly.PendingDeletions);
        Assert.AreEqual(1, await db.FileNodes.CountAsync(n => n.OwnerId == userId));

        var confirmed = await provisioner.ReconcileAsync(userId, ReconcileMode.AllowDeletions);

        Assert.AreEqual(0, confirmed.PendingDeletions);
        Assert.AreEqual(0, await db.FileNodes.IgnoreQueryFilters().CountAsync(n => n.OwnerId == userId));
        Assert.AreEqual(0, await db.UserDefaultContentItems.CountAsync(i => i.UserId == userId));
    }

    [TestMethod]
    public async Task ReconcileAsync_TwoUsers_ShareOnePhysicalBlob()
    {
        await File.WriteAllTextAsync(Path.Combine(_templateRoot, "letter.txt"), "shared content");

        using var db = CreateContext();
        var provisioner = CreateProvisioner(db, Options());

        var userA = Guid.CreateVersion7();
        var userB = Guid.CreateVersion7();

        await provisioner.ReconcileAsync(userA, ReconcileMode.AddOnly);
        await provisioner.ReconcileAsync(userB, ReconcileMode.AddOnly);

        var nodes = await db.FileNodes.AsNoTracking()
            .Where(n => n.OwnerId == userA || n.OwnerId == userB)
            .ToListAsync();

        Assert.AreEqual(2, nodes.Count);
        Assert.AreEqual(nodes[0].StoragePath, nodes[1].StoragePath);
        Assert.AreEqual(1, Directory.EnumerateFiles(_storageRoot, "*", SearchOption.AllDirectories).Count(),
            "identical content must be stored once");
    }

    [TestMethod]
    public async Task GetProtectedNodeIdsAsync_TrackedFile_IncludesAncestorFolders()
    {
        Directory.CreateDirectory(Path.Combine(_templateRoot, "Documents"));
        await File.WriteAllTextAsync(Path.Combine(_templateRoot, "Documents", "letter.txt"), "hello");

        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        await CreateProvisioner(db, Options()).ReconcileAsync(userId, ReconcileMode.AddOnly);

        var nodes = await db.FileNodes.AsNoTracking().Where(n => n.OwnerId == userId).ToListAsync();
        var protectedIds = await DefaultContentTracking.GetProtectedNodeIdsAsync(db);

        Assert.AreEqual(2, nodes.Count);
        Assert.IsTrue(nodes.All(n => protectedIds.Contains(n.Id)),
            "both the starter file and its ancestor folder must be protected from retention");
    }

    [TestMethod]
    public async Task HasSufficientQuotaAsync_DefaultContentExemption_WidensAllowance()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        db.FileQuotas.Add(new FileQuota
        {
            UserId = userId,
            MaxBytes = 10,
            UsedBytes = 10,
            ExemptBytes = 5
        });
        await db.SaveChangesAsync();

        var service = new QuotaService(db, new NoOpEventBus(), MsOptions.Create(new QuotaOptions()),
            NullLogger<QuotaService>.Instance);

        Assert.IsTrue(await service.HasSufficientQuotaAsync(userId, 5));
        Assert.IsFalse(await service.HasSufficientQuotaAsync(userId, 6));
    }

    [TestMethod]
    public async Task ReconcileAsync_RemovedFolderWithTrackedFile_DeletesBoth()
    {
        var folder = Path.Combine(_templateRoot, "Docs");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "letter.txt"), "hello");

        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var provisioner = CreateProvisioner(db, Options());

        await provisioner.ReconcileAsync(userId, ReconcileMode.AddOnly);
        Assert.AreEqual(2, await db.FileNodes.CountAsync(n => n.OwnerId == userId));

        Directory.Delete(folder, recursive: true);

        var result = await provisioner.ReconcileAsync(userId, ReconcileMode.AllowDeletions);

        Assert.AreEqual(0, result.PendingDeletions);
        Assert.AreEqual(0, await db.FileNodes.IgnoreQueryFilters().CountAsync(n => n.OwnerId == userId),
            "the file must be deleted before its now-empty folder is evaluated");
        Assert.AreEqual(0, await db.UserDefaultContentItems.CountAsync(i => i.UserId == userId));
    }

    [TestMethod]
    public async Task ReconcileAsync_UserModifiedCopy_IsOnlyReplacedByOverwriteModified()
    {
        await File.WriteAllTextAsync(Path.Combine(_templateRoot, "letter.txt"), "template content");

        using var db = CreateContext();
        var userId = Guid.CreateVersion7();

        // Adoption scenario: the user already owns a same-named file with different content.
        db.FileNodes.Add(new FileNode
        {
            Name = "letter.txt",
            OwnerId = userId,
            NodeType = FileNodeType.File,
            ContentHash = "user-modified-hash",
            Size = 42,
            IsReadOnly = false
        });
        await db.SaveChangesAsync();

        var provisioner = CreateProvisioner(db, Options());

        var addOnly = await provisioner.ReconcileAsync(userId, ReconcileMode.AddOnly);
        var node = await db.FileNodes.SingleAsync(n => n.OwnerId == userId);
        var row = await db.UserDefaultContentItems.SingleAsync(i => i.UserId == userId);

        Assert.AreEqual(DefaultContentItemState.ChangedByUser, row.State);
        Assert.IsFalse(node.IsReadOnly, "an adopted, differing file stays writable until overwritten");
        Assert.AreEqual(0, await db.FileVersions.CountAsync(v => v.FileNodeId == node.Id));
        Assert.AreEqual(0, addOnly.PendingUpdates);

        await provisioner.ReconcileAsync(userId, ReconcileMode.OverwriteModified);

        var updated = await db.FileNodes.SingleAsync(n => n.OwnerId == userId);
        var updatedRow = await db.UserDefaultContentItems.SingleAsync(i => i.UserId == userId);

        Assert.IsTrue(updated.IsReadOnly);
        Assert.AreEqual(DefaultContentItemState.Present, updatedRow.State);
        Assert.AreEqual(1, await db.FileVersions.CountAsync(v => v.FileNodeId == updated.Id));
        Assert.AreEqual("template content", await ReadNodeContentAsync(db, updated.Id));
    }

    // ── Read-only guard ───────────────────────────────────────────────────────
    [TestMethod]
    public async Task ReadOnlyContentGuard_DefaultContentNode_ThrowsFileReadOnly()
    {
        await File.WriteAllTextAsync(Path.Combine(_templateRoot, "letter.txt"), "hello");

        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        await CreateProvisioner(db, Options()).ReconcileAsync(userId, ReconcileMode.AddOnly);

        var node = await db.FileNodes.SingleAsync(n => n.OwnerId == userId);

        var exception = await Assert.ThrowsExactlyAsync<FileReadOnlyException>(
            () => ReadOnlyContentGuard.EnsureWritableAsync(db, node.Id, "rename"));

        Assert.AreEqual(ErrorCodes.FileReadOnly, exception.ErrorCode);
    }

    [TestMethod]
    public async Task ReadOnlyContentGuard_WritableNode_DoesNotThrow()
    {
        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        var node = new FileNode { Name = "mine.txt", OwnerId = userId };
        db.FileNodes.Add(node);
        await db.SaveChangesAsync();

        await ReadOnlyContentGuard.EnsureWritableAsync(db, node.Id, "rename");
    }

    [TestMethod]
    public async Task RenameAsync_DefaultContent_ThrowsFileReadOnly()
    {
        await File.WriteAllTextAsync(Path.Combine(_templateRoot, "letter.txt"), "hello");

        using var db = CreateContext();
        var userId = Guid.CreateVersion7();
        await CreateProvisioner(db, Options()).ReconcileAsync(userId, ReconcileMode.AddOnly);

        var node = await db.FileNodes.SingleAsync(n => n.OwnerId == userId);
        var permissions = new Mock<IPermissionService>();
        permissions
            .Setup(p => p.RequirePermissionAsync(It.IsAny<Guid>(), It.IsAny<CallerContext>(),
                It.IsAny<SharePermission>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var quota = new QuotaService(db, new NoOpEventBus(), MsOptions.Create(new QuotaOptions()),
            NullLogger<QuotaService>.Instance);
        var service = new FileService(
            db,
            new NoOpEventBus(),
            NullLogger<FileService>.Instance,
            permissions.Object,
            Mock.Of<IDeviceContext>(),
            quota,
            MsOptions.Create(new FileSystemOptions()),
            new NoOpSyncNotifier());

        await Assert.ThrowsExactlyAsync<FileReadOnlyException>(() =>
            service.RenameAsync(node.Id, new RenameNodeDto { Name = "renamed.txt" },
                new CallerContext(userId, [], CallerType.User)));
    }

    // ── Archive ───────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ImportAsync_PathTraversal_IsRejected()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"dnc-import-{Guid.CreateVersion7():N}.zip");
        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("../escape.txt");
                await using var writer = new StreamWriter(entry.Open());
                await writer.WriteAsync("bad");
            }

            var importer = new DefaultUserContentArchive(NullLogger<DefaultUserContentArchive>.Instance);
            var result = await importer.ImportAsync(archivePath, Options(), replace: false, overwrite: false, dryRun: false);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Issues.Any(i => i.Contains("traversal", StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(File.Exists(Path.Combine(Path.GetDirectoryName(_templateRoot)!, "escape.txt")));
        }
        finally
        {
            TryDeleteFile(archivePath);
        }
    }

    [TestMethod]
    public async Task ImportAsync_Merge_AddsFilesAndSkipsExisting()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"dnc-import-{Guid.CreateVersion7():N}.zip");
        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("Documents/readme.txt");
                await using var writer = new StreamWriter(entry.Open());
                await writer.WriteAsync("imported");
            }

            var importer = new DefaultUserContentArchive(NullLogger<DefaultUserContentArchive>.Instance);

            var first = await importer.ImportAsync(archivePath, Options(), replace: false, overwrite: false, dryRun: false);
            var second = await importer.ImportAsync(archivePath, Options(), replace: false, overwrite: false, dryRun: false);

            Assert.IsTrue(first.Success);
            Assert.AreEqual(1, first.Added);
            Assert.AreEqual(1, second.Skipped);
            Assert.AreEqual("imported",
                await File.ReadAllTextAsync(Path.Combine(_templateRoot, "Documents", "readme.txt")));
        }
        finally
        {
            TryDeleteFile(archivePath);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> ReadNodeContentAsync(FilesDbContext db, Guid nodeId)
    {
        var node = await db.FileNodes.AsNoTracking().SingleAsync(n => n.Id == nodeId);
        var path = Path.Combine(_storageRoot, node.StoragePath!.Replace('/', Path.DirectorySeparatorChar));
        return await File.ReadAllTextAsync(path);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private sealed class StaticSettings : IDefaultUserContentSettingsProvider
    {
        private readonly DefaultUserContentOptions _options;

        public StaticSettings(DefaultUserContentOptions options) => _options = options;

        public DefaultUserContentOptions Current => _options;

        public Task<DefaultUserContentOptions> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_options);

        public void Invalidate()
        {
        }
    }

    private sealed class NoOpEventBus : IEventBus
    {
        public Task PublishAsync<TEvent>(TEvent @event, CallerContext caller, CancellationToken cancellationToken = default)
            where TEvent : IEvent => Task.CompletedTask;

        public Task SubscribeAsync<TEvent>(IEventHandler<TEvent> handler, CancellationToken cancellationToken = default)
            where TEvent : IEvent => Task.CompletedTask;

        public Task UnsubscribeAsync<TEvent>(IEventHandler<TEvent> handler, CancellationToken cancellationToken = default)
            where TEvent : IEvent => Task.CompletedTask;
    }

    private sealed class NoOpSyncNotifier : ISyncChangeNotifier
    {
        public Task NotifyAsync(Guid userId, long latestSequence, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public async IAsyncEnumerable<SyncChangeNotification> SubscribeAsync(
            Guid userId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public int GetConnectionCount(Guid userId) => 0;
    }
}
