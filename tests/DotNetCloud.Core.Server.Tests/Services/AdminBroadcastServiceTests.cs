using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Admin;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="AdminBroadcastService"/>.
/// </summary>
[TestClass]
public sealed class AdminBroadcastServiceTests
{
    private CoreDbContext _db = null!;
    private Mock<IRealtimeBroadcaster> _broadcasterMock = null!;
    private AdminBroadcastService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.CreateVersion7().ToString())
            .Options;

        _db = new CoreDbContext(options, new PostgreSqlNamingStrategy());
        _broadcasterMock = new Mock<IRealtimeBroadcaster>();
        _service = new AdminBroadcastService(
            _db,
            _broadcasterMock.Object,
            NullLogger<AdminBroadcastService>.Instance);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ── CreateAsync ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateAsync_WhenNotScheduled_DeliversImmediatelyAndBroadcastsToRelayGroup()
    {
        var userId = Guid.CreateVersion7();

        var result = await _service.CreateAsync(
            new CreateAdminBroadcastRequest
            {
                Title = "Reboot",
                Message = "Server reboots in 10 minutes.",
                Severity = AdminBroadcastSeverity.Warning,
            },
            userId);

        Assert.AreEqual(AdminBroadcastStatus.Sent, result.Status);
        Assert.IsNotNull(result.SentAtUtc);

        var stored = await _db.AdminBroadcasts.SingleAsync();
        Assert.AreEqual(userId, stored.CreatedByUserId);
        Assert.IsNotNull(stored.SentAtUtc, "A broadcast without a schedule must be delivered immediately.");

        _broadcasterMock.Verify(b => b.BroadcastAsync(
            "admin-broadcast",
            "admin.broadcast",
            It.Is<ActiveAdminBroadcastDto>(d => d.Id == result.Id && d.Title == "Reboot"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CreateAsync_WhenScheduledForFuture_DoesNotDeliverYet()
    {
        var result = await _service.CreateAsync(
            new CreateAdminBroadcastRequest
            {
                Title = "Maintenance",
                Message = "Planned maintenance tonight.",
                ScheduledForUtc = DateTime.UtcNow.AddHours(2),
            },
            Guid.CreateVersion7());

        Assert.AreEqual(AdminBroadcastStatus.Scheduled, result.Status);
        Assert.IsNull(result.SentAtUtc);

        var stored = await _db.AdminBroadcasts.SingleAsync();
        Assert.IsNull(stored.SentAtUtc);

        _broadcasterMock.Verify(
            b => b.BroadcastAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task CreateAsync_WhenScheduledTimeAlreadyPassed_DeliversImmediately()
    {
        var result = await _service.CreateAsync(
            new CreateAdminBroadcastRequest
            {
                Title = "Late",
                Message = "Scheduled time already passed.",
                ScheduledForUtc = DateTime.UtcNow.AddMinutes(-5),
            },
            Guid.CreateVersion7());

        Assert.AreEqual(AdminBroadcastStatus.Sent, result.Status);
        _broadcasterMock.Verify(
            b => b.BroadcastAsync(
                "admin-broadcast", "admin.broadcast", It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task CreateAsync_WhenExpiryBeforeSendTime_ThrowsArgumentException()
    {
        var now = DateTime.UtcNow;

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => _service.CreateAsync(
            new CreateAdminBroadcastRequest
            {
                Title = "Expired",
                Message = "Expiry before the scheduled send.",
                ScheduledForUtc = now.AddHours(1),
                ExpiresAtUtc = now.AddMinutes(30),
            },
            Guid.CreateVersion7()));
    }

    [TestMethod]
    public async Task CreateAsync_WhenTitleBlank_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => _service.CreateAsync(
            new CreateAdminBroadcastRequest { Title = "   ", Message = "Body" },
            Guid.CreateVersion7()));
    }

    // ── PublishPendingAsync ──────────────────────────────────────────────

    [TestMethod]
    public async Task PublishPendingAsync_WhenScheduledTimeArrived_DeliversAndBroadcasts()
    {
        var entity = new AdminBroadcast
        {
            Id = Guid.CreateVersion7(),
            Title = "Due",
            Message = "This one is due.",
            Severity = AdminBroadcastSeverity.Info,
            CreatedByUserId = Guid.CreateVersion7(),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            ScheduledForUtc = DateTime.UtcNow.AddSeconds(-1),
        };
        _db.AdminBroadcasts.Add(entity);
        await _db.SaveChangesAsync();

        var published = await _service.PublishPendingAsync();

        Assert.AreEqual(1, published);
        Assert.IsNotNull(entity.SentAtUtc);
        _broadcasterMock.Verify(
            b => b.BroadcastAsync(
                "admin-broadcast",
                "admin.broadcast",
                It.Is<ActiveAdminBroadcastDto>(d => d.Id == entity.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task PublishPendingAsync_WhenNothingDue_ReturnsZero()
    {
        _db.AdminBroadcasts.Add(new AdminBroadcast
        {
            Id = Guid.CreateVersion7(),
            Title = "Later",
            Message = "Not due yet.",
            CreatedByUserId = Guid.CreateVersion7(),
            ScheduledForUtc = DateTime.UtcNow.AddHours(3),
        });
        await _db.SaveChangesAsync();

        Assert.AreEqual(0, await _service.PublishPendingAsync());
        _broadcasterMock.Verify(
            b => b.BroadcastAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task PublishPendingAsync_WhenAlreadySent_DoesNotDeliverAgain()
    {
        _db.AdminBroadcasts.Add(new AdminBroadcast
        {
            Id = Guid.CreateVersion7(),
            Title = "Sent",
            Message = "Already delivered.",
            CreatedByUserId = Guid.CreateVersion7(),
            ScheduledForUtc = DateTime.UtcNow.AddMinutes(-10),
            SentAtUtc = DateTime.UtcNow.AddMinutes(-10),
        });
        await _db.SaveChangesAsync();

        Assert.AreEqual(0, await _service.PublishPendingAsync());
    }

    // ── GetActiveForUserAsync ────────────────────────────────────────────

    [TestMethod]
    public async Task GetActiveForUserAsync_WhenSentAndNotExpired_ReturnsBroadcast()
    {
        var broadcast = await SeedSentBroadcastAsync(expiresAtUtc: DateTime.UtcNow.AddHours(1));

        var result = await _service.GetActiveForUserAsync(Guid.CreateVersion7());

        Assert.IsNotNull(result);
        Assert.AreEqual(broadcast.Id, result.Id);
        Assert.AreEqual("Reboot", result.Title);
    }

    [TestMethod]
    public async Task GetActiveForUserAsync_WhenExpired_ReturnsNull()
    {
        await SeedSentBroadcastAsync(expiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

        Assert.IsNull(await _service.GetActiveForUserAsync(Guid.CreateVersion7()));
    }

    [TestMethod]
    public async Task GetActiveForUserAsync_WhenStillScheduled_ReturnsNull()
    {
        _db.AdminBroadcasts.Add(new AdminBroadcast
        {
            Id = Guid.CreateVersion7(),
            Title = "Pending",
            Message = "Not yet delivered.",
            CreatedByUserId = Guid.CreateVersion7(),
            ScheduledForUtc = DateTime.UtcNow.AddHours(1),
        });
        await _db.SaveChangesAsync();

        Assert.IsNull(await _service.GetActiveForUserAsync(Guid.CreateVersion7()));
    }

    [TestMethod]
    public async Task GetActiveForUserAsync_WhenDismissedByUser_ReturnsNullForThatUserOnly()
    {
        var broadcast = await SeedSentBroadcastAsync(expiresAtUtc: null);
        var dismissingUser = Guid.CreateVersion7();
        var otherUser = Guid.CreateVersion7();

        await _service.DismissAsync(broadcast.Id, dismissingUser);

        Assert.IsNull(await _service.GetActiveForUserAsync(dismissingUser));
        Assert.IsNotNull(
            await _service.GetActiveForUserAsync(otherUser),
            "One user's dismissal must not hide the broadcast from other users.");
    }

    // ── DismissAsync ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task DismissAsync_WhenCalledTwice_RecordsSingleDismissal()
    {
        var broadcast = await SeedSentBroadcastAsync(expiresAtUtc: null);
        var userId = Guid.CreateVersion7();

        await _service.DismissAsync(broadcast.Id, userId);
        await _service.DismissAsync(broadcast.Id, userId);

        Assert.AreEqual(1, await _db.AdminBroadcastDismissals.CountAsync());
    }

    [TestMethod]
    public async Task DismissAsync_WhenBroadcastUnknown_DoesNotRecordAnything()
    {
        await _service.DismissAsync(Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.AreEqual(0, await _db.AdminBroadcastDismissals.CountAsync());
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteAsync_WhenBroadcastExists_RemovesBroadcastAndDismissalsAndNotifiesClients()
    {
        var broadcast = await SeedSentBroadcastAsync(expiresAtUtc: null);
        await _service.DismissAsync(broadcast.Id, Guid.CreateVersion7());
        await _service.DismissAsync(broadcast.Id, Guid.CreateVersion7());

        var deleted = await _service.DeleteAsync(broadcast.Id);

        Assert.IsTrue(deleted);
        Assert.AreEqual(0, await _db.AdminBroadcasts.CountAsync());
        Assert.AreEqual(
            0,
            await _db.AdminBroadcastDismissals.CountAsync(),
            "Dismissal rows must not outlive the broadcast they refer to.");

        _broadcasterMock.Verify(
            b => b.BroadcastAsync(
                "admin-broadcast",
                "admin.broadcast.removed",
                It.Is<Guid>(id => id == broadcast.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsync_WhenBroadcastMissing_ReturnsFalse()
    {
        Assert.IsFalse(await _service.DeleteAsync(Guid.CreateVersion7()));
    }

    // ── SendNowAsync ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task SendNowAsync_WhenPending_DeliversImmediately()
    {
        var entity = new AdminBroadcast
        {
            Id = Guid.CreateVersion7(),
            Title = "Soon",
            Message = "Bring it forward.",
            CreatedByUserId = Guid.CreateVersion7(),
            ScheduledForUtc = DateTime.UtcNow.AddDays(1),
        };
        _db.AdminBroadcasts.Add(entity);
        await _db.SaveChangesAsync();

        Assert.IsTrue(await _service.SendNowAsync(entity.Id));
        Assert.IsNotNull(entity.SentAtUtc);
        _broadcasterMock.Verify(
            b => b.BroadcastAsync(
                "admin-broadcast", "admin.broadcast", It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SendNowAsync_WhenAlreadySent_ReturnsFalse()
    {
        var broadcast = await SeedSentBroadcastAsync(expiresAtUtc: null);

        Assert.IsFalse(await _service.SendNowAsync(broadcast.Id));
    }

    // ── ListAsync ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task ListAsync_WhenMultipleBroadcasts_ReturnsNewestFirstWithDismissalCounts()
    {
        var older = await SeedSentBroadcastAsync(expiresAtUtc: null, title: "Older");
        older.CreatedAtUtc = DateTime.UtcNow.AddHours(-2);

        var newer = await SeedSentBroadcastAsync(expiresAtUtc: null, title: "Newer");
        newer.CreatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _service.DismissAsync(newer.Id, Guid.CreateVersion7());
        await _service.DismissAsync(newer.Id, Guid.CreateVersion7());

        var result = await _service.ListAsync();

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Newer", result[0].Title);
        Assert.AreEqual(2, result[0].DismissedCount);
        Assert.AreEqual("Older", result[1].Title);
        Assert.AreEqual(0, result[1].DismissedCount);
    }

    [TestMethod]
    public async Task ListAsync_WhenExpiryPassed_ReportsExpiredStatus()
    {
        await SeedSentBroadcastAsync(expiresAtUtc: DateTime.UtcNow.AddMinutes(-5));

        var result = await _service.ListAsync();

        Assert.AreEqual(AdminBroadcastStatus.Expired, result[0].Status);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<AdminBroadcast> SeedSentBroadcastAsync(DateTime? expiresAtUtc, string title = "Reboot")
    {
        var entity = new AdminBroadcast
        {
            Id = Guid.CreateVersion7(),
            Title = title,
            Message = "The server reboots shortly.",
            Severity = AdminBroadcastSeverity.Warning,
            CreatedByUserId = Guid.CreateVersion7(),
            CreatedAtUtc = DateTime.UtcNow,
            SentAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc,
        };

        _db.AdminBroadcasts.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }
}
