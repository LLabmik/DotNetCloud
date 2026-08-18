using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Notifications;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Regression tests for notification read-state persistence. Mirrors production
/// by configuring <see cref="QueryTrackingBehavior.NoTracking"/> so the tests
/// fail if a mark-as-read method forgets to use <c>AsTracking()</c>.
/// </summary>
[TestClass]
public class NotificationServiceTests
{
    // Unique per test method: the InMemory store is keyed by name, so a fresh
    // name avoids cross-test pollution while still being reused between the
    // seed and verify contexts WITHIN a single test.
    private string _dbName = null!;

    private CoreDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<CoreDbContext>()
                .UseInMemoryDatabase(_dbName)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                .Options,
            new PostgreSqlNamingStrategy());

    [TestInitialize]
    public void Setup()
    {
        _dbName = $"NotificationServiceTests_{Guid.NewGuid():N}";
    }

    private static NotificationService CreateService(CoreDbContext db) =>
        new(db, Mock.Of<DotNetCloud.Core.Events.IEventBus>());

    private static Notification SeedNotification(CoreDbContext db, Guid userId)
    {
        var n = new Notification
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SourceModuleId = "test",
            Type = NotificationType.Info,
            Title = "Test",
            CreatedAtUtc = DateTime.UtcNow,
            ReadAtUtc = null
        };
        db.Notifications.Add(n);
        db.SaveChanges();
        return n;
    }

    [TestMethod]
    public async Task MarkAllReadAsync_PersistsReadState()
    {
        var userId = Guid.CreateVersion7();
        using (var seed = CreateContext())
        {
            SeedNotification(seed, userId);
            SeedNotification(seed, userId);
            await CreateService(seed).MarkAllReadAsync(userId);
        }

        using var verify = CreateContext();
        var unread = await verify.Notifications.CountAsync(n => n.UserId == userId && n.ReadAtUtc == null);
        Assert.AreEqual(0, unread);
    }

    [TestMethod]
    public async Task MarkReadAsync_PersistsReadState()
    {
        var userId = Guid.CreateVersion7();
        Guid notificationId;
        using (var seed = CreateContext())
        {
            notificationId = SeedNotification(seed, userId).Id;
            await CreateService(seed).MarkReadAsync(notificationId, userId);
        }

        using var verify = CreateContext();
        var n = await verify.Notifications.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == notificationId);
        Assert.IsNotNull(n);
        Assert.IsNotNull(n!.ReadAtUtc);
    }
}
