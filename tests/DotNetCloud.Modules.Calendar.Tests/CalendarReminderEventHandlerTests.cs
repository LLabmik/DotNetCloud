using DotNetCloud.Core.Events;
using DotNetCloud.Core.Grpc.Capabilities;
using DotNetCloud.Modules.Calendar.Host.Services;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Calendar.Tests;

/// <summary>
/// Tests for <see cref="CalendarReminderEventHandler"/> — the CoreCapabilities gRPC calls it makes
/// when a calendar reminder fires.
/// </summary>
/// <remarks>
/// Core.Server's <c>AuthenticationInterceptor</c> rejects any capability call that does not carry the
/// <c>module-id</c> metadata header (<c>Unauthenticated: "Missing module-id metadata header"</c>), so the
/// header is part of the contract, not an optional extra: without it the reminder never reaches the bell.
/// </remarks>
[TestClass]
public class CalendarReminderEventHandlerTests
{
    private static AsyncUnaryCall<SendNotificationResponse> NotificationCall()
        => new(
            Task.FromResult(new SendNotificationResponse { Success = true, DeliveredCount = 1 }),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

    private static AsyncUnaryCall<BroadcastRealtimeEventResponse> BroadcastCall()
        => new(
            Task.FromResult(new BroadcastRealtimeEventResponse { Success = true }),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

    private static AsyncUnaryCall<SendNotificationResponse> FaultedNotificationCall(RpcException exception)
        => new(
            Task.FromException<SendNotificationResponse>(exception),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

    private static CalendarReminderTriggeredEvent CreateEvent() => new()
    {
        EventId = Guid.CreateVersion7(),
        CreatedAt = DateTime.UtcNow,
        CalendarEventId = Guid.CreateVersion7(),
        UserId = Guid.CreateVersion7(),
        EventTitle = "Rush Concert!",
        EventStartUtc = DateTime.UtcNow.AddDays(7)
    };

    [TestMethod]
    public async Task HandleAsync_SendsReminderCategoryNotification_WithModuleIdHeader()
    {
        const string expectedModuleId = "dotnetcloud.calendar";
        Environment.SetEnvironmentVariable("DOTNETCLOUD_MODULE_ID", expectedModuleId);
        try
        {
            var client = new Mock<CoreCapabilities.CoreCapabilitiesClient>();
            client
                .Setup(c => c.SendNotificationAsync(
                    It.IsAny<SendNotificationRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(NotificationCall());
            client
                .Setup(c => c.BroadcastRealtimeEventAsync(
                    It.IsAny<BroadcastRealtimeEventRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(BroadcastCall());

            var @event = CreateEvent();
            var handler = new CalendarReminderEventHandler(client.Object, NullLogger.Instance);

            await handler.HandleAsync(@event);

            client.Verify(c => c.SendNotificationAsync(
                It.Is<SendNotificationRequest>(r =>
                    r.Category == "Reminder"
                    && r.Title == @event.EventTitle
                    && r.RecipientUserIds.Contains(@event.UserId.ToString())),
                It.Is<Metadata>(m => m.GetValue("module-id") == expectedModuleId),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNETCLOUD_MODULE_ID", null);
        }
    }

    [TestMethod]
    public async Task HandleAsync_BroadcastsRealtimeEvent_WithModuleIdHeader()
    {
        const string expectedModuleId = "dotnetcloud.calendar";
        Environment.SetEnvironmentVariable("DOTNETCLOUD_MODULE_ID", expectedModuleId);
        try
        {
            var client = new Mock<CoreCapabilities.CoreCapabilitiesClient>();
            client
                .Setup(c => c.SendNotificationAsync(
                    It.IsAny<SendNotificationRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(NotificationCall());
            client
                .Setup(c => c.BroadcastRealtimeEventAsync(
                    It.IsAny<BroadcastRealtimeEventRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(BroadcastCall());

            var @event = CreateEvent();
            var handler = new CalendarReminderEventHandler(client.Object, NullLogger.Instance);

            await handler.HandleAsync(@event);

            client.Verify(c => c.BroadcastRealtimeEventAsync(
                It.Is<BroadcastRealtimeEventRequest>(r =>
                    r.EventName == "CalendarReminder"
                    && r.TargetUserId == @event.UserId.ToString()),
                It.Is<Metadata>(m => m.GetValue("module-id") == expectedModuleId),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNETCLOUD_MODULE_ID", null);
        }
    }

    [TestMethod]
    public async Task HandleAsync_NotificationRejected_StillAttemptsRealtimeBroadcast()
    {
        var client = new Mock<CoreCapabilities.CoreCapabilitiesClient>();
        client
            .Setup(c => c.SendNotificationAsync(
                It.IsAny<SendNotificationRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(FaultedNotificationCall(
                new RpcException(new Status(StatusCode.Unauthenticated, "Missing module-id metadata header"))));
        client
            .Setup(c => c.BroadcastRealtimeEventAsync(
                It.IsAny<BroadcastRealtimeEventRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(BroadcastCall());

        var handler = new CalendarReminderEventHandler(client.Object, NullLogger.Instance);

        await handler.HandleAsync(CreateEvent());

        client.Verify(c => c.BroadcastRealtimeEventAsync(
            It.IsAny<BroadcastRealtimeEventRequest>(),
            It.IsAny<Metadata>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
