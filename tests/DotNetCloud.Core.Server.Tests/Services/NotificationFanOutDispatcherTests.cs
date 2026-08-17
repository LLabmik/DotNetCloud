using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Services;

[TestClass]
public class NotificationFanOutDispatcherTests
{
    private static NotificationCreatedEvent CreateEvent() => new()
    {
        EventId = Guid.CreateVersion7(),
        CreatedAt = DateTime.UtcNow,
        Notification = new NotificationDto
        {
            Id = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            SourceModuleId = "dotnetcloud.files",
            Type = NotificationType.Share,
            Title = "File shared with you",
            Message = "\"report.pdf\" has been shared with you.",
            Priority = NotificationPriority.Normal,
            ActionUrl = "/apps/files?node=abc",
            CreatedAtUtc = DateTime.UtcNow
        }
    };

    private static NotificationFanOutDispatcher CreateDispatcher(
        IReadOnlyList<INotificationChannel> channels)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(sp => sp.GetService(typeof(IEnumerable<INotificationChannel>)))
            .Returns(channels);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return new NotificationFanOutDispatcher(
            scopeFactory.Object,
            NullLogger<NotificationFanOutDispatcher>.Instance);
    }

    [TestMethod]
    public async Task HandleAsync_CallsEveryChannelOnceWithTheNotification()
    {
        var notificationEvent = CreateEvent();
        var notification = notificationEvent.Notification;
        var channel1 = new Mock<INotificationChannel>();
        var channel2 = new Mock<INotificationChannel>();

        var dispatcher = CreateDispatcher([channel1.Object, channel2.Object]);

        await dispatcher.HandleAsync(notificationEvent);

        channel1.Verify(c => c.DeliverAsync(
            It.Is<NotificationDto>(n => n.Id == notification.Id),
            It.IsAny<CancellationToken>()), Times.Once);

        channel2.Verify(c => c.DeliverAsync(
            It.Is<NotificationDto>(n => n.Id == notification.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_ThrowingChannel_DoesNotBlockOtherChannels()
    {
        var throwingChannel = new Mock<INotificationChannel>();
        throwingChannel
            .Setup(c => c.DeliverAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var healthyChannel = new Mock<INotificationChannel>();

        var dispatcher = CreateDispatcher([throwingChannel.Object, healthyChannel.Object]);

        await dispatcher.HandleAsync(CreateEvent());

        healthyChannel.Verify(c => c.DeliverAsync(
            It.IsAny<NotificationDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
