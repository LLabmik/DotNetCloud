using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services.Background;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class AdminSharedFolderMaintenanceServiceTests
{
    private static FilesDbContext CreateContext()
        => new(new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AdminSharedFolderMaintenanceService CreateService(
        FilesDbContext db,
        IAdminSharedFolderReindexDispatcher? reindexDispatcher = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);

        if (reindexDispatcher is not null)
        {
            services.AddSingleton(reindexDispatcher);
        }

        var scopeFactory = new Mock<IServiceScopeFactory>();
        var scope = new Mock<IServiceScope>();
        var provider = services.BuildServiceProvider();

        scope.Setup(s => s.ServiceProvider).Returns(provider);
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return new AdminSharedFolderMaintenanceService(
            scopeFactory.Object,
            NullLogger<AdminSharedFolderMaintenanceService>.Instance,
            new BackgroundServiceTracker());
    }

    [TestMethod]
    public async Task ProcessPendingAsync_DueScheduledFolder_UpdatesScanStatusAndReschedules()
    {
        var rootPath = CreateTempDirectory();

        try
        {
            using var db = CreateContext();
            var folder = new AdminSharedFolderDefinition
            {
                DisplayName = "Media",
                SourcePath = rootPath,
                CrawlMode = AdminSharedFolderCrawlMode.Scheduled,
                NextScheduledScanAt = DateTime.UtcNow.AddMinutes(-5),
                CreatedByUserId = Guid.NewGuid(),
            };
            db.AdminSharedFolders.Add(folder);
            await db.SaveChangesAsync();

            var service = CreateService(db);
            var lowerBound = DateTime.UtcNow.AddHours(23).AddMinutes(58);

            await service.ProcessPendingAsync(CancellationToken.None);

            var persisted = await db.AdminSharedFolders.SingleAsync();
            var upperBound = DateTime.UtcNow.AddHours(24).AddMinutes(2);

            Assert.AreEqual(AdminSharedFolderScanStatus.Succeeded, persisted.LastScanStatus);
            Assert.AreEqual(AdminSharedFolderReindexState.Idle, persisted.ReindexState);
            Assert.IsNull(persisted.LastIndexedAt);
            Assert.IsTrue(persisted.NextScheduledScanAt >= lowerBound);
            Assert.IsTrue(persisted.NextScheduledScanAt <= upperBound);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessPendingAsync_RequestedReindex_DispatchesFilesReindexAndUpdatesTimestamp()
    {
        var rootPath = CreateTempDirectory();

        try
        {
            using var db = CreateContext();
            var dispatcher = new RecordingReindexDispatcher(true);

            var folder = new AdminSharedFolderDefinition
            {
                DisplayName = "Media",
                SourcePath = rootPath,
                CrawlMode = AdminSharedFolderCrawlMode.Manual,
                ReindexState = AdminSharedFolderReindexState.Requested,
                CreatedByUserId = Guid.NewGuid(),
            };
            db.AdminSharedFolders.Add(folder);
            await db.SaveChangesAsync();

            var service = CreateService(db, dispatcher);

            await service.ProcessPendingAsync(CancellationToken.None);

            var persisted = await db.AdminSharedFolders.SingleAsync();
            Assert.AreEqual(AdminSharedFolderScanStatus.Succeeded, persisted.LastScanStatus);
            Assert.AreEqual(AdminSharedFolderReindexState.Idle, persisted.ReindexState);
            Assert.IsNotNull(persisted.LastIndexedAt);
            Assert.IsNull(persisted.NextScheduledScanAt);
            Assert.AreEqual(1, dispatcher.CallCount);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProcessPendingAsync_ReindexDispatchRejected_MarksFolderFailed()
    {
        var rootPath = CreateTempDirectory();

        try
        {
            using var db = CreateContext();
            var dispatcher = new RecordingReindexDispatcher(false);

            var folder = new AdminSharedFolderDefinition
            {
                DisplayName = "Media",
                SourcePath = rootPath,
                CrawlMode = AdminSharedFolderCrawlMode.Manual,
                ReindexState = AdminSharedFolderReindexState.Requested,
                CreatedByUserId = Guid.NewGuid(),
            };
            db.AdminSharedFolders.Add(folder);
            await db.SaveChangesAsync();

            var service = CreateService(db, dispatcher);

            await service.ProcessPendingAsync(CancellationToken.None);

            var persisted = await db.AdminSharedFolders.SingleAsync();
            Assert.AreEqual(AdminSharedFolderScanStatus.Failed, persisted.LastScanStatus);
            Assert.AreEqual(AdminSharedFolderReindexState.Idle, persisted.ReindexState);
            Assert.IsNull(persisted.LastIndexedAt);
            Assert.AreEqual(1, dispatcher.CallCount);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dotnetcloud-admin-maintenance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingReindexDispatcher : IAdminSharedFolderReindexDispatcher
    {
        private readonly bool _result;

        public RecordingReindexDispatcher(bool result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public Task<bool> RequestFilesReindexAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }
}