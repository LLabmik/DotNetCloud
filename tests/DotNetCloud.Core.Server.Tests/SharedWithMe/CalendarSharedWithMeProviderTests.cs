using DotNetCloud.Core.Server.SharedWithMe;
using DotNetCloud.Modules.Calendar.Models;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.SharedWithMe;

/// <summary>
/// Tests for <see cref="CalendarSharedWithMeProvider"/>: the Calendar module adapter that feeds
/// Files' virtual "_DotNetCloud/SharedWithMe/Calendar" folder.
/// </summary>
[TestClass]
public class CalendarSharedWithMeProviderTests
{
    /// <summary>
    /// Builds a provider whose DI scope resolves the given mocked module HTTP client.
    /// </summary>
    private static CalendarSharedWithMeProvider BuildProvider(ICalendarApiClient? client = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(client ?? Mock.Of<ICalendarApiClient>());
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<CalendarSharedWithMeProvider>>(
            NullLogger<CalendarSharedWithMeProvider>.Instance);
        services.AddSingleton<CalendarSharedWithMeProvider>();
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<CalendarSharedWithMeProvider>();
    }

    private static CalendarSharedItem SharedCalendar(Guid calendarId, string name, Guid ownerId, Guid? createdBy, DateTime updatedAt) => new()
    {
        CalendarId = calendarId,
        Name = name,
        OwnerId = ownerId,
        CreatedByUserId = createdBy,
        SharedWithUserId = Guid.CreateVersion7(),
        Permission = CalendarSharePermission.ReadOnly,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = updatedAt
    };

    [TestMethod]
    public async Task ListAsync_CalendarSharedWithUser_ReturnsMappedItemWithCalendarDeepLink()
    {
        var userId = Guid.CreateVersion7();
        var ownerId = Guid.CreateVersion7();
        var calendarId = Guid.CreateVersion7();
        var updatedAt = DateTime.UtcNow.AddHours(-3);

        var client = new Mock<ICalendarApiClient>();
        client
            .Setup(c => c.ListSharedWithMeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([SharedCalendar(calendarId, "Family", ownerId, ownerId, updatedAt)]);

        var provider = BuildProvider(client.Object);
        var items = await provider.ListAsync(userId);

        Assert.AreEqual(1, items.Count);
        var item = items.Single();
        Assert.AreEqual("calendar", item.ModuleId);
        Assert.AreEqual("Calendar", item.DisplayName);
        Assert.AreEqual("Calendar", item.EntityType);
        Assert.AreEqual(calendarId, item.EntityId);
        Assert.AreEqual("Family", item.Title);
        Assert.AreEqual($"/apps/calendar?calendarId={calendarId}", item.DeepLink);
        Assert.AreEqual("calendar_today", item.IconName);
        Assert.AreEqual(updatedAt, item.UpdatedAt);
        Assert.AreEqual(1, await provider.CountAsync(userId));
    }

    [TestMethod]
    public async Task ListAsync_OwnedOrSelfSharedCalendar_Excluded()
    {
        var userId = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();

        var client = new Mock<ICalendarApiClient>();
        client
            .Setup(c => c.ListSharedWithMeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                SharedCalendar(Guid.CreateVersion7(), "Mine", userId, userId, DateTime.UtcNow),
                SharedCalendar(Guid.CreateVersion7(), "My team share", userId, Guid.CreateVersion7(), DateTime.UtcNow),
                SharedCalendar(Guid.CreateVersion7(), "Work", other, other, DateTime.UtcNow)
            ]);

        var provider = BuildProvider(client.Object);
        var items = await provider.ListAsync(userId);

        // Calendars the caller owns (or shared to their own team) are not surfaced.
        Assert.AreEqual(1, items.Count);
        Assert.AreEqual("Work", items.Single().Title);
    }

    [TestMethod]
    public async Task ListAsync_NoShares_ReturnsEmpty()
    {
        var client = new Mock<ICalendarApiClient>();
        client
            .Setup(c => c.ListSharedWithMeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var provider = BuildProvider(client.Object);
        var items = await provider.ListAsync(Guid.CreateVersion7());

        Assert.AreEqual(0, items.Count);
        Assert.AreEqual(0, await provider.CountAsync(Guid.CreateVersion7()));
    }

    [TestMethod]
    public async Task ListAsync_ModuleClientThrows_ReturnsEmpty()
    {
        var client = new Mock<ICalendarApiClient>();
        client
            .Setup(c => c.ListSharedWithMeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Calendar module unavailable"));

        var provider = BuildProvider(client.Object);
        var items = await provider.ListAsync(Guid.CreateVersion7());

        Assert.AreEqual(0, items.Count);
        Assert.AreEqual(0, await provider.CountAsync(Guid.CreateVersion7()));
    }
}
