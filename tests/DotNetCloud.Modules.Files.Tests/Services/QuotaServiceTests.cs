using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class QuotaServiceTests
{
    private static FilesDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new FilesDbContext(options);
    }

    private static QuotaService CreateService(FilesDbContext db, QuotaOptions? opts = null, IEventBus? eventBus = null)
    {
        var iOptions = MsOptions.Create(opts ?? new QuotaOptions());
        var bus = eventBus ?? Mock.Of<IEventBus>();
        return new QuotaService(db, bus, iOptions, NullLogger<QuotaService>.Instance);
    }

    private static CallerContext SystemCaller => CallerContext.CreateSystemContext();
    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

    // ─── GetQuotaAsync ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetQuotaAsync_ExistingQuota_ReturnsDto()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 1000, UsedBytes = 200 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetQuotaAsync(userId, SystemCaller);

        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual(1000, result.MaxBytes);
        Assert.AreEqual(200, result.UsedBytes);
        Assert.AreEqual(800, result.RemainingBytes);
    }

    [TestMethod]
    public async Task GetQuotaAsync_NonExistentQuota_ThrowsNotFoundException()
    {
        using var db = CreateContext();
        var service = CreateService(db);

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => service.GetQuotaAsync(Guid.NewGuid(), SystemCaller));
    }

    // ─── GetOrCreateQuotaAsync ─────────────────────────────────────────────────

    [TestMethod]
    public async Task GetOrCreateQuotaAsync_ExistingQuota_ReturnsExisting()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 5000, UsedBytes = 100 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetOrCreateQuotaAsync(userId, SystemCaller);

        Assert.AreEqual(5000, result.MaxBytes);
        Assert.AreEqual(100, result.UsedBytes);
        Assert.AreEqual(1, await db.FileQuotas.CountAsync());
    }

    [TestMethod]
    public async Task GetOrCreateQuotaAsync_NoQuota_CreatesWithDefault()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var opts = new QuotaOptions { DefaultQuotaBytes = 2000 };

        var service = CreateService(db, opts);
        var result = await service.GetOrCreateQuotaAsync(userId, SystemCaller);

        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual(2000, result.MaxBytes);
        Assert.AreEqual(0, result.UsedBytes);
        Assert.AreEqual(1, await db.FileQuotas.CountAsync());
    }

    // ─── GetAllQuotasAsync ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetAllQuotasAsync_MultipleUsers_ReturnsAll()
    {
        using var db = CreateContext();
        db.FileQuotas.Add(new FileQuota { UserId = Guid.NewGuid(), MaxBytes = 1000 });
        db.FileQuotas.Add(new FileQuota { UserId = Guid.NewGuid(), MaxBytes = 2000 });
        db.FileQuotas.Add(new FileQuota { UserId = Guid.NewGuid(), MaxBytes = 3000 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetAllQuotasAsync(SystemCaller);

        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public async Task GetAllQuotasAsync_NoQuotas_ReturnsEmpty()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var result = await service.GetAllQuotasAsync(SystemCaller);
        Assert.AreEqual(0, result.Count);
    }

    // ─── SetQuotaAsync ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SetQuotaAsync_NewUser_CreatesQuota()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        var userId = Guid.NewGuid();

        var result = await service.SetQuotaAsync(userId, 5000, SystemCaller);

        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual(5000, result.MaxBytes);
        Assert.AreEqual(0, result.UsedBytes);
    }

    [TestMethod]
    public async Task SetQuotaAsync_ExistingUser_UpdatesQuota()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 1000 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.SetQuotaAsync(userId, 9999, SystemCaller);

        Assert.AreEqual(9999, result.MaxBytes);
    }

    // ─── HasSufficientQuotaAsync ───────────────────────────────────────────────

    [TestMethod]
    public async Task HasSufficientQuotaAsync_WithinLimits_ReturnsTrue()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 1000, UsedBytes = 200 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        Assert.IsTrue(await service.HasSufficientQuotaAsync(userId, 500));
    }

    [TestMethod]
    public async Task HasSufficientQuotaAsync_ExceedsLimit_ReturnsFalse()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 1000, UsedBytes = 800 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        Assert.IsFalse(await service.HasSufficientQuotaAsync(userId, 500));
    }

    [TestMethod]
    public async Task HasSufficientQuotaAsync_UnlimitedQuota_ReturnsTrue()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 0, UsedBytes = 999999 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        Assert.IsTrue(await service.HasSufficientQuotaAsync(userId, long.MaxValue));
    }

    // ─── AdjustUsedBytesAsync ──────────────────────────────────────────────────

    [TestMethod]
    public async Task AdjustUsedBytesAsync_PositiveDelta_IncrementsUsedBytes()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 10000, UsedBytes = 500 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.AdjustUsedBytesAsync(userId, 200);

        var quota = await db.FileQuotas.FirstAsync(q => q.UserId == userId);
        Assert.AreEqual(700, quota.UsedBytes);
    }

    [TestMethod]
    public async Task AdjustUsedBytesAsync_NegativeDelta_DecrementsUsedBytes()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 10000, UsedBytes = 500 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.AdjustUsedBytesAsync(userId, -300);

        var quota = await db.FileQuotas.FirstAsync(q => q.UserId == userId);
        Assert.AreEqual(200, quota.UsedBytes);
    }

    [TestMethod]
    public async Task AdjustUsedBytesAsync_WouldGoBelowZero_ClampsToZero()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 1000, UsedBytes = 100 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.AdjustUsedBytesAsync(userId, -9999);

        var quota = await db.FileQuotas.FirstAsync(q => q.UserId == userId);
        Assert.AreEqual(0, quota.UsedBytes);
    }

    [TestMethod]
    public async Task AdjustUsedBytesAsync_NoQuotaRecord_DoesNotThrow()
    {
        using var db = CreateContext();
        var service = CreateService(db);
        // Should silently no-op when quota record does not exist
        await service.AdjustUsedBytesAsync(Guid.NewGuid(), 100);
    }

    // ─── Quota notifications ───────────────────────────────────────────────────

    [TestMethod]
    public async Task AdjustUsedBytesAsync_CrossesWarnThreshold_PublishesWarningEvent()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 1000, UsedBytes = 790 });
        await db.SaveChangesAsync();

        QuotaWarningEvent? published = null;
        var busMock = new Mock<IEventBus>();
        busMock.Setup(b => b.PublishAsync(It.IsAny<QuotaWarningEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
               .Callback<QuotaWarningEvent, CallerContext, CancellationToken>((e, _, _) => published = e)
               .Returns(Task.CompletedTask);

        var service = CreateService(db, eventBus: busMock.Object);
        await service.AdjustUsedBytesAsync(userId, 50); // 840/1000 = 84%

        Assert.IsNotNull(published);
        Assert.AreEqual(userId, published!.UserId);
        Assert.AreEqual(840, published.UsedBytes);
    }

    [TestMethod]
    public async Task AdjustUsedBytesAsync_CrossesCriticalThreshold_PublishesCriticalEvent()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 1000, UsedBytes = 940 });
        await db.SaveChangesAsync();

        QuotaCriticalEvent? published = null;
        var busMock = new Mock<IEventBus>();
        busMock.Setup(b => b.PublishAsync(It.IsAny<QuotaCriticalEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
               .Callback<QuotaCriticalEvent, CallerContext, CancellationToken>((e, _, _) => published = e)
               .Returns(Task.CompletedTask);

        var service = CreateService(db, eventBus: busMock.Object);
        await service.AdjustUsedBytesAsync(userId, 20); // 960/1000 = 96%

        Assert.IsNotNull(published);
        Assert.AreEqual(userId, published!.UserId);
    }

    [TestMethod]
    public async Task AdjustUsedBytesAsync_ExceedsQuota_PublishesExceededEvent()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 1000, UsedBytes = 990 });
        await db.SaveChangesAsync();

        QuotaExceededEvent? published = null;
        var busMock = new Mock<IEventBus>();
        busMock.Setup(b => b.PublishAsync(It.IsAny<QuotaExceededEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()))
               .Callback<QuotaExceededEvent, CallerContext, CancellationToken>((e, _, _) => published = e)
               .Returns(Task.CompletedTask);

        var service = CreateService(db, eventBus: busMock.Object);
        await service.AdjustUsedBytesAsync(userId, 50); // 1040/1000 = 104%

        Assert.IsNotNull(published);
        Assert.AreEqual(userId, published!.UserId);
        Assert.AreEqual(1040, published.UsedBytes);
        Assert.IsTrue(published.UsagePercent >= 100.0);
    }

    [TestMethod]
    public async Task AdjustUsedBytesAsync_UnlimitedQuota_DoesNotPublishAnyEvent()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 0, UsedBytes = 0 });
        await db.SaveChangesAsync();

        var busMock = new Mock<IEventBus>();
        var service = CreateService(db, eventBus: busMock.Object);
        await service.AdjustUsedBytesAsync(userId, long.MaxValue / 2);

        busMock.Verify(
            b => b.PublishAsync(It.IsAny<IEvent>(), It.IsAny<CallerContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── RecalculateAsync ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task RecalculateAsync_SumsFileSizes()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 10000, UsedBytes = 0 });
        db.FileNodes.Add(new FileNode { Name = "a.txt", NodeType = FileNodeType.File, OwnerId = userId, Size = 100 });
        db.FileNodes.Add(new FileNode { Name = "b.txt", NodeType = FileNodeType.File, OwnerId = userId, Size = 250 });
        db.FileNodes.Add(new FileNode { Name = "folder", NodeType = FileNodeType.Folder, OwnerId = userId, Size = 0 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.RecalculateAsync(userId);

        var quota = await db.FileQuotas.FirstAsync(q => q.UserId == userId);
        Assert.AreEqual(350, quota.UsedBytes);
    }

    [TestMethod]
    public async Task RecalculateAsync_ExcludeTrashedEnabled_ExcludesSoftDeletedFiles()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 10000, UsedBytes = 0 });
        db.FileNodes.Add(new FileNode { Name = "active.txt", NodeType = FileNodeType.File, OwnerId = userId, Size = 100, IsDeleted = false });
        db.FileNodes.Add(new FileNode { Name = "trashed.txt", NodeType = FileNodeType.File, OwnerId = userId, Size = 500, IsDeleted = true });
        await db.SaveChangesAsync();

        var opts = new QuotaOptions { ExcludeTrashedFromQuota = true };
        var service = CreateService(db, opts);
        await service.RecalculateAsync(userId);

        var quota = await db.FileQuotas.FirstAsync(q => q.UserId == userId);
        Assert.AreEqual(100, quota.UsedBytes); // only active file counted
    }

    [TestMethod]
    public async Task RecalculateAsync_ExcludeTrashedDisabled_IncludesSoftDeletedFiles()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 10000, UsedBytes = 0 });
        db.FileNodes.Add(new FileNode { Name = "active.txt", NodeType = FileNodeType.File, OwnerId = userId, Size = 100, IsDeleted = false });
        db.FileNodes.Add(new FileNode { Name = "trashed.txt", NodeType = FileNodeType.File, OwnerId = userId, Size = 500, IsDeleted = true });
        await db.SaveChangesAsync();

        var opts = new QuotaOptions { ExcludeTrashedFromQuota = false };
        var service = CreateService(db, opts);
        await service.RecalculateAsync(userId);

        var quota = await db.FileQuotas.FirstAsync(q => q.UserId == userId);
        Assert.AreEqual(600, quota.UsedBytes); // both files counted
    }
}
