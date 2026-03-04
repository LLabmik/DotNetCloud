using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

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

    private static QuotaService CreateService(FilesDbContext db) =>
        new(db, NullLoggerFactory.Instance.CreateLogger<QuotaService>());

    private static CallerContext SystemCaller => CallerContext.CreateSystemContext();
    private static CallerContext UserCaller(Guid userId) => new(userId, Array.Empty<string>(), CallerType.User);

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

    [TestMethod]
    public async Task HasSufficientQuotaAsync_WithinLimits_ReturnsTrue()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 1000, UsedBytes = 200 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.HasSufficientQuotaAsync(userId, 500);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task HasSufficientQuotaAsync_ExceedsLimit_ReturnsFalse()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 1000, UsedBytes = 800 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.HasSufficientQuotaAsync(userId, 500);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task HasSufficientQuotaAsync_UnlimitedQuota_ReturnsTrue()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.FileQuotas.Add(new FileQuota { UserId = userId, MaxBytes = 0, UsedBytes = 999999 });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.HasSufficientQuotaAsync(userId, long.MaxValue);

        Assert.IsTrue(result);
    }

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
}
